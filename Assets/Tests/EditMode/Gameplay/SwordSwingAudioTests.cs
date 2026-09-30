using System.Linq;
using System.Reflection;
using JapaneseDemonHunter.Monsters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class SwordSwingAudioTests
    {
        private GameObject weapon;
        private SwordDamage damage;
        private AudioSource source;
        private SwordSwingAudio feedback;
        private AudioClip[] clips;

        [SetUp]
        public void SetUp()
        {
            weapon = new GameObject("Sword audio verification");
            damage = weapon.AddComponent<SwordDamage>();
            source = weapon.AddComponent<AudioSource>();
            source.playOnAwake = false;
            feedback = weapon.AddComponent<SwordSwingAudio>();
            clips = Enumerable.Range(1, 5).Select(index =>
                AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Art/Audio/swordSound/{index}.wav")).ToArray();
            Assert.That(clips.All(clip => clip != null), Is.True, "All five original sound extracts must import.");
            feedback.Configure(damage, source, clips);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(weapon);

        [Test]
        public void AttackWindowProducesOneSoundDespiteRepeatedBeginsAndSweeps()
        {
            damage.BeginAttackWindow();
            for (int i = 0; i < 10; i++)
            {
                damage.BeginAttackWindow();
                damage.Sweep(Vector3.zero, Vector3.zero);
            }
            Assert.That(feedback.PlayedSoundCount, Is.EqualTo(1));
            damage.EndAttackWindow();
            damage.BeginAttackWindow();
            Assert.That(feedback.PlayedSoundCount, Is.EqualTo(2));
            Assert.That(weapon.GetComponents<AudioSource>().Length, Is.EqualTo(1));
        }

        [Test]
        public void ConsecutiveWindowsNeverRepeatThePreviousClip()
        {
            AudioClip previous = null;
            for (int i = 0; i < 50; i++)
            {
                damage.BeginAttackWindow();
                Assert.That(feedback.LastPlayedClip, Is.Not.Null);
                Assert.That(feedback.LastPlayedClip, Is.Not.SameAs(previous));
                Assert.That(clips, Does.Contain(feedback.LastPlayedClip));
                previous = feedback.LastPlayedClip;
                damage.EndAttackWindow();
            }
            Assert.That(feedback.PlayedSoundCount, Is.EqualTo(50));
        }

        [Test]
        public void NullAndUnassignedClipsAreSafeAndOneAvailableClipCanRepeat()
        {
            feedback.Configure(damage, source, new[] {null, clips[2], null});
            for (int i = 0; i < 10; i++)
            {
                damage.BeginAttackWindow();
                Assert.That(feedback.LastPlayedClip, Is.SameAs(clips[2]));
                damage.EndAttackWindow();
            }
            int played = feedback.PlayedSoundCount;
            feedback.Configure(damage, source, new AudioClip[5]);
            Assert.DoesNotThrow(() => damage.BeginAttackWindow());
            Assert.That(feedback.PlayedSoundCount, Is.EqualTo(played));
            damage.EndAttackWindow();
            feedback.Configure(damage, source, null);
            Assert.DoesNotThrow(() => damage.BeginAttackWindow());
            Assert.That(feedback.PlayedSoundCount, Is.EqualTo(played));
        }

        [Test]
        public void HoldingWeaponDuringCartTranslationAndRotationDoesNotOpenWindow()
        {
            var cart = new GameObject("Moving reference without XR rig");
            try
            {
                weapon.transform.SetParent(cart.transform);
                weapon.transform.localPosition = new Vector3(0.4f, 1f, -0.7f);
                damage.ConfigureVelocityReference(cart.transform);
                cart.transform.SetPositionAndRotation(new Vector3(12f, 2f, -30f), Quaternion.Euler(0f, 90f, 0f));
                typeof(SwordDamage).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(damage, null);
                Assert.That(damage.IsAttackWindowOpen, Is.False);
                Assert.That(feedback.PlayedSoundCount, Is.Zero);
            }
            finally { weapon.transform.SetParent(null); Object.DestroyImmediate(cart); }
        }
    }
}
