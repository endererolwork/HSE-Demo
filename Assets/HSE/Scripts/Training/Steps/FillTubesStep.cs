using System.Collections.Generic;
using System.Linq;
using HSE.Clinical;
using HSE.Devices;
using HSE.Localization;
using HSE.Scenario;
using HSE.Training.UI;
using UnityEngine;

namespace HSE.Training.Steps
{
    /// <summary>
    /// "Fill the tubes", one tube at a time: only the tubes this patient's orders need, in the standard order of
    /// draw. The next tube lights up and the guide arrow names it ("Tube 1/2: light blue cap"); when it is full
    /// the next one lights up. Completes on "tubes_complete".
    /// </summary>
    public class FillTubesStep : ProcedureStep, IStepTextProvider
    {
        /// <summary>Order of draw (CLSI): citrate, serum, heparin, EDTA, fluoride.</summary>
        public static readonly string[] OrderOfDraw =
            { "Tube_Citrate_LightBlue", "Tube_SST_Gold", "Tube_Heparin_Green", "Tube_EDTA_Lavender", "Tube_Fluoride_Gray" };

        readonly HashSet<string> _filled = new HashSet<string>();
        List<string> _plan = new List<string>();
        string _next;

        public string CueText => State == StepState.Active && _next != null
            ? Loc.F("tubes.cue", _plan.IndexOf(_next) + 1, _plan.Count, Loc.T("tube." + _next))
            : null;

        public string InstructionText => State == StepState.Active && _plan.Count > 0
            ? Loc.F("tubes.instr", _plan.Count, string.Join(" → ", _plan.Select(t => Loc.T("tube." + t))))
            : null;

        protected override void OnBegin()
        {
            base.OnBegin();
            if (State != StepState.Active) return; // already complete
            var session = FindFirstObjectByType<BloodDrawSession>();
            _filled.Clear();
            _plan = session ? OrderOfDraw.Where(session.RequiredTubes.Contains).ToList() : new List<string>();
            if (session) foreach (var t in session.FilledTubes) _filled.Add(t);
            ClinicalEvents.ProcedureEvent += OnTubeEvent;
            PointAtNext();
        }

        protected override void OnEnd()
        {
            ClinicalEvents.ProcedureEvent -= OnTubeEvent;
            _next = null;
            base.OnEnd();
        }

        void OnTubeEvent(string id)
        {
            if (!id.StartsWith("tube_filled:")) return;
            _filled.Add(id.Substring("tube_filled:".Length));
            PointAtNext();
        }

        void PointAtNext()
        {
            _next = _plan.FirstOrDefault(t => !_filled.Contains(t));
            var tube = _next != null ? FindTube(_next) : null;
            SetHighlightTargets(tube ? new[] { tube } : null);
        }

        static GameObject FindTube(string name)
        {
            var fill = FindFirstObjectByType<TubeFilling>();
            var t = fill ? fill.Tubes.FirstOrDefault(x => x && x.name == name) : null;
            return t ? t.gameObject : GameObject.Find(name);
        }
    }
}
