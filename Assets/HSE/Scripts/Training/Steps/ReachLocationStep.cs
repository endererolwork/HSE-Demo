using HSE.Scenario;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Training.Steps
{
    /// <summary>Completes when the trainee's head comes within <see cref="radius"/> of a point (e.g. "go to the bedside").</summary>
    public class ReachLocationStep : ScenarioStep
    {
        [SerializeField] Transform location;
        [SerializeField, Min(0.1f)] float radius = 0.8f;
        [Tooltip("Head transform. Defaults to the main camera.")]
        [SerializeField] Transform head;

        public void SetLocation(Transform where, float within)
        {
            location = where;
            radius = within;
        }

        protected override void OnBegin()
        {
            if (!head && Camera.main) head = Camera.main.transform;
        }

        void Update()
        {
            if (State != StepState.Active || !location || !head) return;
            var d = head.position - location.position;
            d.y = 0;
            if (d.sqrMagnitude <= radius * radius) Complete();
        }
    }
}
