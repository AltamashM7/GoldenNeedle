using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Player
{
    public enum GoldenNeedlePlayerVerticalState
    {
        Unavailable = 0,
        Standing = 1,
        Crouch = 2,
        Jump = 3,
    }

    public enum GoldenNeedleAvatarDriveMode
    {
        Unsupported = 0,
        StabilizedCanonical = 1,
        RawCanonical = 2,
    }

    [DisallowMultipleComponent]
    public sealed class GoldenNeedlePlayerFacade : MonoBehaviour
    {
        [Header("Accepted production motion stack")]
        [SerializeField] private MediaPipePoseProvider poseProvider;
        [SerializeField] private MediaPipeCanonicalPoseSource canonicalPoseSource;
        [SerializeField] private MotionEngineRuntime motionRuntime;
        [SerializeField] private HumanoidRigBinding rigBinding;
        [SerializeField] private HumanoidRetargeter humanoidRetargeter;
        [SerializeField] private EmbodiedLocomotionController locomotionController;

        [Header("Gameplay-facing components")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GoldenNeedleBodyAnchors bodyAnchors;

        public bool IsCalibrationUsable => motionRuntime != null && motionRuntime.Calibration != null && motionRuntime.Calibration.IsValid;
        public bool IsCalibrationComplete => motionRuntime != null && motionRuntime.Calibration != null && motionRuntime.Calibration.IsComplete;
        public bool IsBodyTrackingAvailable => motionRuntime != null && motionRuntime.IsSourceReady && motionRuntime.StabilizedFrame != null && motionRuntime.StabilizedFrame.hasMeaningfulPose;
        public Transform PlayerRoot => locomotionController != null && locomotionController.PlayerRoot != null
            ? locomotionController.PlayerRoot
            : rigBinding != null && rigBinding.IsBound ? rigBinding.AvatarRoot : null;
        public GoldenNeedlePlayerVerticalState VerticalState => MapVerticalState(locomotionController == null ? default : locomotionController.VerticalSample);
        public bool IsAvatarPoseDriveEnabled => humanoidRetargeter != null && humanoidRetargeter.DriveRig;
        public bool IsAvatarAnimationAuthorityEnabled => humanoidRetargeter != null && humanoidRetargeter.ExternalAnimationAuthority;
        public bool IsLocomotionEnabled => locomotionController != null && locomotionController.DriveLocomotion;
        public bool IsMotionControlEnabled => IsAvatarPoseDriveEnabled && IsLocomotionEnabled;
        public bool HasWorldHeading => locomotionController != null && locomotionController.HasWorldHeading;
        public Vector2 WorldHeadingXZ => locomotionController == null
            ? Vector2.zero
            : locomotionController.WorldHeadingXZ;
        public PlayerHealth Health => playerHealth;
        public Transform LeftWristAnchor => bodyAnchors == null ? null : bodyAnchors.LeftWrist;
        public Transform RightWristAnchor => bodyAnchors == null ? null : bodyAnchors.RightWrist;
        public Transform LeftFootAnchor => bodyAnchors == null ? null : bodyAnchors.LeftFoot;
        public Transform RightFootAnchor => bodyAnchors == null ? null : bodyAnchors.RightFoot;

        public Texture CameraPreviewTexture => poseProvider == null ? null : poseProvider.CameraTexture;
        public int CameraPreviewSourceWidth => poseProvider == null ? 0 : poseProvider.ActualCameraWidth;
        public int CameraPreviewSourceHeight => poseProvider == null ? 0 : poseProvider.ActualCameraHeight;
        public int CameraPreviewRotationDegrees => poseProvider == null ? 0 : poseProvider.Orientation.DisplayRotationDegrees;
        public bool CameraPreviewPresentationHorizontalMirror => poseProvider != null && poseProvider.Orientation.PresentationHorizontalMirror;
        public bool CameraPreviewDisplayVerticalCorrection => poseProvider != null && poseProvider.Orientation.DisplayVerticalCorrection;

        public GoldenNeedleAvatarDriveMode AvatarDriveMode
        {
            get
            {
                if (motionRuntime == null)
                {
                    return GoldenNeedleAvatarDriveMode.Unsupported;
                }

                return motionRuntime.AvatarDriveSource switch
                {
                    AvatarDrivePoseSource.StabilizedCanonical => GoldenNeedleAvatarDriveMode.StabilizedCanonical,
                    AvatarDrivePoseSource.RawCanonical => GoldenNeedleAvatarDriveMode.RawCanonical,
                    _ => GoldenNeedleAvatarDriveMode.Unsupported,
                };
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        public void Recenter()
        {
            locomotionController?.Recenter();
        }

        public bool TryPlacePlayerAt(Vector3 worldPosition)
        {
            ResolveReferences();
            return locomotionController != null && locomotionController.TryPlacePlayerRoot(worldPosition);
        }

        public void BeginCalibration()
        {
            motionRuntime?.BeginCalibration();
        }

        public void RetryTracking()
        {
            poseProvider?.Retry();
        }

        public void SetMotionControlEnabled(bool enabled)
        {
            SetAvatarPoseDriveEnabled(enabled);
            SetLocomotionEnabled(enabled);
        }

        public void SetAvatarPoseDriveEnabled(bool enabled)
        {
            if (humanoidRetargeter != null)
            {
                humanoidRetargeter.DriveRig = enabled;
            }
        }

        public void SetAvatarAnimationAuthorityEnabled(bool enabled)
        {
            if (humanoidRetargeter != null)
            {
                humanoidRetargeter.SetExternalAnimationAuthority(enabled);
            }
        }

        public void SetLocomotionEnabled(bool enabled)
        {
            if (locomotionController != null)
            {
                locomotionController.DriveLocomotion = enabled;
            }
        }

        public bool SetAvatarDriveMode(GoldenNeedleAvatarDriveMode mode)
        {
            if (motionRuntime == null)
            {
                return false;
            }

            return mode switch
            {
                GoldenNeedleAvatarDriveMode.StabilizedCanonical => motionRuntime.TrySetAvatarDriveSource(AvatarDrivePoseSource.StabilizedCanonical),
                GoldenNeedleAvatarDriveMode.RawCanonical => motionRuntime.TrySetAvatarDriveSource(AvatarDrivePoseSource.RawCanonical),
                _ => false,
            };
        }

        private static GoldenNeedlePlayerVerticalState MapVerticalState(VerticalLocomotionSample sample)
        {
            if (!sample.isAvailable)
            {
                return GoldenNeedlePlayerVerticalState.Unavailable;
            }

            return sample.state switch
            {
                VerticalLocomotionState.Crouch => GoldenNeedlePlayerVerticalState.Crouch,
                VerticalLocomotionState.Jump => GoldenNeedlePlayerVerticalState.Jump,
                VerticalLocomotionState.Standing => GoldenNeedlePlayerVerticalState.Standing,
                _ => GoldenNeedlePlayerVerticalState.Unavailable,
            };
        }

        private void ResolveReferences()
        {
            poseProvider = poseProvider == null ? GetComponent<MediaPipePoseProvider>() : poseProvider;
            canonicalPoseSource = canonicalPoseSource == null ? GetComponent<MediaPipeCanonicalPoseSource>() : canonicalPoseSource;
            motionRuntime = motionRuntime == null ? GetComponent<MotionEngineRuntime>() : motionRuntime;
            rigBinding = rigBinding == null ? GetComponent<HumanoidRigBinding>() : rigBinding;
            humanoidRetargeter = humanoidRetargeter == null ? GetComponent<HumanoidRetargeter>() : humanoidRetargeter;
            locomotionController = locomotionController == null ? GetComponent<EmbodiedLocomotionController>() : locomotionController;
            playerHealth = playerHealth == null ? GetComponent<PlayerHealth>() : playerHealth;
            bodyAnchors = bodyAnchors == null ? GetComponent<GoldenNeedleBodyAnchors>() : bodyAnchors;
        }
    }
}
