using System.Collections.Generic;
using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// Biohazard specimen bag. Labelled tubes brought to it go inside; the request slip goes in the outer pocket.
    /// When everything is in, a SEAL strip appears; once sealed, the bag's barcode can be scanned with the PDA.
    /// Unlabelled tubes are refused with a mistake.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SpecimenBagDevice : MonoBehaviour
    {
        [SerializeField] TubeFilling tubes;
        [SerializeField] XRBaseInteractable sealButton;
        [SerializeField] ScannableCode barcode;
        [SerializeField] float reach = 0.1f;

        XRGrabInteractable _grab;
        readonly List<Transform> _inside = new List<Transform>();
        bool _slipIn, _sealed;
        float _warnCooldown;

        public bool Sealed => _sealed;

        public void Configure(TubeFilling sampleTubes, XRBaseInteractable seal, ScannableCode code)
        {
            tubes = sampleTubes;
            sealButton = seal;
            barcode = code;
        }

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            if (sealButton) sealButton.selectEntered.AddListener(_ => Seal());
            ResetBag();
        }

        void OnEnable() => ActivePatient.Changed += OnPatient;
        void OnDisable() => ActivePatient.Changed -= OnPatient;
        void OnPatient(PatientRecord _) => ResetBag();

        public void ResetBag()
        {
            _inside.Clear();
            _slipIn = _sealed = false;
            if (sealButton) sealButton.gameObject.SetActive(false);
            if (barcode) barcode.enabled = false;
        }

        void Update()
        {
            if (_sealed) return;
            if (tubes)
                foreach (var t in tubes.Tubes)
                    if (t && !_inside.Contains(t.transform) && Near(t.transform) && (t.isSelected || _grab.isSelected))
                        TryInsertTube(t);

            if (!_slipIn)
                foreach (var slip in FindObjectsByType<RequestSlipMarker>(FindObjectsSortMode.None))
                {
                    var g = slip.GetComponent<XRGrabInteractable>();
                    if (g && (g.isSelected || _grab.isSelected) && Near(slip.transform)) InsertSlip(slip.transform, g);
                }

            if (sealButton && !sealButton.gameObject.activeSelf && _slipIn && _inside.Count > 0 &&
                FindFirstObjectByType<BloodDrawSession>() is BloodDrawSession s && s.IsDone("tubes_bagged"))
                sealButton.gameObject.SetActive(true);
        }

        bool Near(Transform t) => (t.position - transform.position).sqrMagnitude <= reach * reach;

        void TryInsertTube(XRGrabInteractable tube)
        {
            if (!tubes.IsFull(tube)) return;
            var label = tube.GetComponentInChildren<LabelSticker>();
            if (!label || !label.Applied)
            {
                if (Time.time >= _warnCooldown)
                {
                    _warnCooldown = Time.time + 2f;
                    ClinicalEvents.RaiseProcedureMistake("mistake.labelBeforeBagging");
                }
                return;
            }
            Stow(tube.transform, tube, new Vector3(-0.03f + _inside.Count * 0.02f, 0.012f, 0), Quaternion.Euler(0, 0, 90));
            _inside.Add(tube.transform);
            ClinicalEvents.RaiseProcedure("tube_bagged:" + tube.name);
        }

        void InsertSlip(Transform slip, XRGrabInteractable grab)
        {
            Stow(slip, grab, new Vector3(0, 0.0045f, 0.03f), Quaternion.identity);
            _slipIn = true;
            ClinicalEvents.RaiseProcedure("slip_pocketed");
        }

        Transform _contents;

        /// <summary>Unscaled child holding what goes into the bag; the bag mesh itself is a non-uniformly scaled box
        /// and rotated children under it would shear.</summary>
        Transform Contents
        {
            get
            {
                if (_contents) return _contents;
                _contents = new GameObject("Contents").transform;
                _contents.SetParent(transform, false);
                var s = transform.lossyScale;
                _contents.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
                return _contents;
            }
        }

        void Stow(Transform item, XRGrabInteractable grab, Vector3 localPos, Quaternion localRot)
        {
            if (grab.isSelected && grab.interactionManager)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = false;
            var home = item.GetComponent<ReturnHome>();
            if (home) home.enabled = false;
            foreach (var c in item.GetComponentsInChildren<Collider>()) c.enabled = false;
            item.SetParent(Contents, true);
            // Offsets are in metres in the bag's frame (the bag mesh is a scaled box, so not in its local units).
            item.SetPositionAndRotation(transform.position + transform.rotation * localPos, transform.rotation * localRot);
        }

        void Seal()
        {
            if (_sealed) return;
            _sealed = true;
            sealButton.gameObject.SetActive(false);
            if (barcode) barcode.enabled = true;
            ClinicalEvents.RaiseProcedure("bag_sealed");
        }
    }
}