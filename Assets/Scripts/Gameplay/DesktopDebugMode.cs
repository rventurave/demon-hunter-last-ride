using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Prototype;
using Reins;
using UnityEditor;
using UnityEngine.InputSystem;
#endif

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Editor-only desktop harness. Never drives the XR Origin or ships desktop input in a player.</summary>
    [DefaultExecutionOrder(-31000)]
    [DisallowMultipleComponent]
    public sealed class DesktopDebugMode : MonoBehaviour
    {
        [Tooltip("Editor only. Explicitly bypass XR startup for this Play session; saved XR settings are unchanged.")]
        [SerializeField] private bool desktopDebugMode = true;
        [SerializeField, Min(0.2f)] private float eyeHeight = 1.6f;
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float cameraMoveSpeed = 1.5f;
        [SerializeField] private Vector2 cameraHalfExtents = new Vector2(1.05f, 1.75f);
        [SerializeField, Min(1f)] private float zombieSpawnRadius = 15f;
        [SerializeField, Range(0f, 60f)] private float lateralSpawnAngle = 12f;
        [SerializeField, Range(1, 10)] private int groupSpawnCount = 4;
        [SerializeField] private Vector3 swordViewOffset = new Vector3(0.28f, -0.35f, 0.4f);
        [SerializeField] private Vector3 swingStartEuler = new Vector3(0f, -55f, 0f);
        [SerializeField] private Vector3 swingEndEuler = new Vector3(0f, 55f, 0f);
        [SerializeField] private bool showHud = true;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool logEvents = true;
        public bool EnabledForDesktop => desktopDebugMode;

#if UNITY_EDITOR
        private CarriageMotor motor;
        private MonsterSpawner spawner;
        private CartAttachmentPoints attachmentPoints;
        private SwordDamage sword;
        private SwordSwingAudio audioFeedback;
        private PrototypeSwordController swing;
        private GameObject viewRoot;
        private GameObject cameraObject;
        private Transform weaponParent;
        private Transform weaponRoot;
        private Vector3 weaponPosition, weaponScale;
        private Quaternion weaponRotation;
        private Transform savedVelocityReference;
        private bool savedVelocityWindows;
        private float savedSwingSpeed, savedGrace;
        private float swordDamageAmount;
        private readonly Dictionary<Behaviour, bool> savedBehaviours = new Dictionary<Behaviour, bool>();
        private readonly HashSet<MonsterBase> observedMonsters = new HashSet<MonsterBase>();
        private bool active;
        private bool savedRunInBackground;
        private int hitsThisSwing;
        public Camera DebugCamera => cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        public SmoothFollowCamera MouseCamera => cameraObject != null ? cameraObject.GetComponent<SmoothFollowCamera>() : null;
        public Transform DebugHunter => viewRoot != null ? viewRoot.transform.GetChild(0) : null;
        public bool IsActive => active;
        public bool HudVisible => active && showHud;
        public int HitsThisSwing => hitsThisSwing;
        public bool IsSwinging => swing != null && swing.IsSwinging;

        private void Start() { if (desktopDebugMode) Activate(); }
        private void Update()
        {
            if (desktopDebugMode != active) { if (desktopDebugMode) Activate(); else Deactivate(); }
            if (!active) return;
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.spaceKey.wasPressedThisFrame || keys.leftShiftKey.wasPressedThisFrame) SimulateLash();
                if (keys.zKey.wasPressedThisFrame) SpawnZombie(0);
                if (keys.xKey.wasPressedThisFrame) SpawnZombie(-1);
                if (keys.cKey.wasPressedThisFrame) SpawnZombie(1);
                if (keys.vKey.wasPressedThisFrame) SpawnGroup();
                if (keys.f1Key.wasPressedThisFrame) ToggleHud();
                if (keys.rKey.wasPressedThisFrame) RestartDebugScene();
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                MouseCamera.CaptureCursor();
                SwingSword();
            }
        }

        public void SetDesktopMode(bool value) { desktopDebugMode = value; if (!value) Deactivate(); else Activate(); }
        public void ToggleHud() => showHud = !showHud;
        public void RestartDebugScene()
        {
            if (active) UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                gameObject.scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        }
        public void SimulateLash()
        {
            if (!active || motor == null) return;
            FindAnyObjectByType<GameSessionController>()?.TryStartFromGrip(true,true);
            ((ICartAccelerationRequester)motor).RequestAcceleration();
            // RequestAcceleration intentionally has no gesture event. Reuse the spawner's existing start gate.
            if (spawner.IsWaitingForFirstGallop) spawner.NotifyDebugFirstGallop();
        }

        public bool SpawnZombie(int side)
        {
            if (!active) return false;
            MonsterSpawnEntry entry = spawner.SpawnEntries.FirstOrDefault(item => item != null &&
                item.prefab != null && item.movementType == MonsterMovementType.Ground && !item.isFaceThreat);
            if (entry == null) return false;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = 180f - Mathf.Clamp(side, -1, 1) * lateralSpawnAngle;
                if (attempt > 0) angle += UnityEngine.Random.Range(-3f, 3f);
                if (spawner.TrySpawnAtAngle(entry, angle, zombieSpawnRadius + attempt * 0.6f)) return true;
            }
            if (logEvents) Debug.Log("Desktop debug: spawn rejected by terrain/clearance or active-monster limit.", this);
            return false;
        }

        public void SpawnGroup()
        {
            if(active && spawner!=null && spawner.UsesEncounterGroups)
            {
                spawner.TrySpawnEncounter(UnityEngine.Random.value<.5f ? MonsterSpawnDirection.LeftForest : MonsterSpawnDirection.RightForest);
                return;
            }
            for (int index = 0; index < groupSpawnCount; index++) SpawnZombie(index % 3 - 1);
        }

        public void SwingSword()
        {
            if (!active || swing == null || swing.IsSwinging) return;
            hitsThisSwing = 0;
            swing.StartSwing();
        }

        private void Activate()
        {
            if (active || !Application.isPlaying) return;
            motor = FindAnyObjectByType<CarriageMotor>();
            spawner = FindAnyObjectByType<MonsterSpawner>();
            attachmentPoints = FindAnyObjectByType<CartAttachmentPoints>();
            GrabbableWeapon weapon = FindAnyObjectByType<GrabbableWeapon>();
            Transform rig = GameObject.Find("OVRCameraRig")?.transform;
            if (motor == null || spawner == null || weapon == null || rig == null)
            { Debug.LogError("Desktop debug requires the existing rig, motor, spawner and weapon.", this); desktopDebugMode = false; return; }
            sword = weapon.GetComponentInChildren<SwordDamage>();
            audioFeedback = weapon.GetComponent<SwordSwingAudio>();
            if (sword == null) { desktopDebugMode = false; return; }
            savedRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            // Separate camera and simulated hunter; XR transforms are never changed.
            viewRoot = new GameObject("DesktopDebug_ViewOrigin");
            viewRoot.transform.SetParent(motor.transform, false);
            viewRoot.transform.SetPositionAndRotation(rig.position, rig.rotation);
            GameObject hunter = new GameObject("DesktopDebug_Hunter");
            hunter.transform.SetParent(viewRoot.transform, false);
            hunter.AddComponent<SimulatedHunterMovement>().Configure(cameraMoveSpeed, cameraHalfExtents);
            cameraObject = new GameObject("DesktopDebug_Camera");
            Camera view = cameraObject.AddComponent<Camera>();
            Camera original = rig.GetComponentsInChildren<Camera>(true).FirstOrDefault(item => item.CompareTag("MainCamera"))
                ?? rig.GetComponentInChildren<Camera>();
            if (original != null) view.CopyFrom(original);
            if (original != null) foreach (Component data in original.GetComponents<Component>())
                if (data != null && data.GetType().Name == "UniversalAdditionalCameraData")
                {
                    Component desktopData = cameraObject.AddComponent(data.GetType());
                    EditorUtility.CopySerialized(data, desktopData);
                    data.GetType().GetProperty("allowXRRendering")?.SetValue(desktopData, false);
                }
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                view.stereoTargetEye = StereoTargetEyeMask.None;
            view.targetTexture = null;
            view.enabled = true;
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SmoothFollowCamera>().ConfigureDesktopHunterView(hunter.transform,
                Vector3.up * eyeHeight, mouseSensitivity, false);
            foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true)) Suspend(camera);
            foreach (AudioListener listener in rig.GetComponentsInChildren<AudioListener>(true)) Suspend(listener);
            weaponParent = weapon.transform.parent;
            weaponRoot = weapon.transform;
            weaponPosition = weapon.transform.localPosition;
            weaponRotation = weapon.transform.localRotation;
            weaponScale = weapon.transform.localScale;
            Suspend(weapon);
            foreach (Behaviour behaviour in weapon.GetComponentsInChildren<Behaviour>(true))
                if (behaviour.GetType().Name == "HandGrabInteractable") Suspend(behaviour);
            var settings = new SerializedObject(sword);
            savedVelocityReference = settings.FindProperty("velocityReference").objectReferenceValue as Transform;
            savedVelocityWindows = settings.FindProperty("allowVelocityActivatedWindows").boolValue;
            savedSwingSpeed = settings.FindProperty("minimumSwingSpeed").floatValue;
            savedGrace = settings.FindProperty("velocityWindowGrace").floatValue;
            swordDamageAmount = settings.FindProperty("damage").floatValue;
            sword.EndAttackWindow();
            sword.ConfigureSwingWindows(false, savedSwingSpeed, savedGrace);
            sword.ConfigureVelocityReference(cameraObject.transform);
            weapon.transform.SetParent(cameraObject.transform, false);
            weapon.transform.localPosition = swordViewOffset;
            weapon.transform.localScale = weaponScale;
            swing = weapon.gameObject.AddComponent<PrototypeSwordController>();
            swing.Configure(sword);
            swing.ConfigureDesktopSwing(false, swingStartEuler, swingEndEuler);
            spawner.MonsterSpawned += ObserveMonster;
            foreach (MonsterBase monster in spawner.ActiveMonsters) ObserveMonster(monster);
            sword.MonsterHit += OnHit;
            if (audioFeedback != null) audioFeedback.SoundPlayed += OnSound;
            active = true;
        }

        private void Suspend(Behaviour behaviour)
        { if (behaviour != null && !savedBehaviours.ContainsKey(behaviour)) { savedBehaviours.Add(behaviour, behaviour.enabled); behaviour.enabled = false; } }
        private void ObserveMonster(MonsterBase monster)
        { if (monster != null && observedMonsters.Add(monster)) monster.StateChanged += OnStateChanged; }
        private void OnStateChanged(MonsterBase monster, MonsterState previous, MonsterState next)
        {
            if (!logEvents) return;
            if (next == MonsterState.Attached)
                Debug.Log($"{monster.name}_{monster.GetEntityId()} attached to {monster.Attachment.ReservedPoint.name}", monster);
            else if (next == MonsterState.Dead) Debug.Log($"{monster.name}_{monster.GetEntityId()} died", monster);
        }
        private void OnSound(AudioClip clip)
        { if (logEvents) Debug.Log($"Sword sound selected: {clip.name}.wav", this); }
        private void OnHit(MonsterDamageable target) { hitsThisSwing++; }
        private void OnDisable() => Deactivate();
        private void OnApplicationFocus(bool focused)
        { if (!focused && active) MouseCamera.ReleaseCursor(); }
        private void Deactivate()
        {
            if (!active) return;
            active = false;
            Application.runInBackground = savedRunInBackground;
            if (spawner != null) spawner.MonsterSpawned -= ObserveMonster;
            foreach (MonsterBase monster in observedMonsters) if (monster != null) monster.StateChanged -= OnStateChanged;
            observedMonsters.Clear();
            if (audioFeedback != null) audioFeedback.SoundPlayed -= OnSound;
            if (sword != null)
            {
                sword.MonsterHit -= OnHit;
                sword.EndAttackWindow();
                if (swing != null) { swing.enabled = false; Destroy(swing); }
                if (weaponRoot != null)
                {
                    weaponRoot.SetParent(weaponParent, false);
                    weaponRoot.localPosition = weaponPosition;
                    weaponRoot.localRotation = weaponRotation;
                    weaponRoot.localScale = weaponScale;
                }
                sword.ConfigureSwingWindows(savedVelocityWindows, savedSwingSpeed, savedGrace);
                sword.ConfigureVelocityReference(savedVelocityReference);
            }
            foreach (var pair in savedBehaviours) if (pair.Key != null) pair.Key.enabled = pair.Value;
            savedBehaviours.Clear();
            if (cameraObject != null) Destroy(cameraObject);
            if (viewRoot != null) Destroy(viewRoot);
        }

        private void OnGUI()
        {
            if (!active || !showHud) return;
            int zombies = spawner.ActiveMonsters.Count(item => item != null && item.MovementType == MonsterMovementType.Ground);
            int attached = spawner.ActiveMonsters.Count(item => item != null && item.MovementType == MonsterMovementType.Ground && item.State == MonsterState.Attached);
            GUI.Box(new Rect(12, 12, 420, 184), "Desktop Debug (solo Editor)");
            GUI.Label(new Rect(24, 36, 395, 155), $"Speed: {motor.Speed:F2} m/s   Effective: {motor.EffectiveSpeed:F2} m/s\n" +
                $"Active Zombies: {zombies}   Attached Zombies: {attached}\n" +
                $"Last Sword Sound: {(audioFeedback?.LastPlayedClip != null ? audioFeedback.LastPlayedClip.name + ".wav" : "—")}\n" +
                $"Sword Damage: {swordDamageAmount:F0}   Hits: {hitsThisSwing}   Sounds: {audioFeedback?.PlayedSoundCount ?? 0}\n" +
                "Mouse: mirar | WASD: posición local | Space/Shift: latigazo\n" +
                "Z/X/C: zombie delante/izq./der. | V: grupo\n" +
                "Click: atacar | F1: HUD | Esc: liberar mouse | R: reiniciar");
        }

        private void OnDrawGizmos()
        {
            if (!desktopDebugMode || !showGizmos) return;
            CartAttachmentPoints registry = attachmentPoints != null ? attachmentPoints : FindAnyObjectByType<CartAttachmentPoints>();
            if (registry != null) foreach (MonsterAttachmentPoint point in registry.Points)
            {
                if (point == null || !point.Accepts(MonsterAttachmentKind.Ground)) continue;
                Gizmos.color = point.HasCapacity ? Color.green : Color.red;
                Gizmos.DrawWireSphere(point.transform.position, 0.12f);
                Vector3 end = point.transform.position + point.WorldFacingDirection * 0.5f;
                Gizmos.DrawLine(point.transform.position, end);
                Handles.Label(point.transform.position + Vector3.up * 0.2f, point.name);
            }
            SwordDamage weapon = sword != null ? sword : FindAnyObjectByType<GrabbableWeapon>()?.GetComponentInChildren<SwordDamage>();
            if (weapon == null) return;
            CapsuleCollider collider = weapon.GetComponentInParent<GrabbableWeapon>()?.GetComponent<CapsuleCollider>();
            if (collider == null) return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            Gizmos.color = weapon.IsAttackWindowOpen ? Color.red : Color.cyan;
            Vector3 axis = collider.direction == 0 ? Vector3.right : collider.direction == 1 ? Vector3.up : Vector3.forward;
            float half = Mathf.Max(0f, collider.height * 0.5f - collider.radius);
            Gizmos.DrawWireSphere(collider.center + axis * half, collider.radius);
            Gizmos.DrawWireSphere(collider.center - axis * half, collider.radius);
            Vector3 side = Vector3.Cross(axis, collider.direction == 1 ? Vector3.forward : Vector3.up) * collider.radius;
            Gizmos.DrawLine(collider.center + axis * half + side, collider.center - axis * half + side);
            Gizmos.DrawLine(collider.center + axis * half - side, collider.center - axis * half - side);
            Gizmos.matrix = previous;
            var settings = new SerializedObject(weapon);
            Transform tip = settings.FindProperty("sweepTip").objectReferenceValue as Transform;
            if (tip != null) Gizmos.DrawWireSphere(tip.position, settings.FindProperty("sweepRadius").floatValue);
        }
#endif
    }
}
