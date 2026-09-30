using System.Linq;
using System.Reflection;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using UnityEngine;
using UnityEditor;
public static class InspectDesktopCombat
{
 public static object Run()
 {
  var weapon=GameObject.Find("Knife");var swing=weapon.GetComponent<PrototypeSwordController>();
  return new {Time.time,Time.timeScale,Time.deltaTime,Time.frameCount,Application.isFocused,Application.runInBackground,EditorApplication.isPaused,
   swing=swing.IsSwinging,elapsed=swing.GetType().GetField("swingElapsed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(swing),
   clips=weapon.GetComponent<SwordSwingAudio>().PlayedSoundCount,monsters=Object.FindObjectsByType<MonsterDamageable>().Select(m=>new{health=m.CurrentHealth,state=m.GetComponent<MonsterBase>().State.ToString()}),
   sourcePlaying=weapon.GetComponent<AudioSource>().isPlaying,mouse=UnityEngine.InputSystem.Mouse.current.name,
   tip=weapon.GetComponentInChildren<SwordDamage>().transform.position.ToString(),knifeEuler=weapon.transform.localEulerAngles.ToString()};
 }
}
