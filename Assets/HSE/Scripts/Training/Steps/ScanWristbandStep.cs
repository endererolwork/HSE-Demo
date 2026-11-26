using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>Scan the right patient's wristband with the PDA. Any other code is a mistake.</summary>
    public class ScanWristbandStep : ClinicalEventStep
    {
        protected override void Subscribe(bool on)
        {
            if (on) ClinicalEvents.CodeScanned += OnScanned;
            else ClinicalEvents.CodeScanned -= OnScanned;
        }

        void OnScanned(ScannableCode code)
        {
            if (code.kind != CodeKind.PatientWristband) ReportMistake(Loc.T("mistake.notWristband"));
            else Complete(); // a wrong band is caught at identity confirmation (compare with what the patient said)
        }
    }
}
