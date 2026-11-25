using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HSE.EditorTools
{
    /// <summary>
    /// Places the Blender-made FBX models (Tools/Blender/*.py) in a scene.
    /// Each model carries Marker_Forward / Marker_Up empties, so it is turned to face +Z with +Y up regardless of
    /// how the FBX axes were converted, and every mesh named "Part__Key" gets the room's URP material "Key".
    /// </summary>
    public static class ModelLibrary
    {
        public const string Bed = "Assets/HSE/Models/Bed/HospitalBed.fbx";

        public static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

        public static GameObject Place(string path, Transform parent, Func<string, Material> materialFor, string name = null)
        {
            Configure(path);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inst.name = name ?? asset.name;
            inst.transform.SetParent(parent, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            var all = inst.GetComponentsInChildren<Transform>(true);
            var fwd = all.FirstOrDefault(t => t.name == "Marker_Forward");
            var up = all.FirstOrDefault(t => t.name == "Marker_Up");
            if (fwd && up)
            {
                Vector3 f = Dominant(inst.transform.InverseTransformPoint(fwd.position));
                Vector3 u = inst.transform.InverseTransformPoint(up.position);
                u = Dominant(u - Vector3.Project(u, f));
                inst.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(f, u));
            }

            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var key = MaterialKey(r.name);
                if (key == null) continue;
                var m = materialFor(key);
                r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            }
            return inst;
        }

        public static Transform FindPart(GameObject model, string prefix) =>
            model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(prefix + "__") || t.name == prefix);

        static string MaterialKey(string objectName)
        {
            int i = objectName.IndexOf("__", StringComparison.Ordinal);
            if (i < 0) return null;
            var key = objectName.Substring(i + 2);
            int dot = key.IndexOf('.');
            return dot >= 0 ? key.Substring(0, dot) : key;
        }

        static void Configure(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) return;
            if (imp.materialImportMode == ModelImporterMaterialImportMode.None && !imp.importAnimation) return;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.SaveAndReimport();
        }

        static Vector3 Dominant(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (a.y >= a.z) return new Vector3(0, Mathf.Sign(v.y), 0);
            return new Vector3(0, 0, Mathf.Sign(v.z));
        }
    }
}
