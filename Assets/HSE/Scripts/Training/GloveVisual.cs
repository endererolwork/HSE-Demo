using System.Collections.Generic;
using System.Linq;
using HSE.Scenario;
using UnityEngine;

namespace HSE.Training
{
    /// <summary>
    /// When the "put on gloves" step completes, the tracked hand meshes switch to a nitrile glove material.
    /// Restored when the scenario restarts.
    /// </summary>
    public class GloveVisual : MonoBehaviour
    {
        [SerializeField] ScenarioRunner runner;
        [SerializeField] string gloveStepId = "gloves";
        [SerializeField] Material gloveMaterial;

        readonly Dictionary<Renderer, Material[]> _original = new Dictionary<Renderer, Material[]>();
        bool _gloved;

        public void Bind(ScenarioRunner scenario, Material glove)
        {
            runner = scenario;
            gloveMaterial = glove;
        }

        void OnEnable()
        {
            if (!runner) return;
            runner.ScenarioStarted += OnStarted;
            runner.StepCompleted += OnStep;
        }

        void OnDisable()
        {
            if (!runner) return;
            runner.ScenarioStarted -= OnStarted;
            runner.StepCompleted -= OnStep;
        }

        void OnStarted(ScenarioRunner _) => SetGloves(false);

        void OnStep(ScenarioStep step)
        {
            // Sub-steps don't raise runner events, so also accept the composite that contains the glove step.
            bool isGlove = step.Id == gloveStepId || (step is CompositeStep c && c.Children.Any(x => x && x.Id == gloveStepId));
            if (isGlove) SetGloves(true);
        }

        void LateUpdate()
        {
            // Hand visuals can be created/enabled after the gloves went on (switching from controllers to hands).
            if (_gloved && Time.frameCount % 30 == 0) Apply();
        }

        public void SetGloves(bool on)
        {
            _gloved = on;
            if (on) Apply();
            else
            {
                foreach (var kv in _original) if (kv.Key) kv.Key.sharedMaterials = kv.Value;
                _original.Clear();
            }
        }

        void Apply()
        {
            if (!gloveMaterial) return;
            foreach (var r in HandRenderers())
            {
                if (_original.ContainsKey(r)) continue;
                _original[r] = r.sharedMaterials;
                r.sharedMaterials = r.sharedMaterials.Length == 0
                    ? new[] { gloveMaterial }
                    : r.sharedMaterials.Select(GloveFor).ToArray();
            }
        }

        // The XRI hand shader has finger/edge colours that the hand's affordance system animates every frame, so a
        // plain Lit material makes it log errors. Keep that shader and just tint it the glove colour.
        readonly Dictionary<Material, Material> _tinted = new Dictionary<Material, Material>();

        Material GloveFor(Material source)
        {
            if (!source || !source.HasProperty("_MainColor")) return gloveMaterial;
            if (_tinted.TryGetValue(source, out var m) && m) return m;
            var c = gloveMaterial.HasProperty("_BaseColor") ? gloveMaterial.GetColor("_BaseColor") : gloveMaterial.color;
            c.a = 1f;
            m = new Material(source) { name = source.name + " (Glove)" };
            m.SetColor("_MainColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(c.r * 1.3f, c.g * 1.3f, c.b * 1.3f, 1f));
            if (m.HasProperty("_Color_1")) m.SetColor("_Color_1", new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 1f));
            _tinted[source] = m;
            return m;
        }

        static IEnumerable<Renderer> HandRenderers() =>
            FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => IsHandVisual(r.transform));

        static bool IsHandVisual(Transform t)
        {
            for (; t; t = t.parent)
                if (t.name.Contains("Hand") && t.name.Contains("Visual")) return true;
            return false;
        }
    }
}
