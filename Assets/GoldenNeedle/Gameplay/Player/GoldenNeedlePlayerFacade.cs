using HDMotionEngine;
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
        [SerializeField] private MotionEngineController motionEngine;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GoldenNeedleBodyAnchors bodyAnchors;
        public MotionEngineController MotionEngine => motionEngine;
        public bool IsCalibrationUsable => motionEngine == null ? default : motionEngine.IsCalibrationUsable;
        public bool IsCalibrationComplete => motionEngine == null ? default : motionEngine.IsCalibrationComplete;
        public bool IsBodyTrackingAvailable => motionEngine != null && motionEngine.IsTrackingAvailable;
        public Transform PlayerRoot => motionEngine == null ? null : motionEngine.AvatarRoot;
        public GoldenNeedlePlayerVerticalState VerticalState => motionEngine == null ? GoldenNeedlePlayerVerticalState.Unavailable : (GoldenNeedlePlayerVerticalState)(int)motionEngine.VerticalState;
        public bool IsAvatarPoseDriveEnabled => motionEngine != null && motionEngine.IsPoseDriveEnabled;
        public bool IsAvatarAnimationAuthorityEnabled => motionEngine != null && motionEngine.IsAvatarAnimationAuthorityEnabled;
        public bool IsLocomotionEnabled => motionEngine == null ? default : motionEngine.IsLocomotionEnabled;
        public bool IsMotionControlEnabled => motionEngine != null && motionEngine.IsPoseDriveEnabled && motionEngine.IsLocomotionEnabled;
        public bool IsRigBound => motionEngine == null ? default : motionEngine.IsRigBound;
        public string RigBindingModeName => motionEngine == null ? string.Empty : motionEngine.RigBindingModeName;
        public bool AreRetargetTargetsLive => motionEngine == null ? default : motionEngine.AreRetargetTargetsLive;
        public int RetargetSourceChainsValid => motionEngine == null ? default : motionEngine.RetargetSourceChainsValid;
        public int RetargetTargetsGenerated => motionEngine == null ? default : motionEngine.RetargetTargetsGenerated;
        public int RetargetIkChainsSolved => motionEngine == null ? default : motionEngine.RetargetIkChainsSolved;
        public bool HasWorldHeading => motionEngine == null ? default : motionEngine.HasWorldHeading;
        public Vector2 WorldHeadingXZ => motionEngine == null ? default : motionEngine.WorldHeadingXZ;
        public bool HasLocomotionVerticalOrigin => motionEngine == null ? default : motionEngine.HasLocomotionVerticalOrigin;
        public float LocomotionVerticalOriginY => motionEngine == null ? default : motionEngine.LocomotionVerticalOriginY;
        public float LocomotionFinalWorldPositionY => motionEngine == null ? default : motionEngine.LocomotionFinalWorldPositionY;
        public string ProviderStatus => motionEngine == null ? string.Empty : motionEngine.ProviderStatus;
        public string ProviderStatusMessage => motionEngine == null ? string.Empty : motionEngine.ProviderStatusMessage;
        public string SelectedCameraName => motionEngine == null ? string.Empty : motionEngine.SelectedCameraName;
        public bool PoseProviderHasCameraTexture => motionEngine == null ? default : motionEngine.PoseProviderHasCameraTexture;
        public bool PoseProviderCameraPlaying => motionEngine == null ? default : motionEngine.PoseProviderCameraPlaying;
        public bool PoseProviderHasSeenFreshCameraFrame => motionEngine == null ? default : motionEngine.PoseProviderHasSeenFreshCameraFrame;
        public double LatestCameraFrameAgeMilliseconds => motionEngine == null ? double.PositiveInfinity : motionEngine.LatestCameraFrameAgeMilliseconds;
        public bool PoseProviderCameraFrameFresh => motionEngine == null ? default : motionEngine.PoseProviderCameraFrameFresh;
        public bool PoseProviderIsUsable => motionEngine == null ? default : motionEngine.PoseProviderIsUsable;
        public bool PoseProviderIsBootstrapping => motionEngine == null ? default : motionEngine.PoseProviderIsBootstrapping;
        public bool PoseProviderHasReceivedResult => motionEngine == null ? default : motionEngine.PoseProviderHasReceivedResult;
        public double LatestPoseAgeMilliseconds => motionEngine == null ? double.PositiveInfinity : motionEngine.LatestPoseAgeMilliseconds;
        public string ActiveInferenceBackendLabel => motionEngine == null ? string.Empty : motionEngine.ActiveInferenceBackendLabel;
        public string ActiveBodyFrameAcquisitionModeLabel => motionEngine == null ? string.Empty : motionEngine.ActiveBodyFrameAcquisitionModeLabel;
        public string WebCamCpuAcquisitionFallbackReason => motionEngine == null ? string.Empty : motionEngine.WebCamCpuAcquisitionFallbackReason;
        public float PoseProviderCameraFramesPerSecond => motionEngine == null ? default : motionEngine.PoseProviderCameraFramesPerSecond;
        public bool OpenVinoRuntimeAvailable => motionEngine == null ? default : motionEngine.OpenVinoRuntimeAvailable;
        public bool OpenVinoWorkerTaskAvailable => motionEngine == null ? default : motionEngine.OpenVinoWorkerTaskAvailable;
        public bool OpenVinoWorkerSignalAvailable => motionEngine == null ? default : motionEngine.OpenVinoWorkerSignalAvailable;
        public float PoseProviderCameraStartupTimeoutSeconds => motionEngine == null ? default : motionEngine.PoseProviderCameraStartupTimeoutSeconds;
        public bool CanTryTrackingRecovery => motionEngine == null ? default : motionEngine.CanTryTrackingRecovery;
        public bool TrackingRecoveryRequested => motionEngine == null ? default : motionEngine.TrackingRecoveryRequested;
        public bool TrackingRecoveryAccepted => motionEngine == null ? default : motionEngine.TrackingRecoveryAccepted;
        public bool TrackingRecoveryPerformed => motionEngine == null ? default : motionEngine.TrackingRecoveryPerformed;
        public int TrackingRecoveryRequestCount => motionEngine == null ? default : motionEngine.TrackingRecoveryRequestCount;
        public int TrackingRecoveryRestartCount => motionEngine == null ? default : motionEngine.TrackingRecoveryRestartCount;
        public bool SoftCameraRecoveryRequested => motionEngine == null ? default : motionEngine.SoftCameraRecoveryRequested;
        public bool SoftCameraRecoveryPerformed => motionEngine == null ? default : motionEngine.SoftCameraRecoveryPerformed;
        public bool SoftCameraRecoverySucceeded => motionEngine == null ? default : motionEngine.SoftCameraRecoverySucceeded;
        public bool HardCameraRecoveryRequested => motionEngine == null ? default : motionEngine.HardCameraRecoveryRequested;
        public bool HardCameraRecoveryAccepted => motionEngine == null ? default : motionEngine.HardCameraRecoveryAccepted;
        public bool HardCameraRecoveryPerformed => motionEngine == null ? default : motionEngine.HardCameraRecoveryPerformed;
        public PlayerHealth Health => playerHealth;
        public Transform LeftWristAnchor => bodyAnchors == null ? null : bodyAnchors.LeftWrist;
        public Transform RightWristAnchor => bodyAnchors == null ? null : bodyAnchors.RightWrist;
        public Transform LeftFootAnchor => bodyAnchors == null ? null : bodyAnchors.LeftFoot;
        public Transform RightFootAnchor => bodyAnchors == null ? null : bodyAnchors.RightFoot;
        public Texture CameraPreviewTexture => motionEngine == null ? default : motionEngine.CameraPreviewTexture;
        public int CameraPreviewSourceWidth => motionEngine == null ? default : motionEngine.CameraPreviewSourceWidth;
        public int CameraPreviewSourceHeight => motionEngine == null ? default : motionEngine.CameraPreviewSourceHeight;
        public int CameraPreviewRotationDegrees => motionEngine == null ? default : motionEngine.CameraPreviewRotationDegrees;
        public bool CameraPreviewPresentationHorizontalMirror => motionEngine == null ? default : motionEngine.CameraPreviewPresentationHorizontalMirror;
        public bool CameraPreviewDisplayVerticalCorrection => motionEngine == null ? default : motionEngine.CameraPreviewDisplayVerticalCorrection;
        public GoldenNeedleAvatarDriveMode AvatarDriveMode => motionEngine == null || !motionEngine.HasAvatarPresentation ? GoldenNeedleAvatarDriveMode.Unsupported : motionEngine.IsRawAvatarPresentation ? GoldenNeedleAvatarDriveMode.RawCanonical : GoldenNeedleAvatarDriveMode.StabilizedCanonical;
        private void Awake() => ResolveReferences();
        public void Recenter() { ResolveReferences(); motionEngine?.Recenter(); }
        public bool TryPlacePlayerAt(Vector3 position) { ResolveReferences(); return motionEngine != null && motionEngine.TryPlaceAvatarRoot(position); }
        public bool TryEnsureRigBinding() { ResolveReferences(); return motionEngine != null && motionEngine.TryEnsureRigBinding(); }
        public void BeginHubContinuityWindow() { ResolveReferences(); motionEngine?.BeginTrackingRecoveryWindow(); }
        public void BeginTrackingRecoveryWindow() => BeginHubContinuityWindow();
        public bool TryBeginSoftCameraRecovery() { ResolveReferences(); return motionEngine != null && motionEngine.TryRecoverCameraContinuity(); }
        public bool TryHardRecoverTracking() { ResolveReferences(); return motionEngine != null && motionEngine.TryHardRecoverTracking(); }
        public bool TryRecoverTracking() => TryHardRecoverTracking();
        public void BeginCalibration() { ResolveReferences(); motionEngine?.BeginCalibration(); }
        public void RetryTracking() { ResolveReferences(); motionEngine?.RetryTracking(); }
        public void SetMotionControlEnabled(bool enabled) { SetAvatarPoseDriveEnabled(enabled); SetLocomotionEnabled(enabled); }
        public void SetAvatarPoseDriveEnabled(bool enabled) { ResolveReferences(); motionEngine?.SetPoseDriveEnabled(enabled); }
        public void SetAvatarAnimationAuthorityEnabled(bool enabled) { ResolveReferences(); motionEngine?.SetAnimationAuthorityEnabled(enabled); }
        public void SetLocomotionEnabled(bool enabled) { ResolveReferences(); motionEngine?.SetLocomotionEnabled(enabled); }
        public bool SetAvatarDriveMode(GoldenNeedleAvatarDriveMode mode)
        {
            ResolveReferences();
            return motionEngine != null && (mode == GoldenNeedleAvatarDriveMode.RawCanonical || mode == GoldenNeedleAvatarDriveMode.StabilizedCanonical) && motionEngine.SetRawAvatarPresentation(mode == GoldenNeedleAvatarDriveMode.RawCanonical);
        }
        private void ResolveReferences()
        {
            if (motionEngine == null) motionEngine = GetComponent<MotionEngineController>();
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
            if (bodyAnchors == null) bodyAnchors = GetComponent<GoldenNeedleBodyAnchors>();
        }
    }
}
