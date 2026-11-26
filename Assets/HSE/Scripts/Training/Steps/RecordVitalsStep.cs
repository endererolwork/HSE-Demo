using HSE.Clinical;
using HSE.Clinical.Vitals;
using HSE.Localization;

namespace HSE.Training.Steps
{
    /// <summary>Record the measured blood pressure and pulse in the patient's record on the PDA.</summary>
    public class RecordVitalsStep : ClinicalEventStep
    {
        protected override void Subscribe(bool on)
        {
            if (on) ClinicalEvents.VitalsRecorded += OnRecorded;
            else ClinicalEvents.VitalsRecorded -= OnRecorded;
        }

        void OnRecorded(PatientRecord p, VitalSample _)
        {
            if (IsExpected(p)) Complete();
            else ReportMistake(Loc.T("mistake.wrongIdentity"));
        }
    }
}