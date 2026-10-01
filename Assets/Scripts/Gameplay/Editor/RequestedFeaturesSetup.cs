using System;
using System.Collections.Generic;
using System.Linq;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Reins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>Focused migration of the open scene, without regenerating it or changing XR packages.</summary>
    public static class RequestedFeaturesSetup
    {
        [MenuItem("Tools/Game/Apply Requested VR Features (in place)")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying scene changes.");
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/JapanDemonHunter.unity")
                throw new InvalidOperationException("Open JapanDemonHunter first; this tool does not replace the open scene.");
            Transform cart = Required("VehicleRoot");
            Bounds wagon = LocalBounds(Required("Wagon_Model"), cart);
            float floor = wagon.center.y;
            // 1: initial authored pose only; tracking anchors and runtime poses remain owned by XR.
            Transform rig = Required("OVRCameraRig");
            if (Mathf.Approximately(rig.localPosition.y, 0.695f))
            {
                Undo.RecordObject(rig, "Align initial rig floor");
                Vector3 position = rig.localPosition;
                position.y = floor;
                rig.localPosition = position;
            }
            Transform leftHorse = Required("Horse_Model_Left");
            Transform rightHorse = Required("Horse_Model_Right");
            Vector3 horseDirection = (leftHorse.position + rightHorse.position) * 0.5f - rig.position;
            horseDirection.y = 0f;
            Undo.RecordObject(rig, "Face horses at scene start");
            rig.rotation = Quaternion.LookRotation(horseDirection, Vector3.up);
            Transform knife = Required("Knife");
            if (Mathf.Approximately(knife.localPosition.x, 0.55f) &&
                Mathf.Approximately(knife.localPosition.z, -0.45f))
            {
                Undo.RecordObject(knife, "Place weapon within reach");
                knife.localPosition = new Vector3(rig.localPosition.x + 0.45f, floor + 0.75f,
                    rig.localPosition.z - 0.35f);
                Set(knife.GetComponent<GrabbableWeapon>(), "deckLocalHeight", knife.localPosition.y);
            }
            // 2: FormerlySerializedAs preserves the existing deceleration setting. No acceleration edits.
            CarriageMotor motor = cart.GetComponent<CarriageMotor>();
            if (motor == null) throw new InvalidOperationException("Missing carriage motor.");
            // 3: reuse ground placement, navigation and attachment chase; front bats remain unchanged.
            MonsterSpawner spawner = Object.FindAnyObjectByType<MonsterSpawner>();
            Undo.RecordObject(spawner, "Front dispersed zombies");
            foreach (MonsterSpawnEntry entry in spawner.SpawnEntries)
                if (entry.prefab != null && entry.movementType == MonsterMovementType.Ground && !entry.isFaceThreat)
                    entry.spawnDirection = MonsterSpawnDirection.FrontDispersed;
            Set(spawner, "minimumHordeSize", 0);
            EditorUtility.SetDirty(spawner);
            // 4: one reservation per named point. Existing rear/flying objects are preserved.
            CartAttachmentPoints registry = Object.FindAnyObjectByType<CartAttachmentPoints>();
            var points = registry.Points.ToList();
            AddPoint(points, cart, "RearCenterAttachPoint", "Attach_RearCenter", new Vector3(0f, 0.15f, wagon.max.z + 0.45f));
            AddPoint(points, cart, "RearLeftAttachPoint", "Attach_LeftRear", new Vector3(wagon.min.x + 0.45f, 0.15f, wagon.max.z + 0.45f));
            AddPoint(points, cart, "RearRightAttachPoint", "Attach_RightRear", new Vector3(wagon.max.x - 0.45f, 0.15f, wagon.max.z + 0.45f));
            AddPoint(points, cart, "LeftAttachPoint", "Attach_LeftFront", new Vector3(wagon.min.x - 0.45f, 0.15f, wagon.max.z - 1.5f));
            AddPoint(points, cart, "RightAttachPoint", "Attach_RightFront", new Vector3(wagon.max.x + 0.45f, 0.15f, wagon.max.z - 1.5f));
            Undo.RecordObject(registry, "Enable side attachment reservations");
            registry.Configure(points);
            Set(registry, "rearPointsOnly", false);
            // 5: tune existing lights only. Existing range, shadows, URP and exposure stay configured.
            CarriageLampAmbience ambience = cart.GetComponent<CarriageLampAmbience>();
            ReplaceDefault(ambience, "frontIntensity", 6f, 7f);
            ReplaceDefault(ambience, "rearIntensity", 5f, 6f);
            Light moon = GameObject.Find("Moonlight")?.GetComponent<Light>();
            if (moon != null && Mathf.Approximately(moon.intensity, 0.34f))
            {
                Undo.RecordObject(moon, "Lift moon fill slightly");
                moon.intensity = 0.4f;
            }
            if (Mathf.Approximately(RenderSettings.ambientLight.r, 0.006f) &&
                Mathf.Approximately(RenderSettings.ambientLight.g, 0.007f) &&
                Mathf.Approximately(RenderSettings.ambientLight.b, 0.008f))
                RenderSettings.ambientLight = new Color(0.012f, 0.014f, 0.016f);
            // 6: visual mesh and existing collider/tip together; no grab or attach transform changes.
            Transform model = Required("Knife_Model");
            Bounds blade = LocalBounds(model, knife);
            if (Mathf.Abs(blade.size.z - 0.85f) < 0.02f)
            {
                const float length = 0.98f;
                float ratio = length / blade.size.z;
                Undo.RecordObject(model, "Extend existing blade");
                model.localScale *= ratio;
                model.localPosition = new Vector3(model.localPosition.x * ratio,
                    model.localPosition.y * ratio, 0.2f + (model.localPosition.z - 0.2f) * ratio);
                CapsuleCollider collider = knife.GetComponent<CapsuleCollider>();
                Undo.RecordObject(collider, "Match blade collider");
                collider.height = length;
                collider.center = new Vector3(0f, 0f, 0.2f);
                Transform tip = knife.Find("BladeTip");
                Undo.RecordObject(tip, "Match sweep tip");
                tip.localPosition = new Vector3(0f, 0f, 0.2f + length * 0.5f);
            }
            // 7: all five original sounds, extracted without replacing the MP4 files.
            SetupSwordAudio(knife.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Requested VR features applied in priority order without rebuilding the scene.");
        }

        public static void SetupSwordAudio(GameObject knife)
        {
            AudioClip[] clips = Enumerable.Range(1, 4).Select(index =>
                AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Art/Audio/swordSound/{index}.wav")).ToArray();
            if (clips.Any(clip => clip == null)) throw new InvalidOperationException("Import all five extracted sword WAV clips first.");
            SwordSwingAudio feedback = knife.GetComponent<SwordSwingAudio>();
            if (feedback != null) return; // Preserve manual clip, volume and source assignments on subsequent runs.
            AudioSource source = knife.GetComponent<AudioSource>();
            if (source == null)
            {
                source = Undo.AddComponent<AudioSource>(knife);
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 0.5f;
                source.maxDistance = 8f;
                source.dopplerLevel = 0f;
            }
            feedback = Undo.AddComponent<SwordSwingAudio>(knife);
            feedback.Configure(knife.GetComponentInChildren<SwordDamage>(), source, clips);
            feedback.ConfigureGrabAudio(knife.GetComponent<GrabbableWeapon>(),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/swordSound/5.wav"));
            EditorUtility.SetDirty(feedback);
        }

        private static void AddPoint(List<MonsterAttachmentPoint> points, Transform cart,
            string name, string legacyName, Vector3 position)
        {
            if (GameObject.Find(name) != null) return;
            GameObject obj = GameObject.Find(legacyName);
            if (obj == null)
            {
                obj = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(obj, "Add cart attachment point");
                obj.transform.SetParent(cart, false);
            }
            else Undo.RecordObject(obj, "Migrate attachment point");
            Undo.RecordObject(obj.transform, "Place attachment at wheels");
            obj.name = name;
            obj.transform.position = cart.TransformPoint(position);
            MonsterAttachmentPoint point = obj.GetComponent<MonsterAttachmentPoint>();
            if (point == null) point = Undo.AddComponent<MonsterAttachmentPoint>(obj);
            Undo.RecordObject(point, "Configure one occupant per point");
            Vector3 inward = -position;
            inward.y = 0f;
            point.Configure(MonsterAttachmentKind.Ground, 1, inward);
            EditorUtility.SetDirty(point);
            if (!points.Contains(point)) points.Add(point);
        }

        internal static Bounds LocalBounds(Transform target, Transform reference)
        {
            bool found = false;
            Bounds result = default;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    Vector3 local = reference.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!found) { result = new Bounds(local, Vector3.zero); found = true; }
                    else result.Encapsulate(local);
                }
            }
            if (!found) throw new InvalidOperationException($"No renderer bounds on {target.name}.");
            return result;
        }

        private static Transform Required(string name) => GameObject.Find(name)?.transform ??
            throw new InvalidOperationException($"Missing scene object: {name}.");

        private static void ReplaceDefault(Object target, string field, float oldValue, float newValue)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            if (Mathf.Approximately(serialized.FindProperty(field).floatValue, oldValue)) Set(target, field, newValue);
        }

        private static void Set(Object target, string field, object value)
        {
            if (target == null) throw new InvalidOperationException($"Missing component for {field}.");
            Undo.RecordObject(target, "Configure requested feature");
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field) ?? throw new InvalidOperationException(field);
            if (value is float f) property.floatValue = f;
            else if (value is int i) property.intValue = i;
            else if (value is bool b) property.boolValue = b;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}
