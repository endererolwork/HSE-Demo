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
            ResetProcedureTools();
            if (runner) runner.StartScenario();
        }

        /// <summary>Cuff, tourniquet, swab, needle, tubes and gauze back on the trolley, emptied and re-armed.</summary>
        static void ResetProcedureTools()
        {
            foreach (var item in FindObjectsByType<ReturnHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                item.gameObject.SetActive(true); // the needle is hidden after going into the sharps box
                item.SendHome();
            }
            foreach (var t in FindObjectsByType<ProximityTrigger>(FindObjectsSortMode.None)) t.ResetTrigger();
            foreach (var n in FindObjectsByType<NeedleInsertion>(FindObjectsSortMode.None)) n.ResetNeedle();
            foreach (var f in FindObjectsByType<TubeFilling>(FindObjectsInactive.Include, FindObjectsSortMode.None)) f.ResetTubes();
            foreach (var pr in FindObjectsByType<LabelPrinterDevice>(FindObjectsSortMode.None)) pr.ClearAll(); // labels and request slip
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
