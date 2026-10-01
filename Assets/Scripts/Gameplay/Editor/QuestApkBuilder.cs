using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JapaneseDemonHunter.GameplayEditor
{
    public sealed class QuestApkBuilder : EditorWindow
    {
        private const string MainScene = "Assets/Scenes/JapanDemonHunter.unity";
        private static string LastApkKey => "JDH.QuestApk." + Application.dataPath;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Quest 2/Compilar APK e instalar manualmente")]
        [MenuItem("Tools/Game/Compilar APK para Quest 2")]
        public static void Open()
        {
            GetWindow<QuestApkBuilder>("APK para Quest 2").minSize = new Vector2(480, 480);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("Japanese Demon Hunter — Quest 2", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Genera un APK de la escena principal usando los ajustes Android y XR actuales. Luego puedes instalarlo en Quest 2 con tu herramienta habitual.", MessageType.Info);
            EditorGUILayout.LabelField("Escena", "JapanDemonHunter");
            EditorGUILayout.LabelField("Plataforma activa", EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.LabelField("Application ID", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));

            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating ||
                        EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer;
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Compilar APK para Quest 2", GUILayout.Height(40)))
                    BuildApk();
            }
            if (busy)
                EditorGUILayout.HelpBox("Detén Play Mode y espera a que Unity termine de importar o compilar.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Instalar manualmente en Quest 2", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Activa el modo desarrollador del visor desde la app Meta Horizon del teléfono (requiere una cuenta de desarrollador).\n\n" +
                "2. Conecta Quest 2 al PC con un cable USB de datos y acepta Permitir depuración USB dentro del visor.\n\n" +
                "3. Abre Meta Quest Developer Hub > Device Manager y selecciona tu Quest 2 conectado.\n\n" +
                "4. En Apps, pulsa Add Build y selecciona el APK, o arrástralo sobre el dispositivo conectado.\n\n" +
                "5. En el visor abre Biblioteca > Fuentes desconocidas y ejecuta el juego.", MessageType.Info);

            string lastApk = EditorPrefs.GetString(LastApkKey, string.Empty);
            EditorGUILayout.LabelField("Último APK generado");
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(lastApk) ? "Todavía no hay un APK generado desde este menú." : lastApk,
                EditorStyles.wordWrappedLabel, GUILayout.Height(38));
            using (new EditorGUI.DisabledScope(!File.Exists(lastApk)))
            {
                if (GUILayout.Button("Mostrar APK en el Explorador"))
                    EditorUtility.RevealInFinder(lastApk);
            }
            if (GUILayout.Button("Seleccionar un APK existente"))
            {
                string selected = EditorUtility.OpenFilePanel("Seleccionar APK", Path.GetDirectoryName(Application.dataPath), "apk");
                if (!string.IsNullOrEmpty(selected))
                    EditorPrefs.SetString(LastApkKey, selected);
            }
            if (GUILayout.Button("Abrir guía oficial de instalación manual"))
                Application.OpenURL("https://developers.meta.com/horizon/documentation/unreal/ts-mqdh-deploy-build/");
            EditorGUILayout.EndScrollView();
        }

        private static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("Selecciona Android", "Activa el perfil Meta Quest (Android) desde File > Build Profiles y vuelve a pulsar el botón.", "Aceptar");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("Falta soporte Android", "Instala Android Build Support, SDK/NDK y OpenJDK para este Editor desde Unity Hub.", "Aceptar");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null)
            {
                EditorUtility.DisplayDialog("Falta la escena", MainScene, "Aceptar");
                return;
            }
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0 ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                EditorUtility.DisplayDialog("Revisa los ajustes Android", "Quest 2 requiere ARM64 e IL2CPP. Configúralos en Project Settings > Player > Android > Other Settings.", "Aceptar");
                return;
            }

            string output = EditorUtility.SaveFilePanel("Guardar APK para Quest 2", Path.GetDirectoryName(Application.dataPath), "JapaneseDemonHunter-Quest2", "apk");
            if (string.IsNullOrEmpty(output) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool previousSplit = PlayerSettings.Android.buildApkPerCpuArchitecture;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                PlayerSettings.Android.buildApkPerCpuArchitecture = false;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainScene },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result == BuildResult.Succeeded)
                {
                    EditorPrefs.SetString(LastApkKey, output);
                    Debug.Log($"APK para Quest 2 generado: {output}");
                    EditorUtility.RevealInFinder(output);
                }
                else
                {
                    EditorUtility.DisplayDialog("APK no generado", $"Resultado: {report.summary.result}. Revisa los errores en la consola de Unity.", "Aceptar");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Error al compilar", "Revisa la consola de Unity para ver el detalle.", "Aceptar");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
                PlayerSettings.Android.buildApkPerCpuArchitecture = previousSplit;
            }
        }
    }
}
