using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>Press "Confirm Identity" on the PDA for the right patient.</summary>
    public class ConfirmIdentityStep : ClinicalEventStep
    {
        protected override void Subscribe(bool on)
        {
            if (on)
            {
                ClinicalEvents.IdentityConfirmed += OnConfirmed;
                ClinicalEvents.ProcedureMistake += OnMistake;
            }
            else
            {
                ClinicalEvents.IdentityConfirmed -= OnConfirmed;
                ClinicalEvents.ProcedureMistake -= OnMistake;
            }
        }

        void OnMistake(string key) => ReportMistake(Loc.T(key));

        void OnConfirmed(PatientRecord p)
        {
            if (IsExpected(p)) Complete();
            else ReportMistake(Loc.T("mistake.wrongIdentity"));
        }
    }
}
