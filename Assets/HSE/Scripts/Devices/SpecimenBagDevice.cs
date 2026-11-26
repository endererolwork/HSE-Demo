using System.Collections.Generic;
using HSE.Clinical;
using HSE.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Devices
{
    /// <summary>
    /// Biohazard specimen bag. Tubes brought to it go inside; the request slip goes in the outer pocket.
    /// When every ordered tube and the slip are in, the bag seals itself, shows "SEALED" and a barcode sticker,
    /// and the barcode can then be scanned with the PDA. A tube without its label (or not filled) still goes in,
    /// but is booked as a mistake.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SpecimenBagDevice : MonoBehaviour
    {
        [SerializeField] TubeFilling tubes;
        [Tooltip("Optional manual seal strip (kept for older scenes; the bag also seals itself).")]
        [SerializeField] XRBaseInteractable sealButton;
        [SerializeField] ScannableCode barcode;
        [SerializeField] float reach = 0.12f;

        XRGrabInteractable _grab;
        readonly List<Transform> _inside = new List<Transform>();
        readonly HashSet<string> _warned = new HashSet<string>();
        bool _slipIn, _sealed;
        GameObject _sealedTag;
        TextMeshPro _status;
        Transform _head;

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

        void OnEnable()
        {
            ActivePatient.Changed += OnPatient;
            ClinicalEvents.ProcedureEvent += OnProcedure;
        }

        void OnDisable()
        {
            ActivePatient.Changed -= OnPatient;
            ClinicalEvents.ProcedureEvent -= OnProcedure;
        }

        void OnPatient(PatientRecord _) => ResetBag();

        public void ResetBag()
        {
            _inside.Clear();
            _warned.Clear();
            _slipIn = _sealed = false;
            if (sealButton) sealButton.gameObject.SetActive(false);
            if (barcode) barcode.enabled = false;
            if (_sealedTag) _sealedTag.SetActive(false);
            if (_status) _status.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_status && _status.gameObject.activeSelf) PlaceStatus();
            if (_sealed) return;
            if (tubes)
                foreach (var t in tubes.Tubes)
                    if (t && !_inside.Contains(t.transform) && Near(t.transform) && (t.isSelected || _grab.isSelected))
                        InsertTube(t);

            if (!_slipIn)
                foreach (var slip in FindObjectsByType<RequestSlipMarker>(FindObjectsSortMode.None))
                {
                    var g = slip.GetComponent<XRGrabInteractable>();
                    if (g && g.enabled && (g.isSelected || _grab.isSelected) && Near(slip.transform)) InsertSlip(slip.transform, g);
                }

            // Everything in: the bag seals itself.
            if (_slipIn && FindFirstObjectByType<BloodDrawSession>() is BloodDrawSession s && s.IsDone("tubes_bagged"))
                Seal();
        }

        bool Near(Transform t) => (t.position - transform.position).sqrMagnitude <= reach * reach;

        void InsertTube(XRGrabInteractable tube)
        {
            var label = tube.GetComponentInChildren<LabelSticker>();
            bool ok = tubes.IsFull(tube) && label && label.Applied;
            if (!ok && _warned.Add(tube.name))
                ClinicalEvents.RaiseProcedureMistake(tubes.IsFull(tube) ? "mistake.labelBeforeBagging" : "mistake.emptyTubeBagged");
            Stow(tube.transform, tube, new Vector3(-0.04f + _inside.Count * 0.02f, 0.013f, 0.01f), Quaternion.Euler(90, 0, 0));
            _inside.Add(tube.transform);
            ClinicalEvents.RaiseProcedure("tube_bagged:" + tube.name);
        }

        void InsertSlip(Transform slip, XRGrabInteractable grab)
        {
            Stow(slip, grab, new Vector3(0, 0.0045f, 0.035f), Quaternion.identity);
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
            if (sealButton) sealButton.gameObject.SetActive(false);
            if (barcode) barcode.enabled = true;
            ShowSealedTag();
            ShowStatus("<color=#2E9E5B><b>" + Loc.T("bag.sealed") + "</b></color>\n<size=60%>" + Loc.T("bag.scanHint") + "</size>");
            ClinicalEvents.RaiseProcedure("bag_sealed");
        }

        void OnProcedure(string id)
        {
            if (!_sealed) return;
            if (id == "dispatch_requested")
                ShowStatus("<color=#2E9E5B><b>" + Loc.T("bag.ready") + "</b></color>\n<size=60%>" + Loc.T("bag.tubeHint") + "</size>");
            else if (id == "specimen_dispatched" && _status)
                _status.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ visuals

        /// <summary>A white barcode sticker across the sealed top of the bag: what the PDA scanner reads.</summary>
        void ShowSealedTag()
        {
            if (_sealedTag) { _sealedTag.SetActive(true); return; }
            var printer = FindFirstObjectByType<LabelPrinterDevice>();
            _sealedTag = new GameObject("SealedBarcode");
            _sealedTag.transform.SetParent(Contents, false);
            _sealedTag.transform.SetPositionAndRotation(transform.position + transform.rotation * new Vector3(0, 0.0045f, -0.075f), transform.rotation);

            Quad(_sealedTag.transform, Vector3.zero, new Vector3(0.12f, 0.0006f, 0.04f), printer ? printer.PaperMaterial : null);
            var red = Quad(_sealedTag.transform, new Vector3(0, 0.0002f, 0.017f), new Vector3(0.12f, 0.0006f, 0.006f), null);
            red.GetComponent<MeshRenderer>().material.color = new Color(0.8f, 0.1f, 0.1f);
            float x = -0.045f;
            uint h = 2166136261;
            foreach (char c in barcode ? barcode.code : "SPEC") { h = (h ^ c) * 16777619; }
            for (int i = 0; x < 0.045f; i++)
            {
                h = h * 1664525 + 1013904223;
                float w = 0.0008f * (1 + (h >> 28) % 3);
                if (i % 2 == 0) Quad(_sealedTag.transform, new Vector3(x + w * 0.5f, 0.0003f, -0.003f), new Vector3(w, 0.0006f, 0.02f), printer ? printer.InkMaterial : null);
                x += w;
            }
            var text = new GameObject("Code").AddComponent<TextMeshPro>();
            text.transform.SetParent(_sealedTag.transform, false);
            text.transform.localPosition = new Vector3(0, 0.0007f, -0.0165f);
            text.transform.localRotation = Quaternion.Euler(90, 0, 0);
            text.rectTransform.sizeDelta = new Vector2(0.1f, 0.006f);
            text.enableAutoSizing = true; text.fontSizeMin = 0.002f; text.fontSizeMax = 0.05f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black;
            text.text = (barcode ? barcode.code : "") + "  SEALED";
        }

        static GameObject Quad(Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            var r = q.GetComponent<MeshRenderer>();
            if (m) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q;
        }

        /// <summary>Floating status above the bag ("SEALED - scan the barcode with the PDA").</summary>
        void ShowStatus(string text)
        {
            if (!_status)
            {
                _status = new GameObject("BagStatus").AddComponent<TextMeshPro>();
                _status.rectTransform.sizeDelta = new Vector2(0.4f, 0.12f);
                _status.enableAutoSizing = true; _status.fontSizeMin = 0.05f; _status.fontSizeMax = 0.4f;
                _status.alignment = TextAlignmentOptions.Center;
                _status.color = Color.white;
                _status.outlineWidth = 0.2f;
                _status.outlineColor = new Color32(0, 0, 0, 220);
            }
            _status.text = text;
            _status.gameObject.SetActive(true);
            PlaceStatus();
        }

        void PlaceStatus()
        {
            if (!_head && Camera.main) _head = Camera.main.transform;
            var t = _status.transform;
            t.position = transform.position + Vector3.up * 0.2f;
            if (_head) t.rotation = Quaternion.LookRotation(t.position - _head.position, Vector3.up);
        }
    }
}
