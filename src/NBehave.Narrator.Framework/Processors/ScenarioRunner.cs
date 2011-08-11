using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NBehave.Narrator.Framework.Tiny;

namespace NBehave.Narrator.Framework.Processors
{
    public class ScenarioRunner : IScenarioRunner
    {
        private readonly ITinyMessengerHub _hub;
        private readonly IStringStepRunner _stringStepRunner;
        private readonly Regex _hasParamsInStep = new Regex(@"\[\w+\]");

        public ScenarioRunner(ITinyMessengerHub hub, IStringStepRunner stringStepRunner)
        {
            _hub = hub;
            _stringStepRunner = stringStepRunner;
        }

        public void Run(Feature feature)
        {
            foreach (var scenario in feature.Scenarios)
            {
                _hub.Publish(new ScenarioStartedEvent(this, scenario));
                if (scenario.Examples.Any())
                    RunExamples(scenario);
                else
                    RunScenario(scenario);
                _hub.Publish(new ScenarioFinishedEvent(this, scenario));
            }
        }

        private void RunScenario(Scenario scenario)
        {
            var scenarioResult = new ScenarioResult(scenario.Feature, scenario.Title);
            _stringStepRunner.BeforeScenario();
            var backgroundResults = RunBackground(scenario.Feature.Background);
            scenarioResult.AddActionStepResults(backgroundResults);
            var stepResults = RunSteps(scenario.Steps);
            scenarioResult.AddActionStepResults(stepResults);
            ExecuteAfterScenario(scenario, scenarioResult);
            _hub.Publish(new ScenarioResultEvent(this, scenarioResult));
        }

        private void RunExamples(Scenario scenario)
        {
            var exampleResults = new ScenarioExampleResult(scenario.Feature, scenario.Title, scenario.Steps, scenario.Examples);

            foreach (var example in scenario.Examples)
            {
                var steps = CloneSteps(scenario);
                InsertColumnValues(steps, example);

                var scenarioResult = new ScenarioResult(scenario.Feature, scenario.Title);
                _stringStepRunner.BeforeScenario();
                var stepResults = RunSteps(steps);
                scenarioResult.AddActionStepResults(stepResults);
                ExecuteAfterScenario(scenario, scenarioResult);
                exampleResults.AddResult(scenarioResult);
            }
            _hub.Publish(new ScenarioResultEvent(this, exampleResults));
        }

        private IEnumerable<StepResult> RunBackground(Scenario background)
        {
            return RunSteps(background.Steps)
                .Select(_ => new BackgroundStepResult(background.Title, _))
                .Cast<StepResult>()
                .ToList();
        }

        private IEnumerable<StepResult> RunSteps(IEnumerable<StringStep> stepsToRun)
        {
            var stepResults = new List<StepResult>();
            foreach (var step in stepsToRun)
            {
                if (step is StringTableStep)
                    RunStringTableStep((StringTableStep)step);
                else if (step is StringStep)
                    step.StepResult = _stringStepRunner.Run(step);
                stepResults.Add(step.StepResult);
            }
            return stepResults;
        }

        private void ExecuteAfterScenario(Scenario scenario, ScenarioResult scenarioResult)
        {
            if (scenario.Steps.Any())
            {
                try
                {
                    _stringStepRunner.AfterScenario();
                }
                catch (Exception e)
                {
                    if (!scenarioResult.HasFailedSteps())
                        scenarioResult.Fail(e);
                }
            }
        }

        private void RunStringTableStep(StringTableStep stringStep)
        {
            var actionStepResult = GetNewActionStepResult(stringStep);
            var hasParamsInStep = HasParametersInStep(stringStep.Step);
            foreach (var row in stringStep.TableSteps)
            {
                StringStep step = stringStep;
                if (hasParamsInStep)
                {
                    step = InsertParametersToStep(stringStep, row);
                }

                var result = _stringStepRunner.Run(step, row);
                actionStepResult.MergeResult(result.Result);
            }

            stringStep.StepResult = actionStepResult;
        }

        private void InsertColumnValues(IEnumerable<StringStep> steps, Row example)
        {
            foreach (var step in steps)
            {
                foreach (var columnName in example.ColumnNames)
                {
                    var columnValue = example.ColumnValues[columnName.Name].TrimWhiteSpaceChars();
                    var replace = new Regex(string.Format(@"(\${0})|(\[{0}\])", columnName), RegexOptions.IgnoreCase);
                    step.Step = replace.Replace(step.Step, columnValue);

                    if (step is StringTableStep)
                    {
                        var tableSteps = ((StringTableStep)step).TableSteps;
                        foreach (var row in tableSteps)
                        {
                            var newValues = row.ColumnValues.ToDictionary(pair => pair.Key, pair => replace.Replace(pair.Value, columnValue));
                            row.ColumnValues.Clear();
                            foreach (var pair in newValues)
                            {
                                row.ColumnValues.Add(pair.Key, pair.Value);
                            }
                        }
                    }
                }
            }
        }

        private ICollection<StringStep> CloneSteps(Scenario scenario)
        {
            var clones = new List<StringStep>();
            foreach (var step in scenario.Steps)
            {
                if (step is StringTableStep)
                {
                    var clone = new StringTableStep(step.Step, step.Source);
                    var tableSteps = ((StringTableStep)step).TableSteps;
                    foreach (var tableStep in tableSteps)
                    {
                        var clonedValues = tableStep.ColumnValues.ToDictionary(pair => pair.Key, pair => pair.Value);
                        var clonedNames = new ExampleColumns(tableStep.ColumnNames);
                        var clonedRow = new Row(clonedNames, clonedValues);
                        clone.AddTableStep(clonedRow);
                    }
                    clones.Add(clone);
                }
                else
                {
                    clones.Add(new StringStep(step.Step, step.Source));
                }
            }

            return clones;
        }

        private StepResult GetNewActionStepResult(StringTableStep stringStep)
        {
            var fullStep = CreateStepText(stringStep);
            return new StepResult(fullStep, new Passed());
        }

        private string CreateStepText(StringTableStep stringStep)
        {
            var step = new StringBuilder(stringStep.Step + Environment.NewLine);
            step.Append(stringStep.TableSteps.First().ColumnNamesToString() + Environment.NewLine);
            foreach (var row in stringStep.TableSteps)
            {
                step.Append(row.ColumnValuesToString() + Environment.NewLine);
            }

            RemoveLastNewLine(step);
            return step.ToString();
        }

        private void RemoveLastNewLine(StringBuilder step)
        {
            step.Remove(step.Length - Environment.NewLine.Length, Environment.NewLine.Length);
        }

        private bool HasParametersInStep(string step)
        {
            return _hasParamsInStep.IsMatch(step);
        }

        private StringStep InsertParametersToStep(StringTableStep step, Row row)
        {
            var stringStep = step.Step;
            foreach (var column in row.ColumnValues)
            {
                var replceWithValue = new Regex(string.Format(@"\[{0}\]", column.Key), RegexOptions.IgnoreCase);
                stringStep = replceWithValue.Replace(stringStep, column.Value);
            }
            return new StringStep(stringStep, step.Source);
        }
    }
}