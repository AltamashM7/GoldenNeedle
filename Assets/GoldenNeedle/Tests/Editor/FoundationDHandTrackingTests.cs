using System;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Hands;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests.Editor
{
    public sealed class FoundationDHandTrackingTests
    {
        [Test]
        public void SemanticContractHasExactlyTwentyOneStableLandmarks()
        {
            Assert.That(Enum.GetValues(typeof(HandLandmarkId)).Length, Is.EqualTo(21));
            Assert.That((int)HandLandmarkId.Wrist, Is.EqualTo(0));
            Assert.That((int)HandLandmarkId.ThumbCmc, Is.EqualTo(1));
            Assert.That((int)HandLandmarkId.ThumbTip, Is.EqualTo(4));
            Assert.That((int)HandLandmarkId.IndexMcp, Is.EqualTo(5));
            Assert.That((int)HandLandmarkId.MiddleMcp, Is.EqualTo(9));
            Assert.That((int)HandLandmarkId.RingMcp, Is.EqualTo(13));
            Assert.That((int)HandLandmarkId.PinkyMcp, Is.EqualTo(17));
            Assert.That((int)HandLandmarkId.PinkyTip, Is.EqualTo(20));
        }

        [Test]
        public void PalmBasisIsFiniteOrthonormalAndRightHanded()
        {
            var hand = BuildHand(false);
            var basis = HandFeatureSolver.BuildPalmBasis(hand);
            Assert.That(basis.isValid, Is.True);
            Assert.That(basis.primaryAxis.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(basis.secondaryAxis.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(basis.thirdAxis.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Mathf.Abs(Vector3.Dot(basis.primaryAxis, basis.secondaryAxis)), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(Vector3.Dot(basis.primaryAxis, basis.thirdAxis)), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(Vector3.Dot(basis.secondaryAxis, basis.thirdAxis)), Is.LessThan(0.001f));
            Assert.That(basis.determinant, Is.GreaterThan(0.98f));
        }

        [Test]
        public void DegeneratePalmBasisRejectsCleanly()
        {
            var hand = new CanonicalHand(CanonicalHandSide.Left);
            hand.Begin(CanonicalHandSide.Left, 1, 1d, CanonicalHandSide.Left, 1f, HandAssociationMode.HandednessFallback, 1f);
            foreach (var id in new[] { HandLandmarkId.Wrist, HandLandmarkId.IndexMcp, HandLandmarkId.MiddleMcp, HandLandmarkId.PinkyMcp })
            {
                Set(hand, id, Vector3.zero);
            }
            Assert.That(HandFeatureSolver.BuildPalmBasis(hand).isValid, Is.False);
        }

        [Test]
        public void ExtendedAndBentFingerProduceLowAndHighCurl()
        {
            var open = BuildHand(false);
            var bent = BuildHand(true);
            HandFeatureSolver.PopulateDerivedFeatures(open);
            HandFeatureSolver.PopulateDerivedFeatures(bent);
            Assert.That(open.indexCurl.isValid, Is.True);
            Assert.That(open.indexCurl.value, Is.LessThan(0.2f));
            Assert.That(bent.indexCurl.isValid, Is.True);
            Assert.That(bent.indexCurl.value, Is.GreaterThan(0.7f));
        }

        [Test]
        public void OpenFistAndPointingStayConservative()
        {
            var open = BuildHand(false);
            HandFeatureSolver.PopulateDerivedFeatures(open);
            Assert.That(open.shape, Is.EqualTo(HandShapeState.Open));
            Assert.That(open.pointing.isKnown, Is.True);
            Assert.That(open.pointing.value, Is.False);

            var fist = BuildHand(true);
            HandFeatureSolver.PopulateDerivedFeatures(fist);
            Assert.That(fist.shape, Is.EqualTo(HandShapeState.Fist));

            var pointing = BuildHand(true);
            SetFinger(pointing, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip, false, 0.08f);
            HandFeatureSolver.PopulateDerivedFeatures(pointing);
            Assert.That(pointing.pointing.isKnown, Is.True);
            Assert.That(pointing.pointing.value, Is.True);
        }

        [Test]
        public void MissingFingerGeometryIsInvalidNotFabricated()
        {
            var hand = BuildHand(false);
            hand.SetLandmark(new CanonicalHandLandmark { id = HandLandmarkId.IndexDip });
            var curl = HandFeatureSolver.ComputeFingerCurl(
                hand, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip);
            Assert.That(curl.isValid, Is.False);
            Assert.That(float.IsNaN(curl.value), Is.False);
        }

        [Test]
        public void MediaPipeMapperOwnsCanonicalImageAndHandLocalCoordinates()
        {
            var raw = new MediaPipeHandDetectionSnapshot
            {
                sourceTimestampMillisec = 123,
                landmarkCount = 1,
                handedness = CanonicalHandSide.Left,
                handednessScore = 0.9f,
            };
            raw.normalized[0] = new MediaPipeHandPointSnapshot { valid = true, x = 0.25f, y = 0.2f, z = 0f };
            raw.handLocal[0] = new MediaPipeHandPointSnapshot { valid = true, x = 0.1f, y = -0.2f, z = 0.3f };
            var mapped = new CanonicalHand(CanonicalHandSide.Left);
            MediaPipeHandLandmarkMapper.MapDetection(raw, CanonicalHandSide.Left, HandAssociationMode.HandednessFallback, 0.6f, 1d, mapped);
            var wrist = mapped.GetLandmark(HandLandmarkId.Wrist);
            Assert.That(wrist.imagePosition.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(wrist.imagePosition.y, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(wrist.handLocalPosition.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(wrist.handLocalPosition.y, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(wrist.handLocalPosition.z, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void AssociationPrefersPoseWristsAndFallsBackWhenNeeded()
        {
            var body = new BodyWristEvidence
            {
                hasLeft = true, left = new Vector2(0.2f, 0.5f), leftConfidence = 1f,
                hasRight = true, right = new Vector2(0.8f, 0.5f), rightConfidence = 1f,
            };
            var first = new HandAssociationCandidate
            {
                isValid = true, wristImagePosition = new Vector2(0.21f, 0.5f),
                handednessEvidence = CanonicalHandSide.Right, handednessScore = 0.95f,
            };
            var second = new HandAssociationCandidate
            {
                isValid = true, wristImagePosition = new Vector2(0.79f, 0.5f),
                handednessEvidence = CanonicalHandSide.Left, handednessScore = 0.95f,
            };
            HandAssociationSolver.AssociatePair(in first, in second, in body, out var firstResult, out var secondResult);
            Assert.That(firstResult.side, Is.EqualTo(CanonicalHandSide.Left));
            Assert.That(secondResult.side, Is.EqualTo(CanonicalHandSide.Right));
            Assert.That(firstResult.mode, Is.EqualTo(HandAssociationMode.PoseWristProximity));

            var noBody = default(BodyWristEvidence);
            var fallback = HandAssociationSolver.AssociateSingle(in first, in noBody);
            Assert.That(fallback.side, Is.EqualTo(CanonicalHandSide.Right));
            Assert.That(fallback.mode, Is.EqualTo(HandAssociationMode.HandednessFallback));
        }

        [Test]
        public void OneVisibleBodyWristCanStillConservativelyRecoverOppositeHand()
        {
            var body = new BodyWristEvidence { hasLeft = true, left = new Vector2(0.2f, 0.5f), leftConfidence = 1f };
            var right = new HandAssociationCandidate
            {
                isValid = true,
                wristImagePosition = new Vector2(0.82f, 0.5f),
                handednessEvidence = CanonicalHandSide.Right,
                handednessScore = 0.9f,
            };
            var result = HandAssociationSolver.AssociateSingle(in right, in body);
            Assert.That(result.side, Is.EqualTo(CanonicalHandSide.Right));
            Assert.That(result.mode, Is.EqualTo(HandAssociationMode.HandednessFallback));
        }

        [Test]
        public void FreshnessAgeAndBodySkewAreIndependentPerHand()
        {
            var settings = HandFreshnessSettings.CreateDefault();
            var left = BuildHand(false);
            left.EvaluateFreshness(1.20d, 1100, settings);
            Assert.That(left.isFresh, Is.True);
            left.EvaluateFreshness(1.50d, 1100, settings);
            Assert.That(left.isFresh, Is.False);

            var right = BuildHand(false, CanonicalHandSide.Right, 1000, 1d);
            right.EvaluateFreshness(1.10d, 1400, settings);
            Assert.That(right.isFresh, Is.False);

            var frame = new CanonicalHandFrame();
            left.Clear(CanonicalHandSide.Left);
            right = BuildHand(false, CanonicalHandSide.Right, 1100, 1.15d);
            right.EvaluateFreshness(1.20d, 1100, settings);
            frame.Begin("test", 1100, 1.20d, true);
            frame.CopyHand(left);
            frame.CopyHand(right);
            Assert.That(frame.Left.isFresh, Is.False);
            Assert.That(frame.Right.isFresh, Is.True);
        }

        [Test]
        public void SchedulerHasOneActiveAndOneReplaceablePendingOnly()
        {
            var scheduler = new BoundedHandInferenceScheduler();
            Assert.That(scheduler.Offer(1, out var launch) && launch, Is.True);
            Assert.That(scheduler.HasActive, Is.True);
            scheduler.Offer(2, out launch);
            Assert.That(launch, Is.False);
            scheduler.Offer(3, out launch);
            Assert.That(scheduler.PendingSequence, Is.EqualTo(3));
            Assert.That(scheduler.ReplacedPendingCount, Is.EqualTo(1));
            Assert.That(scheduler.CompleteActive(out var next), Is.True);
            Assert.That(next, Is.EqualTo(3));
            Assert.That(scheduler.HasPending, Is.False);
            Assert.That(scheduler.CompleteActive(out next), Is.False);
        }

        [Test]
        public void ClearRemovesOldSessionSamples()
        {
            var frame = new CanonicalHandFrame();
            var left = BuildHand(false);
            left.EvaluateFreshness(1.2d, 1100, HandFreshnessSettings.CreateDefault());
            frame.Begin("test", 1100, 1.2d, true);
            frame.CopyHand(left);
            Assert.That(frame.Left.isFresh, Is.True);
            frame.Clear();
            Assert.That(frame.Left.isDetected, Is.False);
            Assert.That(frame.Right.isDetected, Is.False);
            Assert.That(frame.freshHandCount, Is.Zero);
        }

        private static CanonicalHand BuildHand(
            bool bent,
            CanonicalHandSide side = CanonicalHandSide.Left,
            long timestamp = 1000,
            double receivedAt = 1d)
        {
            var hand = new CanonicalHand(side);
            hand.Begin(side, timestamp, receivedAt, side, 1f, HandAssociationMode.PoseWristProximity, 1f);
            Set(hand, HandLandmarkId.Wrist, Vector3.zero);
            Set(hand, HandLandmarkId.IndexMcp, new Vector3(0.04f, 0.06f, 0));
            Set(hand, HandLandmarkId.MiddleMcp, new Vector3(0, 0.07f, 0));
            Set(hand, HandLandmarkId.RingMcp, new Vector3(-0.025f, 0.06f, 0));
            Set(hand, HandLandmarkId.PinkyMcp, new Vector3(-0.05f, 0.05f, 0));
            SetFinger(hand, HandLandmarkId.ThumbCmc, HandLandmarkId.ThumbMcp, HandLandmarkId.ThumbIp, HandLandmarkId.ThumbTip, bent, 0.055f);
            SetFinger(hand, HandLandmarkId.IndexMcp, HandLandmarkId.IndexPip, HandLandmarkId.IndexDip, HandLandmarkId.IndexTip, bent, 0.08f);
            SetFinger(hand, HandLandmarkId.MiddleMcp, HandLandmarkId.MiddlePip, HandLandmarkId.MiddleDip, HandLandmarkId.MiddleTip, bent, 0.085f);
            SetFinger(hand, HandLandmarkId.RingMcp, HandLandmarkId.RingPip, HandLandmarkId.RingDip, HandLandmarkId.RingTip, bent, 0.078f);
            SetFinger(hand, HandLandmarkId.PinkyMcp, HandLandmarkId.PinkyPip, HandLandmarkId.PinkyDip, HandLandmarkId.PinkyTip, bent, 0.07f);
            return hand;
        }

        private static void SetFinger(
            CanonicalHand hand,
            HandLandmarkId mcp,
            HandLandmarkId pip,
            HandLandmarkId dip,
            HandLandmarkId tip,
            bool bent,
            float length)
        {
            var existing = hand.GetLandmark(mcp);
            var root = existing.hasHandLocalPosition ? existing.handLocalPosition : Vector3.zero;
            if (!existing.hasHandLocalPosition) Set(hand, mcp, root);
            if (!bent)
            {
                Set(hand, pip, root + new Vector3(0, length * 0.33f, 0));
                Set(hand, dip, root + new Vector3(0, length * 0.66f, 0));
                Set(hand, tip, root + new Vector3(0, length, 0));
            }
            else
            {
                Set(hand, pip, root + new Vector3(0, length * 0.25f, 0));
                Set(hand, dip, root + new Vector3(0, length * 0.15f, length * 0.15f));
                Set(hand, tip, root + new Vector3(0, length * 0.05f, 0));
            }
        }

        private static void Set(CanonicalHand hand, HandLandmarkId id, Vector3 position)
        {
            hand.SetLandmark(new CanonicalHandLandmark
            {
                id = id,
                hasHandLocalPosition = true,
                handLocalPosition = position,
                hasImagePosition = true,
                imagePosition = new Vector2(0.5f, 0.5f),
                confidence = 1f,
            });
        }
    }
}
