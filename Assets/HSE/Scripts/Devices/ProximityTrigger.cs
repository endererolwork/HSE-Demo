using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// Raises a procedure event when a held tool's point stays within <see cref="radius"/> of a target for
    /// <see cref="holdSeconds"/> (e.g. alcohol swab on the puncture site, needle into the sharps box).
    /// Distance-based, so it doesn't depend on colliders touching.
    /// </summary>
    public class ProximityTrigger : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable tool;
        [Tooltip("Point on the tool that has to reach the target. Defaults to the tool itself.")]
        [SerializeField] Transform toolPoint;
        [SerializeField] Transform target;
        [SerializeField] float radius = 0.05f;
        [SerializeField] float holdSeconds = 1f;
        [SerializeField] bool requireHeld = true;
        [SerializeField] string eventId;
        [Tooltip("Fire again after the tool leaves and comes back.")]
        [SerializeField] bool repeatable;
        [Tooltip("Hide the tool after the event (e.g. a needle dropped into the sharps box).")]
        [SerializeField] bool consumeTool;

        float _inside;
        bool _fired;

        public void Configure(XRGrabInteractable grab, Transform point, Transform where, float r, float seconds, string id, bool consume = false, bool canRepeat = false, bool mustBeHeld = true)
        {
            tool = grab; toolPoint = point; target = where; radius = r; holdSeconds = seconds; eventId = id; consumeTool = consume; repeatable = canRepeat;
            requireHeld = mustBeHeld;
        }

        public void ResetTrigger()
        {
            _fired = false;
            _inside = 0;
        }

        void Update()
        {
            if (!tool || !target || (_fired && !repeatable)) return;
            var p = toolPoint ? toolPoint : tool.transform;
            // The tool point (e.g. the needle tip) or the tool itself counts, so the angle it is held at doesn't matter.
            float r2 = radius * radius;
            bool close = (p.position - target.position).sqrMagnitude <= r2 || (tool.transform.position - target.position).sqrMagnitude <= r2;
            bool near = close && (!requireHeld || tool.isSelected);
            if (!near)
            {
                if (_inside > 0 && repeatable) _fired = false;
                _inside = 0;
                return;
            }
            if (_fired) return;
            _inside += Time.deltaTime;
            if (_inside < holdSeconds) return;
            _fired = true;
            ClinicalEvents.RaiseProcedure(eventId);
            if (consumeTool)
            {
                if (tool.isSelected && tool.interactionManager)
                    tool.interactionManager.CancelInteractableSelection((IXRSelectInteractable)tool);
                tool.gameObject.SetActive(false);
            }
        }
    }
}
