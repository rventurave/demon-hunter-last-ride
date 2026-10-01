using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Gives both tracked hands a physics sweep so bare fists hurt monsters when the player has no
    /// weapon. Hands are discovered by component, never by GameObject name. The sweep is measured
    /// relative to the carriage so simply riding along never counts as a hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandStrikeController : MonoBehaviour
    {
        private const int MaximumDiscoveryAttempts = 10;

        [SerializeField] private Transform cartRoot;
        [SerializeField, Min(0.01f)] private float punchDamage = 8f;
        [SerializeField, Min(0.01f)] private float sweepRadius = 0.14f;
        [SerializeField, Min(0.1f)] private float minimumSwingSpeed = 2f;
        [SerializeField, Min(0.02f)] private float velocityWindowGrace = 0.12f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool logWhenNoHandsAreFound = true;
        [SerializeField, Min(0f)] private float handHitCooldown = 0.35f;
        [SerializeField] private AudioClip handHitClip;
        [SerializeField] private AudioSource handHitAudioSource;
        [SerializeField, Range(0f,1f)] private float handHitVolume = 0.5f;
        public int PlayedHitSoundCount { get; private set; }

        private readonly List<HandStrike> strikes = new List<HandStrike>();
        private readonly List<GameObject> createdTips = new List<GameObject>();
        private float nextDiscoveryTime;
        private int discoveryAttempts;

        public int StrikeCount => strikes.Count;
        public bool HasBothHands => strikes.Count >= 2;
        public Transform CartRoot => cartRoot;

        private sealed class HandStrike
        {
            public IHand hand;
            public Transform tip;
            public SwordDamage sweep;
            public bool playedSound;
        }

        private void Start()
        {
            if (handHitAudioSource == null && handHitClip != null)
            {
                handHitAudioSource = gameObject.AddComponent<AudioSource>();
                handHitAudioSource.playOnAwake = false;
                handHitAudioSource.spatialBlend = 1f;
                handHitAudioSource.minDistance = .5f;
                handHitAudioSource.maxDistance = 10f;
                handHitAudioSource.dopplerLevel = 0f;
            }
            DiscoverHands();
        }

        private void LateUpdate()
        {
            if (strikes.Count < 2 && discoveryAttempts < MaximumDiscoveryAttempts &&
                Time.time >= nextDiscoveryTime)
            {
                DiscoverHands();
            }

            for (var i = 0; i < strikes.Count; i++)
            {
                var strike = strikes[i];
                if (strike.hand == null || strike.tip == null)
                {
                    continue;
                }

                Pose pose = default;
                bool valid = strike.hand.IsConnected && strike.hand.IsTrackedDataValid &&
                    strike.hand.GetJointPose(HandJointId.HandMiddleTip, out pose);
                if (valid)
                {
                    strike.tip.position = pose.position;
                    strike.sweep.enabled = true;
                }
                else strike.sweep.enabled = false;
            }
        }

        /// <summary>Creates one punch sweep per tracked hand. Safe to call again; old tips are recycled.</summary>
        public void DiscoverHands()
        {
            discoveryAttempts++;
            nextDiscoveryTime = Time.time + 1f;

            for (var i = 0; i < createdTips.Count; i++)
            {
                if (createdTips[i] != null)
                {
                    Destroy(createdTips[i]);
                }
            }

            createdTips.Clear();
            strikes.Clear();

            var interactors = FindObjectsByType<HandGrabInteractor>(FindObjectsInactive.Exclude);
            foreach (var interactor in interactors)
            {
                if (interactor == null)
                {
                    continue;
                }

                IHand hand = interactor.Hand;
                if (hand == null || HasHand(hand.Handedness))
                {
                    continue;
                }

                var tip = new GameObject("PunchTip_" + hand.Handedness).transform;
                tip.SetParent(interactor.transform, false);
                var strike = new HandStrike { hand = hand, tip = tip };
                var sweep = tip.gameObject.AddComponent<SwordDamage>();
                ConfigureStrikeSweep(sweep,tip);
                strike.sweep = sweep;
                sweep.enabled = false; // No damage before the first valid tracked pose.
                sweep.AttackWindowOpened += () => strike.playedSound = false;
                sweep.MonsterHit += monster =>
                {
                    if (strike.playedSound || monster.GetComponent<MonsterBase>()?.MovementType != MonsterMovementType.Ground) return;
                    strike.playedSound = true;
                    var feedback = monster.GetComponent<MonsterSwordHitFeedback>();
                    if (feedback == null) feedback = monster.gameObject.AddComponent<MonsterSwordHitFeedback>();
                    feedback.ShowHit();
                    if (handHitClip != null && handHitAudioSource != null)
                    {
                        handHitAudioSource.PlayOneShot(handHitClip,handHitVolume);
                        PlayedHitSoundCount++;
                    }
                };
                createdTips.Add(tip.gameObject);
                strikes.Add(strike);
            }

            if (strikes.Count == 0 && discoveryAttempts >= MaximumDiscoveryAttempts && logWhenNoHandsAreFound)
            {
                Debug.LogWarning(
                    "HandStrikeController found no active HandGrabInteractor: bare-fist damage is disabled. " +
                    "The XR rig must expose hand grab interactors for punches to work.",
                    this);
            }
        }

        public void Configure(
            Transform configuredCartRoot,
            float damage,
            float radius,
            float swingSpeed,
            LayerMask configuredTargetMask)
        {
            cartRoot = configuredCartRoot;
            punchDamage = Mathf.Max(0.01f, damage);
            sweepRadius = Mathf.Max(0.01f, radius);
            minimumSwingSpeed = Mathf.Max(0.1f, swingSpeed);
            targetMask = configuredTargetMask;
        }

        /// <summary>Uses the same damage windows as the sword, tuned for one tracked hand.</summary>
        public void ConfigureStrikeSweep(SwordDamage sweep, Transform tip)
        {
            sweep.Configure(tip,punchDamage,sweepRadius,targetMask);
            sweep.ConfigureSwingWindows(true,minimumSwingSpeed,velocityWindowGrace);
            sweep.ConfigureWindowCooldown(handHitCooldown);
            sweep.ConfigureVelocityReference(cartRoot);
        }

        private void OnDisable()
        {
            foreach (var strike in strikes) if (strike.sweep != null) strike.sweep.enabled=false;
        }

        private void OnDestroy()
        {
            foreach (var tip in createdTips) if (tip != null) Destroy(tip);
        }

        private bool HasHand(Handedness handedness)
        {
            for (var i = 0; i < strikes.Count; i++)
            {
                if (strikes[i].hand != null && strikes[i].hand.Handedness == handedness)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
