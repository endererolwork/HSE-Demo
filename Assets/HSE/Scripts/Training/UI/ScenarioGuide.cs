using System.Linq;
using HSE.Localization;
using HSE.Scenario;
using TMPro;
using UnityEngine;

namespace HSE.Training.UI
{
    /// <summary>
    /// Points the trainee at the current task: a bobbing arrow with a short prompt ("Take the PDA") above the
    /// active step's target, and a direction arrow in front of the trainee while that target is out of view.
    /// Reads runner state only; never changes it.
    /// </summary>
    public class ScenarioGuide : MonoBehaviour
    {
        [SerializeField] ScenarioRunner runner;
        [Tooltip("Unlit material for the arrows (assigned by the scene builder so the shader ships in builds).")]
        [SerializeField] Material arrowMaterial;
        [SerializeField] Material labelBackground;
        [SerializeField] Color textColor = Color.white;

        [Header("Target arrow")]
        [SerializeField] float heightAboveTarget = 0.12f;
        [SerializeField] float bobAmplitude = 0.025f;
        [SerializeField] float bobSpeed = 3f;
        [SerializeField] float arrowScale = 1.3f;

        [Header("Direction arrow (target out of view)")]
        [SerializeField, Range(10, 90)] float inViewAngle = 35f;
        [SerializeField] float compassDistance = 0.75f;
        [SerializeField] float compassDrop = 0.28f;
        [SerializeField] float doneMessageSeconds = 5f;

        Transform _head;
        Transform _arrow, _compass;
        Transform _label, _compassLabel;
        TextMeshPro _labelText, _compassText;
        float _doneUntil;

        public void Bind(ScenarioRunner scenario, Material arrow, Material background)
        {
            runner = scenario;
            arrowMaterial = arrow;
            labelBackground = background;
        }

        void Awake()
        {
            var mesh = ArrowMesh.Create();
            _arrow = MakeArrow("TargetArrow", mesh);
            _compass = MakeArrow("DirectionArrow", mesh);
            (_label, _labelText) = MakeLabel("TargetLabel", new Vector2(0.42f, 0.07f));
            (_compassLabel, _compassText) = MakeLabel("DirectionLabel", new Vector2(0.36f, 0.06f));
            Show(false, false);
        }

        void OnEnable()
        {
            if (runner) runner.ScenarioCompleted += OnFinished;
        }

        void OnDisable()
        {
            if (runner) runner.ScenarioCompleted -= OnFinished;
        }

        void OnFinished(ScenarioReport _) => _doneUntil = Time.time + doneMessageSeconds;

        void LateUpdate()
        {
            if (!_head && Camera.main) _head = Camera.main.transform;
            if (!runner || !_head) { Show(false, false); return; }

            if (!runner.IsRunning)
            {
                bool showDone = Time.time < _doneUntil;
                Show(false, false);
                if (showDone) PlaceInFront(_compassLabel, _compassText, Loc.T("guide.done") + "\n<size=60%>" + Loc.T("guide.nextHint") + "</size>", 0f);
                return;
            }

            var step = GuidedStep(runner.CurrentStep);
            var target = step ? step.HighlightTargets.FirstOrDefault(t => t && t.activeInHierarchy) : null;
            if (!step || !target) { Show(false, false); return; }

            string cue = StepText.Cue(step);
            var b = WorldBounds(target);
            Vector3 tip = new Vector3(b.center.x, b.max.y + heightAboveTarget + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude, b.center.z);

            // Arrow over the target, pointing down, slowly spinning.
            _arrow.SetPositionAndRotation(tip, Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0, Time.time * 60f, 0) * Vector3.forward));
            _label.position = tip + Vector3.up * (0.12f * arrowScale + 0.06f);
            Billboard(_label);
            _labelText.text = cue;

            // Direction arrow when the target is outside the view cone.
            Vector3 toTarget = b.center - _head.position;
            bool inView = Vector3.Angle(_head.forward, toTarget) < inViewAngle;
            Show(true, !inView);
            if (!inView)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                Vector3 pos = _head.position + fwd * compassDistance + Vector3.down * compassDrop;
                Vector3 flat = Vector3.ProjectOnPlane(b.center - pos, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = fwd;
                _compass.SetPositionAndRotation(pos + flat.normalized * 0.06f, Quaternion.LookRotation(flat.normalized, Vector3.up));
                _compassLabel.position = pos + Vector3.down * 0.07f;
                Billboard(_compassLabel);
                _compassText.text = cue;
            }
        }

        /// <summary>For a composite step, guide to its first open sub-task.</summary>
        static ScenarioStep GuidedStep(ScenarioStep s)
        {
            while (s is CompositeStep c)
            {
                var child = c.Children.FirstOrDefault(x => x && x.State == StepState.Active);
                if (!child) break;
                s = child;
            }
            return s;
        }

        void Show(bool target, bool compass)
        {
            _arrow.gameObject.SetActive(target);
            _label.gameObject.SetActive(target);
            _compass.gameObject.SetActive(compass);
            _compassLabel.gameObject.SetActive(compass);
        }

        void PlaceInFront(Transform label, TextMeshPro text, string message, float drop)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
            label.gameObject.SetActive(true);
            label.position = _head.position + fwd * 1.0f + Vector3.down * drop;
            Billboard(label);
            text.text = message;
        }

        void Billboard(Transform t) => t.rotation = Quaternion.LookRotation(t.position - _head.position, Vector3.up);

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.05f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        Transform MakeArrow(string name, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * arrowScale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = arrowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        (Transform, TextMeshPro) MakeLabel(string name, Vector2 size)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.name = "Background";
            bg.transform.SetParent(root, false);
            bg.transform.localPosition = new Vector3(0, 0, 0.002f);
            bg.transform.localScale = new Vector3(size.x + 0.03f, size.y + 0.02f, 1);
            var bgr = bg.GetComponent<MeshRenderer>();
            bgr.sharedMaterial = labelBackground;
            bgr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root, false);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 0.6f;
            return (root, tmp);
        }
    }
}
