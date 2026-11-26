using System.Collections.Generic;
using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// While the needle is in the vein, touching a sample tube to the back of the holder (or anywhere on the
    /// holder/needle) fills it with blood: a red column rises in the tube. A full tube raises
    /// "tube_filled:&lt;tube name&gt;".
    /// </summary>
    public class TubeFilling : MonoBehaviour
    {
        [SerializeField] NeedleInsertion needle;
        [SerializeField] Transform holderPort;
        [SerializeField] List<XRGrabInteractable> tubes = new List<XRGrabInteractable>();
        [SerializeField] float portRadius = 0.08f;
        [SerializeField] float holderRadius = 0.07f;
        [SerializeField] float fillSeconds = 2f;

        public IReadOnlyList<XRGrabInteractable> Tubes => tubes;
        public bool IsFull(XRGrabInteractable t) => _fill.TryGetValue(t, out var l) && l >= 1f;

        readonly Dictionary<XRGrabInteractable, float> _fill = new Dictionary<XRGrabInteractable, float>();
        float _nextPulse;

        public void Configure(NeedleInsertion insertion, Transform port, IEnumerable<XRGrabInteractable> sampleTubes)
        {
            needle = insertion;
            holderPort = port;
            tubes = new List<XRGrabInteractable>(sampleTubes);
        }

        public void ResetTubes()
        {
            _fill.Clear();
            foreach (var t in tubes) SetLevel(t, 0);
        }

        void Update()
        {
            if (!needle || !needle.Inserted || !holderPort) return;
            var holder = needle.Holder;
            foreach (var t in tubes)
            {
                if (!t || !Touching(t.transform.position, holder)) continue;
                _fill.TryGetValue(t, out var level);
                if (level >= 1f) continue;
                level = Mathf.Min(1f, level + Time.deltaTime / fillSeconds);
                _fill[t] = level;
                SetLevel(t, level);
                Pulse(t, level >= 1f ? 0.6f : 0.15f, level >= 1f);
                if (level >= 1f) ClinicalEvents.RaiseProcedure("tube_filled:" + t.name);
            }
        }

        bool Touching(Vector3 p, Transform holder)
        {
            if ((p - holderPort.position).sqrMagnitude <= portRadius * portRadius) return true;
            return holder && (p - holder.position).sqrMagnitude <= holderRadius * holderRadius;
        }

        void Pulse(XRGrabInteractable tube, float strength, bool force)
        {
            if (!force && Time.time < _nextPulse) return;
            _nextPulse = Time.time + 0.25f;
            foreach (var i in tube.interactorsSelecting)
                if (i is XRBaseInputInteractor input) input.SendHapticImpulse(strength, 0.05f);
        }

        static void SetLevel(XRGrabInteractable tube, float level)
        {
            var blood = tube ? tube.transform.Find("Blood") : null;
            if (!blood) return;
            var s = blood.localScale;
            blood.localScale = new Vector3(s.x, Mathf.Max(0.0001f, level * 0.04f), s.z);
            blood.localPosition = new Vector3(0, -0.05f + level * 0.04f, 0);
            blood.gameObject.SetActive(level > 0.01f);
        }
    }
}
