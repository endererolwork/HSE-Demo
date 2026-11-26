using System.Collections.Generic;
using System.Linq;
using HSE.Clinical;
using HSE.Localization;
using HSE.Training.Steps;
using TMPro;
using UnityEngine;

namespace HSE.Training.UI
{
    /// <summary>
    /// Tells the trainee when a sample tube is full: a short message in front of their eyes plus a chime
    /// ("Tube full (1/2). Next: gold cap"), and a different one when every ordered tube is filled.
    /// </summary>
    public class TubeFillNotice : MonoBehaviour
    {
        [SerializeField] float showSeconds = 3f;
        [SerializeField] float distance = 0.55f;
        [SerializeField] float dropBelowEyes = 0.12f;

        TextMeshPro _text;
        AudioSource _audio;
        AudioClip _ding, _done;
        Transform _head;
        float _hideAt;
        readonly HashSet<string> _filled = new HashSet<string>();

        void OnEnable()
        {
            ClinicalEvents.ProcedureEvent += OnEvent;
            ActivePatient.Changed += OnPatient;
        }

        void OnDisable()
        {
            ClinicalEvents.ProcedureEvent -= OnEvent;
            ActivePatient.Changed -= OnPatient;
        }

        void OnPatient(PatientRecord _) => _filled.Clear();

        void OnEvent(string id)
        {
            var session = FindFirstObjectByType<BloodDrawSession>();
            if (!session) return;
            var plan = FillTubesStep.OrderOfDraw.Where(session.RequiredTubes.Contains).ToList();

            if (id == "tubes_complete")
            {
                Show("<color=#5BE38A><b>" + Loc.T("tubes.allDone") + "</b></color>\n<size=70%>" + Loc.T("tubes.allDoneHint") + "</size>", true);
                return;
            }
            if (!id.StartsWith("tube_filled:")) return;
            string tube = id.Substring("tube_filled:".Length);
            if (!plan.Contains(tube)) return; // a wrong tube is reported as a mistake elsewhere
            _filled.Add(tube);
            if (plan.All(_filled.Contains)) return; // "tubes_complete" follows straight away
            string next = plan.FirstOrDefault(t => !_filled.Contains(t));
            Show("<b>" + Loc.F("tubes.full", _filled.Count, plan.Count) + "</b>" +
                 (next != null ? "\n<size=70%>" + Loc.F("tubes.next", Loc.T("tube." + next)) + "</size>" : ""), false);
        }

        void Show(string message, bool complete)
        {
            EnsureView();
            _text.text = message;
            _text.gameObject.SetActive(true);
            _hideAt = Time.time + showSeconds;
            Place();
            var clip = complete ? _done : _ding;
            if (clip) _audio.PlayOneShot(clip, 0.8f);
        }

        void LateUpdate()
        {
            if (!_text || !_text.gameObject.activeSelf) return;
            if (Time.time > _hideAt) { _text.gameObject.SetActive(false); return; }
            Place();
        }

        void Place()
        {
            if (!_head && Camera.main) _head = Camera.main.transform;
            if (!_head) return;
            var flat = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = _head.up;
            var target = _head.position + flat.normalized * distance + Vector3.down * dropBelowEyes;
            var t = _text.transform;
            t.position = Vector3.Lerp(t.position, target, Mathf.Clamp01(Time.deltaTime * 8f));
            t.rotation = Quaternion.LookRotation(t.position - _head.position, Vector3.up);
        }

        void EnsureView()
        {
            if (_text) return;
            var go = new GameObject("TubeFillNotice_Text");
            go.transform.SetParent(transform, false);
            _text = go.AddComponent<TextMeshPro>();
            _text.rectTransform.sizeDelta = new Vector2(0.5f, 0.15f);
            _text.fontSize = 0.45f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = Color.white;
            _text.outlineWidth = 0.2f;
            _text.outlineColor = new Color32(0, 0, 0, 200);
            _text.sortingOrder = 50;
            if (!_head && Camera.main) _head = Camera.main.transform;
            if (_head) go.transform.position = _head.position + _head.forward * distance;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _ding = Chime("TubeFull", new[] { 880f }, 0.25f);
            _done = Chime("TubesDone", new[] { 660f, 880f, 1320f }, 0.18f);
        }

        /// <summary>Soft sine "ding" notes, generated so no audio asset is needed.</summary>
        static AudioClip Chime(string name, float[] notes, float noteSeconds)
        {
            const int rate = 44100;
            int per = (int)(rate * noteSeconds);
            var data = new float[per * notes.Length];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Exp(-t * 9f) * Mathf.Clamp01(i / 200f);
                    data[n * per + i] = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * env * 0.5f;
                }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
