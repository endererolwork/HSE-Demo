using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HSE.Scenario
{
    /// <summary>
    /// Plays a scenario: opens mandatory steps one by one, each only after the previous one is completed,
    /// and keeps optional steps open in the background until the end.
    ///
    /// It knows nothing about what a step checks or how highlights look. UI, audio, analytics and
    /// scoring screens subscribe to its events instead of being called by it (Observer).
    /// </summary>
    public class ScenarioRunner : MonoBehaviour, IScenarioContext
    {
        [SerializeField] string scenarioTitle = "Scenario";
        [Tooltip("Mandatory steps run in this order. Optional steps are open for the whole run.")]
        [SerializeField] List<ScenarioStep> steps = new List<ScenarioStep>();
        [Tooltip("Any component implementing IHighlightService (e.g. PrismHighlightService). Empty = no highlights.")]
        [SerializeField] MonoBehaviour highlighter;
        [SerializeField, Min(0)] int penaltyPerMistake = 2;
        [SerializeField] bool playOnStart = true;

        public event Action<ScenarioRunner> ScenarioStarted;
        public event Action<ScenarioStep> StepStarted;
        public event Action<ScenarioStep> StepCompleted;
        public event Action<ScenarioStep, string> MistakeMade;
        public event Action<ScenarioReport> ScenarioCompleted;

        public string Title => scenarioTitle;
        public IReadOnlyList<ScenarioStep> Steps => steps;
        public IEnumerable<ScenarioStep> MandatorySteps => steps.Where(s => s && s.Requirement == StepRequirement.Mandatory);
        public IEnumerable<ScenarioStep> OptionalSteps => steps.Where(s => s && s.Requirement == StepRequirement.Optional);
        public ScenarioStep CurrentStep { get; private set; }
        public ScenarioReport Report { get; private set; }
        public bool IsRunning { get; private set; }

        public IHighlightService Highlighter => _highlighter ??= (highlighter as IHighlightService) ?? NullHighlightService.Instance;
        public IScenarioClock Clock { get; set; } = UnityClock.Instance;

        IHighlightService _highlighter;

        public void Configure(string title, IEnumerable<ScenarioStep> orderedSteps, MonoBehaviour highlightService, bool autoPlay = true)
        {
            scenarioTitle = title;
            steps = new List<ScenarioStep>(orderedSteps);
            highlighter = highlightService;
            playOnStart = autoPlay;
            _highlighter = null;
        }

        /// <summary>Overrides the inspector highlighter (useful for tests or other VFX packages).</summary>
        public void SetHighlighter(IHighlightService service) => _highlighter = service ?? NullHighlightService.Instance;

        void Start()
        {
            if (playOnStart) StartScenario();
        }

        void OnValidate()
        {
            if (highlighter && !(highlighter is IHighlightService))
            {
                Debug.LogWarning($"{highlighter.GetType().Name} does not implement IHighlightService.", this);
                highlighter = null;
            }
        }

        public void StartScenario()
        {
            if (IsRunning) StopScenario();
            foreach (var s in steps.Where(s => s)) s.ResetStep();

            Report = new ScenarioReport(steps.Where(s => s), new DefaultScoringPolicy(penaltyPerMistake));
            Report.Begin(Clock.Now);
            IsRunning = true;
            ScenarioStarted?.Invoke(this);

            foreach (var s in OptionalSteps) Run(s);
            AdvanceToNextMandatory();
        }

        /// <summary>Stops without finishing (scene change, restart).</summary>
        public void StopScenario()
        {
            foreach (var s in steps.Where(s => s && s.State == StepState.Active))
            {
                Unsubscribe(s);
                s.Abort();
            }
            CurrentStep = null;
            IsRunning = false;
        }

        /// <summary>Instructor / debug shortcut: closes the current mandatory step as skipped and moves on.</summary>
        public void SkipCurrentStep()
        {
            if (!IsRunning || CurrentStep == null) return;
            var s = CurrentStep;
            Unsubscribe(s);
            s.Abort();
            Report.StepEnded(s, StepState.Skipped, Clock.Now);
            AdvanceToNextMandatory();
        }

        /// <summary>
        /// Ends the run now for a valid reason (e.g. the patient refused consent and it was handled correctly).
        /// Open and remaining steps are marked not applicable, so they don't count against the score.
        /// </summary>
        public void EndEarly(string reason)
        {
            if (!IsRunning) return;
            Report.EndedEarlyReason = reason;
            foreach (var s in steps.Where(s => s && s.State != StepState.Completed))
            {
                if (s.State == StepState.Active) { Unsubscribe(s); s.Abort(); }
                Report.StepEnded(s, s.Requirement == StepRequirement.Mandatory ? StepState.NotApplicable : StepState.Skipped, Clock.Now);
            }
            CurrentStep = null;
            Report.Finish(Clock.Now);
            IsRunning = false;
            ScenarioCompleted?.Invoke(Report);
        }

        void AdvanceToNextMandatory()
        {
            CurrentStep = MandatorySteps.FirstOrDefault(s => s.State == StepState.Locked);
            if (CurrentStep == null)
            {
                FinishScenario();
                return;
            }
            Run(CurrentStep);
        }

        void Run(ScenarioStep step)
        {
            step.Completed += OnStepCompleted;
            step.MistakeMade += OnStepMistake;
            Report.StepStarted(step, Clock.Now);
            step.Begin(this);
            StepStarted?.Invoke(step);
        }

        void OnStepCompleted(ScenarioStep step)
        {
            Unsubscribe(step);
            Report.StepEnded(step, StepState.Completed, Clock.Now);
            StepCompleted?.Invoke(step);
            if (step == CurrentStep) AdvanceToNextMandatory();
        }

        void OnStepMistake(ScenarioStep source, string message)
        {
            // Sub-task mistakes are booked against the top-level step the runner is tracking.
            var owner = steps.FirstOrDefault(s => s == source) ?? CurrentStep ?? source;
            Report.Mistake(owner, message);
            MistakeMade?.Invoke(source, message);
        }

        void FinishScenario()
        {
            foreach (var s in OptionalSteps.Where(s => s.State == StepState.Active))
            {
                Unsubscribe(s);
                s.Abort();
                Report.StepEnded(s, StepState.Skipped, Clock.Now);
            }
            Report.Finish(Clock.Now);
            IsRunning = false;
            ScenarioCompleted?.Invoke(Report);
        }

        void Unsubscribe(ScenarioStep step)
        {
            step.Completed -= OnStepCompleted;
            step.MistakeMade -= OnStepMistake;
        }

        void OnDisable()
        {
            if (IsRunning) StopScenario();
        }
    }
}
