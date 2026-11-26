using HSE.Clinical;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// Venipuncture with the vacutainer holder: bringing the needle tip to the puncture site at a shallow angle
    /// (default 10-45 degrees to the skin) inserts it, with a haptic pulse. Moving the tip away withdraws it.
    /// A steep angle is reported as a mistake and the needle doesn't go in.
    /// </summary>
    public class NeedleInsertion : MonoBehaviour
    {
        [SerializeField] XRGrabInteractable holder;
        [SerializeField] Transform needleTip;
        [Tooltip("Points from the needle base to the tip.")]
        [SerializeField] Transform needleDirection;
        [SerializeField] Transform site;
        [SerializeField] float insertRadius = 0.03f;
        [SerializeField] float withdrawRadius = 0.07f;
        [SerializeField] Vector2 angleRange = new Vector2(10, 45);

        public bool Inserted { get; private set; }
        float _angleWarnCooldown;

        public void Configure(XRGrabInteractable grab, Transform tip, Transform direction, Transform puncture)
        {
            holder = grab; needleTip = tip; needleDirection = direction; site = puncture;
        }

        public void ResetNeedle() => Inserted = false;

        void Update()
        {
            if (!holder || !needleTip || !site) return;
            float d = Vector3.Distance(needleTip.position, site.position);

            if (Inserted)
            {
                if (d > withdrawRadius)
                {
                    Inserted = false;
                    ClinicalEvents.RaiseProcedure("needle_out");
                }
                return;
            }

            if (d > insertRadius || !holder.isSelected) return;

            // Angle between the needle and the skin surface (the site's up axis is the skin normal).
            Vector3 dir = needleDirection ? needleDirection.forward : needleTip.forward;
            float angle = 90f - Vector3.Angle(-dir, site.up);
            if (angle < angleRange.x || angle > angleRange.y)
            {
                if (Time.time >= _angleWarnCooldown)
                {
                    _angleWarnCooldown = Time.time + 3f;
                    ClinicalEvents.RaiseProcedureMistake("mistake.needleAngle");
                }
                return;
            }

            Inserted = true;
            foreach (var i in holder.interactorsSelecting)
                if (i is XRBaseInputInteractor input) input.SendHapticImpulse(0.7f, 0.15f);
            ClinicalEvents.RaiseProcedure("needle_in");
        }
    }
}
