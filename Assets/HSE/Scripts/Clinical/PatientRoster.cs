using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSE.Clinical
{
    /// <summary>
    /// Chooses which patient is in the bed for this session and applies it to the scene:
    /// the wristband QR, the patient figure's look, and <see cref="ActivePatient"/> for the scenario steps.
    /// </summary>
    public class PatientRoster : MonoBehaviour
    {
        [SerializeField] List<PatientRecord> patients = new List<PatientRecord>();
        [Tooltip("Pick a random patient each session. Off = always use Fixed Index.")]
        [SerializeField] bool randomPatient = true;
        [SerializeField, Min(0)] int fixedIndex;
        [SerializeField] ScannableCode wristband;
        [Tooltip("Root of the patient figure; renderers named Skull/Nose/Ear_*/Forearm_*/Hand_*/Neck, Hair and Torso/Shoulder_*/UpperArm_* get recoloured.")]
        [SerializeField] Transform[] figureRoots = Array.Empty<Transform>();

        public IReadOnlyList<PatientRecord> Patients => patients;

        public void Configure(IEnumerable<PatientRecord> list, ScannableCode band, Transform[] figures)
        {
            patients = new List<PatientRecord>(list);
            wristband = band;
            figureRoots = figures;
        }

        void Awake()
        {
            if (patients.Count == 0) return;
            int i = randomPatient ? UnityEngine.Random.Range(0, patients.Count) : Mathf.Clamp(fixedIndex, 0, patients.Count - 1);
            Apply(patients[i]);
        }

        /// <summary>Puts a different patient in the bed (random, never the same one twice in a row).</summary>
        public PatientRecord ApplyNext()
        {
            if (patients.Count == 0) return null;
            int current = patients.IndexOf(ActivePatient.Current);
            int next = current;
            if (patients.Count > 1)
                while (next == current) next = UnityEngine.Random.Range(0, patients.Count);
            else next = 0;
            Apply(patients[next]);
            return patients[next];
        }

        public void Apply(PatientRecord p)
        {
            ActivePatient.Set(p);
            if (wristband)
            {
                wristband.patient = p;
                wristband.code = p.WristbandCode;
            }
            foreach (var root in figureRoots)
            {
                if (!root) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var c = ColorFor(r.name, p);
                    if (c.HasValue) Tint(r, c.Value);
                }
            }
        }

        static Color? ColorFor(string name, PatientRecord p)
        {
            if (name.StartsWith("Hair") || name.StartsWith("Eye_")) return name.StartsWith("Hair") ? p.hairColor : (Color?)null;
            if (name.StartsWith("Skull") || name.StartsWith("Nose") || name.StartsWith("Ear_") || name.StartsWith("Neck") ||
                name.StartsWith("Forearm_") || name.StartsWith("Hand_") || name.StartsWith("AntecubitalFossa")) return p.skinTone;
            if (name.StartsWith("Torso") || name.StartsWith("Shoulder_") || name.StartsWith("UpperArm_")) return p.gownColor;
            return null;
        }

        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        static void Tint(Renderer r, Color c)
        {
            r.GetPropertyBlock(Block);
            Block.SetColor(BaseColor, c);
            r.SetPropertyBlock(Block);
        }
    }

    /// <summary>The patient in the bed this session. Steps compare scans against this.</summary>
    public static class ActivePatient
    {
        public static PatientRecord Current { get; private set; }
        public static event Action<PatientRecord> Changed;

        public static void Set(PatientRecord p)
        {
            Current = p;
            Changed?.Invoke(p);
        }
    }
}
