using HSE.Scenario;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Training.Steps
{
    /// <summary>Completes when the trainee picks up an object (or is already holding it when the step opens).</summary>
    public class GrabStep : ScenarioStep
    {
        [SerializeField] XRGrabInteractable item;

        public void SetItem(XRGrabInteractable grab) => item = grab;

        protected override void OnBegin()
        {
            if (!item) { Debug.LogWarning($"{name}: no item assigned.", this); return; }
            if (item.isSelected) { Complete(); return; }
            item.selectEntered.AddListener(OnGrabbed);
        }

        protected override void OnEnd()
        {
            if (item) item.selectEntered.RemoveListener(OnGrabbed);
        }

        void OnGrabbed(SelectEnterEventArgs _) => Complete();
    }
}
