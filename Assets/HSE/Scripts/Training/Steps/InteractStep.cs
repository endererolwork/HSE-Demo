using HSE.Scenario;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Training.Steps
{
    /// <summary>Completes when the trainee selects (grab/poke) or activates (trigger) an interactable.</summary>
    public class InteractStep : ScenarioStep
    {
        [SerializeField] XRBaseInteractable target;
        [SerializeField] bool completeOnSelect = true;
        [SerializeField] bool completeOnActivate = true;
        [SerializeField] bool completeOnHover = false;

        public void SetTarget(XRBaseInteractable interactable, bool onSelect = true, bool onActivate = true, bool onHover = false)
        {
            target = interactable;
            completeOnSelect = onSelect;
            completeOnActivate = onActivate;
            completeOnHover = onHover;
        }

        protected override void OnBegin()
        {
            if (!target) { Debug.LogWarning($"{name}: no target assigned.", this); return; }
            if (completeOnSelect) target.selectEntered.AddListener(OnSelect);
            if (completeOnActivate) target.activated.AddListener(OnActivate);
            if (completeOnHover) target.hoverEntered.AddListener(OnHover);
        }

        protected override void OnEnd()
        {
            if (!target) return;
            target.selectEntered.RemoveListener(OnSelect);
            target.activated.RemoveListener(OnActivate);
            target.hoverEntered.RemoveListener(OnHover);
        }

        void OnSelect(SelectEnterEventArgs _) => Complete();
        void OnActivate(ActivateEventArgs _) => Complete();
        void OnHover(HoverEnterEventArgs _) => Complete();
    }
}
