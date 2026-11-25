using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>Send a lab request that matches the physician's order. Rejected attempts count as mistakes.</summary>
    public class SubmitLabOrderStep : ClinicalEventStep
    {
        protected override void Subscribe(bool on)
        {
            if (on)
            {
                ClinicalEvents.LabOrderSubmitted += OnSubmitted;
                ClinicalEvents.LabOrderRejected += OnRejected;
            }
            else
            {
                ClinicalEvents.LabOrderSubmitted -= OnSubmitted;
                ClinicalEvents.LabOrderRejected -= OnRejected;
            }
        }

        void OnSubmitted(LabOrder order)
        {
            if (IsExpected(order.Patient)) Complete();
            else ReportMistake(Loc.T("mistake.orderWrongPatient"));
        }

        void OnRejected(string reason) => ReportMistake(reason);
    }
}
