using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JapaneseDemonHunter.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>Read-only diagnostics for the loaded scenes. Does not start XR or modify rigs/settings.</summary>
    public sealed class QuestDiagnostics : EditorWindow
    {
        private Vector2 scroll;
        private string report = "";

        [MenuItem("Tools/VR/Quest Diagnostics")]
        public static void Open()
        {
            var window = GetWindow<QuestDiagnostics>("Quest Diagnostics");
            window.minSize = new Vector2(650, 400);
            window.report = CreateReport();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Diagnóstico de escenas cargadas. El tracking real requiere visor/runtime. No modifica el proyecto ni diagnostica USB.", MessageType.Info);
            if (GUILayout.Button("Actualizar diagnóstico")) report = CreateReport();
            if (GUILayout.Button("Copiar informe")) EditorGUIUtility.systemCopyBuffer = report;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel,
                GUILayout.Height(Mathf.Max(600, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(report), position.width - 35))));
            EditorGUILayout.EndScrollView();
        }

        public static string CreateReport()
        {
            var output = new StringBuilder();
            output.AppendLine($"Quest Diagnostics — Unity {Application.unityVersion} — {DateTime.UtcNow:O}");
            output.AppendLine($"Escena activa: {SceneManager.GetActiveScene().path}; Play: {Application.isPlaying}; plataforma: {EditorUserBuildSettings.activeBuildTarget}");
            int errors = 0, warnings = 0;
            Action<string, string> row = (level, message) =>
            {
                if (level == "ERROR") errors++;
                if (level == "AVISO") warnings++;
                output.AppendLine($"[{level}] {message}");
            };
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            Func<string, bool> installed = name => packages.Any(package => package.name == name);
            foreach (string name in new[] { "com.unity.xr.openxr", "com.unity.xr.management", "com.unity.inputsystem" })
                row(installed(name) ? "OK" : "ERROR", $"Paquete {name}: {packages.FirstOrDefault(package => package.name == name)?.version ?? "ausente"}");
            bool xri = installed("com.unity.xr.interaction.toolkit");
            row(xri ? "OK" : "AVISO", "XR Interaction Toolkit: " + (xri ? "instalado" : "no instalado; el proyecto utiliza Meta Interaction SDK"));
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                bool loader = settings != null && settings.Manager != null && settings.Manager.activeLoaders.Any(item => item != null && item.GetType().Name == "OpenXRLoader");
                row(loader ? "OK" : "ERROR", $"OpenXR activo en {group}: {loader}");
                row(settings != null && settings.InitManagerOnStart ? "OK" : "ERROR", $"Initialize XR on Startup {group}: {settings != null && settings.InitManagerOnStart}");
                var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (openxr == null) { row("ERROR", $"Sin OpenXR Settings para {group}"); continue; }
                var features = openxr.GetFeatures<OpenXRFeature>();
                foreach (string feature in new[] { "MetaXRFeature", "OculusTouchControllerProfile" })
                {
                    var found = features.FirstOrDefault(item => item != null && item.GetType().Name == feature);
                    row(found != null && found.enabled ? "OK" : "ERROR", $"{group}/{feature}: {found != null && found.enabled}");
                }
                if (group == BuildTargetGroup.Android)
                {
                    var quest = features.FirstOrDefault(item => item != null && item.GetType().Name == "MetaQuestFeature");
                    row(quest != null && quest.enabled ? "OK" : "AVISO", $"Meta Quest Support (Unity): {quest != null && quest.enabled}; distinto de Meta XR Feature");
                }
                output.AppendLine($"[INFO] {group}: renderMode={openxr.renderMode}; features activas: " + string.Join(", ", features.Where(item => item != null && item.enabled).Select(item => item.GetType().Name)));
                var issues = new List<OpenXRFeature.ValidationRule>();
                OpenXRProjectValidation.GetCurrentValidationIssues(issues, group);
                foreach (var issue in issues) row(issue.error ? "ERROR" : "AVISO", $"OpenXR Validation {group}: {issue.message}");
            }

            var objects = Resources.FindObjectsOfTypeAll<GameObject>().Where(item => item.scene.IsValid() && item.scene.isLoaded && !EditorUtility.IsPersistent(item)).ToArray();
            var components = objects.SelectMany(item => item.GetComponents<Component>()).Where(item => item != null).ToArray();
            Func<string, Component[]> find = type => components.Where(item => item.GetType().Name == type).ToArray();
            var origins = find("XROrigin");
            var rigs = find("OVRCameraRig");
            row(origins.Length > 0 ? "OK" : "AVISO", $"XR Origin (XRI/Core Utils): {origins.Length}; OVRCameraRig: {rigs.Length}");
            var roots = origins.Concat(rigs).ToArray();
            row(roots.Length == 1 ? "OK" : "ERROR", $"Rigs XR encontrados: {roots.Length} (se espera uno)");
            foreach (var root in roots)
            {
                row(root.gameObject.activeInHierarchy && (!(root is Behaviour behaviour) || behaviour.enabled) ? "OK" : "ERROR", $"Rig {Path(root.transform)} activo: {root.gameObject.activeInHierarchy}");
                var cameras = root.GetComponentsInChildren<Camera>(true);
                row(cameras.Length > 0 ? "OK" : "ERROR", $"Cámaras XR encontradas: {cameras.Length}");
                foreach (var camera in cameras)
                {
                    bool optionalEye = rigs.Contains(root) && camera.transform.name != "CenterEyeAnchor";
                    row(camera.isActiveAndEnabled || optionalEye ? "OK" : "AVISO", $"Cámara {Path(camera.transform)} activa={camera.isActiveAndEnabled}, tag={camera.tag}, stereo={camera.stereoTargetEye}, near={camera.nearClipPlane}" + (optionalEye ? " (ojo auxiliar; normal desactivado con cámara central)" : ""));
                    bool tracking = rigs.Contains(root) || camera.GetComponents<Component>().Any(item => item != null && item.GetType().Name == "TrackedPoseDriver");
                    row(tracking ? "OK" : "ERROR", $"Proveedor de tracking: {(rigs.Contains(root) ? "OVRCameraRig" : "TrackedPoseDriver")}; configurado={tracking}");
                }
                foreach (string side in new[] { "Left", "Right" })
                {
                    var anchors = root.GetComponentsInChildren<Transform>(true).Where(item => item.name == side + "HandAnchor" || item.name == side + "ControllerAnchor").ToArray();
                    var controllers = root.GetComponentsInChildren<Component>(true).Where(item => item != null && new[] { "ActionBasedController", "XRController", "OVRController", "Controller" }.Contains(item.GetType().Name) && Path(item.transform).IndexOf(side, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    row(anchors.Length + controllers.Length > 0 ? "OK" : "AVISO", $"{side}: anclajes={anchors.Length}, componentes de controlador={controllers.Length}. No demuestra conexión física ni interacción Touch.");
                }
            }
            var managers = find("OVRManager");
            foreach (var manager in managers)
            {
                var serialized = new SerializedObject(manager);
                foreach (string property in new[] { "usePositionTracking", "useRotationTracking", "controllerDrivenHandPosesType", "_trackingOriginType" })
                {
                    var value = serialized.FindProperty(property);
                    if (value != null) output.AppendLine($"[INFO] OVRManager/{property}: {(value.propertyType == SerializedPropertyType.Boolean ? value.boolValue.ToString() : value.intValue.ToString())}");
                }
            }
            var actionManagers = find("InputActionManager");
            row(actionManagers.Length > 0 ? "OK" : "AVISO", $"Input Action Manager (XRI): {actionManagers.Length}; Meta SDK gestiona su propio input");
            foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
                output.AppendLine($"[INFO] Input Actions {asset.name}: {asset.actionMaps.Count(map => map.enabled)}/{asset.actionMaps.Count} mapas activos (Edit Mode no prueba activación en Play)");
            var debug = components.OfType<DesktopDebugMode>().ToArray();
            foreach (var mode in debug)
                row(mode.isActiveAndEnabled && mode.EnabledForDesktop ? "AVISO" : "OK", $"DesktopDebugMode={mode.EnabledForDesktop}, componente activo={mode.isActiveAndEnabled}; exclusivo del Editor. Cambiar antes de Play y reiniciar la sesión.");
            if (debug.Length == 0) output.AppendLine("[INFO] DesktopDebugMode no encontrado en las escenas cargadas.");
            var arm = PlayerSettings.Android.targetArchitectures;
            row(arm == AndroidArchitecture.ARM64 ? "OK" : "ERROR", $"Arquitectura Android: {arm}");
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            row(backend == ScriptingImplementation.IL2CPP ? "OK" : "ERROR", $"Backend Android: {backend}");
            output.AppendLine($"[INFO] API Android: min={PlayerSettings.Android.minSdkVersion}, target={PlayerSettings.Android.targetSdkVersion}; gráficos={string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            row(identifier.Contains("UnityTechnologies") ? "AVISO" : "OK", $"Application Identifier Android: {identifier}");
            row(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/JapanDemonHunter.unity") ? "OK" : "AVISO", "Escena principal habilitada en lista global; revisar también override del Build Profile activo.");
            row(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android) ? "OK" : "ERROR", "Android Build Support instalado");
            if (Application.isPlaying)
            {
                var displays = new List<XRDisplaySubsystem>();
                var inputs = new List<XRInputSubsystem>();
                SubsystemManager.GetSubsystems(displays);
                SubsystemManager.GetSubsystems(inputs);
                output.AppendLine($"[INFO] Runtime: display activo={displays.Any(item => item.running)}, input activo={inputs.Any(item => item.running)}");
                foreach (var node in new[] { XRNode.Head, XRNode.LeftHand, XRNode.RightHand })
                {
                    var device = InputDevices.GetDeviceAtXRNode(node);
                    bool tracked = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool value) && value;
                    output.AppendLine($"[INFO] {node}: device válido={device.isValid}, tracked={tracked}, nombre={device.name}");
                }
            }
            output.AppendLine($"Resultado: {errors} errores; {warnings} avisos. Hardware/USB/ADB y rendimiento Quest no verificados.");
            return output.ToString();
        }

        private static string Path(Transform transform) => transform.parent == null ? transform.name : Path(transform.parent) + "/" + transform.name;
    }
}
