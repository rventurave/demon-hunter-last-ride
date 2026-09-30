using System;
using System.Linq;
using UnityEditor;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

public static class ConfigureQuestOpenXR
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var quest = settings.GetFeatures<OpenXRFeature>().FirstOrDefault(feature => feature.GetType().Name == "MetaQuestFeature");
        if (quest == null) throw new InvalidOperationException("Installed OpenXR does not expose Meta Quest Support.");
        if (quest.enabled) return "Meta Quest Support already enabled. No changes.";
        Undo.RecordObject(quest, "Enable Meta Quest Support");
        quest.enabled = true;
        EditorUtility.SetDirty(quest);
        AssetDatabase.SaveAssetIfDirty(quest);
        return "Meta Quest Support enabled for Android. Meta XR Feature, input, desktop settings and scenes preserved.";
    }
}
