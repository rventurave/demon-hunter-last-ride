using System.Linq;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Reins;
using UnityEngine;
using UnityEditor;
public static class InspectDesktopPlay
{
 public static object Run()
 {
  var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
  var rig=GameObject.Find("OVRCameraRig").transform;
  var motor=Object.FindAnyObjectByType<CarriageMotor>();
  var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
  return new { playing=EditorApplication.isPlaying, active=debug.IsActive, camera=debug.DebugCamera.transform.position.ToString(),forward=debug.DebugCamera.transform.forward.ToString(),rig=rig.localPosition.ToString(),speed=motor.Speed,
   cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c=>new{c.name,c.enabled}),
   monsters=spawner.ActiveMonsters.Select(m=>new{m.name,state=m.State.ToString(),position=m.transform.position.ToString(),relative=motor.transform.InverseTransformPoint(m.transform.position).ToString()})};
 }
}
