using System.Reflection;
using GoldenNeedle.Gameplay.Calibration;
using GoldenNeedle.Gameplay.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests.Editor
{
    public sealed class CalibrationPresentationTests
    {
        [Test]
        public void WorldSpacePresentationGroupImmediateVisibilityReachesExactAlpha()
        {
            var gameObject = new GameObject("WorldSpacePresentationGroupTest");
            try
            {
                var canvasGroup = gameObject.AddComponent<CanvasGroup>();
                var presentationGroup = gameObject.AddComponent<WorldSpacePresentationGroup>();

                presentationGroup.ShowImmediate();
                Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f);
                Assert.IsFalse(canvasGroup.blocksRaycasts);

                presentationGroup.HideImmediate();
                Assert.AreEqual(0f, canvasGroup.alpha, 0.0001f);
                Assert.IsFalse(canvasGroup.blocksRaycasts);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CalibrationSceneControllerStateChangedFiresOnlyOnActualChange()
        {
            var gameObject = new GameObject("CalibrationSceneControllerStateTest");
            try
            {
                var controller = gameObject.AddComponent<CalibrationSceneController>();
                var stateChangedCount = 0;
                controller.StateChanged += _ => stateChangedCount++;
                var setState = typeof(CalibrationSceneController).GetMethod(
                    "SetState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(setState);

                setState.Invoke(controller, new object[] { CalibrationSceneState.Ready });
                setState.Invoke(controller, new object[] { CalibrationSceneState.Ready });
                setState.Invoke(controller, new object[] { CalibrationSceneState.Calibrating });

                Assert.AreEqual(2, stateChangedCount);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CalibrationSceneControllerDoesNotTransitionWithoutPresentationRequest()
        {
            var gameObject = new GameObject("CalibrationSceneControllerTransitionGuardTest");
            try
            {
                var controller = gameObject.AddComponent<CalibrationSceneController>();

                Assert.IsFalse(controller.RequestHubTransitionAfterPresentation());
                Assert.AreEqual(CalibrationSceneState.Initializing, controller.State);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
