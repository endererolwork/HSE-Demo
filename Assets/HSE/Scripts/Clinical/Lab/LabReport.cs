using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HSE.Clinical.Lab
{
    public enum ResultFlag { Normal, Borderline, Low, High, CriticalLow, CriticalHigh, Abnormal }

    /// <summary>One line of a lab report.</summary>
    public class LabResult
    {
        public string TestId;
        public string Analyte;
        public string Name;
        public string Value;
        public string Unit;
        public string Reference;
        public ResultFlag Flag;
        public double? Numeric;

        public bool IsOutOfRange => Flag != ResultFlag.Normal && Flag != ResultFlag.Borderline;
    }

    /// <summary>A finished (simulated) laboratory report for one request.</summary>
    public class LabReport
    {
        public PatientRecord Patient;
        public string OrderReference;
        public DateTime Collected;
        public DateTime Reported;
        public List<LabResult> Results = new List<LabResult>();

        public IEnumerable<LabResult> OutOfRange => Results.Where(r => r.IsOutOfRange);
        public IEnumerable<LabResult> Borderline => Results.Where(r => r.Flag == ResultFlag.Borderline);

        /// <summary>Plain-text version for the AI commentary prompt and logs.</summary>
        public string ToPlainText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Patient: {Patient.fullName}, {Patient.Age} y, {Patient.gender}");
            sb.AppendLine($"Diagnosis: {Patient.diagnosis}");
            sb.AppendLine($"Request: {OrderReference}, collected {Collected:dd.MM.yyyy HH:mm}");
            foreach (var g in Results.GroupBy(r => r.TestId))
            {
                sb.AppendLine($"[{LabTestCatalog.Get(g.Key)?.Name ?? g.Key}]");
                foreach (var r in g)
                    sb.AppendLine($"  {r.Name}: {r.Value} {r.Unit} (ref {r.Reference}) - {FlagText(r.Flag)}");
            }
            return sb.ToString();
        }

        public static string FlagText(ResultFlag f) => f switch
        {
            ResultFlag.Normal => "normal",
            ResultFlag.Borderline => "borderline",
            ResultFlag.Low => "low",
            ResultFlag.High => "high",
            ResultFlag.CriticalLow => "critically low",
            ResultFlag.CriticalHigh => "critically high",
            _ => "abnormal"
        };
    }
}
