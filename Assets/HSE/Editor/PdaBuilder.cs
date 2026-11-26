using System.Linq;
using HSE.Clinical;
using HSE.Devices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HSE.EditorTools
{
    /// <summary>
    /// Turns the Blender-exported PDA.fbx into a grabbable, scanning PDA prefab and places it in the patient room.
    /// Menu: HSE > Setup PDA In Scene (also run automatically by Build Patient Room Scene).
    /// </summary>
    public static class PdaBuilder
    {
        const string ModelPath = "Assets/HSE/Models/PDA/PDA.fbx";
        const string PrefabDir = "Assets/HSE/Prefabs";
        const string PrefabPath = PrefabDir + "/PDA.prefab";
        const string MatDir = "Assets/HSE/Materials/PDA";
        const string DataDir = "Assets/HSE/Data";
        const string PatientPath = DataDir + "/Patient_Room204_Bed1.asset";

        [MenuItem("HSE/Advanced/Setup PDA In Scene")]
        public static void SetupMenu()
        {
            SetupInScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void SetupInScene()
        {
            var prefab = BuildPrefab();
            if (prefab == null) return;

            // Replace the placeholder handheld on the trolley.
            var placeholder = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "HandheldDevice");
            Vector3 pos = new Vector3(-1.4f, 0.97f, 0.18f);
            Quaternion rot = Quaternion.Euler(0, 90, 0);
            if (placeholder)
            {
                pos = placeholder.position + Vector3.up * 0.005f;
                rot = Quaternion.Euler(0, placeholder.parent ? placeholder.parent.eulerAngles.y : 90, 0);
                Object.DestroyImmediate(placeholder.gameObject);
            }
            foreach (var old in Object.FindObjectsByType<PdaApp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                pos = old.transform.position; rot = old.transform.rotation;
                Object.DestroyImmediate(old.gameObject);
            }
            var pda = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            pda.transform.SetPositionAndRotation(pos, rot);
            var equipment = GameObject.Find("Equipment");
            if (equipment) pda.transform.SetParent(equipment.transform, true);

            SetupWristband();
            EnsureXrEventSystem();
            Debug.Log("[HSE] PDA placed in scene.");
        }

        // ------------------------------------------------------------ prefab

        static GameObject BuildPrefab()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[HSE] " + ModelPath + " is missing. Run Tools/Blender/make_pda.py first.");
                return null;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            EnsureFolder(PrefabDir); EnsureFolder("Assets/HSE/Materials"); EnsureFolder(MatDir);

            var root = new GameObject("PDA");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);

            // Work out the device's axes from the empties exported by Blender, then turn the model so that
            // the scanner points along +Z and the screen faces +Y in the PDA's own frame.
            var laser = Find(inst.transform, "LaserOrigin");
            var screenAnchor = Find(inst.transform, "ScreenAnchor");
            var bodyR = Find(inst.transform, "Body").GetComponent<Renderer>();
            Vector3 center = bodyR.bounds.center;
            Vector3 tip = Dominant(laser.position - center);
            Vector3 toScreen = screenAnchor.position - center;
            Vector3 normal = Dominant(toScreen - Vector3.Project(toScreen, tip));
            var toDevice = Quaternion.Inverse(Quaternion.LookRotation(tip, normal));
            inst.transform.localRotation = toDevice;
            inst.transform.localPosition = -(toDevice * center);

            AssignMaterials(inst);

            Vector3 laserPos = root.transform.InverseTransformPoint(laser.position);
            Vector3 screenPos = root.transform.InverseTransformPoint(screenAnchor.position);
            var screenR = Find(inst.transform, "Screen").GetComponent<Renderer>();
            var sb = screenR.bounds;

            var laserPoint = new GameObject("LaserPoint").transform;
            laserPoint.SetParent(root.transform, false);
            laserPoint.localPosition = laserPos;

            var canvasAnchor = new GameObject("ScreenCanvasAnchor").transform;
            canvasAnchor.SetParent(root.transform, false);
            canvasAnchor.localPosition = screenPos + Vector3.up * 0.0004f;
            canvasAnchor.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            var grip = new GameObject("GripAttach").transform;
            grip.SetParent(root.transform, false);
            grip.localPosition = new Vector3(0, -0.005f, -0.04f);

            // Physics + interaction.
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                foreach (var c in Corners(b)) bounds.Encapsulate(root.transform.InverseTransformPoint(c));
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = bounds.center;
            col.size = bounds.size;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = grip;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;

            var app = root.AddComponent<PdaApp>();
            app.screenAnchor = canvasAnchor;
            app.screenSizeMeters = new Vector2(sb.size.x * 0.98f, sb.size.z * 0.98f);

            var scanner = root.AddComponent<PdaScanner>();
            scanner.laserOrigin = laserPoint;
            scanner.app = app;
            scanner.beamMaterial = BeamMaterial();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void AssignMaterials(GameObject inst)
        {
            var white = Lit("PDA_White", new Color(0.93f, 0.94f, 0.95f), 0.6f);
            var blue = Lit("PDA_Blue", new Color(0.12f, 0.3f, 0.7f), 0.35f);
            var glass = Lit("PDA_Glass", new Color(0.02f, 0.025f, 0.03f), 0.95f);
            var red = Lit("PDA_ScanRed", new Color(0.45f, 0.03f, 0.04f), 0.95f, new Color(0.35f, 0.0f, 0.0f));
            var orange = Lit("PDA_Orange", new Color(0.95f, 0.5f, 0.1f), 0.45f);
            var dark = Lit("PDA_Dark", new Color(0.1f, 0.105f, 0.11f), 0.4f);
            var gold = Lit("PDA_Gold", new Color(0.85f, 0.66f, 0.3f), 0.75f, default, 1f);
            var gray = Lit("PDA_Gray", new Color(0.55f, 0.57f, 0.6f), 0.4f);

            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.name;
                Material m =
                    n.StartsWith("Bumper") || n.StartsWith("VolumeKey") ? blue :
                    n.StartsWith("Screen") || n.StartsWith("CameraLens") ? glass :
                    n.StartsWith("ScanWindow") || n.StartsWith("StatusLED") ? red :
                    n.StartsWith("ScanButton") ? orange :
                    n.StartsWith("ScannerHood") || n.StartsWith("Speaker") || n.StartsWith("FrontCamera") || n.StartsWith("CameraIsland") ? dark :
                    n.StartsWith("Contact") ? gold :
                    n.StartsWith("HomeKey") || n.StartsWith("BatteryCover") || n.StartsWith("PowerKey") ? gray :
                    white;
                r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.shadowCastingMode = n.StartsWith("Body") || n.StartsWith("Bumper") ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        static Material BeamMaterial()
        {
            // Particles/Unlit multiplies by vertex colour, so each LineRenderer can tint the laser red (or green on a read).
            string path = MatDir + "/PDA_ScanBeam.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 2); // additive
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3100;
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Lit(string name, Color c, float smooth, Color emission = default, float metal = 0)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------ scene wiring

        static void SetupWristband()
        {
            var qr = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Wristband_QR");
            if (!qr)
            {
                Debug.LogWarning("[HSE] Wristband_QR not found; patient cannot be scanned.");
                return;
            }
            var patient = PatientLibrary.CreateOrUpdate()[0]; // PatientRoster swaps it per session
            var code = qr.GetComponent<ScannableCode>();
            if (code == null) code = qr.gameObject.AddComponent<ScannableCode>();
            code.kind = CodeKind.PatientWristband;
            code.code = "HSE-PT-" + patient.medicalRecordNumber;
            code.patient = patient;
            // The QR quad is 2 mm thick; give the scanner a slightly deeper target.
            if (qr.GetComponent<BoxCollider>() is BoxCollider box) box.size = new Vector3(1.2f, 6f, 1.2f);
            EditorUtility.SetDirty(code);
        }

        static void EnsureXrEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (!es) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var moduleType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (moduleType == null) return;
            foreach (var other in es.GetComponents<BaseInputModule>())
                if (other.GetType() != moduleType) Object.DestroyImmediate(other);
            if (!es.GetComponent(moduleType)) es.gameObject.AddComponent(moduleType);
        }

        // ------------------------------------------------------------ utils

        static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) ??
            throw new System.Exception("[HSE] PDA model has no child named " + name);

        static Vector3 Dominant(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (a.y >= a.z) return new Vector3(0, Mathf.Sign(v.y), 0);
            return new Vector3(0, 0, Mathf.Sign(v.z));
        }

        static Vector3[] Corners(Bounds b)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            return c;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
