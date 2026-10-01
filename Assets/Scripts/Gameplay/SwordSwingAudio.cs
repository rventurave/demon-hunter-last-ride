using System;
using JapaneseDemonHunter.Monsters;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Grab audio and one damage-confirmed impact per existing attack window.</summary>
    [DisallowMultipleComponent]
    public sealed class SwordSwingAudio : MonoBehaviour
    {
        [SerializeField] private SwordDamage swordDamage;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private GrabbableWeapon weapon;
        [SerializeField] private AudioClip grabClip;
        [SerializeField, Range(0f, 1f)] private float grabVolume = 0.35f;
        [SerializeField] private AudioClip[] swordSounds = new AudioClip[5];
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private bool avoidImmediateRepeat = true;
        private AudioClip lastClip;
        private bool impactPlayed;
        public int GrabSoundCount { get; private set; }

        public int PlayedSoundCount { get; private set; }
        public AudioClip LastPlayedClip => lastClip;
        public event Action<AudioClip> SoundPlayed;

        private void Awake()
        {
            if (swordDamage == null) swordDamage = GetComponentInChildren<SwordDamage>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (weapon == null) weapon = GetComponent<GrabbableWeapon>();
        }

        private void OnEnable()
        {
            if (swordDamage != null)
            {
                swordDamage.AttackWindowOpened += ResetImpact;
                swordDamage.MonsterHit += ShowHit;
            }
            if (weapon != null) weapon.Grabbed += PlayGrab;
        }

        private void OnDisable()
        {
            if (swordDamage != null)
            {
                swordDamage.AttackWindowOpened -= ResetImpact;
                swordDamage.MonsterHit -= ShowHit;
            }
            if (weapon != null) weapon.Grabbed -= PlayGrab;
        }

        private void ResetImpact() => impactPlayed = false;
        private bool IsImpactClip(AudioClip clip) => clip != null && clip != grabClip && clip.name != "5";
        private void PlayGrab()
        {
            if (audioSource == null || grabClip == null) return;
            audioSource.PlayOneShot(grabClip, grabVolume);
            GrabSoundCount++;
            SoundPlayed?.Invoke(grabClip);
        }

        private void PlayImpact()
        {
            if (audioSource == null || swordSounds == null) return;
            int candidates = 0;
            foreach (AudioClip clip in swordSounds)
                if (IsImpactClip(clip) && (!avoidImmediateRepeat || clip != lastClip)) candidates++;
            bool excludeLast = candidates > 0 && avoidImmediateRepeat;
            if (candidates == 0)
                foreach (AudioClip clip in swordSounds) if (IsImpactClip(clip)) candidates++;
            if (candidates == 0) return;
            int choice = UnityEngine.Random.Range(0, candidates);
            foreach (AudioClip clip in swordSounds)
            {
                if (!IsImpactClip(clip) || (excludeLast && clip == lastClip)) continue;
                if (choice-- != 0) continue;
                lastClip = clip;
                audioSource.PlayOneShot(clip, volume);
                PlayedSoundCount++;
                SoundPlayed?.Invoke(clip);
                return;
            }
        }

        public void Configure(SwordDamage configuredDamage, AudioSource configuredSource, AudioClip[] clips)
        {
            if (isActiveAndEnabled) OnDisable();
            swordDamage = configuredDamage;
            audioSource = configuredSource;
            swordSounds = clips;
            if (isActiveAndEnabled) OnEnable();
        }

        public void ConfigureGrabAudio(GrabbableWeapon configuredWeapon, AudioClip clip)
        {
            if (isActiveAndEnabled) OnDisable();
            weapon = configuredWeapon;
            grabClip = clip;
            if (isActiveAndEnabled) OnEnable();
        }

        private void ShowHit(MonsterDamageable monster)
        {
            if (monster == null) return;
            if (!impactPlayed && monster.GetComponent<MonsterBase>()?.MovementType == MonsterMovementType.Ground)
            {
                impactPlayed = true;
                PlayImpact();
            }
            MonsterSwordHitFeedback feedback = monster.GetComponent<MonsterSwordHitFeedback>();
            if (feedback == null) feedback = monster.gameObject.AddComponent<MonsterSwordHitFeedback>();
            feedback.ShowHit();
        }
    }
}
