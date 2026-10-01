using NUnit.Framework;
using Oculus.Interaction.Input;
using Reins;
using UnityEditor;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay.Tests
{
    public sealed class RequestedRideFeaturesTests
    {
        [TestCase(-1)]
        [TestCase(1)]
        public void ReinSteeringUsesTheExistingSmoothLaneTransitionWithoutHeldPullSpam(int direction)
        {
            var model = new BilateralReinGestureModel(new ReinGestureStateMachine(brakeEnabled: false));
            Assert.AreEqual(ReinGestureKind.None, model.Step(true,true,Vector3.zero,Vector3.zero,.016f).Kind);
            var pull = Vector3.right * direction * .3f;
            Assert.AreEqual(direction, model.Step(true,true,pull,pull,.016f).Direction);
            for (int i=0;i<100;i++) Assert.AreEqual(ReinGestureKind.None, model.Step(true,true,pull,pull,.016f).Kind);
            var transition = new LaneTransitionModel();
            transition.Begin(0,direction,2.8f,.8f);
            Assert.AreEqual(0,transition.CurrentX);
            Assert.AreEqual(direction*1.4f,transition.Step(.4f),.001f);
            Assert.AreEqual(direction*2.8f,transition.Step(.4f),.001f);
        }

        [TestCase(Handedness.Left, Handedness.Left)]
        [TestCase(Handedness.Left, Handedness.Right)]
        [TestCase(Handedness.Right, Handedness.Left)]
        [TestCase(Handedness.Right, Handedness.Right)]
        public void EitherTrackedHandCanDriveEitherHandleAndTheExistingLash(Handedness handleSide, Handedness handSide)
        {
            var owner = new GameObject("Handle under test");
            try
            {
                var handle = owner.AddComponent<ReinHandle>();
                var settings = new SerializedObject(handle);
                settings.FindProperty("expectedHand").intValue = (int)handleSide;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(handle.CanDriveHand(handSide, true, true));
                Assert.IsFalse(handle.CanDriveHand(handSide, true, false));
                Assert.IsFalse(handle.CanDriveHand(handSide, false, true));
                var gestures = new BilateralReinGestureModel(handle.CreateGestureStateMachine());
                gestures.Step(true, true, Vector3.zero, Vector3.zero, .016f);
                gestures.Step(true, true, Vector3.up * .2f, Vector3.up * .2f, .1f);
                Assert.AreEqual(ReinGestureKind.Accelerate,
                    gestures.Step(true, true, Vector3.zero, Vector3.zero, .1f).Kind);
                Assert.AreEqual(ReinGestureKind.None,
                    gestures.Step(true, false, Vector3.up, Vector3.up, .1f).Kind);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
