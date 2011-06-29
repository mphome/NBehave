using System.IO;
using NBehave.Narrator.Framework.EventListeners;
using NBehave.Narrator.Framework.Specifications.Features;
using NUnit.Framework;
using TestPlainTextAssembly;

namespace NBehave.Narrator.Framework.Specifications
{
    [TestFixture]
    public abstract class EmbeddedRunnerSpec
    {
        public class AcceptanceTest : EmbeddedRunnerSpec
        {
            [Test]
            public void Running_a_passing_feature()
            {
                TestFeatures.FeatureNamedStory.Execute(typeof(GreetingSystemActionSteps).Assembly);
            }

            [Test]
            public void Running_a_failing_feature()
            {
                Assert.Throws<StepFailedException>(() => TestFeatures.FeatureWithFailingStep.Execute(typeof(GreetingSystemActionSteps).Assembly));
            }
        }

        public class When_running_passing_scenario : EmbeddedRunnerSpec
        {
            private string _messages;

            [SetUp]
            public void Run_Feature()
            {
                var messages = new StringWriter();
                var listener = new TextWriterEventListener(messages);
                TestFeatures.FeatureNamedStory.Execute(typeof(GreetingSystemActionSteps).Assembly, listener);
                _messages = messages.ToString();
            }

            [Test]
            public void Should_write_Feature()
            {
                StringAssert.Contains("Feature: Greeting system", _messages);
            }

            [Test]
            public void Should_write_narrative()
            {
                StringAssert.Contains("As a project member", _messages);
                StringAssert.Contains("I want", _messages);
                StringAssert.Contains("So that", _messages);
            }

            [Test]
            public void Should_write_scenario_title()
            {
                StringAssert.Contains("Scenario: Greeting someone", _messages);
            }

            [Test]
            public void Should_write_scenario_steps()
            {
                StringAssert.Contains("Given my name is Morgan", _messages);
                StringAssert.Contains("When I'm greeted", _messages);
                StringAssert.Contains("Then I should be greeted with �Hello, Morgan!�", _messages);
            }

            [Test]
            public void Should_write_summary()
            {
                StringAssert.Contains("Scenarios run: 1, Failures: 0, Pending: 0", _messages);
                StringAssert.Contains("Steps 3, failed 0, pending 0", _messages);
            }
        }

        public class When_running_failing_scenario : EmbeddedRunnerSpec
        {
            private string _messages;

            [SetUp]
            public void Run_Feature()
            {
                var messages = new StringWriter();
                try
                {
                    var listener = new TextWriterEventListener(messages);
                    TestFeatures.FeatureWithFailingStep.Execute(typeof(GreetingSystemActionSteps).Assembly, listener);
                }
                catch (StepFailedException)
                { }
                finally
                {
                    _messages = messages.ToString();
                }
            }

            [Test]
            public void Should_write_Feature()
            {
                StringAssert.Contains("Feature: Feature with failing step", _messages);
            }

            [Test]
            public void Should_write_narrative()
            {
                StringAssert.Contains("As a nbehave user", _messages);
                StringAssert.Contains("I want", _messages);
                StringAssert.Contains("So that", _messages);
            }

            [Test]
            public void Should_write_scenario_title()
            {
                StringAssert.Contains("Scenario: scenario with failing step", _messages);
            }

            [Test]
            public void Should_write_scenario_steps()
            {
                StringAssert.Contains("Given my name is Morgan", _messages);
                StringAssert.Contains("When I'm greeted", _messages);
                StringAssert.Contains("Then I should be greeted with �Hello, Scott!�", _messages);
            }

            [Test]
            public void Should_write_summary()
            {
                StringAssert.Contains("Scenarios run: 1, Failures: 1, Pending: 0", _messages);
                StringAssert.Contains("Steps 3, failed 1, pending 0", _messages);
            }

            [Test]
            public void Should_write_Failure()
            {
                StringAssert.Contains("Failures:", _messages);
                StringAssert.Contains("1) Feature with failing step (scenario with failing step) FAILED", _messages);
                StringAssert.Contains("", _messages);
            }
        }

        public class When_running_pending_scenario : EmbeddedRunnerSpec
        {
            private string _messages;

            [SetUp]
            public void Run_Feature()
            {
                var messages = new StringWriter();
                var listener = new TextWriterEventListener(messages);
                TestFeatures.FeatureWithPendingStep.Execute(listener);
                _messages = messages.ToString();
            }

            [Test]
            public void Should_write_Feature()
            {
                StringAssert.Contains("Feature: Not implemented feature", _messages);
            }

            [Test]
            public void Should_write_narrative()
            {
                StringAssert.Contains("As a", _messages);
                StringAssert.Contains("I want", _messages);
                StringAssert.Contains("So that", _messages);
            }

            [Test]
            public void Should_write_scenario_title()
            {
                StringAssert.Contains("Scenario: Pending scenario", _messages);
            }

            [Test]
            public void Should_write_scenario_steps()
            {
                StringAssert.Contains("Given an unimplemented step", _messages);
                StringAssert.Contains("When another unimplemented step", _messages);
                StringAssert.Contains("Then there should be 3 pending steps", _messages);
            }

            [Test]
            public void Should_write_summary()
            {
                StringAssert.Contains("Scenarios run: 1, Failures: 0, Pending: 1", _messages);
                StringAssert.Contains("Steps 3, failed 0, pending 3", _messages);
            }
        }
    }
}