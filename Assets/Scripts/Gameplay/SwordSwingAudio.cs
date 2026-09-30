using System;
using JapaneseDemonHunter.Monsters;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>One one-shot per existing attack window, independent of the number of contacts.</summary>
    [DisallowMultipleComponent]
    public sealed class SwordSwingAudio : MonoBehaviour
    {
        [SerializeField] private SwordDamage swordDamage;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] swordSounds = new AudioClip[5];
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private bool avoidImmediateRepeat = true;
        private AudioClip lastClip;

        public int PlayedSoundCount { get; private set; }
        public AudioClip LastPlayedClip => lastClip;
        public event Action<AudioClip> SoundPlayed;

        private void Awake()
        {
            if (swordDamage == null) swordDamage = GetComponentInChildren<SwordDamage>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (swordDamage != null)
            {
                swordDamage.AttackWindowOpened += PlaySwing;
                swordDamage.MonsterHit += ShowHit;
            }
        }

        private void OnDisable()
        {
            if (swordDamage != null)
            {
                swordDamage.AttackWindowOpened -= PlaySwing;
                swordDamage.MonsterHit -= ShowHit;
            }
        }

        private void PlaySwing()
        {
            if (audioSource == null || swordSounds == null) return;
            int candidates = 0;
            foreach (AudioClip clip in swordSounds)
                if (clip != null && (!avoidImmediateRepeat || clip != lastClip)) candidates++;
            bool excludeLast = candidates > 0 && avoidImmediateRepeat;
            if (candidates == 0)
                foreach (AudioClip clip in swordSounds) if (clip != null) candidates++;
            if (candidates == 0) return;
            int choice = UnityEngine.Random.Range(0, candidates);
            foreach (AudioClip clip in swordSounds)
            {
                if (clip == null || (excludeLast && clip == lastClip)) continue;
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

        private void ShowHit(MonsterDamageable monster)
        {
            if (monster == null) return;
            MonsterSwordHitFeedback feedback = monster.GetComponent<MonsterSwordHitFeedback>();
            if (feedback == null) feedback = monster.gameObject.AddComponent<MonsterSwordHitFeedback>();
            feedback.ShowHit();
        }
    }
}
