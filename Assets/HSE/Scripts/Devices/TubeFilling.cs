using System.Collections.Generic;
using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// While the needle is in the vein, pushing a held sample tube onto the back of the holder fills it with blood
    /// (a growing red column). A full tube raises "tube_filled:&lt;tube name&gt;".
    /// </summary>
    public class TubeFilling : MonoBehaviour
    {
        [SerializeField] NeedleInsertion needle;
        [SerializeField] Transform holderPort;
        [SerializeField] List<XRGrabInteractable> tubes = new List<XRGrabInteractable>();
        [SerializeField] float portRadius = 0.06f;
        [SerializeField] float fillSeconds = 2.5f;

        readonly Dictionary<XRGrabInteractable, float> _fill = new Dictionary<XRGrabInteractable, float>();

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
            foreach (var t in tubes)
            {
                if (!t || !t.isSelected) continue;
                if ((t.transform.position - holderPort.position).sqrMagnitude > portRadius * portRadius) continue;
                _fill.TryGetValue(t, out var level);
                if (level >= 1f) continue;
                level = Mathf.Min(1f, level + Time.deltaTime / fillSeconds);
                _fill[t] = level;
                SetLevel(t, level);
                if (level >= 1f) ClinicalEvents.RaiseProcedure("tube_filled:" + t.name);
            }
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
