using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Calibration
{
    public enum MotionCalibrationState
    {
        Idle = 0,
        AwaitingBodyReference = 1,
        SamplingBodyReference = 2,
        AcquiringGeometry = 3,
        Ready = 4,
    }

    public enum MotionCalibrationChainId
    {
        LeftArm = 0,
        RightArm = 1,
        LeftLeg = 2,
        RightLeg = 3,
    }

    [Serializable]
    public struct MotionCalibrationChainGeometry
    {
        public bool isValid;
        public int sampleCount;
        public float upperLength;
        public float lowerLength;
        public float reach;
        public Vector3 referenceUpperDirection;
        public Vector3 referenceLowerDirection;
    }

    /// <summary>
    /// In-memory user calibration. Body-reference validity is independent from each limb chain.
    /// The compatibility isValid field means "body reference usable"; it no longer means every
    /// chain has completed geometry sampling.
    /// </summary>
    [Serializable]
    public sealed class MotionCalibrationProfile
    {
        public const int CurrentVersion = 5;

        public int version = CurrentVersion;

        /// <summary>
        /// Compatibility/global readiness flag. In version 5 this is synchronized to
        /// bodyReferenceValid and deliberately does not require all four limb chains.
        /// </summary>
        public bool isValid;
        public bool bodyReferenceValid;
        public MotionCalibrationState state;
        public double bodyReferenceCapturedAtSeconds;
        public double completedAtSeconds;

        public Vector3 neutralPelvisPosition;
        public Vector3 neutralChestPosition;
        public Vector3 neutralLeftShoulderPosition;
        public Vector3 neutralRightShoulderPosition;
        public Vector3 neutralLeftHipPosition;
        public Vector3 neutralRightHipPosition;

        public float shoulderWidth;
        public float hipWidth;
        public float torsoLength;
        public Vector3 neutralBodyUp;
        public Vector3 neutralBodyRight;
        public Vector3 neutralBodyForward;

        public MotionCalibrationChainGeometry leftArmGeometry;
        public MotionCalibrationChainGeometry rightArmGeometry;
        public MotionCalibrationChainGeometry leftLegGeometry;
        public MotionCalibrationChainGeometry rightLegGeometry;

        public int bodyReferenceSampleCount;

        public bool HasBodyReference => bodyReferenceValid;
        public bool AllChainGeometryValid =>
            leftArmGeometry.isValid &&
            rightArmGeometry.isValid &&
            leftLegGeometry.isValid &&
            rightLegGeometry.isValid;

        public int ValidChainCount
        {
            get
            {
                var count = 0;
                if (leftArmGeometry.isValid) count++;
                if (rightArmGeometry.isValid) count++;
                if (leftLegGeometry.isValid) count++;
                if (rightLegGeometry.isValid) count++;
                return count;
            }
        }

        public MotionCalibrationChainGeometry GetChainGeometry(MotionCalibrationChainId id)
        {
            switch (id)
            {
                case MotionCalibrationChainId.LeftArm:
                    return leftArmGeometry;
                case MotionCalibrationChainId.RightArm:
                    return rightArmGeometry;
                case MotionCalibrationChainId.LeftLeg:
                    return leftLegGeometry;
                case MotionCalibrationChainId.RightLeg:
                    return rightLegGeometry;
                default:
                    return default;
            }
        }

        public void SetChainGeometry(MotionCalibrationChainId id, in MotionCalibrationChainGeometry geometry)
        {
            switch (id)
            {
                case MotionCalibrationChainId.LeftArm:
                    leftArmGeometry = geometry;
                    break;
                case MotionCalibrationChainId.RightArm:
                    rightArmGeometry = geometry;
                    break;
                case MotionCalibrationChainId.LeftLeg:
                    leftLegGeometry = geometry;
                    break;
                case MotionCalibrationChainId.RightLeg:
                    rightLegGeometry = geometry;
                    break;
            }
        }

        public void Clear()
        {
            version = CurrentVersion;
            isValid = false;
            bodyReferenceValid = false;
            state = MotionCalibrationState.Idle;
            bodyReferenceCapturedAtSeconds = 0d;
            completedAtSeconds = 0d;

            neutralPelvisPosition = Vector3.zero;
            neutralChestPosition = Vector3.zero;
            neutralLeftShoulderPosition = Vector3.zero;
            neutralRightShoulderPosition = Vector3.zero;
            neutralLeftHipPosition = Vector3.zero;
            neutralRightHipPosition = Vector3.zero;
            shoulderWidth = 0f;
            hipWidth = 0f;
            torsoLength = 0f;
            neutralBodyUp = Vector3.zero;
            neutralBodyRight = Vector3.zero;
            neutralBodyForward = Vector3.zero;

            leftArmGeometry = default;
            rightArmGeometry = default;
            leftLegGeometry = default;
            rightLegGeometry = default;
            bodyReferenceSampleCount = 0;
        }
    }
}
