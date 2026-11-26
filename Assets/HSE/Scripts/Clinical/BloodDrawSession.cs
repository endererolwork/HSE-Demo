using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HSE.Clinical
{
    /// <summary>
    /// Safety rules of the blood draw, independent of the scenario steps:
    /// tourniquet and skin cleaning before the needle, only the tubes the physician's order needs,
    /// and the tourniquet released before the needle comes out. Raises "tubes_complete" when every
    /// required tube is filled.
    /// </summary>
    public class BloodDrawSession : MonoBehaviour
    {
        /// <summary>Sample tube (scene object name) for each blood test.</summary>
        public static readonly Dictionary<string, string> TubeForTest = new Dictionary<string, string>
        {
            ["CBC"] = "Tube_EDTA_Lavender",
            ["BIOCHEM"] = "Tube_SST_Gold",
            ["LFT"] = "Tube_SST_Gold",
            ["CRP"] = "Tube_SST_Gold",
            ["COAG"] = "Tube_Citrate_LightBlue",
            ["GLU"] = "Tube_Fluoride_Gray",
        };

        public bool TourniquetOn { get; private set; }
        public bool SiteCleaned { get; private set; }
        public bool NeedleIn { get; private set; }
        public IReadOnlyCollection<string> RequiredTubes => _required;
        public IReadOnlyCollection<string> FilledTubes => _filled;

        readonly HashSet<string> _happened = new HashSet<string>();
        readonly HashSet<string> _required = new HashSet<string>();

        /// <summary>Whether the state an event stands for currently holds (used when a step opens after the fact).</summary>
        public bool IsDone(string eventId) => eventId switch
        {
            "tourniquet_on" => TourniquetOn,
            "tourniquet_off" => !TourniquetOn && _happened.Contains("tourniquet_on"),
            "needle_in" => NeedleIn,
            "needle_out" => !NeedleIn && _happened.Contains("needle_in"),
            "tubes_complete" => _required.Count > 0 && _required.All(_filled.Contains),
            _ => _happened.Contains(eventId),
        };
        readonly HashSet<string> _filled = new HashSet<string>();

        void OnEnable()
        {
            ClinicalEvents.ProcedureEvent += OnEvent;
            ActivePatient.Changed += OnPatient;
            if (ActivePatient.Current) OnPatient(ActivePatient.Current);
        }

        void OnDisable()
        {
            ClinicalEvents.ProcedureEvent -= OnEvent;
            ActivePatient.Changed -= OnPatient;
        }

        void OnPatient(PatientRecord p)
        {
            TourniquetOn = SiteCleaned = NeedleIn = false;
            _happened.Clear();
            _filled.Clear();
            _required.Clear();
            if (!p) return;
            foreach (var id in p.physicianOrderedTestIds)
                if (TubeForTest.TryGetValue(id, out var tube)) _required.Add(tube);
        }

        void OnEvent(string id)
        {
            _happened.Add(id);
            switch (id)
            {
                case "tourniquet_on": TourniquetOn = true; break;
                case "tourniquet_off": TourniquetOn = false; break;
                case "site_cleaned": SiteCleaned = true; break;
                case "needle_in":
                    NeedleIn = true;
                    if (!TourniquetOn) ClinicalEvents.RaiseProcedureMistake("mistake.noTourniquet");
                    if (!SiteCleaned) ClinicalEvents.RaiseProcedureMistake("mistake.siteNotCleaned");
                    break;
                case "needle_out":
                    NeedleIn = false;
                    if (TourniquetOn) ClinicalEvents.RaiseProcedureMistake("mistake.tourniquetStillOn");
                    break;
                default:
                    if (id.StartsWith("tube_filled:")) OnTubeFilled(id.Substring("tube_filled:".Length));
                    break;
            }
        }

        void OnTubeFilled(string tube)
        {
            if (!_required.Contains(tube))
            {
                ClinicalEvents.RaiseProcedureMistake("mistake.wrongTube");
                return;
            }
            _filled.Add(tube);
            if (_required.All(_filled.Contains)) ClinicalEvents.RaiseProcedure("tubes_complete");
        }
    }
}
