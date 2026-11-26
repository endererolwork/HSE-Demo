using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// Venipuncture with the vacutainer holder. A hologram of the holder shows the correct pose at the inside of
    /// the elbow; touching the needle tip to that spot lets the needle go in by itself: the holder leaves the hand
    /// and stays fixed in the vein at the hologram's pose, so both hands are free to push the tubes on.
    /// Grabbing the holder again and pulling it away withdraws the needle.
    /// </summary>
    public class NeedleInsertion : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable holder;
        [SerializeField] Transform needleTip;
        [SerializeField] Transform site;
        [Tooltip("Where the holder rests while the needle is in the vein (the hologram's root).")]
        [SerializeField] Transform insertedPose;
        [SerializeField] float insertRadius = 0.06f;
        [SerializeField] float withdrawRadius = 0.08f;

        public bool Inserted { get; private set; }
        public Transform Holder => holder ? holder.transform : null;

        ReturnHome _home;

        public void Configure(XRGrabInteractable grab, Transform tip, Transform puncture, Transform pose)
        {
            holder = grab; needleTip = tip; site = puncture; insertedPose = pose;
        }

        void Awake()
        {
            if (holder) _home = holder.GetComponent<ReturnHome>();
        }

        public void ResetNeedle()
        {
            Inserted = false;
            if (_home) _home.enabled = true;
        }

        void Update()
        {
            if (!holder || !needleTip || !site) return;

            if (Inserted)
            {
                if (!holder.isSelected)
                {
                    LockToPose(); // stays in the vein while nobody holds it
                    return;
                }
                if (Vector3.Distance(needleTip.position, site.position) > withdrawRadius)
                {
                    Inserted = false;
                    if (_home) _home.enabled = true;
                    ClinicalEvents.RaiseProcedure("needle_out");
                }
                return;
            }

            if (!holder.isSelected || Vector3.Distance(needleTip.position, site.position) > insertRadius) return;
            Insert();
        }

        void Insert()
        {
            Inserted = true;
            foreach (var i in holder.interactorsSelecting)
                if (i is XRBaseInputInteractor input) input.SendHapticImpulse(0.7f, 0.15f);

            // Let go of the holder so it can stay in the arm; ReturnHome must not pull it back to the trolley.
            if (_home) _home.enabled = false;
            if (holder.interactionManager) holder.interactionManager.CancelInteractableSelection((IXRSelectInteractable)holder);
            LockToPose();
            ClinicalEvents.RaiseProcedure("needle_in");
        }

        void LockToPose()
        {
            if (!insertedPose) return;
            var t = holder.transform;
            if ((t.position - insertedPose.position).sqrMagnitude > 1e-8f || t.rotation != insertedPose.rotation)
                t.SetPositionAndRotation(insertedPose.position, insertedPose.rotation);
        }
    }
}
