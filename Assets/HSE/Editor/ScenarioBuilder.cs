using System.Linq;
using HSE.Clinical;
using HSE.Devices;
using HSE.Scenario;
using HSE.Scenario.PrismFX;
using HSE.Training.Steps;
using HSE.Training.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.EditorTools
{
    /// <summary>
    /// Builds the "Blood collection and lab handoff" scenario in the open patient room scene:
    /// a ScenarioRunner with PrismFX highlights, its steps as child objects, and the wrist checklist.
    /// Menu: HSE > Setup Scenario (also run by Build Patient Room Scene). Safe to re-run.
    /// </summary>
    public static class ScenarioBuilder
    {
        const string PatientPath = "Assets/HSE/Data/Patient_Room204_Bed1.asset";

        [MenuItem("HSE/Setup Scenario")]
        public static void SetupMenu()
        {
            Setup();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void Setup()
        {
            RunPrismFxProjectSetup();

            var old = GameObject.Find("Scenario");
            if (old) Object.DestroyImmediate(old);

            var root = new GameObject("Scenario");
            var runner = root.AddComponent<ScenarioRunner>();
            var highlighter = root.AddComponent<PrismHighlightService>();
            var patient = AssetDatabase.LoadAssetAtPath<PatientRecord>(PatientPath);

            var pda = Object.FindFirstObjectByType<PdaApp>();
            var pdaGrab = pda ? pda.GetComponent<XRGrabInteractable>() : null;
            var bed = Find("HospitalBed");
            var wristband = Find("Wristband");
            var wristQr = Find("Wristband_QR");
            var handRub = Interactable(Find("HandRubDispenser"));
            var gloves = Interactable(Find("GloveBox_M"));
            var careBoard = Interactable(Find("CareBoard"));
            var monitor = Interactable(Find("VitalsMonitor"));

            var bedside = new GameObject("BedsidePoint").transform;
            bedside.SetParent(root.transform, false);
            bedside.position = bed ? bed.position + new Vector3(-0.95f, 0, -0.2f) : new Vector3(-0.65f, 0, 1.15f);

            T Step<T>(string id, string title, string instruction, StepRequirement req, int points, HighlightStyle style, params GameObject[] targets) where T : ScenarioStep
            {
                var go = new GameObject($"{root.transform.childCount:00}_{id}");
                go.transform.SetParent(root.transform, false);
                var s = go.AddComponent<T>();
                s.Configure(id, title, instruction, req, points, targets.Where(t => t), style);
                return s;
            }

            // ---------------- mandatory flow (GDD step 1: enter, identify, prepare; step 7: lab request)
            var grab = Step<GrabStep>("take_pda", "El terminalini al",
                "Tedavi arabasındaki PDA'yı al.", StepRequirement.Mandatory, 5, HighlightStyle.Objective, pda ? pda.gameObject : null);
            grab.SetItem(pdaGrab);

            var approach = Step<ReachLocationStep>("approach", "Hastanın yanına git",
                "Hasta yatağının sağ tarafına geç.", StepRequirement.Mandatory, 5, HighlightStyle.Hint, bed ? bed.gameObject : null);
            approach.SetLocation(bedside, 0.9f);

            var scan = Step<ScanWristbandStep>("scan_wristband", "Hasta bilekliğini okut",
                "PDA'nın tetiğine basılı tut, kırmızı ışını hastanın sağ bileğindeki QR koda tut.", StepRequirement.Mandatory, 15, HighlightStyle.Objective,
                wristband ? wristband.gameObject : null, wristQr ? wristQr.gameObject : null);
            scan.SetExpectedPatient(patient);

            var confirm = Step<ConfirmIdentityStep>("confirm_identity", "Kimliği doğrula",
                "PDA'daki bilgileri hastayla karşılaştır ve \"Kimliği Doğrula\"ya bas.", StepRequirement.Mandatory, 10, HighlightStyle.Objective,
                pda ? pda.gameObject : null);
            confirm.SetExpectedPatient(patient);

            var hygiene = Step<CompositeStep>("hand_hygiene", "El hijyeni ve eldiven",
                "Hastaya dokunmadan önce ellerini dezenfekte et, sonra eldiven giy.", StepRequirement.Mandatory, 15, HighlightStyle.Objective);
            var rub = Child<InteractStep>(hygiene, "hand_rub", "Ellerini dezenfekte et", "Antiseptik dispenserine dokun.", handRub);
            rub.SetTarget(handRub);
            var glove = Child<InteractStep>(hygiene, "gloves", "Eldiven giy (M)", "M beden eldiven kutusundan eldiven al.", gloves);
            glove.SetTarget(gloves);
            hygiene.SetChildren(new ScenarioStep[] { rub, glove }, CompositeStep.Order.InSequence);

            var order = Step<SubmitLabOrderStep>("lab_order", "Tahlil istemi yap",
                "PDA'da \"Tahlil İstemi\"ni aç, hekim istemindeki testleri seç ve gönder.", StepRequirement.Mandatory, 20, HighlightStyle.Objective,
                pda ? pda.gameObject : null);
            order.SetExpectedPatient(patient);

            // ---------------- optional extras: no highlight, bonus points for initiative
            var readBoard = Step<InteractStep>("read_care_board", "Bakım panosunu kontrol et",
                "Odadaki bilgi panosuna bak.", StepRequirement.Optional, 5, HighlightStyle.Hint);
            readBoard.SetTarget(careBoard, onSelect: true, onActivate: true, onHover: true);
            var checkMonitor = Step<InteractStep>("check_monitor", "Monitörü kontrol et",
                "Vital bulgu monitörüne bak.", StepRequirement.Optional, 5, HighlightStyle.Hint);
            checkMonitor.SetTarget(monitor, onSelect: true, onActivate: true, onHover: true);

            runner.Configure("Kan Alma ve Laboratuvar İstemi",
                new ScenarioStep[] { grab, approach, scan, confirm, hygiene, order, readBoard, checkMonitor },
                highlighter);

            var panel = new GameObject("WristChecklist").AddComponent<ScenarioChecklistPanel>();
            panel.transform.SetParent(root.transform, false);
            panel.transform.position = new Vector3(-1.6f, 1.45f, -1.2f);
            panel.transform.rotation = Quaternion.Euler(0, 35, 0);
            panel.Bind(runner);

            EditorUtility.SetDirty(root);
            Debug.Log("[HSE] Scenario set up with " + runner.Steps.Count + " steps.");
        }

        static T Child<T>(ScenarioStep parent, string id, string title, string instruction, XRBaseInteractable target) where T : ScenarioStep
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent.transform, false);
            var s = go.AddComponent<T>();
            s.Configure(id, title, instruction, StepRequirement.Mandatory, 0, target ? new[] { target.gameObject } : null, HighlightStyle.Objective);
            return s;
        }

        /// <summary>Makes a scene prop selectable/hoverable by the XR interactors.</summary>
        static XRBaseInteractable Interactable(Transform t)
        {
            if (!t) return null;
            var existing = t.GetComponent<XRBaseInteractable>();
            if (existing) return existing;
            if (!t.GetComponentInChildren<Collider>())
            {
                var r = t.GetComponentsInChildren<Renderer>();
                if (r.Length > 0)
                {
                    var b = r[0].bounds;
                    foreach (var x in r) b.Encapsulate(x.bounds);
                    var box = t.gameObject.AddComponent<BoxCollider>();
                    box.center = t.InverseTransformPoint(b.center);
                    var size = t.InverseTransformVector(b.size);
                    box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                }
            }
            return t.gameObject.AddComponent<XRSimpleInteractable>();
        }

        static Transform Find(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);

        /// <summary>Adds the PrismFX renderer feature to the URP renderers (Tools > PrismFX > Setup Project).</summary>
        static void RunPrismFxProjectSetup()
        {
            var setup = System.Type.GetType("PrismFX.EditorTools.PrismFXSetup, PrismFX.Editor");
            var method = setup?.GetMethod("SetupProject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Debug.LogWarning("[HSE] PrismFX package not found; highlights will not render until it is installed.");
                return;
            }
            method.Invoke(null, null);
        }
    }
}
