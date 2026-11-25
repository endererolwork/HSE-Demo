using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSE.Scenario
{
    /// <summary>
    /// Base class of every task in a scenario (the "BaseObject" of the system).
    ///
    /// A step owns exactly one thing: deciding when it is done. The runner decides when it starts,
    /// the highlight service decides how guidance looks, the report decides what it is worth.
    /// New kinds of tasks are added by subclassing and overriding <see cref="OnBegin"/> / <see cref="OnEnd"/>;
    /// nothing else in the system has to change (Open/Closed).
    /// </summary>
    public abstract class ScenarioStep : MonoBehaviour, IStepInfo
    {
        [SerializeField] string id;
        [SerializeField] string title = "New step";
        [SerializeField, TextArea] string instruction;
        [SerializeField] StepRequirement requirement = StepRequirement.Mandatory;
        [SerializeField, Min(0)] int points = 10;

        [Header("Guidance")]
        [Tooltip("Objects that light up while this step is active and switch off when it ends.")]
        [SerializeField] List<GameObject> highlightTargets = new List<GameObject>();
        [SerializeField] HighlightStyle highlightStyle = HighlightStyle.Objective;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string Title => title;
        public string Instruction => instruction;
        public StepRequirement Requirement => requirement;
        public int Points => points;
        public IReadOnlyList<GameObject> HighlightTargets => highlightTargets;
        public StepState State { get; private set; } = StepState.Locked;

        /// <summary>Raised once when the step finishes successfully.</summary>
        public event Action<ScenarioStep> Completed;

        /// <summary>Raised for every wrong action while the step is active. Does not end the step.</summary>
        public event Action<ScenarioStep, string> MistakeMade;

        protected IScenarioContext Context { get; private set; }

        /// <summary>Configures the step from code (used by scene builders and tests).</summary>
        public void Configure(string stepId, string stepTitle, string stepInstruction, StepRequirement stepRequirement = StepRequirement.Mandatory,
            int stepPoints = 10, IEnumerable<GameObject> targets = null, HighlightStyle style = HighlightStyle.Objective)
        {
            id = stepId;
            title = stepTitle;
            instruction = stepInstruction;
            requirement = stepRequirement;
            points = stepPoints;
            highlightTargets = targets != null ? new List<GameObject>(targets) : new List<GameObject>();
            highlightStyle = style;
        }

        // ------------------------------------------------------------ driven by the runner (or a parent step)

        public void Begin(IScenarioContext context)
        {
            if (State != StepState.Locked) return;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            State = StepState.Active;
            SetGuidance(true);
            OnBegin();
        }

        /// <summary>Stops an active step without completing it (scenario ended, instructor skip).</summary>
        public void Abort()
        {
            if (State != StepState.Active) return;
            Finish(StepState.Skipped);
        }

        /// <summary>Returns the step to Locked so the scenario can be replayed.</summary>
        public virtual void ResetStep()
        {
            if (State == StepState.Active) Finish(StepState.Skipped);
            State = StepState.Locked;
        }

        // ------------------------------------------------------------ for subclasses

        /// <summary>Start listening for whatever completes this step.</summary>
        protected abstract void OnBegin();

        /// <summary>Stop listening. Called on completion and on abort, exactly once per Begin.</summary>
        protected virtual void OnEnd() { }

        protected void Complete()
        {
            if (State != StepState.Active) return;
            Finish(StepState.Completed);
            Completed?.Invoke(this);
        }

        protected void ReportMistake(string message)
        {
            if (State != StepState.Active) return;
            foreach (var t in highlightTargets)
                if (t) Context.Highlighter.Flash(t, HighlightStyle.Error);
            MistakeMade?.Invoke(this, message);
        }

        /// <summary>Lets composite steps forward their children's mistakes as their own.</summary>
        protected void ForwardMistake(ScenarioStep source, string message) => MistakeMade?.Invoke(source, message);

        // ------------------------------------------------------------ internals

        void Finish(StepState endState)
        {
            OnEnd();
            SetGuidance(false);
            State = endState;
        }

        void SetGuidance(bool on)
        {
            var highlighter = Context?.Highlighter ?? NullHighlightService.Instance;
            foreach (var t in highlightTargets)
            {
                if (!t) continue;
                if (on) highlighter.Show(t, highlightStyle);
                else highlighter.Hide(t);
            }
        }
    }
}
