using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using Reins;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.Gameplay
{
    public enum RideSessionPhase { WaitingToStart, Playing, GameOver }
    /// <summary>
    /// Finite ride and in-world outcome. No HUD, timer, head-locked panel or forced camera motion.
    /// Added by LevelVictoryController at runtime so existing scenes keep their Inspector changes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20000)]
    public sealed class GameSessionController : MonoBehaviour
    {
        [Header("Partida")]
        [SerializeField, Min(1)] private int playerHitPoints = 5;
        [SerializeField, Min(0.5f)] private float restartHoldSeconds = 1.5f;
        [SerializeField] private bool waitForBothReins = true;
        [Header("Menu inicial en el mundo")]
        [SerializeField] private Transform presentationEye;
        [SerializeField, Min(1f)] private float introDistance = 2.4f;
        [SerializeField, Min(.5f)] private float fallbackEyeHeight = 1.6f;
        [SerializeField, Min(.01f)] private float introTitleSize = .035f;
        [SerializeField, Min(.01f)] private float introInstructionSize = .032f;

        [Header("Texto integrado en el mundo")]
        [SerializeField] private Vector3 resultLocalPosition = new Vector3(0f, 2.4f, -1.35f);
        [SerializeField, Min(1f)] private float resultDistance = 2.4f;
        [SerializeField, Min(0.01f)] private float resultTitleSize = 0.065f;
        [SerializeField, Min(0.01f)] private float restartInstructionSize = 0.025f;

        private LevelVictoryController victory;
        private DeathScreenEffect deathEffect;
        private PrototypeHunterMonsterTarget hunterTarget;
        private MonsterSpawner spawner;
        private GiantZombieSpawner giantSpawner;
        private CarriageMotor motor;
        private GameRunModel run;
        private GameObject resultRoot;
        private TextMesh resultText;
        private TextMesh restartText;
        private bool ended;
        private bool restartArmed;
        private bool restarting;
        private float restartHeldFor;
        private GameObject introRoot;
        private readonly Dictionary<Behaviour,bool> waitingBehaviours = new Dictionary<Behaviour,bool>();
        public RideSessionPhase Phase { get; private set; } = RideSessionPhase.WaitingToStart;
        public bool HasStarted => Phase != RideSessionPhase.WaitingToStart;
        public event System.Action OnGameStarted;
        public int StartCount { get; private set; }

        private void Awake()
        {
            motor = FindAnyObjectByType<CarriageMotor>();
            spawner = FindAnyObjectByType<MonsterSpawner>();
            giantSpawner = FindAnyObjectByType<GiantZombieSpawner>();
            if (!waitForBothReins) { Phase=RideSessionPhase.Playing; return; }
            motor?.SetTravelPaused(true);
            PauseUntilStart(spawner);
            PauseUntilStart(giantSpawner);
            PauseUntilStart(FindAnyObjectByType<FaceBatThreatController>());
            PauseUntilStart(FindAnyObjectByType<GameSoundscape>());
        }

        private void PauseUntilStart(Behaviour component)
        {
            if (component == null) return;
            waitingBehaviours[component]=component.enabled;
            component.enabled=false;
        }

        public void TryStartFromGrip(bool firstHeld, bool secondHeld)
        {
            if (firstHeld && secondHeld) StartGame();
        }

        public void StartGame()
        {
            if (Phase != RideSessionPhase.WaitingToStart || ended || run == null) return;
            Phase=RideSessionPhase.Playing;
            StartCount++;
            if (introRoot != null) introRoot.SetActive(false);
            motor?.SetTravelPaused(false);
            // Gameplay now begins with two grips, using the existing spawner's timing/placement.
            spawner?.ConfigureFirstGallopWait(false);
            foreach (var entry in waitingBehaviours) if (entry.Key != null) entry.Key.enabled=entry.Value;
            waitingBehaviours.Clear();
            OnGameStarted?.Invoke();
        }

        public float ElapsedSeconds => run != null ? run.ElapsedSeconds : 0f;
        public int RemainingHits => run != null ? run.RemainingHits : playerHitPoints;
        public GameRunResult Result => run != null ? run.Result : GameRunResult.Playing;

        public void Configure(int hitPoints, float holdSeconds)
        {
            playerHitPoints = Mathf.Max(1, hitPoints);
            restartHoldSeconds = Mathf.Max(0.5f, holdSeconds);
        }

        private void Start()
        {
            victory = GetComponent<LevelVictoryController>();
            deathEffect = FindAnyObjectByType<DeathScreenEffect>();
            hunterTarget = FindAnyObjectByType<PrototypeHunterMonsterTarget>();
            spawner = FindAnyObjectByType<MonsterSpawner>();
            giantSpawner = FindAnyObjectByType<GiantZombieSpawner>();
            motor = FindAnyObjectByType<CarriageMotor>();
            run = new GameRunModel(playerHitPoints);

            if (victory != null) victory.LevelCompleted += HandleVictory;
            if (deathEffect != null) deathEffect.DefeatTriggered += HandleGiantDefeat;
            if (hunterTarget != null) hunterTarget.SimulatedHit += HandlePlayerHit;
            CreateWorldResult();
            if (Phase == RideSessionPhase.WaitingToStart) CreateWorldIntro();
        }

        private void OnDestroy()
        {
            if (victory != null) victory.LevelCompleted -= HandleVictory;
            if (deathEffect != null) deathEffect.DefeatTriggered -= HandleGiantDefeat;
            if (hunterTarget != null) hunterTarget.SimulatedHit -= HandlePlayerHit;
        }

        private void Update()
        {
            if (run == null || restarting) return;
            if (Phase == RideSessionPhase.WaitingToStart)
            {
                motor?.LeftRein?.UpdateGrip(Time.unscaledDeltaTime);
                motor?.RightRein?.UpdateGrip(Time.unscaledDeltaTime);
                TryStartFromGrip(motor != null && motor.LeftRein != null && motor.LeftRein.IsHeld,
                    motor != null && motor.RightRein != null && motor.RightRein.IsHeld);
                return;
            }
            if (ended)
            {
                UpdateRestart();
                return;
            }

            run.Tick(Time.unscaledDeltaTime);
            if (deathEffect != null && motor != null)
            {
                // The persistent danger redness follows how much the hanging monsters are slowing the carriage.
                deathEffect.SetDanger(1f - Mathf.Clamp01(motor.SpeedMultiplier));
            }
        }

        private void HandleVictory()
        {
            if (run == null || !run.Win()) return;
            ended = true;
            Phase = RideSessionPhase.GameOver;
            if (deathEffect != null) deathEffect.ResetDefeat();
            ShowWorldResult("VICTORIA", new Color(1f, 0.76f, 0.35f));
        }

        private void HandleGiantDefeat()
        {
            if (Phase == RideSessionPhase.Playing && run != null && run.Lose()) EndDefeat("El gigante alcanzó la carreta.");
        }

        private void HandlePlayerHit(MonsterBase attacker)
        {
            if (Phase != RideSessionPhase.Playing || run == null || run.Result != GameRunResult.Playing) return;
            // Small monsters never defeat the player: each hit cuts speed and flashes the danger overlay.
            if (motor != null) motor.ApplyHitPenalty();
            if (deathEffect != null) deathEffect.FlashDamage();
        }

        private void EndDefeat(string reason)
        {
            if (ended) return;
            ended = true;
            Phase = RideSessionPhase.GameOver;
            if (motor != null) motor.enabled = false;
            if (spawner != null)
            {
                spawner.enabled = false;
                foreach (MonsterBase monster in new List<MonsterBase>(spawner.ActiveMonsters))
                    if (monster != null) monster.Retire();
            }

            if (giantSpawner != null)
            {
                giantSpawner.enabled = false;
                if (giantSpawner.SpawnedGiant != null)
                    Destroy(giantSpawner.SpawnedGiant.gameObject);
            }

            if (deathEffect != null) deathEffect.TriggerDefeat();
            ShowWorldResult("GAME OVER", new Color(0.95f, 0.18f, 0.12f));
            Debug.Log("Japanese Demon Hunter: derrota. " + reason, this);
        }

        private void CreateWorldResult()
        {
            if (motor == null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                Debug.LogError("No se encontró una fuente para el resultado en el mundo.", this);
                return;
            }

            resultRoot = new GameObject("ResultadoEnCarreta");
            resultRoot.transform.SetParent(motor.transform, false);
            resultRoot.transform.localPosition = resultLocalPosition;
            resultText = CreateLetters("Resultado", font, Vector3.zero, resultTitleSize);
            restartText = CreateLetters("Reiniciar", font, new Vector3(0f, -0.34f, 0f),
                restartInstructionSize);
            restartText.text = "Suelta y toma ambas riendas para reiniciar";
            resultRoot.SetActive(false);
        }

        private TextMesh CreateLetters(string name, Font font, Vector3 localPosition, float size, Transform parent = null)
        {
            GameObject words = new GameObject(name, typeof(TextMesh), typeof(MeshRenderer));
            words.transform.SetParent(parent != null ? parent : resultRoot.transform, false);
            words.transform.localPosition = localPosition;
            words.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh letters = words.GetComponent<TextMesh>();
            letters.font = font;
            letters.fontSize = 64;
            letters.characterSize = size;
            letters.anchor = TextAnchor.MiddleCenter;
            letters.alignment = TextAlignment.Center;
            MeshRenderer renderer = words.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 100;
            return letters;
        }

        private void CreateWorldIntro()
        {
            if (motor == null) return;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            introRoot=new GameObject("RideIntroMenu");
            introRoot.transform.SetParent(motor.transform,false);
            PositionPresentation(introRoot.transform,introDistance);
            var title=CreateLetters("Titulo",font,new Vector3(0,.3f,0),introTitleSize,introRoot.transform);
            title.text="DEMON HUNTER: LAST RIDE"; title.color=new Color(1,.8f,.4f);
            var instruction=CreateLetters("Inicio",font,Vector3.zero,introInstructionSize,introRoot.transform);
            instruction.text="Toma ambas riendas para comenzar";
            var help=CreateLetters("Ayuda",font,new Vector3(0,-.35f,0),introInstructionSize*.7f,introRoot.transform);
            help.text="Dirige con las riendas. Acelera con el latigazo.\nDefiendete de los zombies.";
        }

        private void PositionPresentation(Transform panel, float distance)
        {
            if (presentationEye == null)
            {
                if (Camera.main != null) presentationEye=Camera.main.transform;
            }
#if UNITY_EDITOR
            var desktop=FindAnyObjectByType<DesktopDebugMode>();
            Transform eye=desktop != null && desktop.IsActive ? desktop.DebugCamera.transform : presentationEye;
#else
            Transform eye=presentationEye;
#endif
            if (eye == null) return;
            Vector3 position=eye.position;
            var rigRoot=GameObject.Find("OVRCameraRig");
            if (rigRoot != null && position.y-rigRoot.transform.position.y<.5f)
                position.y=rigRoot.transform.position.y+fallbackEyeHeight;
            Vector3 forward=Vector3.ProjectOnPlane(eye.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude<.001f) forward=-motor.transform.forward;
            panel.SetPositionAndRotation(position+forward*Mathf.Max(1,distance),Quaternion.LookRotation(-forward,Vector3.up));
        }

        private void ShowWorldResult(string title, Color color)
        {
            if (resultText == null) return;
            resultText.text = title;
            resultText.color = color;
            restartText.color = color;
            PositionPresentation(resultRoot.transform, resultDistance);
            resultRoot.SetActive(true);
        }

        private void UpdateRestart()
        {
            if (motor == null || motor.LeftRein == null || motor.RightRein == null) return;
            ReinHandle left = motor.LeftRein;
            ReinHandle right = motor.RightRein;
            left.UpdateGrip(Time.unscaledDeltaTime);
            right.UpdateGrip(Time.unscaledDeltaTime);
            if (!left.IsHeld && !right.IsHeld) restartArmed = true;
            if (!restartArmed || !left.IsHeldByExpectedHand || !right.IsHeldByExpectedHand)
            {
                restartHeldFor = 0f;
                return;
            }

            restartHeldFor += Time.unscaledDeltaTime;
            if (restartHeldFor < restartHoldSeconds) return;
            restarting = true;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadSceneAsync(scene.buildIndex);
            else SceneManager.LoadSceneAsync(scene.path);
        }
    }
}
