using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HSE.Devices
{
    /// <summary>
    /// A kinematic grabbable that goes back to where it started when let go anywhere except a socket
    /// (e.g. the blood pressure cuff returns to its wall basket).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ReturnHome : MonoBehaviour
    {
        [SerializeField] float delay = 0.6f;

        XRGrabInteractable _grab;
        Vector3 _homePos;
        Quaternion _homeRot;
        float _returnAt = -1;

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _grab.selectExited.AddListener(_ => _returnAt = Time.time + delay);
            _grab.selectEntered.AddListener(_ => _returnAt = -1);
        }

        void Update()
        {
            if (_returnAt < 0 || Time.time < _returnAt) return;
            _returnAt = -1;
            if (_grab.isSelected) return;
            foreach (var i in _grab.interactorsSelecting)
                if (i is XRSocketInteractor) return;
            transform.SetPositionAndRotation(_homePos, _homeRot);
        }

        /// <summary>Back to the start immediately (used when the next patient starts).</summary>
        public void SendHome()
        {
            if (_grab.isSelected && _grab.interactionManager)
                _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
            transform.SetPositionAndRotation(_homePos, _homeRot);
            _returnAt = -1;
        }
    }
}
