using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Oculus.Interaction.HandGrab;
using Reins;

public static class InspectRequestedFeatures
{
    public static object Run()
    {
        return new {
            scene = SceneManager.GetActiveScene().path,
            dirty = SceneManager.GetActiveScene().isDirty,
            components = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c is ReinHandle || c is CarriageMotor || c is GrabbableWeapon || c is SwordSwingAudio ||
                    c is HandStrikeController || c is GameSessionController || c is GameSoundscape || c is DesktopDebugMode ||
                    c is MonsterSpawner || c is GiantZombieSpawner || c is LevelVictoryController || c is SwordDamage)
                .Select(c => new { type = c.GetType().Name, name = c.name, enabled = c.enabled,
                    position = c.transform.localPosition.ToString("F3"), data = EditorJsonUtility.ToJson(c) }).ToArray(),
            grabs = Object.FindObjectsByType<HandGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(g => new { name = g.name, poses = g.HandGrabPoses.Count, data = EditorJsonUtility.ToJson(g) }).ToArray(),
            bounds = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.transform.name.Contains("Wagon") || r.transform.name.Contains("Knife"))
                .Select(r => new { name = r.name, bounds = r.bounds.ToString() }).ToArray()
        };
    }
}
