using HSE.Clinical;
using UnityEngine;

namespace HSE.Devices
{
    /// <summary>Keeps an object hidden until a procedure event happens (e.g. the pneumatic tube station on "dispatch_requested").</summary>
    public class ShowOnProcedureEvent : MonoBehaviour
    {
        [SerializeField] GameObject target;
        [SerializeField] string showOn;

        public void Configure(GameObject what, string eventId)
        {
            target = what;
            showOn = eventId;
        }

        void Start() { if (target) target.SetActive(false); }

        void OnEnable()
        {
            ClinicalEvents.ProcedureEvent += OnEvent;
            ActivePatient.Changed += Hide;
        }

        void OnDisable()
        {
            ClinicalEvents.ProcedureEvent -= OnEvent;
            ActivePatient.Changed -= Hide;
        }

        void OnEvent(string id) { if (id == showOn && target) target.SetActive(true); }
        void Hide(PatientRecord _) { if (target) target.SetActive(false); }
    }
}