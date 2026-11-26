using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// A printed specimen label. Held against the tube it was printed for, it sticks around the tube and raises
    /// "label_applied:&lt;tube&gt;". Held against a different tube it raises a label-mismatch mistake.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class LabelSticker : MonoBehaviour
    {
        [SerializeField] Transform tube;
        [SerializeField] float reach = 0.05f;

        XRGrabInteractable _grab;
        float _mismatchCooldown;

        public PatientRecord Patient { get; private set; }
        public bool Applied { get; private set; }

        public void Configure(Transform targetTube, PatientRecord patient)
        {
            tube = targetTube;
            Patient = patient;
        }

        void Awake() => _grab = GetComponent<XRGrabInteractable>();

        void Update()
        {
            if (Applied || !_grab.isSelected || !tube) return;
            if ((transform.position - tube.position).sqrMagnitude <= reach * reach)
            {
                Stick();
                return;
            }
            // Another filled tube nearby: wrong label.
            foreach (var other in FindObjectsByType<TubeFilling>(FindObjectsSortMode.None))
            foreach (var t in other.Tubes)
            {
                if (!t || t.transform == tube) continue;
                if ((transform.position - t.transform.position).sqrMagnitude <= reach * reach && Time.time >= _mismatchCooldown)
                {
                    _mismatchCooldown = Time.time + 2f;
                    ClinicalEvents.RaiseProcedureMistake("mistake.labelMismatch");
                }
            }
        }

        void Stick()
        {
            Applied = true;
            if (_grab.interactionManager)
                _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
            _grab.enabled = false;
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
            transform.SetParent(tube, false);
            // Wrapped along the tube body, facing out.
            transform.localPosition = new Vector3(0, 0.005f, -0.0095f);
            transform.localRotation = Quaternion.Euler(90, 0, 90);
            transform.localScale = new Vector3(0.5f, 1f, 0.8f);
            ClinicalEvents.RaiseProcedure("label_applied:" + tube.name);
        }
    }
}