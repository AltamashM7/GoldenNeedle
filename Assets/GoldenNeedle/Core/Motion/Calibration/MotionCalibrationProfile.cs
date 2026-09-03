using System;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Calibration
{
    public enum MotionCalibrationState
    {
        Idle = 0,
        AwaitingNeutral = 1,
        SamplingNeutral = 2,
        AwaitingTPose = 3,
        SamplingTPose = 4,
        Complete = 5,
    }

    /// <summary>
    /// In-memory calibration result. These are user reference measurements, not avatar bone
    /// lengths, and are intentionally not persisted to disk.
    /// </summary>
    [Serializable]
    public sealed class MotionCalibrationProfile
    {
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public bool isValid;
        public MotionCalibrationState state;
        public double completedAtSeconds;

        public Vector3 neutralPelvisPosition;
        public Vector3 neutralChestPosition;
        public Vector3 neutralLeftShoulderPosition;
        public Vector3 neutralRightShoulderPosition;
        public Vector3 neutralLeftHipPosition;
        public Vector3 neutralRightHipPosition;
        public Vector3 neutralLeftKneePosition;
        public Vector3 neutralRightKneePosition;
        public Vector3 neutralLeftAnklePosition;
        public Vector3 neutralRightAnklePosition;

        public float shoulderWidth;
        public float hipWidth;
        public float torsoLength;
        public Vector3 neutralBodyUp;
        public Vector3 neutralBodyRight;
        public Vector3 neutralBodyForward;

        public Vector3 tPoseLeftArmDirection;
        public Vector3 tPoseRightArmDirection;
        public Vector3 tPoseLeftShoulderPosition;
        public Vector3 tPoseLeftElbowPosition;
        public Vector3 tPoseLeftWristPosition;
        public Vector3 tPoseRightShoulderPosition;
        public Vector3 tPoseRightElbowPosition;
        public Vector3 tPoseRightWristPosition;
        public float tPoseArmSpan;
        public float leftArmReach;
        public float rightArmReach;
        public float leftLegReach;
        public float rightLegReach;

        public int neutralSampleCount;
        public int tPoseSampleCount;

        public void Clear()
        {
            version = CurrentVersion;
            isValid = false;
            state = MotionCalibrationState.Idle;
            completedAtSeconds = 0d;
            neutralPelvisPosition = Vector3.zero;
            neutralChestPosition = Vector3.zero;
            neutralLeftShoulderPosition = Vector3.zero;
            neutralRightShoulderPosition = Vector3.zero;
            neutralLeftHipPosition = Vector3.zero;
            neutralRightHipPosition = Vector3.zero;
            neutralLeftKneePosition = Vector3.zero;
            neutralRightKneePosition = Vector3.zero;
            neutralLeftAnklePosition = Vector3.zero;
            neutralRightAnklePosition = Vector3.zero;
            shoulderWidth = 0f;
            hipWidth = 0f;
            torsoLength = 0f;
            neutralBodyUp = Vector3.zero;
            neutralBodyRight = Vector3.zero;
            neutralBodyForward = Vector3.zero;
            tPoseLeftArmDirection = Vector3.zero;
            tPoseRightArmDirection = Vector3.zero;
            tPoseLeftShoulderPosition = Vector3.zero;
            tPoseLeftElbowPosition = Vector3.zero;
            tPoseLeftWristPosition = Vector3.zero;
            tPoseRightShoulderPosition = Vector3.zero;
            tPoseRightElbowPosition = Vector3.zero;
            tPoseRightWristPosition = Vector3.zero;
            tPoseArmSpan = 0f;
            leftArmReach = 0f;
            rightArmReach = 0f;
            leftLegReach = 0f;
            rightLegReach = 0f;
            neutralSampleCount = 0;
            tPoseSampleCount = 0;
        }
    }
}
