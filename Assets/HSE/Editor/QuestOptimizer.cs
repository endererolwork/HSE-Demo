using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HSE.EditorTools
{
    /// <summary>
    /// Cheap wins for Meta Quest run on every room build: GPU instancing on the room materials, no shadows from small
    /// props, hard sun shadows. "HSE > Bake Lighting (Quest)" additionally bakes the room lights into lightmaps.
    /// </summary>
    public static class QuestOptimizer
    {
        const float SmallPropSize = 0.25f;

        public static void Apply()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/HSE/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
            }

            int shadowsOff = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                if (r.bounds.size.magnitude < SmallPropSize) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shadowsOff++; }
            }

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.shadows == LightShadows.Soft) l.shadows = LightShadows.Hard;

            Debug.Log($"[HSE] Quest optimisation: instancing on, shadows off for {shadowsOff} small props, hard sun shadows.");
        }

        [MenuItem("HSE/Bake Lighting (Quest)")]
        public static void Bake()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                l.lightmapBakeType = l.type == LightType.Directional ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
            foreach (var imp in AssetDatabase.FindAssets("t:Model", new[] { "Assets/HSE/Models" })
                         .Select(g => AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as ModelImporter)
                         .Where(i => i && !i.generateSecondaryUV))
            {
                imp.generateSecondaryUV = true;
                imp.SaveAndReimport();
            }
            LightingSettings settings;
            try { settings = Lightmapping.lightingSettings; } catch { settings = null; } // throws when the scene has none
            if (settings == null)
            {
                settings = new LightingSettings { name = "HSE Lighting" };
                AssetDatabase.CreateAsset(settings, "Assets/HSE/Scenes/HSE Lighting.lighting");
                Lightmapping.lightingSettings = settings;
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapResolution = 12;
            Lightmapping.BakeAsync();
            Debug.Log("[HSE] Lightmap bake started (see the progress bar at the bottom right). Save the scene when it finishes.");
        }
    }
}