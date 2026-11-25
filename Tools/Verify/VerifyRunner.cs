using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch entry point: -executeMethod VerifyRunner.Run
public static class VerifyRunner
{
    static double _deadline;

    public static void Run()
    {
        VerifyDriver.OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../verify_out"));
        Directory.CreateDirectory(VerifyDriver.OutDir);
        HSE.EditorTools.PatientRoomBuilder.Build();
        EditorSceneManager.OpenScene("Assets/HSE/Scenes/PatientRoom.unity");
        SessionState.SetString("VerifyOut", VerifyDriver.OutDir);
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Poll;
        _deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Rehook()
    {
        // Domain reload on entering play mode drops static state; restore it.
        var dir = SessionState.GetString("VerifyOut", "");
        if (string.IsNullOrEmpty(dir)) return;
        VerifyDriver.OutDir = dir;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Poll;
        if (_deadline <= 0) _deadline = EditorApplication.timeSinceStartup + 240;
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode)
            new GameObject("VerifyDriver").AddComponent<VerifyDriver>();
    }

    static void Poll()
    {
        if (VerifyDriver.Done || EditorApplication.timeSinceStartup > _deadline)
        {
            if (!VerifyDriver.Done) File.WriteAllText(Path.Combine(VerifyDriver.OutDir, "report.txt"), "TIMEOUT");
            SessionState.EraseString("VerifyOut");
            EditorApplication.Exit(VerifyDriver.Done && VerifyDriver.Failures == 0 ? 0 : 1);
        }
    }
}
