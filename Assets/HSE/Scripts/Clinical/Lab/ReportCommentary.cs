using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace HSE.Clinical.Lab
{
    /// <summary>Writes a short plain-language comment on a lab report. Implementations may use stored text or build it from the flags.</summary>
    public interface IReportCommentator
    {
        /// <summary>Coroutine; calls <paramref name="done"/> exactly once with the comment (never null).</summary>
        IEnumerator Comment(LabReport report, Action<string> done);
    }

    /// <summary>Uses the comment stored on the patient record; falls back to <see cref="RuleBasedCommentator"/> if it is empty.</summary>
    public class StoredCommentator : IReportCommentator
    {
        public IEnumerator Comment(LabReport report, Action<string> done)
        {
            var stored = report.Patient ? report.Patient.labComment : null;
            done(string.IsNullOrWhiteSpace(stored) ? RuleBasedCommentator.Build(report) : stored.Trim());
            yield break;
        }
    }

    /// <summary>Offline commentary built from the flags. Used when no AI endpoint is configured, and as the fallback.</summary>
    public class RuleBasedCommentator : IReportCommentator
    {
        public IEnumerator Comment(LabReport report, Action<string> done)
        {
            done(Build(report));
            yield break;
        }

        public static string Build(LabReport r)
        {
            var sb = new StringBuilder();
            var out_ = r.OutOfRange.ToList();
            var border = r.Borderline.ToList();
            if (out_.Count == 0 && border.Count == 0)
            {
                sb.Append("All requested results are within the reference ranges. No action needed beyond the routine plan.");
                return sb.ToString();
            }
            if (out_.Count > 0)
                sb.Append("Outside the reference range: ")
                  .Append(string.Join(", ", out_.Select(x => $"{x.Name} {x.Value}{(string.IsNullOrEmpty(x.Unit) ? "" : " " + x.Unit)} ({LabReport.FlagText(x.Flag)})")))
                  .Append(". ");
            if (border.Count > 0)
                sb.Append("Borderline: ").Append(string.Join(", ", border.Select(x => $"{x.Name} {x.Value}"))).Append(". ");

            bool Has(params string[] codes) => out_.Any(x => codes.Contains(x.Analyte));
            if (Has("WBC", "CRP")) sb.Append("Raised inflammatory markers fit an ongoing infection. ");
            if (Has("INR", "PT")) sb.Append("Clotting is prolonged; check the anticoagulation plan and watch for bleeding. ");
            if (Has("GLU", "UA_GLU", "UA_KET")) sb.Append("Glucose control is poor and ketones are present; follow the diabetes plan. ");
            if (Has("ALT", "AST", "ALP", "BILI")) sb.Append("Liver tests are raised, in keeping with the suspected liver problem. ");
            if (Has("CREA", "UREA")) sb.Append("Kidney function is reduced; keep the fluid chart and review medicines. ");
            if (Has("UA_LEU", "UA_NIT", "UCULT")) sb.Append("Urine findings support a urinary tract infection. ");
            if (out_.Any(x => x.Flag == ResultFlag.CriticalHigh || x.Flag == ResultFlag.CriticalLow))
                sb.Append("There is a critical value: inform the doctor straight away. ");
            else
                sb.Append("Share these results with the responsible doctor.");
            return sb.ToString().Trim();
        }
    }
}
