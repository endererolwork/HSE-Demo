using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// A socket that accepts one specific object (e.g. the tourniquet on the arm) and raises procedure events
    /// when it is placed and removed.
    /// </summary>
    public class ProcedureSocket : XRSocketInteractor
    {
        [SerializeField] GameObject accepted;
        [SerializeField] string placedEvent = "placed";
        [SerializeField] string removedEvent = "removed";

        public void Configure(GameObject item, string onPlaced, string onRemoved)
        {
            accepted = item;
            placedEvent = onPlaced;
            removedEvent = onRemoved;
        }

        public override bool CanHover(IXRHoverInteractable interactable) => base.CanHover(interactable) && Accepts(interactable.transform);

        public override bool CanSelect(IXRSelectInteractable interactable) => base.CanSelect(interactable) && Accepts(interactable.transform);

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            ClinicalEvents.RaiseProcedure(placedEvent);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            ClinicalEvents.RaiseProcedure(removedEvent);
        }

        bool Accepts(Transform t) => accepted && t && (t == accepted.transform || t.IsChildOf(accepted.transform));
    }
}
