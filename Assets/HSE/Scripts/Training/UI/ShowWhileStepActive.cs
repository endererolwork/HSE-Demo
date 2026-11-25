using HSE.Scenario;
using UnityEngine;

namespace HSE.Training.UI
{
    /// <summary>Shows this object's renderers only while a given step is active (e.g. a floor marker for "go here").</summary>
    public class ShowWhileStepActive : MonoBehaviour
    {
        [SerializeField] ScenarioStep step;
        [SerializeField] float pulseSpeed = 2.5f;
        [SerializeField] float pulseAmount = 0.08f;

        Renderer[] _renderers;
        Vector3 _baseScale;

        public void Bind(ScenarioStep target) => step = target;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseScale = transform.localScale;
        }

        void Update()
        {
            bool on = step && step.State == StepState.Active;
            foreach (var r in _renderers) r.enabled = on;
            if (on) transform.localScale = _baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
        }
    }
}
