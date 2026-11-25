using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// Barcode/QR scanner on the PDA's top edge. While the PDA is held and the trigger is pressed,
    /// a red aiming beam comes out of the scan window; when it lands on a ScannableCode the code is read
    /// (beep + haptic) and handed to the PDA app.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PdaScanner : MonoBehaviour
    {
        public Transform laserOrigin;
        public float maxDistance = 0.6f;
        public float aimRadius = 0.012f;
        public LayerMask scanMask = ~0;
        public PdaApp app;

        [Header("Feedback")]
        public Color beamColor = new Color(1f, 0.08f, 0.05f, 0.85f);
        public Color readColor = new Color(0.2f, 1f, 0.3f, 0.9f);
        public float rescanDelay = 1.0f;
        [Tooltip("Additive unlit URP material for the beam. Assigned by the PDA builder so the shader is included in builds.")]
        public Material beamMaterial;

        XRGrabInteractable _grab;
        LineRenderer _beam;
        Transform _dot;
        AudioSource _audio;
        AudioClip _beep;
        IXRInteractor _holder;
        bool _scanning;
        float _flashUntil;
        float _nextScanTime;
        ScannableCode _lastRead;

        public bool IsScanning => _scanning;

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.selectEntered.AddListener(a => _holder = a.interactorObject);
            _grab.selectExited.AddListener(_ => { _holder = null; SetScanning(false); });
            _grab.activated.AddListener(_ => SetScanning(true));
            _grab.deactivated.AddListener(_ => SetScanning(false));

            if (!laserOrigin) laserOrigin = transform;
            BuildBeam();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 0.3f;
            _beep = MakeBeep();
        }

        /// <summary>Lets desktop testing (or a UI button) drive the scanner without a controller trigger.</summary>
        public void SetScanning(bool on)
        {
            _scanning = on;
            if (!on) _lastRead = null;
            _beam.enabled = on;
            _dot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_scanning) return;

            Vector3 origin = laserOrigin.position;
            Vector3 dir = laserOrigin.forward;
            float length = maxDistance;
            bool hitSomething = Physics.SphereCast(origin, aimRadius, dir, out var hit, maxDistance, scanMask, QueryTriggerInteraction.Collide);
            if (hitSomething)
            {
                length = hit.distance;
                var code = hit.collider.GetComponentInParent<ScannableCode>();
                if (code && code != _lastRead && Time.time >= _nextScanTime) Read(code);
            }

            var c = Time.time < _flashUntil ? readColor : beamColor;
            _beam.startColor = c;
            _beam.endColor = new Color(c.r, c.g, c.b, 0.25f);
            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, origin + dir * length);
            _dot.gameObject.SetActive(hitSomething);
            if (hitSomething) _dot.position = origin + dir * length;
        }

        void Read(ScannableCode code)
        {
            _lastRead = code;
            _nextScanTime = Time.time + rescanDelay;
            _flashUntil = Time.time + 0.35f;
            _audio.PlayOneShot(_beep, 0.6f);
            if (_holder is XRBaseInputInteractor input) input.SendHapticImpulse(0.5f, 0.08f);
            ClinicalEvents.RaiseCodeScanned(code);
            if (app) app.OnCodeScanned(code);
        }

        void BuildBeam()
        {
            var go = new GameObject("ScanBeam");
            go.transform.SetParent(transform, false);
            _beam = go.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.startWidth = 0.0025f;
            _beam.endWidth = 0.0012f;
            _beam.numCapVertices = 2;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            var mat = beamMaterial ? new Material(beamMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 2); // additive
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3100;
            mat.SetColor("_BaseColor", Color.white);
            _beam.material = mat;
            _beam.enabled = false;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "ScanDot";
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * 0.008f;
            var dotMat = new Material(mat);
            dotMat.SetColor("_BaseColor", beamColor);
            dot.GetComponent<MeshRenderer>().sharedMaterial = dotMat;
            _dot = dot.transform;
            dot.SetActive(false);
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
