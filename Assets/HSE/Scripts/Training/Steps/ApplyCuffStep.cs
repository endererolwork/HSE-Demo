using HSE.Clinical;

namespace HSE.Training.Steps
{
    /// <summary>Put the blood pressure cuff on the patient's upper arm (the arm socket).</summary>
    public class ApplyCuffStep : ClinicalEventStep
    {
        protected override void Subscribe(bool on)
        {
            if (on)
            {
                ClinicalEvents.CuffApplied += Complete;
                if (ClinicalEvents.CuffOnPatient) Complete();
            }
            else ClinicalEvents.CuffApplied -= Complete;
        }
    }
}