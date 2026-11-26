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

        [MenuItem("HSE/Advanced/Setup Scenario")]
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

            // GDD 1: introduce yourself; the patient states name and date of birth.
            var dialogue = new GameObject("PatientDialogue").AddComponent<PatientDialogue>();
            dialogue.transform.SetParent(root.transform, false);
            dialogue.Bind(patientHead);
            // Breathing, blinking, looking at the nurse, reactions to the procedure.
            var behaviour = root.AddComponent<PatientBehaviour>();
            behaviour.Bind(patientHead, Find("Torso"), new[] { Find("Eye_L"), Find("Eye_R") }, dialogue);
            var greet = Step<GreetingStep>("greeting", "Introduce yourself",
                "Greet the patient, say who you are and ask for their full name and date of birth.", StepRequirement.Mandatory, 5, HighlightStyle.Hint,
                patientHead ? patientHead.gameObject : null);

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

            // ---------------- GDD step 2: blood pressure (cuff on the arm -> NIBP on the monitor -> record on the PDA)
            var cuff = Find("BP_Cuff");
            var nibpButton = SetupBloodPressure(cuff, Find("VitalsMonitor"), Find("UpperBody"));
            var bp = Step<CompositeStep>("blood_pressure", "Measure blood pressure",
                "Put the cuff on the patient's right upper arm, start NIBP on the monitor, then record the values on the PDA.", StepRequirement.Mandatory, 15, HighlightStyle.Objective);
            var applyCuff = ChildStep<ApplyCuffStep>(bp, "apply_cuff", "Apply the cuff", "Take the cuff from the wall and place it on the patient's right upper arm.", cuff ? cuff.gameObject : null);
            var measure = ChildStep<MeasureBloodPressureStep>(bp, "measure_bp", "Start NIBP", "Press NIBP START on the vital signs monitor.", nibpButton);
            var record = ChildStep<RecordVitalsStep>(bp, "record_vitals", "Record vitals", "On the PDA Vitals page, check the values and press Record.", pda ? pda.gameObject : null);
            bp.SetChildren(new ScenarioStep[] { applyCuff, measure, record }, CompositeStep.Order.InSequence);

            // ---------------- GDD steps 3-4: venous blood collection
            var draw = SetupBloodDraw(Find("UpperBody"), Find("RightArm"));
            root.AddComponent<BloodDrawSession>();
            var blood = Step<CompositeStep>("blood_draw", "Take the blood sample",
                "Tourniquet, clean the site, insert the needle, fill the tubes for the ordered tests, release the tourniquet, withdraw, dispose of the needle, cover the site.",
                StepRequirement.Mandatory, 25, HighlightStyle.Objective);
            var consent = ChildStep<ConsentStep>(blood, "consent", "Explain and get consent", "Explain the blood test to the patient and ask for verbal consent.",
                patientHead ? patientHead.gameObject : null);
            ProcedureStep P(string id, string title, string instr, string evt, Transform target)
            {
                var s = ChildStep<ProcedureStep>(blood, id, title, instr, target ? target.gameObject : null);
                s.SetEvent(evt);
                return s;
            }
            // Lights the needed tubes one by one in the order of draw.
            ProcedureStep FillTubes()
            {
                var s = ChildStep<FillTubesStep>(blood, "fill_tubes", "Fill the tubes",
                    "Touch each glowing tube to the back of the holder until it fills with blood; the next one then lights up.",
                    draw.Rack ? draw.Rack.gameObject : null);
                s.SetEvent("tubes_complete");
                return s;
            }
            var drawSteps = new ScenarioStep[]
            {
                consent,
                P("apply_tourniquet", "Apply the tourniquet", "Put the tourniquet on the upper arm, above the elbow.", "tourniquet_on", draw.Tourniquet),
                P("clean_site", "Clean the site", "Wipe the inside of the elbow with an alcohol swab and let it dry.", "site_cleaned", draw.Swab),
                P("insert_needle", "Insert the needle", "Pick up the holder and touch the needle tip to the blue hologram on the inside of the elbow. The needle goes in by itself and stays in the vein.", "needle_in", draw.Holder),
                FillTubes(),
                P("release_tourniquet", "Release the tourniquet", "Remove the tourniquet before taking the needle out.", "tourniquet_off", draw.Tourniquet),
                P("withdraw_needle", "Withdraw the needle", "Grab the holder and pull the needle out of the arm.", "needle_out", draw.Holder),
                P("dispose_needle", "Dispose of the needle", "Put the needle straight into the sharps container.", "needle_disposed", draw.Sharps),
                P("cover_site", "Cover the site", "Press gauze on the puncture site.", "site_covered", draw.Gauze),
            };
            blood.SetChildren(drawSteps, CompositeStep.Order.InSequence);
            if (draw.NeedleGhost) draw.NeedleGhost.AddComponent<ShowWhileStepActive>().Bind(drawSteps[3]);
            if (draw.TubeGhost) draw.TubeGhost.AddComponent<ShowWhileStepActive>().Bind(drawSteps[4]);

            // ---------------- GDD 5: verify identity again, print and apply labels
            var spec = SetupSpecimenHandling(root.transform);
            var label = Step<CompositeStep>("labelling", "Label the specimens",
                "Scan the wristband again, print the labels on the PDA and stick each one on its tube.", StepRequirement.Mandatory, 15, HighlightStyle.Objective);
            var rescan = ChildStep<ScanWristbandStep>(label, "rescan_wristband", "Verify identity again", "Scan the patient's wristband before labelling.",
                wristband ? wristband.gameObject : null);
            var print = ChildStep<ProcedureStep>(label, "print_labels", "Print labels", "On the PDA Orders tab press Print labels.", pda ? pda.gameObject : null);
            print.SetEvent("labels_printed");
            var stick = ChildStep<ProcedureStep>(label, "apply_labels", "Apply the labels", "Take each label from the printer and hold it against its tube.", spec.Printer);
            stick.SetEvent("labels_complete");
            label.SetChildren(new ScenarioStep[] { rescan, print, stick }, CompositeStep.Order.InSequence);

            // ---------------- GDD 6: document the collection
            var document = Step<ProcedureStep>("document_collection", "Document the collection",
                "On the PDA open Document collection, choose the site and the patient's tolerance, then save.", StepRequirement.Mandatory, 10, HighlightStyle.Objective,
                pda ? pda.gameObject : null);
            document.SetEvent("collection_documented");

            // ---------------- GDD 8: bag, seal, scan and send by pneumatic tube
            var dispatch = Step<CompositeStep>("dispatch", "Send the specimen",
                "Put the labelled tubes in the bag, the request slip in the outer pocket, seal it, scan it with the PDA and send it by pneumatic tube.",
                StepRequirement.Mandatory, 15, HighlightStyle.Objective);
            ProcedureStep D(string id, string title, string instr, string evt, GameObject target)
            {
                var st = ChildStep<ProcedureStep>(dispatch, id, title, instr, target);
                st.SetEvent(evt);
                return st;
            }
            dispatch.SetChildren(new ScenarioStep[]
            {
                D("bag_tubes", "Bag the tubes", "Put each labelled tube into the specimen bag.", "tubes_bagged", spec.Bag),
                D("pocket_slip", "Add the request slip", "Take the printed request slip and put it in the bag's outer pocket.", "slip_pocketed", spec.Printer),
                D("seal_bag", "Seal the bag", "Press SEAL on the bag.", "bag_sealed", spec.Bag),
                D("scan_bag", "Scan the bag", "Scan the bag barcode with the PDA and press Send to laboratory.", "dispatch_requested", spec.Bag),
                D("send_tube", "Send by pneumatic tube", "Put the sealed bag into the pneumatic tube station.", "specimen_dispatched", spec.StationOpening),
            }, CompositeStep.Order.InSequence);

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
                new ScenarioStep[] { hygiene, grab, approach, greet, scan, confirm, bp, blood, label, document, order, dispatch, readBoard, checkMonitor }, // hand hygiene first, before going to the patient
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
            root.AddComponent<ResultsLogger>().Bind(runner); // JSON + sessions.csv for the instructor

            // Simulated lab and its report panel (results and comments are stored on each patient).
            root.AddComponent<HSE.Clinical.Lab.LabService>();
            var report = new GameObject("BloodReport").AddComponent<BloodReportPanel>();
            report.transform.SetParent(root.transform, false);

            // Care board text and the live bedside monitor follow the patient in the bed.
            var board = Find("CareBoard");
            if (board)
            {
                var texts = board.GetComponentsInChildren<TMPro.TextMeshPro>(true);
                var display = board.GetComponent<CareBoardDisplay>();
                if (!display) display = board.gameObject.AddComponent<CareBoardDisplay>();
                display.Bind(texts.FirstOrDefault(t => t.name == "Header"), texts.FirstOrDefault(t => t.name == "Body"));
            }
            var monitorRoot = Find("VitalsMonitor");
            if (monitorRoot)
            {
                TMPro.TextMeshPro T(string n) => monitorRoot.GetComponentsInChildren<TMPro.TextMeshPro>(true).FirstOrDefault(t => t.name == n);
                var vitals = monitorRoot.GetComponent<HSE.Clinical.Vitals.VitalsMonitorDisplay>();
                if (!vitals) vitals = monitorRoot.gameObject.AddComponent<HSE.Clinical.Vitals.VitalsMonitorDisplay>();
                vitals.Bind(T("Monitor_HR"), T("Monitor_SpO2"), T("Monitor_RR"), T("Monitor_NIBP"), T("Monitor_NIBPTime"), Find("ECGTrace"), TraceMaterial());
            }

            EditorUtility.SetDirty(root);
            Debug.Log("[HSE] Scenario set up with " + runner.Steps.Count + " steps.");
        }

        struct SpecimenKit { public GameObject Printer, Bag, StationOpening; }

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/HSE/Materials/{name}.mat");

        /// <summary>Label printer, specimen bag (seal + barcode) and the pneumatic tube station by the door.</summary>
        static SpecimenKit SetupSpecimenHandling(Transform root)
        {
            var kit = new SpecimenKit();
            var printerT = Find("LabelPrinter");
            if (printerT)
            {
                var outlet = Child(printerT, "Outlet", new Vector3(0, 0.1f, 0.12f), Quaternion.identity);
                printerT.gameObject.AddComponent<LabelPrinterDevice>().Configure(Mat("Sheet"), outlet);
                kit.Printer = printerT.gameObject;
            }

            var bagT = Find("SpecimenBag");
            var fill = Object.FindFirstObjectByType<TubeFilling>();
            if (bagT)
            {
                var grab = MakeGrab(bagT);
                // Seal strip, shown once tubes and slip are in.
                var seal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seal.name = "SealButton";
                seal.transform.SetParent(bagT, false);
                seal.transform.localPosition = new Vector3(0, 0.5f, -0.45f);
                seal.transform.localScale = new Vector3(0.9f, 2f, 0.12f);
                seal.GetComponent<MeshRenderer>().sharedMaterial = Mat("Red");
                var sealInteract = seal.AddComponent<XRSimpleInteractable>();
                var poke = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Filtering.XRPokeFilter, Unity.XR.Interaction.Toolkit");
                if (poke != null) seal.AddComponent(poke);
                var code = bagT.gameObject.AddComponent<ScannableCode>();
                code.kind = CodeKind.SpecimenBag;
                code.code = "SPEC-BAG-01";
                bagT.gameObject.AddComponent<SpecimenBagDevice>().Configure(fill, sealInteract, code);
                kit.Bag = bagT.gameObject;

                // Pneumatic tube station on the west wall by the door; appears when the specimen is sent.
                var station = new GameObject("PneumaticTubeStation").transform;
                station.position = new Vector3(-2.93f, 0, -1.85f);
                station.rotation = Quaternion.Euler(0, 90, 0);
                Part(station, "Housing", PrimitiveType.Cube, new Vector3(0, 1.2f, 0.1f), new Vector3(0.36f, 0.6f, 0.2f), Mat("PlasticWhite"));
                Part(station, "Pipe", PrimitiveType.Cylinder, new Vector3(0, 2.2f, 0.1f), new Vector3(0.12f, 0.7f, 0.12f), Mat("Glass"));
                var cup = Part(station, "TubeOpening", PrimitiveType.Cylinder, new Vector3(0, 1.05f, 0.22f), new Vector3(0.16f, 0.04f, 0.16f), Mat("DarkMetal"));
                Part(station, "ReadyLight", PrimitiveType.Sphere, new Vector3(0.12f, 1.42f, 0.205f), Vector3.one * 0.03f, Mat("EmissiveGreen"));
                var text = new GameObject("Label").AddComponent<TMPro.TextMeshPro>();
                text.transform.SetParent(station, false);
                text.transform.localPosition = new Vector3(0, 1.38f, 0.203f);
                text.transform.localRotation = Quaternion.Euler(0, 180, 0);
                text.rectTransform.sizeDelta = new Vector2(0.3f, 0.08f);
                text.text = "<b>PNEUMATIC TUBE</b>\nto Central Laboratory";
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.enableAutoSizing = true; text.fontSizeMin = 0.01f; text.fontSizeMax = 1f;
                text.color = new Color(0.1f, 0.3f, 0.45f);
                cup.AddComponent<ProximityTrigger>().Configure(grab, null, cup.transform, 0.16f, 0.3f, "specimen_dispatched", consume: true);
                station.SetParent(root, true);
                root.gameObject.AddComponent<ShowOnProcedureEvent>().Configure(station.gameObject, "dispatch_requested");
                kit.StationOpening = cup;
            }
            return kit;
        }

        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (m) go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        struct DrawKit { public Transform Tourniquet, Swab, Holder, Rack, Sharps, Gauze; public GameObject NeedleGhost, TubeGhost; }

        static XRGrabInteractable MakeGrab(Transform t)
        {
            if (!t) return null;
            var rb = t.GetComponent<Rigidbody>();
            if (!rb) rb = t.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var grab = t.GetComponent<XRGrabInteractable>();
            if (!grab) grab = t.gameObject.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            if (!t.GetComponent<ReturnHome>()) t.gameObject.AddComponent<ReturnHome>();
            return grab;
        }

        static Transform Child(Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go.transform;
        }

        /// <summary>Makes the trolley supplies usable for venipuncture and wires the safety-checked interactions.</summary>
        static DrawKit SetupBloodDraw(Transform upperBody, Transform rightArm)
        {
            var kit = new DrawKit
            {
                Tourniquet = Find("Tourniquet"), Swab = Find("AlcoholSwab_0"), Holder = Find("VacutainerHolder"),
                Rack = Find("TubeRack"), Sharps = Find("SharpsContainer"), Gauze = Find("Gauze"),
            };
            var tourniquet = MakeGrab(kit.Tourniquet);
            var swab = MakeGrab(kit.Swab);
            var holder = MakeGrab(kit.Holder);
            var gauze = MakeGrab(kit.Gauze);

            // Inside of the right elbow; up = skin normal.
            var site = rightArm ? Child(rightArm, "PunctureSite", new Vector3(-0.255f, 0.7f, 0.27f), Quaternion.identity) : null;

            if (upperBody && tourniquet)
            {
                var sock = new GameObject("TourniquetSocket");
                sock.transform.SetParent(upperBody, false);
                sock.transform.localPosition = new Vector3(-0.25f, 0.15f, 0.13f);
                sock.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var c = sock.AddComponent<SphereCollider>();
                c.isTrigger = true;
                c.radius = 0.09f;
                sock.AddComponent<ProcedureSocket>().Configure(tourniquet.gameObject, "tourniquet_on", "tourniquet_off");
            }

            if (site && swab) site.gameObject.AddComponent<ProximityTrigger>().Configure(swab, null, site, 0.06f, 1.2f, "site_cleaned");
            if (site && gauze) site.gameObject.AddComponent<ProximityTrigger>().Configure(gauze, null, site, 0.06f, 1f, "site_covered");

            if (holder)
            {
                // The holder lies on its side on the trolley; its needle points along local -Y (tip at y = -0.095).
                var tipLocal = new Vector3(0, -0.095f, 0);
                var tip = Child(holder.transform, "NeedleTip", tipLocal, Quaternion.identity);
                var port = Child(holder.transform, "HolderPort", new Vector3(0, 0.05f, 0), Quaternion.identity);
                NeedleInsertion needle = null;
                if (site)
                {
                    // Correct pose: needle pointing up the arm towards the shoulder (+Z), 20 degrees into the skin,
                    // tip 1 cm in the vein. The pose root is where the holder rests once inserted.
                    var into = Quaternion.Euler(20, 0, 0) * Vector3.forward;
                    var rot = Quaternion.FromToRotation(Vector3.up, -into);
                    var pose = Child(site, "NeedleInsertedPose", into * 0.01f - rot * tipLocal, rot);
                    kit.NeedleGhost = HologramHolder(pose);

                    needle = site.gameObject.AddComponent<NeedleInsertion>();
                    needle.Configure(holder, tip, site, pose);
                }

                var bloodMat = BloodMaterial();
                var tubes = new System.Collections.Generic.List<XRGrabInteractable>();
                foreach (var n in new[] { "Tube_EDTA_Lavender", "Tube_SST_Gold", "Tube_Citrate_LightBlue", "Tube_Heparin_Green", "Tube_Fluoride_Gray" })
                {
                    var t = Find(n);
                    var g = MakeGrab(t);
                    if (!g) continue;
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    b.name = "Blood";
                    Object.DestroyImmediate(b.GetComponent<Collider>());
                    b.transform.SetParent(t, false);
                    b.transform.localScale = new Vector3(0.013f, 0.0001f, 0.013f);
                    b.transform.localPosition = new Vector3(0, -0.05f, 0);
                    b.GetComponent<MeshRenderer>().sharedMaterial = bloodMat;
                    b.SetActive(false);
                    tubes.Add(g);
                }
                if (needle) holder.gameObject.AddComponent<TubeFilling>().Configure(needle, port, tubes);

                // Hologram tube on the back of the holder: "push a tube on here".
                var tubeGhost = new GameObject("TubeGhost").transform;
                tubeGhost.SetParent(port, false);
                Part(tubeGhost, "Body", PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0), new Vector3(0.016f, 0.05f, 0.016f), HologramMaterial());
                Part(tubeGhost, "Cap", PrimitiveType.Cylinder, new Vector3(0, 0.0f, 0), new Vector3(0.02f, 0.011f, 0.02f), HologramMaterial());
                kit.TubeGhost = tubeGhost.gameObject;

                if (kit.Sharps)
                {
                    var mouth = Child(kit.Sharps, "Opening", new Vector3(0, 0.2f, 0), Quaternion.identity);
                    kit.Sharps.gameObject.AddComponent<ProximityTrigger>().Configure(holder, tip, mouth, 0.09f, 0.3f, "needle_disposed", consume: true);
                }
            }
            return kit;
        }

        static Material BloodMaterial()
        {
            const string path = "Assets/HSE/Materials/Guide/Blood.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                GuideMaterial("GuideArrow", new Color(1f, 0.78f, 0.1f, 1f), false);
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.62f, 0.03f, 0.05f));
            m.SetFloat("_Smoothness", 0.7f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material HologramMaterial() => GuideMaterial("Hologram", new Color(0.25f, 0.9f, 1f, 0.35f), true);

        /// <summary>A see-through copy of the vacutainer holder showing where and how the needle goes in.</summary>
        static GameObject HologramHolder(Transform pose)
        {
            var root = new GameObject("NeedleGhost").transform;
            root.SetParent(pose, false);
            var m = HologramMaterial();
            Part(root, "Barrel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.022f, 0.04f, 0.022f), m);
            Part(root, "Hub", PrimitiveType.Cylinder, new Vector3(0, -0.046f, 0), new Vector3(0.012f, 0.007f, 0.012f), m);
            Part(root, "Needle", PrimitiveType.Cylinder, new Vector3(0, -0.0725f, 0), new Vector3(0.0025f, 0.0225f, 0.0025f), m);
            // Target ring on the skin at the puncture point.
            var ring = Part(pose.parent, "PunctureMarker", PrimitiveType.Cylinder, new Vector3(0, 0.002f, 0), new Vector3(0.03f, 0.0008f, 0.03f), m);
            ring.transform.SetParent(root, true);
            return root.gameObject;
        }

        static T ChildStep<T>(ScenarioStep parent, string id, string title, string instruction, GameObject target) where T : ScenarioStep
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent.transform, false);
            var s = go.AddComponent<T>();
            s.Configure(id, title, instruction, StepRequirement.Mandatory, 0, target ? new[] { target } : null, HighlightStyle.Objective);
            return s;
        }

        /// <summary>Makes the cuff grabbable, adds the arm socket and the NIBP START button. Returns the button.</summary>
        static GameObject SetupBloodPressure(Transform cuff, Transform monitor, Transform upperBody)
        {
            if (cuff)
            {
                var rb = cuff.GetComponent<Rigidbody>();
                if (!rb) rb = cuff.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                var grab = cuff.GetComponent<XRGrabInteractable>();
                if (!grab) grab = cuff.gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false;
                grab.useDynamicAttach = true;
                if (!cuff.GetComponent<ReturnHome>()) cuff.gameObject.AddComponent<ReturnHome>();
            }

            if (upperBody && cuff)
            {
                // Mid upper arm on the patient's right; the cuff's sleeve axis lines up with the arm (head-section local Z).
                var socketGo = new GameObject("CuffSocket");
                socketGo.transform.SetParent(upperBody, false);
                socketGo.transform.localPosition = new Vector3(-0.25f, 0.15f, 0.27f);
                var trigger = socketGo.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.11f;
                socketGo.AddComponent<CuffSocket>().Bind(cuff.gameObject);
            }

            if (!monitor) return null;
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "NIBP_Start";
            button.transform.SetParent(monitor, false);
            button.transform.localPosition = new Vector3(0.13f, -0.135f, -0.042f);
            button.transform.localScale = new Vector3(0.1f, 0.03f, 0.014f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            const string matPath = "Assets/HSE/Materials/Guide/NibpButton.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing) mat = existing; else AssetDatabase.CreateAsset(mat, matPath);
            mat.SetColor("_BaseColor", new Color(0.15f, 0.55f, 0.95f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.05f, 0.2f, 0.4f));
            button.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(monitor, false);
            labelGo.transform.localPosition = new Vector3(0.13f, -0.135f, -0.05f);
            var label = labelGo.AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(0.09f, 0.022f);
            label.text = "<b>NIBP START</b>";
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.01f;
            label.fontSizeMax = 1f;
            label.color = Color.white;

            var interactable = button.AddComponent<XRSimpleInteractable>();
            // The monitor itself is an interactable ("check the monitor") and would otherwise also claim the
            // button's collider; give it an explicit list without the button.
            var monitorInteractable = monitor.GetComponent<XRBaseInteractable>();
            if (monitorInteractable)
            {
                monitorInteractable.colliders.Clear();
                foreach (var c in monitor.GetComponentsInChildren<Collider>(true))
                    if (c.gameObject != button) monitorInteractable.colliders.Add(c);
                EditorUtility.SetDirty(monitorInteractable);
            }
            var pokeFilter = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Filtering.XRPokeFilter, Unity.XR.Interaction.Toolkit");
            if (pokeFilter != null) button.AddComponent(pokeFilter);

            var display = monitor.GetComponent<HSE.Clinical.Vitals.VitalsMonitorDisplay>();
            if (!display) display = monitor.gameObject.AddComponent<HSE.Clinical.Vitals.VitalsMonitorDisplay>();
            var nibp = monitor.GetComponent<HSE.Clinical.Vitals.NibpController>();
            if (!nibp) nibp = monitor.gameObject.AddComponent<HSE.Clinical.Vitals.NibpController>();
            nibp.Bind(interactable, display);
            return button;
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
            "VitalsMonitor", "CommsTerminal", "CareBoard", "ClinicalWasteBin", "NIBP_Start", "Tube_Fluoride_Gray", "TubeRack", "TubeOpening"
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
            h.settings.glowPasses = Mathf.Min(h.settings.glowPasses, 2); // Quest: 2 glow passes is plenty
            h.highlighted = false;
            EditorUtility.SetDirty(h);
        }

        static Material TraceMaterial()
        {
            const string path = "Assets/HSE/Materials/Guide/MonitorTrace.mat";
            GuideMaterial("GuideArrow", new Color(1f, 0.78f, 0.1f, 1f), false); // folder
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
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
