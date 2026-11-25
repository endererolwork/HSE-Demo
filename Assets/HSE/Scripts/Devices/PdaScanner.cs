using System.Collections.Generic;
using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// Barcode/QR scanner on the PDA's top edge. Scanning starts with the controller trigger while the PDA is held,
    /// or with the SCAN button on the PDA screen (for hand tracking). While scanning, a fan of red laser lines
    /// sweeps a cone in front of the scan window; any ScannableCode inside the cone with a clear line of sight is read.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PdaScanner : MonoBehaviour
    {
        public Transform laserOrigin;
        public PdaApp app;
        [Tooltip("Additive unlit URP material for the laser lines. Assigned by the PDA builder so the shader is included in builds.")]
        public Material beamMaterial;

        [Header("Scan area")]
        public float maxDistance = 0.6f;
        [Tooltip("Half-angle of the scan cone, degrees.")]
        public float coneAngle = 14f;
        public LayerMask occluderMask = ~0;
        [Tooltip("How long one press of the on-screen SCAN button keeps the scanner on.")]
        public float buttonScanSeconds = 3f;

        [Header("Look")]
        public int laserLines = 7;
        public Color beamColor = new Color(1f, 0.05f, 0.03f, 1f);
        public Color readColor = new Color(0.2f, 1f, 0.3f, 1f);
        public float rescanDelay = 1.0f;

        XRGrabInteractable _grab;
        readonly List<LineRenderer> _lines = new List<LineRenderer>();
        AudioSource _audio;
        AudioClip _beep;
        IXRInteractor _holder;
        bool _trigger;
        float _scanUntil;
        float _flashUntil;
        float _nextScanTime;
        ScannableCode _lastRead;

        public bool IsScanning => _trigger || Time.time < _scanUntil;

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.selectEntered.AddListener(a => _holder = a.interactorObject);
            _grab.selectExited.AddListener(_ => { _holder = null; _trigger = false; });
            _grab.activated.AddListener(_ => _trigger = true);
            _grab.deactivated.AddListener(_ => _trigger = false);

            if (!laserOrigin) laserOrigin = transform;
            if (!app) app = GetComponent<PdaApp>();
            for (int i = 0; i < Mathf.Max(1, laserLines); i++) _lines.Add(MakeLine(i));
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 0.3f;
            _beep = MakeBeep();
            SetLinesVisible(false);
        }

        /// <summary>Scan for a few seconds (the on-screen SCAN button; also handy for desktop testing).</summary>
        public void ScanBurst() => _scanUntil = Time.time + buttonScanSeconds;

        /// <summary>Kept for tests and tools: true scans until set false.</summary>
        public void SetScanning(bool on)
        {
            _trigger = on;
            if (!on) _scanUntil = 0;
        }

        void Update()
        {
            bool on = IsScanning;
            SetLinesVisible(on);
            if (!on) { _lastRead = null; return; }

            var code = FindInCone();
            if (code && code != _lastRead && Time.time >= _nextScanTime) Read(code);
            DrawFan();
        }

        ScannableCode FindInCone()
        {
            Vector3 origin = laserOrigin.position, fwd = laserOrigin.forward;
            ScannableCode best = null;
            float bestAngle = float.MaxValue;
            foreach (var c in FindObjectsByType<ScannableCode>(FindObjectsSortMode.None))
            {
                if (!c.isActiveAndEnabled) continue;
                Vector3 to = c.transform.position - origin;
                float dist = to.magnitude;
                if (dist > maxDistance || dist < 0.005f) continue;
                float angle = Vector3.Angle(fwd, to);
                if (angle > coneAngle || angle >= bestAngle) continue;
                // Line of sight: the first thing hit must be the code itself (or part of the PDA).
                if (Physics.Raycast(origin, to / dist, out var hit, dist + 0.02f, occluderMask, QueryTriggerInteraction.Collide) &&
                    hit.collider.GetComponentInParent<ScannableCode>() != c && !hit.collider.transform.IsChildOf(transform))
                    continue;
                best = c;
                bestAngle = angle;
            }
            return best;
        }

        void Read(ScannableCode code)
        {
            _lastRead = code;
            _nextScanTime = Time.time + rescanDelay;
            _flashUntil = Time.time + 0.4f;
            _scanUntil = Mathf.Min(_scanUntil, Time.time + 0.4f); // a read ends a button burst shortly after the flash
            _audio.PlayOneShot(_beep, 0.6f);
            if (_holder is XRBaseInputInteractor input) input.SendHapticImpulse(0.5f, 0.08f);
            ClinicalEvents.RaiseCodeScanned(code);
            if (app) app.OnCodeScanned(code);
        }

        // ------------------------------------------------------------ visuals

        void DrawFan()
        {
            Vector3 origin = laserOrigin.position;
            var c = Time.time < _flashUntil ? readColor : beamColor;
            float spread = coneAngle * 0.8f;
            for (int i = 0; i < _lines.Count; i++)
            {
                // Lines spread across the cone and jitter a little, like a sweeping laser scanner.
                float t = _lines.Count == 1 ? 0.5f : i / (float)(_lines.Count - 1);
                float yaw = Mathf.Lerp(-spread, spread, t) + Random.Range(-1.5f, 1.5f);
                float pitch = Mathf.Sin(Time.time * 9f + i * 1.7f) * spread * 0.35f;
                Vector3 dir = laserOrigin.rotation * Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
                float len = maxDistance;
                if (Physics.Raycast(origin, dir, out var hit, maxDistance, occluderMask, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
                    len = hit.distance;
                var line = _lines[i];
                line.SetPosition(0, origin);
                line.SetPosition(1, origin + dir * len);
                line.startColor = new Color(c.r, c.g, c.b, 0.9f);
                line.endColor = new Color(c.r, c.g, c.b, 0.15f);
            }
        }

        void SetLinesVisible(bool on)
        {
            foreach (var l in _lines) if (l.enabled != on) l.enabled = on;
        }

        LineRenderer MakeLine(int i)
        {
            var go = new GameObject("ScanLaser_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.0025f;
            lr.endWidth = 0.0012f;
            lr.numCapVertices = 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = beamMaterial ? beamMaterial : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            return lr;
        }

        static AudioClip MakeBeep()
        {
            const int rate = 44100;
            const float seconds = 0.11f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (n - i) / 400f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 2700f * t) * 0.5f * env;
            }
            var clip = AudioClip.Create("ScanBeep", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
