using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JapaneseDemonHunter.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public static class VerifyQuestDesktopTransition
{
    private const string Key = "JDH.QuestAudit.DesktopTest";
    public static string Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Requires stopped Play and a saved scene; preserve manual edits.");
        var mode = UnityEngine.Object.FindAnyObjectByType<DesktopDebugMode>();
        SessionState.SetBool(Key + ".OriginalMode", mode.EnabledForDesktop);
        SessionState.SetString(Key + ".Settings", File.ReadAllText("Assets/XR/XRGeneralSettingsPerBuildTarget.asset"));
        SessionState.SetString(Key + ".Features", File.ReadAllText("Assets/XR/Settings/OpenXR Package Settings.asset"));
        SessionState.SetBool(Key, true);
        var serialized = new SerializedObject(mode);
        serialized.FindProperty("desktopDebugMode").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return "Desktop test enabled in memory only; saved scene untouched.";
    }

    public static async Task<string> CheckRestoration()
    {
        var mode = UnityEngine.Object.FindAnyObjectByType<DesktopDebugMode>();
        if (!Application.isPlaying || !mode.IsActive || mode.DebugCamera == null)
            throw new InvalidOperationException("Desktop debug did not activate.");
        var rig = GameObject.Find("OVRCameraRig");
        Vector3 position = rig.transform.localPosition;
        Quaternion rotation = rig.transform.localRotation;
        mode.SetDesktopMode(false);
        await Task.Delay(150);
        var camera = rig.GetComponentsInChildren<Camera>(true).Single(item => item.transform.name == "CenterEyeAnchor");
        if (!camera.isActiveAndEnabled || mode.IsActive || GameObject.Find("DesktopDebug_Camera") != null)
            throw new InvalidOperationException("XR camera state not restored or debug camera leaked.");
        if (position != rig.transform.localPosition || rotation != rig.transform.localRotation)
            throw new InvalidOperationException("Debug changed XR rig transform.");
        string result = "PASS: Desktop activation, XR central camera restoration, temporary camera cleanup and unchanged rig transform. XR restart requires stopping and restarting Play.";
        File.WriteAllText("Docs/Verification/QuestDesktopRestoration-2026-09-30.txt", result);
        return result;
    }

    public static string Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key, false))
            throw new InvalidOperationException("Stop the prepared desktop test first.");
        var mode = UnityEngine.Object.FindAnyObjectByType<DesktopDebugMode>();
        var serialized = new SerializedObject(mode);
        serialized.FindProperty("desktopDebugMode").boolValue = SessionState.GetBool(Key + ".OriginalMode", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // Only the test checkbox was changed; reload the original saved scene instead of saving test state.
        EditorSceneManager.OpenScene(SceneManager.GetActiveScene().path);
        bool unchanged = File.ReadAllText("Assets/XR/XRGeneralSettingsPerBuildTarget.asset") == SessionState.GetString(Key + ".Settings", "") &&
            File.ReadAllText("Assets/XR/Settings/OpenXR Package Settings.asset") == SessionState.GetString(Key + ".Features", "");
        SessionState.SetBool(Key, false);
        if (!unchanged || XRGeneralSettings.Instance == null || !XRGeneralSettings.Instance.InitManagerOnStart)
            throw new InvalidOperationException("XR settings not restored.");
        const string result = "PASS: saved OpenXR settings unchanged and Initialize XR on Startup restored after Desktop Debug Play session.";
        File.AppendAllText("Docs/Verification/QuestDesktopRestoration-2026-09-30.txt", "\n" + result);
        return result;
    }
}
