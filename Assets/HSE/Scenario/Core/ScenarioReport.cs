using System;
using System.Collections.Generic;
using System.Linq;

namespace HSE.Scenario
{
    /// <summary>What happened in one step.</summary>
    public sealed class StepResult
    {
        public IStepInfo Step { get; }
        public StepState Outcome { get; internal set; } = StepState.Locked;
        public float StartedAt { get; internal set; } = -1;
        public float EndedAt { get; internal set; } = -1;
        public List<string> Mistakes { get; } = new List<string>();

        public float Duration => StartedAt < 0 || EndedAt < 0 ? 0 : EndedAt - StartedAt;

        internal StepResult(IStepInfo step) => Step = step;
    }

    /// <summary>Scoring rules, kept apart from bookkeeping so they can change on their own.</summary>
    public interface IScoringPolicy
    {
        int Score(IReadOnlyList<StepResult> results);
        int MaxScore(IReadOnlyList<StepResult> results);
    }

    /// <summary>
    /// Mandatory steps give their points, minus a penalty per mistake (never below zero per step).
    /// Optional steps add their points as a bonus. Max score counts mandatory steps only.
    /// </summary>
    public sealed class DefaultScoringPolicy : IScoringPolicy
    {
        public int PenaltyPerMistake { get; }

        public DefaultScoringPolicy(int penaltyPerMistake = 2) => PenaltyPerMistake = Math.Max(0, penaltyPerMistake);

        public int Score(IReadOnlyList<StepResult> results) => results.Sum(r =>
        {
            if (r.Outcome != StepState.Completed) return 0;
            if (r.Step.Requirement == StepRequirement.Optional) return r.Step.Points;
            return Math.Max(0, r.Step.Points - PenaltyPerMistake * r.Mistakes.Count);
        });

        public int MaxScore(IReadOnlyList<StepResult> results) =>
            results.Where(r => r.Step.Requirement == StepRequirement.Mandatory && r.Outcome != StepState.NotApplicable).Sum(r => r.Step.Points);
    }

    /// <summary>Records a run: timing, outcome and mistakes per step. Plain C#, no Unity objects.</summary>
    public sealed class ScenarioReport
    {
        readonly List<StepResult> _results = new List<StepResult>();
        readonly Dictionary<IStepInfo, StepResult> _byStep = new Dictionary<IStepInfo, StepResult>();
        readonly IScoringPolicy _scoring;

        public float StartedAt { get; private set; } = -1;
        public float EndedAt { get; private set; } = -1;
        public bool IsFinished => EndedAt >= 0;
        public IReadOnlyList<StepResult> Results => _results;

        public ScenarioReport(IEnumerable<IStepInfo> steps, IScoringPolicy scoring = null)
        {
            _scoring = scoring ?? new DefaultScoringPolicy();
            foreach (var s in steps)
            {
                if (s == null || _byStep.ContainsKey(s)) continue;
                var r = new StepResult(s);
                _results.Add(r);
                _byStep.Add(s, r);
            }
        }

        public void Begin(float time) => StartedAt = time;
        public void Finish(float time) => EndedAt = time;

        public void StepStarted(IStepInfo step, float time) => Get(step).StartedAt = time;

        public void StepEnded(IStepInfo step, StepState outcome, float time)
        {
            var r = Get(step);
            r.Outcome = outcome;
            r.EndedAt = time;
        }

        public void Mistake(IStepInfo step, string message) => Get(step).Mistakes.Add(message);

        /// <summary>Why the run ended before all steps, if it did (e.g. "Consent refused").</summary>
        public string EndedEarlyReason { get; set; }

        public int Score => _scoring.Score(_results);
        public int MaxScore => _scoring.MaxScore(_results);
        public int MistakeCount => _results.Sum(r => r.Mistakes.Count);
        public float TotalTime => StartedAt < 0 ? 0 : (IsFinished ? EndedAt : StartedAt) - StartedAt;

        public int CompletedCount(StepRequirement requirement) =>
            _results.Count(r => r.Step.Requirement == requirement && r.Outcome == StepState.Completed);

        public int Count(StepRequirement requirement) => _results.Count(r => r.Step.Requirement == requirement);

        public StepResult For(IStepInfo step) => _byStep.TryGetValue(step, out var r) ? r : null;

        StepResult Get(IStepInfo step)
        {
            if (!_byStep.TryGetValue(step, out var r))
            {
                // Sub-steps of composites are not listed up front; record them as they appear.
                r = new StepResult(step);
                _results.Add(r);
                _byStep.Add(step, r);
            }
            return r;
        }
    }
}
