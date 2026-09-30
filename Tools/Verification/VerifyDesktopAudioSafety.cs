using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Object = UnityEngine.Object;
public static class VerifyDesktopAudioSafety
{
 static T Read<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
 static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
 static async Task Swing(DesktopDebugMode debug)
 {
  debug.SwingSword();float until=Time.realtimeSinceStartup+3;
  while(debug.IsSwinging){Check(Time.realtimeSinceStartup<until,"Swing stalled");await Task.Delay(20);}
  await Task.Delay(80);
 }
 public static async Task<object> Run()
 {
  var debug=Object.FindAnyObjectByType<DesktopDebugMode>();Check(debug.IsActive,"Enable desktop debug first");
  var weapon=GameObject.Find("Knife");var audio=weapon.GetComponent<SwordSwingAudio>();
  var sword=weapon.GetComponentInChildren<SwordDamage>();var source=weapon.GetComponent<AudioSource>();
  var clips=Read<AudioClip[]>(audio,"swordSounds");
  int before=audio.PlayedSoundCount;
  try
  {
   audio.Configure(sword,source,new[]{null,clips[2],null});
   await Swing(debug); await Swing(debug);
   Check(audio.PlayedSoundCount==before+2&&audio.LastPlayedClip==clips[2],"Single valid clip/null handling failed");
   audio.Configure(sword,source,new AudioClip[5]);await Swing(debug);
   Check(audio.PlayedSoundCount==before+2,"Empty array produced sound");
   audio.Configure(sword,source,null);await Swing(debug);
   Check(audio.PlayedSoundCount==before+2,"Null array produced sound");
   audio.Configure(sword,source,clips);
   debug.SwingSword();await Task.Delay(100);Check(source.isPlaying,"PlayOneShot did not start on existing source");
   float[] samples=new float[512];float rms=0f,listenerRms=0f;
   for(int sample=0;sample<6;sample++)
   {
    source.GetOutputData(samples,0);rms=Mathf.Max(rms,Mathf.Sqrt(samples.Sum(value=>value*value)/samples.Length));
    AudioListener.GetOutputData(samples,0);listenerRms=Mathf.Max(listenerRms,Mathf.Sqrt(samples.Sum(value=>value*value)/samples.Length));
    await Task.Delay(80);
   }
   while(debug.IsSwinging)await Task.Delay(20);
   int after=audio.PlayedSoundCount;await Task.Delay(600);
   Check(after==audio.PlayedSoundCount,"Idle audio repeats");
   Check(weapon.GetComponents<AudioSource>().Length==1,"AudioSource duplicated");
   var result=new{singleValidClipSafe=true,unassignedArraySafe=true,nullArraySafe=true,idleSilent=true,audioSourceCount=1,playOneShotStarted=true,sourceOutputRms=rms,listenerOutputRms=listenerRms,editorMuted=EditorUtility.audioMasterMute,oneShotVolume=Read<float>(audio,"volume"),sourceVolume=source.volume,spatialBlend=source.spatialBlend};
   System.IO.File.WriteAllText("Docs/Verification/DesktopAudioSafety-2026-09-29.json",Unity.Plastic.Newtonsoft.Json.JsonConvert.SerializeObject(result,Unity.Plastic.Newtonsoft.Json.Formatting.Indented));
   return result;
  }
  finally {audio.Configure(sword,source,clips);}
 }
}
