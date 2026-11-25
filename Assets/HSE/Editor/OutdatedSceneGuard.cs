using HSE.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HSE.EditorTools
{
    /// <summary>
    /// Pressing Play on a patient room built by an older version of the builders rebuilds it first,
    /// so new features (language menu, patients, guidance) are never silently missing.
    /// </summary>
    [InitializeOnLoad]
    static class OutdatedSceneGuard
    {
        const string ScenePath = "Assets/HSE/Scenes/PatientRoom.unity";

        static OutdatedSceneGuard() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;

            var stamp = Object.FindFirstObjectByType<SceneBuildStamp>();
            if (stamp && stamp.version >= SceneBuildStamp.CurrentVersion) return;

            Debug.Log($"[HSE] Patient room is from builder v{(stamp ? stamp.version : 0)}, current is v{SceneBuildStamp.CurrentVersion}. Rebuilding before Play.");
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () =>
            {
                PatientRoomBuilder.Build();
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            };
        }
    }
}
