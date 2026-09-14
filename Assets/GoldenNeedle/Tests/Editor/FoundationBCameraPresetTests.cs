using System;
using System.Linq;
using GoldenNeedle.Debug.PoseTrackingSpike;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests
{
    public sealed class FoundationBCameraPresetTests
    {
        [Test]
        public void DefaultPresetSetContainsRequiredNames()
        {
            var names = CameraViewPreset.CreateDefaults()
                .Select(preset => preset.presetName)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Back", "Front", "Left", "Right",
                    "FullBody", "Hands", "LeftHand", "RightHand",
                },
                names);
        }

        [Test]
        public void DuplicateAndInvalidPresetNamesAreRejected()
        {
            var presets = new[]
            {
                new CameraViewPreset { presetName = "Hands" },
                new CameraViewPreset { presetName = " hands " },
                new CameraViewPreset { presetName = "" },
            };

            Assert.That(
                CameraViewPresetMath.TryFindUniquePreset(
                    presets,
                    "Hands",
                    out _,
                    out _,
                    out var duplicateReason),
                Is.False);
            Assert.That(duplicateReason, Does.Contain("ambiguous").IgnoreCase);

            Assert.That(
                CameraViewPresetMath.TryFindUniquePreset(
                    presets,
                    "",
                    out _,
                    out _,
                    out var blankReason),
                Is.False);
            Assert.That(blankReason, Does.Contain("blank").IgnoreCase);
        }

        [Test]
        public void PresetLookupIsTrimmedAndCaseInsensitive()
        {
            var presets = CameraViewPreset.CreateDefaults();

            Assert.That(
                CameraViewPresetMath.TryFindUniquePreset(
                    presets,
                    "  lEfT hAnD  ",
                    out var preset,
                    out _,
                    out _),
                Is.True);
            Assert.That(preset.presetName, Is.EqualTo("LeftHand"));
        }

        [Test]
        public void BackPresetMatchesLegacyThirdPersonGeometry()
        {
            var back = FindDefault("Back");
            var root = new Vector3(1f, 0f, 2f);

            Assert.That(
                CameraViewPresetMath.TryCalculateView(
                    new Vector2(0f, 1f),
                    root,
                    back,
                    out var cameraPosition,
                    out var lookTarget),
                Is.True);

            AssertVector(cameraPosition, new Vector3(1f, 2.2f, -2f));
            AssertVector(lookTarget, new Vector3(1f, 1.15f, 2f));
            Assert.That(back.distance, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(back.positionResponse, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(back.orientationResponse, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(back.fieldOfView, Is.EqualTo(55f).Within(0.0001f));
        }

        [Test]
        public void FrontLeftRightPlacementIsHeadingRelative()
        {
            var root = new Vector3(10f, 0f, 20f);
            var heading = new Vector2(0f, 1f);

            Calculate(root, heading, FindDefault("Front"), out var front);
            Calculate(root, heading, FindDefault("Left"), out var left);
            Calculate(root, heading, FindDefault("Right"), out var right);

            AssertVector(front, new Vector3(10f, 2.2f, 24f));
            AssertVector(left, new Vector3(6f, 2.2f, 20f));
            AssertVector(right, new Vector3(14f, 2.2f, 20f));
        }

        [Test]
        public void HandFocusUsesMidpointWhenBothHandsAreAvailable()
        {
            var candidates = new CameraFocusCandidates
            {
                hasLeftHand = true,
                leftHand = new Vector3(-1f, 2f, 3f),
                hasRightHand = true,
                rightHand = new Vector3(1f, 4f, 5f),
            };

            Assert.That(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.Hands,
                    candidates,
                    out var resolution),
                Is.True);
            AssertVector(resolution.Position, new Vector3(0f, 3f, 4f));
            Assert.That(resolution.ResolvedTarget, Is.EqualTo("BothHands"));
            Assert.That(resolution.UsedFallback, Is.False);
        }

        [Test]
        public void HandsFocusFallsBackToOneAvailableHand()
        {
            var candidates = new CameraFocusCandidates
            {
                hasRightHand = true,
                rightHand = new Vector3(1f, 2f, 3f),
            };

            Assert.That(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.Hands,
                    candidates,
                    out var resolution),
                Is.True);
            AssertVector(resolution.Position, candidates.rightHand);
            Assert.That(resolution.ResolvedTarget, Is.EqualTo("RightHandOnly"));
            Assert.That(resolution.UsedFallback, Is.True);
        }

        [Test]
        public void MissingHandsFallBackToBodyThenRoot()
        {
            var chestCandidates = new CameraFocusCandidates
            {
                hasChest = true,
                chest = new Vector3(0f, 1.4f, 0f),
                hasAvatarRoot = true,
                avatarRoot = new Vector3(0f, 0f, 0f),
            };
            var rootOnlyCandidates = new CameraFocusCandidates
            {
                hasAvatarRoot = true,
                avatarRoot = new Vector3(2f, 0f, 1f),
            };

            Assert.That(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.LeftHand,
                    chestCandidates,
                    out var chestResolution),
                Is.True);
            Assert.That(chestResolution.ResolvedTarget, Is.EqualTo("Chest"));
            Assert.That(chestResolution.UsedFallback, Is.True);

            Assert.That(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.RightHand,
                    rootOnlyCandidates,
                    out var rootResolution),
                Is.True);
            Assert.That(rootResolution.ResolvedTarget, Is.EqualTo("AvatarRoot"));
            AssertVector(rootResolution.Position, rootOnlyCandidates.avatarRoot);
        }

        [Test]
        public void LeftAndRightHandUseLowerArmBeforeBodyFallback()
        {
            var candidates = new CameraFocusCandidates
            {
                hasLeftLowerArm = true,
                leftLowerArm = new Vector3(-0.5f, 1.2f, 0f),
                hasRightLowerArm = true,
                rightLowerArm = new Vector3(0.5f, 1.2f, 0f),
                hasChest = true,
                chest = new Vector3(0f, 1f, 0f),
            };

            Assert.That(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.LeftHand,
                    candidates,
                    out var left),
                Is.True);
            Assert.That(left.ResolvedTarget, Is.EqualTo("LeftLowerArm"));

            Assert.That(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.RightHand,
                    candidates,
                    out var right),
                Is.True);
            Assert.That(right.ResolvedTarget, Is.EqualTo("RightLowerArm"));
        }

        [Test]
        public void PresetSelectionIsCaseInsensitiveAndDoesNotToggleLabGameState()
        {
            var gameObject = new GameObject("FoundationBCameraPresetSelectionTest");
            try
            {
                gameObject.AddComponent<Camera>();
                var labCamera = gameObject.AddComponent<ThirdPersonLabCamera>();
                labCamera.SetGameViewActive(false);

                Assert.That(labCamera.SelectViewPreset("  hAnDs  "), Is.True);
                Assert.That(labCamera.CurrentViewPresetName, Is.EqualTo("Hands"));
                Assert.That(labCamera.IsGameViewActive, Is.False);
                Assert.That(labCamera.SelectViewPreset("unknown preset"), Is.False);
                Assert.That(labCamera.IsGameViewActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ExponentialResponseComposesAcrossEquivalentElapsedTime()
        {
            const float response = 7f;
            var half = CameraViewPresetMath.ResponseAlpha(response, 0.01f);
            var full = CameraViewPresetMath.ResponseAlpha(response, 0.02f);
            var twoHalfSteps = 1f - (1f - half) * (1f - half);

            Assert.That(twoHalfSteps, Is.EqualTo(full).Within(0.000001f));
        }

        private static CameraViewPreset FindDefault(string name)
        {
            Assert.That(
                CameraViewPresetMath.TryFindUniquePreset(
                    CameraViewPreset.CreateDefaults(),
                    name,
                    out var preset,
                    out _,
                    out var reason),
                Is.True,
                reason);
            return preset;
        }

        private static void Calculate(
            Vector3 focus,
            Vector2 heading,
            CameraViewPreset preset,
            out Vector3 position)
        {
            Assert.That(
                CameraViewPresetMath.TryCalculateView(
                    heading,
                    focus,
                    preset,
                    out position,
                    out _),
                Is.True);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
