using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
public static class InspectDesktopSetup
{
 public static object Run()
 {
  Type settings=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.XR.Management.XRGeneralSettings")).First(t=>t!=null);
  var instance=settings.GetProperty("Instance").GetValue(null);
  var debug=UnityEngine.Object.FindAnyObjectByType<DesktopDebugMode>();
  var sword=GameObject.Find("Knife").GetComponent<SwordSwingAudio>();
  var registry=UnityEngine.Object.FindAnyObjectByType<CartAttachmentPoints>();
  var source=new SerializedObject(sword);var clips=source.FindProperty("swordSounds");
  var names=Enumerable.Range(0,clips.arraySize).Select(i=>clips.GetArrayElementAtIndex(i).objectReferenceValue.name).ToArray();
  return new{debugComponents=UnityEngine.Object.FindObjectsByType<DesktopDebugMode>().Length,debugEnabled=debug.EnabledForDesktop,
   xrInitOnStart=settings.GetProperty("InitManagerOnStart").GetValue(instance),xrSettingsAsset=AssetDatabase.GetAssetPath((UnityEngine.Object)instance),
   points=registry.Points.Where(p=>p.Accepts(MonsterAttachmentKind.Ground)).Select(p=>new{p.name,p.Capacity,position=p.transform.localPosition.ToString()}),clips=names,
   runtimeObjectsInEdit=UnityEngine.Object.FindObjectsByType<Transform>().Count(t=>t.name.StartsWith("DesktopDebug_"))};
 }
}
