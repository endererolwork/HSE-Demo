using HSE.Clinical;
using HSE.Devices;
using HSE.Scenario;
using HSE.Training.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.Training
{
    /// <summary>
    /// Runs the scenario again and again: when the trainee asks for the next patient, a different patient is put
    /// in the bed, the PDA goes back to the trolley with a fresh app, gloves come off, and the steps start over.
    /// </summary>
    public class ScenarioSession : MonoBehaviour
    {
        [SerializeField] ScenarioRunner runner;
        [SerializeField] PatientRoster roster;
        [SerializeField] PdaApp pda;
        [SerializeField] ScenarioChecklistPanel checklist;

        Vector3 _pdaPosition;
        Quaternion _pdaRotation;

        public void Bind(ScenarioRunner scenario, PatientRoster patients, PdaApp device, ScenarioChecklistPanel panel)
        {
            runner = scenario;
            roster = patients;
            pda = device;
            checklist = panel;
        }

        void Awake()
        {
            if (pda) { _pdaPosition = pda.transform.position; _pdaRotation = pda.transform.rotation; }
        }

        void OnEnable()
        {
            if (checklist) checklist.NextRequested += NextPatient;
        }

        void OnDisable()
        {
            if (checklist) checklist.NextRequested -= NextPatient;
        }

        public void NextPatient()
        {
            if (roster) roster.ApplyNext();
            ResetPda();
            foreach (var item in FindObjectsByType<ReturnHome>(FindObjectsSortMode.None)) item.SendHome(); // e.g. the BP cuff
            if (runner) runner.StartScenario();
        }

        void ResetPda()
        {
            if (!pda) return;
            var grab = pda.GetComponent<XRGrabInteractable>();
            if (grab && grab.isSelected && grab.interactionManager)
                grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            var rb = pda.GetComponent<Rigidbody>();
            if (rb && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            pda.transform.SetPositionAndRotation(_pdaPosition, _pdaRotation);
            var scanner = pda.GetComponent<PdaScanner>();
            if (scanner) scanner.SetScanning(false);
            pda.ResetApp();
        }
    }
}
