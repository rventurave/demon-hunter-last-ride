using System;
using System.Linq;
using System.Reflection;
using JapaneseDemonHunter.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JapaneseDemonHunter.GameplayEditor
{
    [InitializeOnLoad]
    public static class DesktopDebugSetup
    {
        private const string SessionKey = "JDH.DesktopDebug.PlaySession";
        private static Object originalSettings;
        private static Object sessionSettings;
        private static PropertyInfo instanceProperty;

        static DesktopDebugSetup() { EditorApplication.playModeStateChanged += OnPlayState; }

        [MenuItem("Tools/Game/Setup Desktop Debug (in place)")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/JapanDemonHunter.unity")
                throw new InvalidOperationException("Open JapanDemonHunter and stop Play Mode first.");
            var root = GameObject.Find("DesktopDebug");
            if (root == null) { root = new GameObject("DesktopDebug"); Undo.RegisterCreatedObjectUndo(root, "Add desktop debug"); }
            if (root.GetComponent<DesktopDebugMode>() == null) Undo.AddComponent<DesktopDebugMode>(root);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Desktop debug installed. Disable Desktop Debug Mode before playing with XR; no XR settings were saved.");
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                SessionState.SetBool(SessionKey, Object.FindObjectsByType<DesktopDebugMode>(FindObjectsSortMode.None)
                    .Any(item => item.isActiveAndEnabled && item.EnabledForDesktop));
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (instanceProperty != null && originalSettings != null) instanceProperty.SetValue(null, originalSettings);
                if (sessionSettings != null) Object.DestroyImmediate(sessionSettings);
                originalSettings = sessionSettings = null;
                SessionState.SetBool(SessionKey, false);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void UseSessionOnlyXrSettings()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            // Clone the settings in memory, never edit the saved asset or any loader/feature configuration.
            Type settingsType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly =>
                assembly.GetType("UnityEngine.XR.Management.XRGeneralSettings")).FirstOrDefault(type => type != null);
            if (settingsType == null) return;
            instanceProperty = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            originalSettings = instanceProperty?.GetValue(null) as Object;
            if (originalSettings == null) return;
            sessionSettings = Object.Instantiate(originalSettings);
            sessionSettings.hideFlags = HideFlags.HideAndDontSave;
            settingsType.GetProperty("InitManagerOnStart").SetValue(sessionSettings, false);
            instanceProperty.SetValue(null, sessionSettings);
        }
    }
}
