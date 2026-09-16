using System.Reflection;
using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using GoldenNeedle.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests.Editor
{
    public sealed class GameplayPlayerFoundationTests
    {
        [Test]
        public void PlayerHealth_ClampsDamageAndNotifiesDeathOnAliveToDeadTransition()
        {
            var gameObject = new GameObject("PlayerHealthTest");
            try
            {
                var health = gameObject.AddComponent<PlayerHealth>();
                var healthChangedCount = 0;
                var deathCount = 0;
                health.HealthChanged += (_, _) => healthChangedCount++;
                health.Died += () => deathCount++;

                health.ApplyDamage(25f);
                Assert.AreEqual(75f, health.CurrentHealth, 0.0001f);
                Assert.AreEqual(0.75f, health.NormalizedHealth, 0.0001f);
                Assert.IsTrue(health.IsAlive);

                health.ApplyDamage(500f);
                Assert.AreEqual(0f, health.CurrentHealth, 0.0001f);
                Assert.IsFalse(health.IsAlive);
                Assert.AreEqual(1, deathCount);

                health.ApplyDamage(1f);
                Assert.AreEqual(1, deathCount);
                Assert.AreEqual(2, healthChangedCount);

                health.Heal(10f);
                Assert.AreEqual(10f, health.CurrentHealth, 0.0001f);
                Assert.IsTrue(health.IsAlive);

                health.RestoreHealth();
                Assert.AreEqual(health.MaximumHealth, health.CurrentHealth, 0.0001f);
                Assert.AreEqual(4, healthChangedCount);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void MotionEngineRuntime_GameplayPresentationSetterAcceptsOnlyRawOrStabilized()
        {
            var gameObject = new GameObject("MotionRuntimePresentationTest");
            try
            {
                var runtime = gameObject.AddComponent<MotionEngineRuntime>();
                Assert.IsTrue(runtime.TrySetAvatarDriveSource(AvatarDrivePoseSource.RawCanonical));
                Assert.AreEqual(AvatarDrivePoseSource.RawCanonical, runtime.AvatarDriveSource);

                Assert.IsFalse(runtime.TrySetAvatarDriveSource(AvatarDrivePoseSource.ResponsiveCanonicalA));
                Assert.AreEqual(AvatarDrivePoseSource.RawCanonical, runtime.AvatarDriveSource);

                Assert.IsTrue(runtime.TrySetAvatarDriveSource(AvatarDrivePoseSource.StabilizedCanonical));
                Assert.AreEqual(AvatarDrivePoseSource.StabilizedCanonical, runtime.AvatarDriveSource);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PlayerFacade_SplitsAvatarPoseDriveAndLocomotionControls()
        {
            var gameObject = new GameObject("PlayerFacadeControlSplitTest");
            try
            {
                gameObject.SetActive(false);
                var retargeter = gameObject.AddComponent<HumanoidRetargeter>();
                var locomotion = gameObject.AddComponent<EmbodiedLocomotionController>();
                var facade = gameObject.AddComponent<GoldenNeedlePlayerFacade>();
                retargeter.DriveRig = false;
                locomotion.DriveLocomotion = false;
                SetPrivateField(facade, "humanoidRetargeter", retargeter);
                SetPrivateField(facade, "locomotionController", locomotion);

                facade.SetAvatarPoseDriveEnabled(true);
                Assert.IsTrue(facade.IsAvatarPoseDriveEnabled);
                Assert.IsFalse(facade.IsLocomotionEnabled);

                facade.SetLocomotionEnabled(true);
                Assert.IsTrue(facade.IsAvatarPoseDriveEnabled);
                Assert.IsTrue(facade.IsLocomotionEnabled);

                facade.SetMotionControlEnabled(false);
                Assert.IsFalse(facade.IsAvatarPoseDriveEnabled);
                Assert.IsFalse(facade.IsLocomotionEnabled);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
