using System;
using System.Collections.Generic;
using System.Linq;

namespace HSE.Clinical.Lab
{
    /// <summary>
    /// Builds the lab report for a request from the results stored on the patient (<see cref="PatientRecord.labResults"/>),
    /// so each patient always gets the same, pre-written results. Analytes without a stored value read mid-normal.
    /// </summary>
    public class LabResultGenerator
    {
        class Analyte
        {
            public string Code, Name, Unit;
            public double Low, High;           // reference range
            public double CritLow, CritHigh;   // critical limits (NaN = none)
            public int Decimals;
            public string[] Levels;            // qualitative analytes: text per level 0..n
        }

        static Analyte N(string code, string name, string unit, double lo, double hi, int dec, double critLo = double.NaN, double critHi = double.NaN) =>
            new Analyte { Code = code, Name = name, Unit = unit, Low = lo, High = hi, Decimals = dec, CritLow = critLo, CritHigh = critHi };

        static Analyte Q(string code, string name, params string[] levels) => new Analyte { Code = code, Name = name, Levels = levels };

        static readonly Dictionary<string, Analyte[]> Panels = new Dictionary<string, Analyte[]>
        {
            ["CBC"] = new[]
            {
                N("WBC", "White blood cells", "x10^9/L", 4.0, 11.0, 1, 1.0, 30),
                N("HGB", "Haemoglobin", "g/L", 130, 170, 0, 70, 200),
                N("PLT", "Platelets", "x10^9/L", 150, 400, 0, 50, 1000),
            },
            ["BIOCHEM"] = new[]
            {
                N("NA", "Sodium", "mmol/L", 135, 145, 0, 120, 160),
                N("K", "Potassium", "mmol/L", 3.5, 5.1, 1, 2.5, 6.5),
                N("UREA", "Urea", "mmol/L", 2.5, 7.8, 1, double.NaN, 30),
                N("CREA", "Creatinine", "umol/L", 60, 110, 0, double.NaN, 400),
            },
            ["LFT"] = new[]
            {
                N("ALT", "ALT", "U/L", 7, 56, 0, double.NaN, 1000),
                N("AST", "AST", "U/L", 10, 40, 0, double.NaN, 1000),
                N("ALP", "Alkaline phosphatase", "U/L", 44, 147, 0),
                N("BILI", "Bilirubin", "umol/L", 3, 21, 0, double.NaN, 300),
            },
            ["CRP"] = new[] { N("CRP", "C-reactive protein", "mg/L", 0, 5, 0, double.NaN, 300) },
            ["COAG"] = new[]
            {
                N("PT", "Prothrombin time", "s", 11, 13.5, 1),
                N("APTT", "aPTT", "s", 25, 35, 1, double.NaN, 100),
                N("INR", "INR", "", 0.8, 1.2, 1, double.NaN, 5.0),
            },
            ["GLU"] = new[] { N("GLU", "Fasting glucose", "mmol/L", 3.9, 5.5, 1, 2.5, 25) },
            ["BCULT"] = new[] { Q("BCULT", "Blood culture", "No growth at 48 h", "Gram-negative rods seen - sensitivities to follow") },
            ["UA"] = new[]
            {
                Q("UA_LEU", "Leukocytes", "Negative", "+", "++", "+++"),
                Q("UA_NIT", "Nitrite", "Negative", "Positive"),
                Q("UA_GLU", "Glucose", "Negative", "+", "++", "+++"),
                Q("UA_KET", "Ketones", "Negative", "+", "++", "+++"),
                Q("UA_PRO", "Protein", "Negative", "Trace", "+"),
            },
            ["UCULT"] = new[] { Q("UCULT", "Urine culture", "No significant growth", ">10^5 CFU/mL E. coli - sensitivities to follow") },
            ["SPUTUM"] = new[] { Q("SPUTUM", "Sputum culture", "Normal upper respiratory flora", "Streptococcus pneumoniae isolated") },
        };

        public LabReport Generate(LabOrder order)
        {
            var report = new LabReport
            {
                Patient = order.Patient,
                OrderReference = order.Reference,
                Collected = order.Time,
                Reported = DateTime.Now,
            };
            var bias = order.Patient.labResults.GroupBy(b => b.analyte).ToDictionary(g => g.Key, g => (double)g.First().value);
            foreach (var test in order.Tests)
            {
                if (!Panels.TryGetValue(test.Id, out var analytes)) continue;
                foreach (var a in analytes)
                    report.Results.Add(a.Levels != null ? Qualitative(test.Id, a, bias) : Numeric(test.Id, a, bias, order.Patient));
            }
            return report;
        }

        LabResult Numeric(string testId, Analyte a, Dictionary<string, double> bias, PatientRecord p)
        {
            double lo = a.Low, hi = a.High;
            if (a.Code == "HGB" && p.gender == "Female") { lo = 120; hi = 150; }

            double v = bias.TryGetValue(a.Code, out var stored) ? stored : lo + (hi - lo) * 0.5;
            v = Math.Round(v, a.Decimals);

            return new LabResult
            {
                TestId = testId, Analyte = a.Code, Name = a.Name, Unit = a.Unit, Numeric = v,
                Value = v.ToString("F" + a.Decimals, System.Globalization.CultureInfo.InvariantCulture),
                Reference = lo <= 0 ? $"< {Fmt(hi, a.Decimals)}" : $"{Fmt(lo, a.Decimals)} - {Fmt(hi, a.Decimals)}",
                Flag = Classify(v, lo, hi, a.CritLow, a.CritHigh),
            };
        }

        LabResult Qualitative(string testId, Analyte a, Dictionary<string, double> bias)
        {
            int level = bias.TryGetValue(a.Code, out var b) ? (int)Math.Round(b) : 0;
            level = Math.Max(0, Math.Min(a.Levels.Length - 1, level));
            return new LabResult
            {
                TestId = testId, Analyte = a.Code, Name = a.Name, Unit = "",
                Value = a.Levels[level], Reference = a.Levels[0],
                Flag = level == 0 ? ResultFlag.Normal : ResultFlag.Abnormal,
            };
        }

        /// <summary>Borderline = inside the range but in its outer 5%, or just outside it (within 5% of the limit).</summary>
        public static ResultFlag Classify(double v, double lo, double hi, double critLo = double.NaN, double critHi = double.NaN)
        {
            if (!double.IsNaN(critHi) && v >= critHi) return ResultFlag.CriticalHigh;
            if (!double.IsNaN(critLo) && v <= critLo) return ResultFlag.CriticalLow;
            double span = hi - lo, margin = Math.Max(span * 0.05, 1e-6);
            if (v > hi + hi * 0.05) return ResultFlag.High;
            if (lo > 0 && v < lo - lo * 0.05) return ResultFlag.Low;
            if (v > hi - margin || (lo > 0 && v < lo + margin)) return ResultFlag.Borderline;
            return ResultFlag.Normal;
        }

        static string Fmt(double v, int dec) => v.ToString("F" + dec, System.Globalization.CultureInfo.InvariantCulture);
    }
}
