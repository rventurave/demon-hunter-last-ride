using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class InspectDesktopAudio
{
 public static object Run()
 {
  var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
  var audioUtil=typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
  var mute=audioUtil.GetProperty("masterMute",flags);
  return new{muteProperty=mute?.GetValue(null),methods=audioUtil.GetMethods(flags).Where(m=>m.Name.ToLower().Contains("mute")).Select(m=>m.ToString()),editorMute=typeof(EditorUtility).GetProperties(flags).Where(p=>p.Name.ToLower().Contains("audio")).Select(p=>new{p.Name,value=p.GetValue(null)}),gameViewAudio=typeof(Editor).Assembly.GetType("UnityEditor.GameView").GetProperties(flags|BindingFlags.Instance).Where(p=>p.Name.ToLower().Contains("audio")).Select(p=>p.ToString()),AudioListener.pause,AudioListener.volume,
   listeners=UnityEngine.Object.FindObjectsByType<AudioListener>().Select(l=>new{l.name,l.enabled}),
   settings=AudioSettings.GetConfiguration(),sword=GameObject.Find("Knife").GetComponent<AudioSource>().enabled};
 }
}
