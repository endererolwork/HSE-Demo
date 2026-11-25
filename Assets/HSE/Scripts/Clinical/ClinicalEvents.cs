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

    /// <summary>Clinical events raised by devices (PDA, scanner). Scenario steps listen to these.</summary>
    public static class ClinicalEvents
    {
        public static event Action<ScannableCode> CodeScanned;
        public static event Action<PatientRecord> IdentityConfirmed;
        public static event Action<LabOrder> LabOrderSubmitted;
        public static event Action<string> LabOrderRejected;
        public static event Action<Lab.LabReport> LabReportReady;
        public static event Action<Lab.LabReport, string> LabCommentReady;

        public static void RaiseCodeScanned(ScannableCode c) => CodeScanned?.Invoke(c);
        public static void RaiseIdentityConfirmed(PatientRecord p) => IdentityConfirmed?.Invoke(p);
        public static void RaiseLabOrderSubmitted(LabOrder o) => LabOrderSubmitted?.Invoke(o);
        public static void RaiseLabOrderRejected(string reason) => LabOrderRejected?.Invoke(reason);
        public static void RaiseLabReportReady(Lab.LabReport r) => LabReportReady?.Invoke(r);
        public static void RaiseLabCommentReady(Lab.LabReport r, string comment) => LabCommentReady?.Invoke(r, comment);
    }
}
