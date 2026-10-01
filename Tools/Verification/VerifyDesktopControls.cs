using System;
using System.Linq;
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

public static class VerifyDesktopControls
{
 static void Check(bool value,string message) { if(!value)throw new Exception(message); }
 static async Task Frames(int count=2) { int until=Time.frameCount+count; while(Time.frameCount<until)await Task.Delay(20); }
 static async Task Press(Keyboard keyboard,Key key) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)); await Frames(); InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Frames(); }
 public static async Task<object> Run()
 {
  Check(EditorApplication.isPlaying,"Requires Play Mode");
  var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
  Check(debug.IsActive,"Desktop harness inactive");
  var rig=GameObject.Find("OVRCameraRig").transform;
  var rigPosition=rig.localPosition; var rigRotation=rig.localRotation;
  var motor=Object.FindAnyObjectByType<CarriageMotor>();
  var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
  var previousKeyboard=Keyboard.current; var previousMouse=Mouse.current;
  var inputFocus=InputSystem.settings.editorInputBehaviorInPlayMode;
  var background=InputSystem.settings.backgroundBehavior;
  InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
  bool keyboardEnabled=previousKeyboard!=null&&previousKeyboard.enabled, mouseEnabled=previousMouse!=null&&previousMouse.enabled;
  if(keyboardEnabled)InputSystem.DisableDevice(previousKeyboard);
  if(mouseEnabled)InputSystem.DisableDevice(previousMouse);
  var keyboard=InputSystem.AddDevice<Keyboard>(); var mouse=InputSystem.AddDevice<Mouse>();
  var result=new Dictionary<string,object>();
  try
  {
   debug.MouseCamera.ConfigureDesktopHunterView(debug.DebugHunter,Vector3.up*1.6f,.12f,false);
   await Frames();
   Vector3 direction=debug.DebugCamera.transform.forward;
   var horses=(GameObject.Find("Horse_Model_Left").transform.position+GameObject.Find("Horse_Model_Right").transform.position)*.5f;
   Check(Vector3.Dot(direction,Vector3.ProjectOnPlane(horses-debug.DebugCamera.transform.position,Vector3.up).normalized)>.98f,"Initial camera does not face horses");
   Check(Mathf.Abs(debug.DebugCamera.transform.position.y-debug.DebugHunter.position.y-1.6f)<.02f,"Eye-height offset wrong");
   float yaw=debug.MouseCamera.Yaw;
   debug.MouseCamera.CaptureCursor();
   mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(1500,0)}); await Frames();
   Check(Mathf.Abs(debug.MouseCamera.Yaw-yaw-180f)<.5f,"Mouse cannot look behind: delta="+(debug.MouseCamera.Yaw-yaw));
   mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(1500,0)}); await Frames();
   Check(Mathf.Abs(debug.MouseCamera.Yaw-yaw-360f)<.5f,"Mouse cannot rotate 360 degrees");
   Vector3 start=debug.DebugHunter.localPosition;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); await Task.Delay(400); InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await Frames();
   Check(debug.DebugHunter.localPosition.z>start.z+.1f,"W does not move simulated hunter");
   Check(rig.localPosition==rigPosition&&rig.localRotation==rigRotation,"Debug moved VR rig");
   bool hud=debug.HudVisible; await Press(keyboard,Key.F1); Check(debug.HudVisible!=hud,"F1 does not toggle HUD"); await Press(keyboard,Key.F1);
   result["camera"]=new{initialDirection=direction.ToString(),eyeHeight=1.6,mouseYawChange=debug.MouseCamera.Yaw-yaw,desktopLocalMovement=debug.DebugHunter.localPosition.ToString(),xrRigUnchanged=true,hudToggle=true};
   float before=motor.Speed,time=Time.time;
   await Press(keyboard,Key.Space);
   float after=motor.Speed;
   Check(after>before+1.3f,"Space did not call production acceleration");
   var coast=new List<float>{after};
   for(int i=0;i<3;i++){await Task.Delay(400);coast.Add(motor.Speed);}
   Check(coast.Zip(coast.Skip(1),(a,b)=>b<a).All(v=>v),"Speed does not coast progressively");
   float coastEnd=motor.Speed; await Press(keyboard,Key.Space); Check(motor.Speed>coastEnd+1.3f,"Cannot accelerate again after coasting");
   result["speed"]=new{before,after,coast,afterSecond=motor.Speed,firstSpawnGateReleased=!spawner.IsWaitingForFirstGallop};
   var placements=new List<object>();
   foreach(Key key in new[]{Key.Z,Key.X,Key.C})
   {
    var beforeKey=spawner.ActiveMonsters.ToArray(); await Press(keyboard,key);
    var added=spawner.ActiveMonsters.Except(beforeKey).ToArray();
    Check(added.Length==1,$"{key} failed to spawn through MonsterSpawner");
    var newest=added[0];
    Vector3 local=motor.transform.InverseTransformPoint(newest.transform.position);
    Check(local.z<0,$"{key} did not spawn in front");
    if(key==Key.X)Check(local.x<0,"X did not spawn left");
    if(key==Key.C)Check(local.x>0,"C did not spawn right");
    placements.Add(new{key=key.ToString(),position=local.ToString(),state=newest.State.ToString()});
   }
   var beforeMembers=spawner.ActiveMonsters.ToArray();
   int beforeGroup=spawner.ActiveMonsterCount; await Press(keyboard,Key.V);
   var newMembers=spawner.ActiveMonsters.Except(beforeMembers).ToArray();
   Check(newMembers.Length>0,"V did not create a dispersed group");
   if(spawner.UsesEncounterGroups) Check(newMembers.All(m=>m.SpawnDirection==MonsterSpawnDirection.LeftForest || m.SpawnDirection==MonsterSpawnDirection.RightForest),"V creates a frontal group");
   result["spawn"]=new{placements,groupAdded=newMembers.Length,lateralGroup=true};
   var zombies=spawner.ActiveMonsters.Where(m=>m.MovementType==MonsterMovementType.Ground).ToArray();
   await Task.Delay(500);
   Check(zombies.Any(m=>m.State==MonsterState.ChaseAttachment||m.State==MonsterState.Attach||m.State==MonsterState.Attached),"Ground FSM never pursued attachment");
   await Press(keyboard,Key.Space); await Press(keyboard,Key.Space);
   result["fsm"]=zombies.Select(m=>new{state=m.State.ToString(),position=motor.transform.InverseTransformPoint(m.transform.position).ToString()}).ToArray();
   System.IO.File.WriteAllText("Docs/Verification/DesktopControls-2026-10-01.json",Unity.Plastic.Newtonsoft.Json.JsonConvert.SerializeObject(result,Unity.Plastic.Newtonsoft.Json.Formatting.Indented));
   return result;
  }
  finally
  {
   InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
   if(keyboardEnabled)InputSystem.EnableDevice(previousKeyboard);if(mouseEnabled)InputSystem.EnableDevice(previousMouse);
   InputSystem.settings.editorInputBehaviorInPlayMode=inputFocus;
   InputSystem.settings.backgroundBehavior=background;
  }
 }
}
