using HSE.Clinical;
using HSE.Clinical.Vitals;
using HSE.Localization;

namespace HSE.Training.Steps
{
    /// <summary>Start the NIBP measurement on the monitor. Pressing start without the cuff on is a mistake.</summary>
    public class MeasureBloodPressureStep : ClinicalEventStep
    {
        protected override void Subscribe(bool on)
        {
            if (on)
            {
                ClinicalEvents.BloodPressureMeasured += OnMeasured;
                ClinicalEvents.BloodPressureStartedWithoutCuff += OnNoCuff;
            }
            else
            {
                ClinicalEvents.BloodPressureMeasured -= OnMeasured;
                ClinicalEvents.BloodPressureStartedWithoutCuff -= OnNoCuff;
            }
        }

        void OnMeasured(VitalSample _) => Complete();
        void OnNoCuff() => ReportMistake(Loc.T("mistake.noCuff"));
    }
}