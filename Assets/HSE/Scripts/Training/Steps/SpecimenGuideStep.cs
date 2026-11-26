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
    /// Specimen-handling sub-tasks that work through items created during play (printed labels, the request slip),
    /// one at a time: the highlight and the guide arrow move to the next thing to pick up and where it goes.
    /// Labels: next label + its tube. Bagging: next labelled tube + the bag. Slip: the slip + the bag.
    /// </summary>
    public class SpecimenGuideStep : ProcedureStep, IStepTextProvider
    {
        public enum Mode { ApplyLabels, BagTubes, PocketSlip }

        [SerializeField] Mode mode;
        [SerializeField] GameObject bag;

        string _cue;
        float _nextCheck;
        GameObject[] _current = new GameObject[0];

        public void SetMode(Mode m, GameObject specimenBag)
        {
            mode = m;
            bag = specimenBag;
        }

        public string CueText => State == StepState.Active ? _cue : null;
        public string InstructionText => null;

        protected override void OnBegin()
        {
            base.OnBegin();
            _current = new GameObject[0];
            _nextCheck = 0;
        }

        void Update()
        {
            if (State != StepState.Active || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.25f;
            var targets = Targets().Where(t => t).ToArray();
            if (!targets.SequenceEqual(_current))
            {
                _current = targets;
                SetHighlightTargets(targets);
            }
        }

        IEnumerable<GameObject> Targets()
        {
            var session = FindFirstObjectByType<BloodDrawSession>();
            var plan = session ? FillTubesStep.OrderOfDraw.Where(session.RequiredTubes.Contains).ToList() : new List<string>();
            switch (mode)
            {
                case Mode.ApplyLabels:
                {
                    IReadOnlyCollection<string> done = session ? session.LabeledTubes : new string[0];
                    string next = plan.FirstOrDefault(t => !done.Contains(t));
                    var label = FindObjectsByType<LabelSticker>(FindObjectsSortMode.None)
                        .FirstOrDefault(l => !l.Applied && l.Tube && l.Tube.name == next);
                    _cue = next == null ? null : Loc.F("specimen.labelCue", plan.IndexOf(next) + 1, plan.Count, Loc.T("tube." + next));
                    if (next == null) return new GameObject[0];
                    return new[] { label ? label.gameObject : null, label && label.Tube ? label.Tube.gameObject : null };
                }
                case Mode.BagTubes:
                {
                    IReadOnlyCollection<string> done = session ? session.BaggedTubes : new string[0];
                    string next = plan.FirstOrDefault(t => !done.Contains(t));
                    _cue = next == null ? null : Loc.F("specimen.bagCue", plan.IndexOf(next) + 1, plan.Count, Loc.T("tube." + next));
                    var fill = FindFirstObjectByType<TubeFilling>(FindObjectsInactive.Include);
                    var tube = fill && next != null ? fill.Tubes.FirstOrDefault(t => t && t.name == next) : null;
                    return new[] { tube ? tube.gameObject : null, bag };
                }
                default:
                {
                    var slip = FindObjectsByType<RequestSlipMarker>(FindObjectsSortMode.None).FirstOrDefault();
                    _cue = Loc.T(slip ? "specimen.slipCue" : "specimen.noSlip");
                    return new[] { slip ? slip.gameObject : null, bag };
                }
            }
        }
    }
}
