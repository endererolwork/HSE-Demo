using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HSE.Clinical;
using HSE.Localization;
using HSE.Scenario;
using HSE.Training.UI;
using UnityEngine;

namespace HSE.Training
{
    /// <summary>
    /// Saves every finished run for the instructor: one JSON file per run (steps, timings, mistakes, categories)
    /// and a line in sessions.csv. Folder: Application.persistentDataPath/HSE_Results
    /// (on Quest: Android/data/&lt;package&gt;/files/HSE_Results, reachable over USB).
    /// </summary>
    public class ResultsLogger : MonoBehaviour
    {
        [SerializeField] ScenarioRunner runner;
        [SerializeField] string traineeId = "VR User 1";

        public static string Folder => Path.Combine(Application.persistentDataPath, "HSE_Results");
        public string LastFile { get; private set; }

        public void Bind(ScenarioRunner scenario) => runner = scenario;

        void OnEnable() { if (runner) runner.ScenarioCompleted += Save; }
        void OnDisable() { if (runner) runner.ScenarioCompleted -= Save; }

        void Save(ScenarioReport report)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var p = ActivePatient.Current;
                var stamp = DateTime.Now;
                string name = $"{stamp:yyyyMMdd_HHmmss}_{(p ? p.fullName.Replace(' ', '_') : "unknown")}";
                LastFile = Path.Combine(Folder, name + ".json");
                File.WriteAllText(LastFile, ToJson(report, p, stamp), Encoding.UTF8);

                string csv = Path.Combine(Folder, "sessions.csv");
                bool header = !File.Exists(csv);
                using (var w = new StreamWriter(csv, true, Encoding.UTF8))
                {
                    if (header) w.WriteLine("date,trainee,patient,variant,language,score,max,mistakes,seconds,ended_early");
                    w.WriteLine(string.Join(",", new[]
                    {
                        stamp.ToString("yyyy-MM-dd HH:mm:ss"), Csv(traineeId), Csv(p ? p.fullName : ""), TrainingBranch.Current.ToString(),
                        Loc.Current.ToString(), report.Score.ToString(), report.MaxScore.ToString(), report.MistakeCount.ToString(),
                        report.TotalTime.ToString("0", CultureInfo.InvariantCulture), Csv(report.EndedEarlyReason ?? "")
                    }));
                }
                Debug.Log("[HSE] Results saved to " + LastFile);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HSE] Could not save results: " + e.Message);
            }
        }

        string ToJson(ScenarioReport r, PatientRecord p, DateTime stamp)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"date\": \"{stamp:yyyy-MM-ddTHH:mm:ss}\",\n");
            sb.Append($"  \"trainee\": {Q(traineeId)},\n");
            sb.Append($"  \"patient\": {Q(p ? p.fullName : "")},\n");
            sb.Append($"  \"mrn\": {Q(p ? p.medicalRecordNumber : "")},\n");
            sb.Append($"  \"variant\": {Q(TrainingBranch.Current.ToString())},\n");
            sb.Append($"  \"language\": {Q(Loc.Current.ToString())},\n");
            sb.Append($"  \"score\": {r.Score},\n  \"maxScore\": {r.MaxScore},\n  \"mistakes\": {r.MistakeCount},\n");
            sb.Append($"  \"seconds\": {r.TotalTime.ToString("0.0", CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"endedEarly\": {Q(r.EndedEarlyReason ?? "")},\n");
            sb.Append("  \"categories\": [\n");
            sb.Append(string.Join(",\n", ScoreCard.Build(r).Select(c =>
                $"    {{ \"name\": {Q(c.Name)}, \"score\": {c.Score}, \"max\": {c.Max}, \"mistakes\": {c.Mistakes}, \"notApplicable\": {(c.NotApplicable ? "true" : "false")} }}")));
            sb.Append("\n  ],\n  \"steps\": [\n");
            sb.Append(string.Join(",\n", r.Results.Select(s =>
                $"    {{ \"id\": {Q(s.Step.Id)}, \"title\": {Q(s.Step.Title)}, \"requirement\": {Q(s.Step.Requirement.ToString())}, " +
                $"\"outcome\": {Q(s.Outcome.ToString())}, \"seconds\": {s.Duration.ToString("0.0", CultureInfo.InvariantCulture)}, " +
                $"\"mistakes\": [{string.Join(", ", s.Mistakes.Select(Q))}] }}")));
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";
        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }
}
