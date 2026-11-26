using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HSE.Scenario.Tests
{
    sealed class FakeClock : IScenarioClock
    {
        public float Now { get; set; }
    }

    sealed class RecordingHighlighter : IHighlightService
    {
        public readonly HashSet<GameObject> Lit = new HashSet<GameObject>();
        public int Flashes;
        public void Show(GameObject target, HighlightStyle style) => Lit.Add(target);
        public void Hide(GameObject target) => Lit.Remove(target);
        public void Flash(GameObject target, HighlightStyle style) => Flashes++;
    }

    public class ScenarioRunnerTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        ScenarioRunner _runner;
        FakeClock _clock;
        RecordingHighlighter _highlighter;

        [SetUp]
        public void SetUp()
        {
            _runner = Spawn("Runner").AddComponent<ScenarioRunner>();
            _clock = new FakeClock();
            _highlighter = new RecordingHighlighter();
            _runner.Clock = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        ManualStep Step(string id, StepRequirement req = StepRequirement.Mandatory, int points = 10, GameObject target = null)
        {
            var s = Spawn(id).AddComponent<ManualStep>();
            s.Configure(id, id, "", req, points, target ? new[] { target } : null);
            return s;
        }

        void Play(params ScenarioStep[] steps)
        {
            _runner.Configure("Test", steps, null, autoPlay: false);
            _runner.SetHighlighter(_highlighter);
            _runner.StartScenario();
        }

        [Test]
        public void MandatorySteps_OpenOneAtATime_InOrder()
        {
            var a = Step("a");
            var b = Step("b");
            Play(a, b);

            Assert.AreEqual(StepState.Active, a.State);
            Assert.AreEqual(StepState.Locked, b.State);

            a.Finish();
            Assert.AreEqual(StepState.Completed, a.State);
            Assert.AreEqual(StepState.Active, b.State);
            Assert.AreSame(b, _runner.CurrentStep);
        }

        [Test]
        public void Highlights_FollowTheActiveStep()
        {
            var ta = Spawn("ta");
            var tb = Spawn("tb");
            var a = Step("a", target: ta);
            var b = Step("b", target: tb);
            Play(a, b);

            CollectionAssert.AreEquivalent(new[] { ta }, _highlighter.Lit);
            a.Finish();
            CollectionAssert.AreEquivalent(new[] { tb }, _highlighter.Lit);
            b.Finish();
            Assert.IsEmpty(_highlighter.Lit);
        }

        [Test]
        public void OptionalSteps_RunInBackground_AndAreClosedAtTheEnd()
        {
            var a = Step("a");
            var extra = Step("extra", StepRequirement.Optional);
            ScenarioReport finished = null;
            _runner.ScenarioCompleted += r => finished = r;
            Play(a, extra);

            Assert.AreEqual(StepState.Active, extra.State);
            a.Finish();

            Assert.NotNull(finished);
            Assert.AreEqual(StepState.Skipped, extra.State);
            Assert.AreEqual(1, extra.EndCount);
        }

        [Test]
        public void Mistakes_DoNotEndTheStep_AndLowerTheScore()
        {
            var a = Step("a", points: 10);
            Play(a);

            a.Fail("wrong patient");
            a.Fail("wrong patient");
            Assert.AreEqual(StepState.Active, a.State);

            a.Finish();
            Assert.AreEqual(2, _runner.Report.MistakeCount);
            Assert.AreEqual(10 - 2 * 2, _runner.Report.Score);
            Assert.AreEqual(10, _runner.Report.MaxScore);
        }

        [Test]
        public void CompletingTwice_IsIgnored()
        {
            var a = Step("a");
            var b = Step("b");
            int completed = 0;
            _runner.StepCompleted += _ => completed++;
            Play(a, b);

            a.Finish();
            a.Finish();
            Assert.AreEqual(1, completed);
            Assert.AreEqual(StepState.Active, b.State);
        }

        [Test]
        public void Composite_InSequence_OpensChildrenInOrder_ThenCompletes()
        {
            var c1 = Step("c1", points: 0);
            var c2 = Step("c2", points: 0);
            var parent = Spawn("parent").AddComponent<CompositeStep>();
            parent.Configure("parent", "parent", "", StepRequirement.Mandatory, 15);
            parent.SetChildren(new ScenarioStep[] { c1, c2 }, CompositeStep.Order.InSequence);
            Play(parent);

            Assert.AreEqual(StepState.Active, c1.State);
            Assert.AreEqual(StepState.Locked, c2.State);
            c1.Finish();
            Assert.AreEqual(StepState.Active, c2.State);
            c2.Finish();
            Assert.AreEqual(StepState.Completed, parent.State);
            Assert.AreEqual(15, _runner.Report.Score);
        }

        [Test]
        public void Composite_ChildMistake_IsBookedOnTheParent()
        {
            var c1 = Step("c1", points: 0);
            var parent = Spawn("parent").AddComponent<CompositeStep>();
            parent.Configure("parent", "parent", "", StepRequirement.Mandatory, 10);
            parent.SetChildren(new ScenarioStep[] { c1 }, CompositeStep.Order.AnyOrder);
            Play(parent);

            c1.Fail("no gloves");
            Assert.AreEqual(1, _runner.Report.For(parent).Mistakes.Count);
        }

        [Test]
        public void Report_TracksTime()
        {
            var a = Step("a");
            _clock.Now = 10;
            Play(a);
            _clock.Now = 25;
            a.Finish();

            Assert.AreEqual(15f, _runner.Report.For(a).Duration, 1e-4);
            Assert.AreEqual(15f, _runner.Report.TotalTime, 1e-4);
        }

        [Test]
        public void Skip_MovesOn_WithoutPoints()
        {
            var a = Step("a");
            var b = Step("b");
            Play(a, b);

            _runner.SkipCurrentStep();
            Assert.AreEqual(StepState.Skipped, a.State);
            Assert.AreEqual(StepState.Active, b.State);
            b.Finish();
            Assert.AreEqual(10, _runner.Report.Score);
            Assert.AreEqual(20, _runner.Report.MaxScore);
        }

        [Test]
        public void GoBack_ReopensPreviousStep()
        {
            var a = Step("a");
            var b = Step("b");
            Play(a, b);
            a.Finish();
            Assert.AreEqual(StepState.Active, b.State);

            _runner.GoBackOneStep();
            Assert.AreEqual(StepState.Active, a.State);
            Assert.AreEqual(StepState.Locked, b.State);
            Assert.IsTrue(a.Reopened);
            Assert.AreSame(a, _runner.CurrentStep);

            a.Finish();
            Assert.AreEqual(StepState.Active, b.State);
        }

        [Test]
        public void Restart_ResetsSteps()
        {
            var a = Step("a");
            Play(a);
            a.Finish();
            _runner.StartScenario();
            Assert.AreEqual(StepState.Active, a.State);
            Assert.AreEqual(0, _runner.Report.Score);
        }

        [Test]
        public void EndEarly_MarksRemainingNotApplicable_AndExcludesThemFromMax()
        {
            var a = Step("a", points: 10);
            var b = Step("b", points: 10);
            var c = Step("c", points: 10);
            ScenarioReport finished = null;
            _runner.ScenarioCompleted += r => finished = r;
            Play(a, b, c);

            a.Finish();
            _runner.EndEarly("consent refused");

            Assert.NotNull(finished);
            Assert.IsFalse(_runner.IsRunning);
            Assert.AreEqual(StepState.NotApplicable, finished.For(b).Outcome);
            Assert.AreEqual(StepState.NotApplicable, finished.For(c).Outcome);
            Assert.AreEqual(10, finished.MaxScore);
            Assert.AreEqual(10, finished.Score);
            Assert.AreEqual("consent refused", finished.EndedEarlyReason);
        }
    }
}
