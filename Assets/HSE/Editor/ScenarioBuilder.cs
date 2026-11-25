using System.Linq;
using HSE.Clinical;
using HSE.Devices;
using HSE.Scenario;
using HSE.Scenario.PrismFX;
using PrismFX;
using HSE.Training.Steps;
using HSE.Training;
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
            var patients = PatientLibrary.CreateOrUpdate();

            var pda = Object.FindFirstObjectByType<PdaApp>();
            var pdaGrab = pda ? pda.GetComponent<XRGrabInteractable>() : null;
            var bed = Find("HospitalBed");
            var wristband = Find("Wristband");
            var wristQr = Find("Wristband_QR");
            var patientHead = Find("Head"); // small highlight on the patient instead of the whole bed
            var handRub = Interactable(Find("HandRubDispenser"));
            var gloves = Interactable(Find("GloveBox_M"));
            var careBoard = Interactable(Find("CareBoard"));
            var monitor = Interactable(Find("VitalsMonitor"));

            var bedside = new GameObject("BedsidePoint").transform;
            bedside.SetParent(root.transform, false);
            bedside.position = bed ? bed.position + new Vector3(-0.95f, 0, -0.2f) : new Vector3(-0.65f, 0, 1.15f);

            // A small glowing ring on the floor is the target for "go to the patient" (instead of the whole bed).
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "BedsideMarker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(bedside, false);
            marker.transform.localPosition = new Vector3(0, 0.004f, 0);
            marker.transform.localScale = new Vector3(0.55f, 0.003f, 0.55f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = GuideMaterial("BedsideMarker", new Color(1f, 0.78f, 0.1f, 0.45f), true);
            marker.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            T Step<T>(string id, string title, string instruction, StepRequirement req, int points, HighlightStyle style, params GameObject[] targets) where T : ScenarioStep
            {
                var go = new GameObject($"{root.transform.childCount:00}_{id}");
                go.transform.SetParent(root.transform, false);
                var s = go.AddComponent<T>();
                s.Configure(id, title, instruction, req, points, targets.Where(t => t), style);
                return s;
            }

            // ---------------- mandatory flow (GDD step 1: enter, identify, prepare; step 7: lab request)
            var grab = Step<GrabStep>("take_pda", "Take the handheld",
                "Pick up the PDA from the procedure trolley.", StepRequirement.Mandatory, 5, HighlightStyle.Objective, pda ? pda.gameObject : null);
            grab.SetItem(pdaGrab);

            var approach = Step<ReachLocationStep>("approach", "Go to the patient",
                "Walk to the right side of the bed.", StepRequirement.Mandatory, 5, HighlightStyle.Hint, patientHead ? patientHead.gameObject : marker);
            approach.SetLocation(bedside, 0.9f);
            marker.AddComponent<ShowWhileStepActive>().Bind(approach);

            var scan = Step<ScanWristbandStep>("scan_wristband", "Scan the wristband",
                "Pull the trigger or press SCAN on the PDA and point the laser at the QR code on the patient's right wrist.", StepRequirement.Mandatory, 15, HighlightStyle.Objective,
                wristband ? wristband.gameObject : null, wristQr ? wristQr.gameObject : null);
            scan.SetExpectedPatient(null); // whichever patient the roster put in the bed

            var confirm = Step<ConfirmIdentityStep>("confirm_identity", "Confirm identity",
                "Compare the PDA details with the patient and press \"Confirm Identity\".", StepRequirement.Mandatory, 10, HighlightStyle.Objective,
                pda ? pda.gameObject : null);
            confirm.SetExpectedPatient(null);

            var hygiene = Step<CompositeStep>("hand_hygiene", "Hand hygiene and gloves",
                "Sanitise your hands before touching the patient, then put on gloves.", StepRequirement.Mandatory, 15, HighlightStyle.Objective);
            var rub = Child<InteractStep>(hygiene, "hand_rub", "Sanitise hands", "Use the hand rub dispenser.", handRub);
            rub.SetTarget(handRub);
            var glove = Child<InteractStep>(hygiene, "gloves", "Put on gloves (M)", "Take gloves from the size M box.", gloves);
            glove.SetTarget(gloves);
            hygiene.SetChildren(new ScenarioStep[] { rub, glove }, CompositeStep.Order.InSequence);

            var order = Step<SubmitLabOrderStep>("lab_order", "Send the lab request",
                "Open \"Lab Request\" on the PDA, select the tests the physician ordered and send.", StepRequirement.Mandatory, 20, HighlightStyle.Objective,
                pda ? pda.gameObject : null);
            order.SetExpectedPatient(null);

            // ---------------- optional extras: no highlight, bonus points for initiative
            var readBoard = Step<InteractStep>("read_care_board", "Check the care board",
                "Look at the care board in the room.", StepRequirement.Optional, 5, HighlightStyle.Hint);
            readBoard.SetTarget(careBoard, onSelect: true, onActivate: true, onHover: true);
            var checkMonitor = Step<InteractStep>("check_monitor", "Check the monitor",
                "Look at the vital signs monitor.", StepRequirement.Optional, 5, HighlightStyle.Hint);
            checkMonitor.SetTarget(monitor, onSelect: true, onActivate: true, onHover: true);

            runner.Configure("Blood Collection and Lab Request",
                new ScenarioStep[] { grab, approach, scan, confirm, hygiene, order, readBoard, checkMonitor },
                highlighter, autoPlay: false); // starts when a language is picked at the entrance

            var guide = new GameObject("Guide").AddComponent<ScenarioGuide>();
            guide.transform.SetParent(root.transform, false);
            guide.Bind(runner, GuideMaterial("GuideArrow", new Color(1f, 0.78f, 0.1f, 1f), false), GuideMaterial("GuideLabel", new Color(0.05f, 0.09f, 0.16f, 0.85f), true));

            var menu = new GameObject("LanguageMenu").AddComponent<LanguageMenu>();
            menu.transform.SetParent(root.transform, false);
            menu.transform.SetPositionAndRotation(new Vector3(-1.3f, 1.45f, -0.75f), Quaternion.Euler(0, 35, 0));
            menu.Bind(runner);

            var panel = new GameObject("WristChecklist").AddComponent<ScenarioChecklistPanel>();
            panel.transform.SetParent(root.transform, false);
            panel.transform.position = new Vector3(-1.6f, 1.45f, -1.2f);
            panel.transform.rotation = Quaternion.Euler(0, 35, 0);
            panel.Bind(runner);

            PrepareHighlights(pda ? pda.gameObject : null, patientHead ? patientHead.gameObject : null);

            // Patient for this session: random from the roster; drives the wristband, figure look and step checks.
            var roster = root.AddComponent<PatientRoster>();
            var figureRoots = new[] { Find("Patient"), Find("UpperBody") }.Where(t => t).ToArray();
            roster.Configure(patients, wristQr ? wristQr.GetComponent<ScannableCode>() : null, figureRoots);

            var gloveVisual = root.AddComponent<GloveVisual>();
            gloveVisual.Bind(runner, GloveMaterial());

            // "Next patient" on the results screen restarts with a different patient.
            root.AddComponent<ScenarioSession>().Bind(runner, roster, pda, panel);

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

        // Everything a task will point at gets a PrismHighlight up front, so the look can be tuned in the inspector.
        static readonly string[] TaskObjects =
        {
            "Wristband", "Wristband_QR", "HandRubDispenser", "GloveBox_S", "GloveBox_M", "GloveBox_L",
            "BP_Cuff", "Tourniquet", "AlcoholSwab_0", "AlcoholSwab_1", "AlcoholSwab_2", "Gauze", "Plasters",
            "Tube_EDTA_Lavender", "Tube_SST_Gold", "Tube_Citrate_LightBlue", "Tube_Heparin_Green",
            "VacutainerHolder", "SharpsContainer", "SpecimenBag", "LabelPrinter", "ProcedureTray",
            "VitalsMonitor", "CommsTerminal", "CareBoard", "ClinicalWasteBin"
        };

        static void PrepareHighlights(GameObject pda, GameObject bed)
        {
            foreach (var n in TaskObjects)
            {
                var t = Find(n);
                if (t) Configure(t.gameObject, PrismPreset.Objective, screen: false);
            }
            if (pda) Configure(pda, PrismPreset.Objective, screen: true);
            if (bed) Configure(bed, PrismPreset.SubtleHover, screen: false); // the patient's head for "go to the patient"
        }

        static void Configure(GameObject go, PrismPreset preset, bool screen)
        {
            var h = go.GetComponent<PrismHighlight>();
            if (!h) h = go.AddComponent<PrismHighlight>();
            h.trigger = PrismTrigger.Manual;
            h.selectOnClick = false;
            h.settings.ApplyPreset(preset);
            // See-through also lights an object's own hidden parts (e.g. a device body behind its glass).
            h.settings.seeThroughMode = PrismSeeThroughMode.Never;
            if (screen)
            {
                h.settings.overlay = 0f;
                h.settings.innerGlow = 0f;
                h.settings.target = false;
                h.settings.touchGlow = false;
                h.settings.hologram = false;
            }
            h.highlighted = false;
            EditorUtility.SetDirty(h);
        }

        static Material GloveMaterial()
        {
            const string path = "Assets/HSE/Materials/Guide/NitrileGlove.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                GuideMaterial("GuideArrow", new Color(1f, 0.78f, 0.1f, 1f), false); // makes sure the folder exists
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.32f, 0.5f, 0.92f));
            m.SetFloat("_Smoothness", 0.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GuideMaterial(string name, Color color, bool transparent)
        {
            const string dir = "Assets/HSE/Materials/Guide";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/HSE/Materials", "Guide");
            string path = dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Cull", 0f);
            if (transparent)
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            EditorUtility.SetDirty(m);
            return m;
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
