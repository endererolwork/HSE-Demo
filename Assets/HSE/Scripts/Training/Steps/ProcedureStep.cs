using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>
    /// Completes when a named procedure event happens (e.g. "tourniquet_on", "needle_in").
    /// Procedure rule violations raised while it is active count as its mistakes.
    /// One class covers every simple procedure step; new ones only need a new event name.
    /// </summary>
    public class ProcedureStep : ScenarioStep
    {
        [SerializeField] string completeOnEvent;

        public void SetEvent(string id) => completeOnEvent = id;

        protected override void OnBegin()
        {
            ClinicalEvents.ProcedureEvent += OnEvent;
            ClinicalEvents.ProcedureMistake += OnMistake;
            // Done out of order already (e.g. tourniquet released early)? Then the step is satisfied on arrival.
            var session = FindFirstObjectByType<BloodDrawSession>();
            // Not when the trainee went back to this step on purpose: then the action is done again.
            if (!Reopened && session && session.IsDone(completeOnEvent)) Complete();
        }

        protected override void OnEnd()
        {
            ClinicalEvents.ProcedureEvent -= OnEvent;
            ClinicalEvents.ProcedureMistake -= OnMistake;
        }

        void OnEvent(string id)
        {
            if (id == completeOnEvent) Complete();
        }

        void OnMistake(string key) => ReportMistake(Loc.T(key));
    }
}
