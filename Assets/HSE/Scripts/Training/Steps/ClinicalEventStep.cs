using HSE.Clinical;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>Base for steps that react to <see cref="ClinicalEvents"/>: subscribe while active, unsubscribe on end.</summary>
    public abstract class ClinicalEventStep : ScenarioStep
    {
        [Tooltip("Patient this step expects. Empty = the patient chosen for this session (ActivePatient).")]
        [SerializeField] protected PatientRecord expectedPatient;

        public void SetExpectedPatient(PatientRecord patient) => expectedPatient = patient;

        /// <summary>The patient this step expects: the one set in the inspector, otherwise the patient in the bed this session.</summary>
        protected PatientRecord Expected => expectedPatient ? expectedPatient : ActivePatient.Current;

        protected bool IsExpected(PatientRecord p) => Expected == null || p == Expected;

        protected sealed override void OnBegin() => Subscribe(true);
        protected sealed override void OnEnd() => Subscribe(false);

        protected abstract void Subscribe(bool on);
    }
}
