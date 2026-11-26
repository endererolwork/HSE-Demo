using System.Collections.Generic;
using System.Linq;
using HSE.Localization;
using HSE.Scenario;

namespace HSE.Training.UI
{
    /// <summary>Groups step results into the GDD's end-of-scenario categories.</summary>
    public static class ScoreCard
    {
        static readonly (string key, string[] steps)[] Categories =
        {
            ("score.identification", new[] { "scan_wristband", "confirm_identity" }),
            ("score.vitals", new[] { "blood_pressure" }),
            ("score.asepsis", new[] { "hand_hygiene" }),
            ("score.blooddraw", new[] { "blood_draw" }),
            ("score.labelling", new[] { "labelling" }),
            ("score.documentation", new[] { "document_collection" }),
            ("score.tests", new[] { "lab_order" }),
            ("score.dispatch", new[] { "dispatch" }),
            ("score.communication", new[] { "greeting", "take_pda", "approach" }),
        };

        public struct Line { public string Name; public int Score, Max, Mistakes; public bool NotApplicable; }

        public static List<Line> Build(ScenarioReport report, int penaltyPerMistake = 2)
        {
            var lines = new List<Line>();
            foreach (var (key, ids) in Categories)
            {
                var results = report.Results.Where(r => ids.Contains(r.Step.Id)).ToList();
                if (results.Count == 0) continue;
                var line = new Line { Name = Loc.T(key) };
                foreach (var r in results)
                {
                    line.Mistakes += r.Mistakes.Count;
                    if (r.Outcome == StepState.NotApplicable) { line.NotApplicable = true; continue; }
                    line.Max += r.Step.Points;
                    if (r.Outcome == StepState.Completed) line.Score += System.Math.Max(0, r.Step.Points - penaltyPerMistake * r.Mistakes.Count);
                }
                lines.Add(line);
            }
            return lines;
        }
    }
}