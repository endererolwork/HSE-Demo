using System;
using System.Collections.Generic;

namespace HSE.Clinical
{
    public class LabOrder
    {
        public PatientRecord Patient;
        public List<LabTest> Tests = new List<LabTest>();
        public bool Urgent;
        public string Reference;
        public DateTime Time;
    }

    /// <summary>Scenario-level events the step/checklist manager will listen to.</summary>
    public static class ClinicalEvents
    {
        public static event Action<ScannableCode> CodeScanned;
        public static event Action<PatientRecord> IdentityConfirmed;
        public static event Action<LabOrder> LabOrderSubmitted;

        public static void RaiseCodeScanned(ScannableCode c) => CodeScanned?.Invoke(c);
        public static void RaiseIdentityConfirmed(PatientRecord p) => IdentityConfirmed?.Invoke(p);
        public static void RaiseLabOrderSubmitted(LabOrder o) => LabOrderSubmitted?.Invoke(o);
    }
}
