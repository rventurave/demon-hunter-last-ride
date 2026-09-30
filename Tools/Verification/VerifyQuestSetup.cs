using System.IO;
using JapaneseDemonHunter.GameplayEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyQuestSetup
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "ERROR: Stop Play Mode before checking saved configuration.";
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/JapanDemonHunter.unity")
        {
            if (scene.isDirty) return "ERROR: Unsaved scene; not replacing it.";
            EditorSceneManager.OpenScene("Assets/Scenes/JapanDemonHunter.unity");
        }
        string report = QuestDiagnostics.CreateReport();
        Directory.CreateDirectory("Docs/Verification");
        File.WriteAllText("Docs/Verification/QuestSetup-2026-09-30.txt", report);
        return report;
    }
}
