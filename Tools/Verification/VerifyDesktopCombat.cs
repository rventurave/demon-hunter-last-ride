using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Reins;
using Object = UnityEngine.Object;

public static class VerifyDesktopCombat
{
 static void Check(bool value,string message) { if(!value)throw new Exception(message); }
 static async Task Frames(int count=2) { int until=Time.frameCount+count; float deadline=Time.realtimeSinceStartup+5; while(Time.frameCount<until) {Check(EditorApplication.isPlaying&&Time.realtimeSinceStartup<deadline,"Play frames stopped advancing");await Task.Delay(15);} }
 static T Read<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
 public static async Task<object> Run()
 {
  Check(EditorApplication.isPlaying,"Requires Play Mode");
  var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
  debug.RestartDebugScene(); await Frames(5);
  debug=Object.FindAnyObjectByType<DesktopDebugMode>();
  var motor=Object.FindAnyObjectByType<CarriageMotor>();
  var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
  var rig=GameObject.Find("OVRCameraRig").transform;
  Vector3 rigPosition=rig.localPosition; Quaternion rigRotation=rig.localRotation;
  var weapon=GameObject.Find("Knife");
  var sword=weapon.GetComponentInChildren<SwordDamage>();
  var audio=weapon.GetComponent<SwordSwingAudio>();
  var inputFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
  var background=InputSystem.settings.backgroundBehavior;
  InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
  var previousMouse=Mouse.current; bool mouseEnabled=previousMouse!=null&&previousMouse.enabled;
  if(mouseEnabled)InputSystem.DisableDevice(previousMouse);
  var mouse=InputSystem.AddDevice<Mouse>();
  var result=new Dictionary<string,object>();
  var states=new List<string>();
  var clips=new List<string>();
  Action<AudioClip> onSound=clip=>clips.Add(clip.name);
  audio.SoundPlayed+=onSound;
  try
  {
   Check(debug.SpawnZombie(-1),"Could not spawn left zombie through real spawner");
   var victim=spawner.ActiveMonsters.Last();
   victim.StateChanged+=(monster,from,to)=>states.Add(to.ToString());
   Vector3 initial=motor.transform.InverseTransformPoint(victim.transform.position);
   Check(initial.z<0,"Zombie was not in front");
   Check(debug.SpawnZombie(1),"Could not spawn right zombie through real spawner");
   float timeout=Time.realtimeSinceStartup+12f;
   while(!victim.Attachment.IsAttached&&Time.realtimeSinceStartup<timeout)await Frames();
   Check(victim.Attachment.IsAttached,"Natural pursuit did not attach within 12 seconds");
   Check(states.Contains("ChaseAttachment")&&states.Contains("Attach")&&states.Contains("Attached"),"Attachment state sequence missing");
   Vector3 attachmentPosition=victim.transform.localPosition;
   var point=victim.Attachment.ReservedPoint;
   int bitesBefore=victim.GetComponent<AttachedMonsterAttack>().AnimationCycleCount;
   await Task.Delay(400);
   Check((victim.transform.localPosition-attachmentPosition).magnitude<.02f,"Attached zombie did not follow cart");
   Check(victim.State==MonsterState.Attached,"Attached zombie returned to chase");
   var reserved=spawner.ActiveMonsters.Where(m=>m.Attachment!=null&&m.Attachment.HasReservedPoint).Select(m=>m.Attachment.ReservedPoint).ToArray();
   Check(reserved.Distinct().Count()==reserved.Length,"Two zombies share a reserved point");
   result["attachment"]=new{initialPosition=initial.ToString(),states,point=point.name,localPosition=victim.transform.localPosition.ToString(),followsCart=true,exclusiveReservations=true,attackCycles=victim.GetComponent<AttachedMonsterAttack>().AnimationCycleCount};
   // Move only the simulated hunter toward the occupied side. Keep the real XR Origin untouched.
   Vector3 avatar=debug.DebugHunter.parent.InverseTransformPoint(point.transform.position);
   avatar.x=Mathf.Clamp(avatar.x,-1.05f,1.05f); avatar.y=0f; avatar.z=Mathf.Clamp(avatar.z,-1.35f,1.35f);
   debug.DebugHunter.localPosition=avatar;
   await Frames();
   var body=victim.GetComponent<Collider>();
   Vector3 aim=body.bounds.center+Vector3.up*.2f-debug.DebugCamera.transform.position;
   float targetYaw=Quaternion.LookRotation(aim).eulerAngles.y;
   float targetPitch=Quaternion.LookRotation(aim).eulerAngles.x;
   if(targetPitch>180)targetPitch-=360;
   mouse.MakeCurrent(); debug.MouseCamera.CaptureCursor();
   InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.DeltaAngle(debug.MouseCamera.Yaw,targetYaw)/.12f,(debug.MouseCamera.Pitch-targetPitch)/.12f)});
   await Frames();
   var health=victim.GetComponent<MonsterDamageable>();
   int damageEvents=0; health.Damaged+=(target,amount,source)=>damageEvents++;
   var healthBySwing=new List<float>{health.CurrentHealth};
   var attackDurations=new List<float>();
   int startingSounds=audio.PlayedSoundCount;
   for(int attack=0;attack<30;attack++)
   {
    if(health.IsAlive)
    {
     Vector3 nextAim=body.bounds.center+Vector3.up*.2f-debug.DebugCamera.transform.position;
     Quaternion facing=Quaternion.LookRotation(nextAim);
     float nextPitch=facing.eulerAngles.x; if(nextPitch>180)nextPitch-=360;
     mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.DeltaAngle(debug.MouseCamera.Yaw,facing.eulerAngles.y)/.12f,(debug.MouseCamera.Pitch-nextPitch)/.12f)});
     await Frames();
    }
    int beforeSounds=audio.PlayedSoundCount, beforeDamage=damageEvents;
    Quaternion beforeRotation=weapon.transform.localRotation;
    float maximumPoseChange=0f;
    EditorApplication.CallbackFunction samplePose=()=>{if(weapon!=null)maximumPoseChange=Mathf.Max(maximumPoseChange,Quaternion.Angle(beforeRotation,weapon.transform.localRotation));};
    EditorApplication.update+=samplePose;
    try
    {
     mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true)); await Frames();
     await Frames(2);
     Check(audio.PlayedSoundCount==beforeSounds+1,"Mouse click did not start one swing");
     Check(maximumPoseChange>1f,"Visible sword did not move");
    }
    finally{EditorApplication.update-=samplePose;}
    mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState());
    float start=Time.time;
    float swingDeadline=Time.realtimeSinceStartup+3;
    while(debug.IsSwinging) {Check(Time.realtimeSinceStartup<swingDeadline,"Swing did not finish in 3 seconds");await Frames(1);}
    await Frames(2);
    attackDurations.Add(Time.time-start);
    Check(audio.PlayedSoundCount==beforeSounds+1,"A swing played zero or multiple sounds");
    Check(damageEvents-beforeDamage<=1,"One swing damaged the same zombie more than once");
    healthBySwing.Add(health.CurrentHealth);
    if(attack==0) { Check(damageEvents==1,"Visible swing missed the attached zombie"); Check(victim.GetComponent<MonsterSwordHitFeedback>()!=null,"Visual hit feedback missing"); }
    if(attack>=9&&clips.Distinct().Count()==5)break;
   }
   Check(clips.Distinct().Count()==5,"Not all five audio clips selected");
   Check(clips.Zip(clips.Skip(1),(a,b)=>a!=b).All(v=>v),"Immediate audio repeat");
   Check(health.CurrentHealth==0&&victim.IsDead,$"Zombie did not die from visible sword swings: health=[{string.Join(",",healthBySwing)}], state={victim.State}, motorEnabled={motor.enabled}");
   int idleSounds=audio.PlayedSoundCount; await Task.Delay(500); Check(audio.PlayedSoundCount==idleSounds,"Idle sword plays continuous audio");
   Check(rig.localPosition==rigPosition&&rig.localRotation==rigRotation,"Combat moved XR rig");
   result["sword"]=new{attacks=audio.PlayedSoundCount-startingSounds,clips,healthBySwing,damageEvents,dead=victim.IsDead,oneSoundPerSwing=true,oneHitPerVictimPerSwing=true,idleSilent=true,visualReaction=true,attackDurations};
   // Restore the debug layer while still in Play; verify its transient changes are reversible.
   var savedParent=Read<Transform>(debug,"weaponParent");
   var savedPosition=Read<Vector3>(debug,"weaponPosition");
   var enabledStates=Read<Dictionary<Behaviour,bool>>(debug,"savedBehaviours").ToArray();
   debug.SetDesktopMode(false);
   Check(enabledStates.All(pair=>pair.Key==null||pair.Key.enabled==pair.Value),"VR component enabled states not restored");
   bool restoredBackground=Application.runInBackground;
   Application.runInBackground=true; await Frames(2);
   Check(!debug.IsActive&&debug.DebugCamera==null,"Debug camera survived deactivation");
   Check(weapon.transform.parent==savedParent&&weapon.transform.localPosition==savedPosition,"Sword not restored");
   Check(weapon.GetComponent<GrabbableWeapon>().enabled,"VR weapon driver not restored");
   result["deactivation"]=new{cameraRemoved=true,weaponRestored=true,vrComponentStatesRestored=true,xrRigUnchanged=true};
   Application.runInBackground=restoredBackground;
   debug.SetDesktopMode(true); await Frames(2);
   System.IO.File.WriteAllText("Docs/Verification/DesktopCombat-2026-09-29.json",Unity.Plastic.Newtonsoft.Json.JsonConvert.SerializeObject(result,Unity.Plastic.Newtonsoft.Json.Formatting.Indented));
   return result;
  }
  finally
  {
   if(audio!=null)audio.SoundPlayed-=onSound;
   InputSystem.RemoveDevice(mouse);if(mouseEnabled)InputSystem.EnableDevice(previousMouse);
   InputSystem.settings.editorInputBehaviorInPlayMode=inputFocus;
   InputSystem.settings.backgroundBehavior=background;
  }
 }
}
