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

        // Blood pressure
        public static event Action CuffApplied;
        public static event Action CuffRemoved;
        public static event Action BloodPressureStartedWithoutCuff;
        public static event Action<Vitals.VitalSample> BloodPressureMeasured;
        public static event Action<PatientRecord, Vitals.VitalSample> VitalsRecorded;

        public static bool CuffOnPatient { get; private set; }

        public static void RaiseCuffApplied() { CuffOnPatient = true; CuffApplied?.Invoke(); }
        public static void RaiseCuffRemoved() { CuffOnPatient = false; CuffRemoved?.Invoke(); }
        public static void RaiseBloodPressureStartedWithoutCuff() => BloodPressureStartedWithoutCuff?.Invoke();
        public static void RaiseBloodPressureMeasured(Vitals.VitalSample s) => BloodPressureMeasured?.Invoke(s);
        public static void RaiseVitalsRecorded(PatientRecord p, Vitals.VitalSample s) => VitalsRecorded?.Invoke(p, s);

        // Procedures (blood draw etc.): named events like "tourniquet_on", "needle_in", "tube_filled:Tube_SST_Gold".
        public static event Action<string> ProcedureEvent;
        /// <summary>A procedure rule was broken; the argument is a localisation key for the message.</summary>
        public static event Action<string> ProcedureMistake;

        public static void RaiseProcedure(string id) => ProcedureEvent?.Invoke(id);
        public static void RaiseProcedureMistake(string messageKey) => ProcedureMistake?.Invoke(messageKey);
    }
}
