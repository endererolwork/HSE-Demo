using HSE.Clinical;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>Base for steps that react to <see cref="ClinicalEvents"/>: subscribe while active, unsubscribe on end.</summary>
    public abstract class ClinicalEventStep : ScenarioStep
    {
        [Tooltip("Patient this step expects. Empty = any patient.")]
        [SerializeField] protected PatientRecord expectedPatient;

        public void SetExpectedPatient(PatientRecord patient) => expectedPatient = patient;

        protected bool IsExpected(PatientRecord p) => expectedPatient == null || p == expectedPatient;

        protected sealed override void OnBegin() => Subscribe(true);
        protected sealed override void OnEnd() => Subscribe(false);

        protected abstract void Subscribe(bool on);
    }
}
