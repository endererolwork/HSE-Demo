using HSE.Clinical;
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
            if (code.kind != CodeKind.PatientWristband) ReportMistake("Okunan kod hasta bilekliği değil.");
            else if (!IsExpected(code.patient)) ReportMistake("Yanlış hasta! Bileklik bu hastaya ait değil.");
            else Complete();
        }
    }
}
