using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using JapaneseDemonHunter.Gameplay;
using Object = UnityEngine.Object;
public static class CaptureDesktopView
{
 public static async Task<object> Run()
 {
  var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
  if(!debug.IsActive)debug.SetDesktopMode(true);
  debug.RestartDebugScene();
  await Task.Delay(700);
  debug=Object.FindAnyObjectByType<DesktopDebugMode>();
  debug.MouseCamera.ConfigureDesktopHunterView(debug.DebugHunter,Vector3.up*1.6f,.12f,false);
  debug.MouseCamera.ReleaseCursor();
  if(!debug.HudVisible)debug.ToggleHud();
  Type gameView=typeof(Editor).Assembly.GetType("UnityEditor.GameView");
  EditorWindow.GetWindow(gameView).Focus();
  ScreenCapture.CaptureScreenshot("Docs/Verification/DesktopDebugGameView.png");
  await Task.Delay(700);
  return new{path="Docs/Verification/DesktopDebugGameView.png",hud=debug.HudVisible,camera=debug.DebugCamera.transform.position.ToString()};
 }
}
