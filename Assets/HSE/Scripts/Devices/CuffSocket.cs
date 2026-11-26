using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// Socket on the patient's upper arm that accepts only the blood pressure cuff (so the PDA can't snap onto it)
    /// and reports when the cuff goes on or comes off.
    /// </summary>
    public class CuffSocket : XRSocketInteractor
    {
        [SerializeField] GameObject cuff;

        public void Bind(GameObject bpCuff) => cuff = bpCuff;

        public override bool CanHover(IXRHoverInteractable interactable) => base.CanHover(interactable) && IsCuff(interactable.transform);

        public override bool CanSelect(IXRSelectInteractable interactable) => base.CanSelect(interactable) && IsCuff(interactable.transform);

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            ClinicalEvents.RaiseCuffApplied();
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            ClinicalEvents.RaiseCuffRemoved();
        }

        bool IsCuff(Transform t) => cuff && t && (t == cuff.transform || t.IsChildOf(cuff.transform));
    }
}
