using GoldenNeedle.Core.Motion.Calibration;
using GoldenNeedle.Core.Motion.Canonical;
using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    [RequireComponent(typeof(MediaPipePoseProvider))]
    public sealed class PoseTrackingSpikePresenter : MonoBehaviour
    {
        private static readonly int[,] RawConnections =
        {
            { 0, 1 }, { 0, 2 }, { 1, 3 }, { 2, 4 }, { 0, 7 }, { 0, 8 },
            { 11, 12 }, { 11, 13 }, { 13, 15 }, { 15, 17 }, { 15, 19 }, { 15, 21 },
            { 12, 14 }, { 14, 16 }, { 16, 18 }, { 16, 20 }, { 16, 22 },
            { 11, 23 }, { 12, 24 }, { 23, 24 }, { 23, 25 }, { 25, 27 }, { 27, 29 }, { 27, 31 },
            { 24, 26 }, { 26, 28 }, { 28, 30 }, { 30, 32 },
        };

        private static readonly CanonicalJointId[,] CanonicalConnections =
        {
            { CanonicalJointId.Pelvis, CanonicalJointId.Spine },
            { CanonicalJointId.Spine, CanonicalJointId.Chest },
            { CanonicalJointId.Chest, CanonicalJointId.Head },
            { CanonicalJointId.Chest, CanonicalJointId.LeftShoulder },
            { CanonicalJointId.LeftShoulder, CanonicalJointId.LeftElbow },
            { CanonicalJointId.LeftElbow, CanonicalJointId.LeftWrist },
            { CanonicalJointId.Chest, CanonicalJointId.RightShoulder },
            { CanonicalJointId.RightShoulder, CanonicalJointId.RightElbow },
            { CanonicalJointId.RightElbow, CanonicalJointId.RightWrist },
            { CanonicalJointId.Pelvis, CanonicalJointId.LeftHip },
            { CanonicalJointId.LeftHip, CanonicalJointId.LeftKnee },
            { CanonicalJointId.LeftKnee, CanonicalJointId.LeftAnkle },
            { CanonicalJointId.LeftAnkle, CanonicalJointId.LeftHeel },
            { CanonicalJointId.LeftAnkle, CanonicalJointId.LeftToe },
            { CanonicalJointId.Pelvis, CanonicalJointId.RightHip },
            { CanonicalJointId.RightHip, CanonicalJointId.RightKnee },
            { CanonicalJointId.RightKnee, CanonicalJointId.RightAnkle },
            { CanonicalJointId.RightAnkle, CanonicalJointId.RightHeel },
            { CanonicalJointId.RightAnkle, CanonicalJointId.RightToe },
        };

        private static readonly int[] CoordinateDiagnosticSourceIndices = { 11, 12, 15, 16, 23, 24, 25, 26, 27, 28 };
        private static readonly CanonicalJointId[] CoordinateDiagnosticJointIds =
        {
            CanonicalJointId.LeftShoulder, CanonicalJointId.RightShoulder,
            CanonicalJointId.LeftWrist, CanonicalJointId.RightWrist,
            CanonicalJointId.LeftHip, CanonicalJointId.RightHip,
            CanonicalJointId.LeftKnee, CanonicalJointId.RightKnee,
            CanonicalJointId.LeftAnkle, CanonicalJointId.RightAnkle,
        };
        private static readonly string[] CoordinateDiagnosticLabels =
        {
            "L shoulder", "R shoulder", "L wrist", "R wrist", "L hip", "R hip", "L knee", "R knee", "L ankle", "R ankle",
        };

        [SerializeField] private MediaPipePoseProvider provider;
        [SerializeField] private bool drawRawLandmarks = true;
        [SerializeField] private bool drawCanonical2D = true;
        [SerializeField] private bool drawCanonical3D = true;
        [SerializeField] private bool drawStabilized2D = true;
        [SerializeField] private bool drawUnavailableLandmarks = true;
        [SerializeField] private bool drawCoordinateDiagnostic;
        [SerializeField] private float previewPanelWidth = 360f;

        [SerializeField] private MediaPipeCanonicalPoseSource canonicalSource;
        [SerializeField] private MotionEngineRuntime runtime;
        [SerializeField] private HumanoidRetargeter retargeter;
        [SerializeField] private ProceduralDebugRigView rigView;
        [SerializeField] private EmbodiedLocomotionController locomotion;
        [SerializeField] private LocomotionPrototypeView locomotionView;

        private HumanoidRigBinding _rigBinding;

        private float _renderFps;
        private GUIStyle _labelStyle;
        private GUIStyle _smallLabelStyle;

        private void Awake()
        {
            provider = provider == null ? GetComponent<MediaPipePoseProvider>() : provider;
            canonicalSource = canonicalSource == null ? GetComponent<MediaPipeCanonicalPoseSource>() : canonicalSource;
            if (canonicalSource == null)
            {
                canonicalSource = gameObject.AddComponent<MediaPipeCanonicalPoseSource>();
            }

            runtime = runtime == null ? GetComponent<MotionEngineRuntime>() : runtime;
            if (runtime == null)
            {
                runtime = gameObject.AddComponent<MotionEngineRuntime>();
            }

            if (GetComponent<ProceduralDebugHumanoidRig>() == null)
            {
                gameObject.AddComponent<ProceduralDebugHumanoidRig>();
            }

            rigView = rigView == null ? GetComponent<ProceduralDebugRigView>() : rigView;
            if (rigView == null)
            {
                rigView = gameObject.AddComponent<ProceduralDebugRigView>();
            }

            _rigBinding = GetComponent<HumanoidRigBinding>();
            if (_rigBinding == null)
            {
                _rigBinding = gameObject.AddComponent<HumanoidRigBinding>();
            }

            retargeter = retargeter == null ? GetComponent<HumanoidRetargeter>() : retargeter;
            if (retargeter == null)
            {
                retargeter = gameObject.AddComponent<HumanoidRetargeter>();
            }

            locomotion = locomotion == null ? GetComponent<EmbodiedLocomotionController>() : locomotion;
            if (locomotion == null)
            {
                locomotion = gameObject.AddComponent<EmbodiedLocomotionController>();
            }

            locomotionView = locomotionView == null ? GetComponent<LocomotionPrototypeView>() : locomotionView;
            if (locomotionView == null)
            {
                locomotionView = gameObject.AddComponent<LocomotionPrototypeView>();
            }
        }

        private void Update()
        {
            var frameTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _renderFps = Mathf.Lerp(_renderFps, 1f / frameTime, 1f - Mathf.Exp(-8f * frameTime));

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && provider != null)
            {
                provider.Retry();
            }

            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                drawRawLandmarks = !drawRawLandmarks;
            }

            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                drawCanonical2D = !drawCanonical2D;
            }

            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                drawCanonical3D = !drawCanonical3D;
            }

            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
            {
                drawStabilized2D = !drawStabilized2D;
            }

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame && retargeter != null)
            {
                retargeter.DriveRig = !retargeter.DriveRig;
            }

            if (keyboard != null && keyboard.f6Key.wasPressedThisFrame)
            {
                drawCoordinateDiagnostic = !drawCoordinateDiagnostic;
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame && runtime != null)
            {
                runtime.BeginCalibration();
            }

            if (keyboard != null && keyboard.xKey.wasPressedThisFrame && runtime != null)
            {
                runtime.ResetCalibration();
            }

            if (keyboard != null && keyboard.kKey.wasPressedThisFrame && locomotion != null)
            {
                locomotion.Recenter();
            }
        }

        private void OnGUI()
        {
            if (provider == null)
            {
                return;
            }

            EnsureStyles();

            var previewRect = new Rect(0f, 0f, Screen.width, Screen.height);
            var orientation = provider.Orientation;
            var contentRect = GetContentRect(
                previewRect,
                provider.ActualCameraWidth,
                provider.ActualCameraHeight,
                orientation.DisplayRotationDegrees);
            var oldMatrix = GUI.matrix;

            // The preview uses sensor/display metadata plus any explicitly verified raw-source
            // correction. Inference readback flags are not GUI instructions. Restore the caller's matrix
            // before drawing any overlay so its coordinates are transformed exactly once by the
            // shared content rectangle.
            ApplyDisplayPreviewTransform(previewRect, orientation);
            GUI.color = Color.white;
            if (provider.CameraTexture != null)
            {
                GUI.DrawTexture(previewRect, provider.CameraTexture, ScaleMode.ScaleAndCrop, false);
            }
            else
            {
                GUI.color = new Color(0.04f, 0.05f, 0.07f, 1f);
                GUI.DrawTexture(previewRect, Texture2D.whiteTexture);
            }

            GUI.matrix = oldMatrix;

            if (drawRawLandmarks)
            {
                DrawRawSkeleton(contentRect, orientation);
            }

            if (drawCanonical2D)
            {
                DrawCanonical2DSkeleton(runtime == null ? null : runtime.RawCanonicalFrame, contentRect, false, orientation);
            }

            if (drawStabilized2D)
            {
                DrawCanonical2DSkeleton(runtime == null ? null : runtime.StabilizedFrame, contentRect, true, orientation);
            }
            DrawDiagnostics();
            if (drawCanonical3D)
            {
                DrawCanonical3DView();
            }

            DrawDebugRigView();
            DrawLocomotionDiagnostics();
            DrawLocomotionView();
            if (drawCoordinateDiagnostic)
            {
                DrawCoordinateDiagnostic();
            }
        }

        private void EnsureStyles()
        {
            if (_smallLabelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white },
            };
            _smallLabelStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 12,
            };
        }

        private void DrawRawSkeleton(Rect contentRect, CameraOrientationState orientation)
        {
            var observation = canonicalSource == null ? null : canonicalSource.LatestObservation;
            if (observation == null || !observation.hasPose)
            {
                return;
            }

            for (var i = 0; i < RawConnections.GetLength(0); i++)
            {
                var from = observation.GetLandmark(RawConnections[i, 0]);
                var to = observation.GetLandmark(RawConnections[i, 1]);
                if (from.IsTracked && to.IsTracked)
                {
                    DrawLine(
                        InferenceTopLeftToGuiScreen(new Vector2(from.x, from.y), contentRect, orientation),
                        InferenceTopLeftToGuiScreen(new Vector2(to.x, to.y), contentRect, orientation),
                        new Color(0.1f, 1f, 0.55f, 0.9f),
                        3f);
                }
            }

            for (var i = 0; i < PoseObservation.LandmarkCount; i++)
            {
                var landmark = observation.GetLandmark(i);
                if (landmark.IsTracked)
                {
                    DrawPoint(
                        InferenceTopLeftToGuiScreen(new Vector2(landmark.x, landmark.y), contentRect, orientation),
                        new Color(0.2f, 1f, 0.7f, 1f),
                        10f);
                }
                else if (drawUnavailableLandmarks && IsFinite(landmark.x) && IsFinite(landmark.y))
                {
                    DrawPoint(
                        InferenceTopLeftToGuiScreen(new Vector2(landmark.x, landmark.y), contentRect, orientation),
                        new Color(1f, 0.55f, 0.15f, 0.35f),
                        6f);
                }
            }
        }

        private void DrawCanonical2DSkeleton(CanonicalPoseFrame frame, Rect contentRect, bool stabilized, CameraOrientationState orientation)
        {
            if (frame == null || !frame.hasMeaningfulPose)
            {
                return;
            }

            for (var i = 0; i < CanonicalConnections.GetLength(0); i++)
            {
                var from = frame.GetJoint(CanonicalConnections[i, 0]);
                var to = frame.GetJoint(CanonicalConnections[i, 1]);
                if (from.IsTracked && to.IsTracked && from.hasImagePosition && to.hasImagePosition)
                {
                    var lineColor = stabilized
                        ? new Color(0.15f, 1f, 1f, 0.92f)
                        : new Color(1f, 0.85f, 0.1f, 0.95f);
                    DrawLine(
                        CanonicalImageToGuiScreen(from.imagePosition, contentRect, orientation),
                        CanonicalImageToGuiScreen(to.imagePosition, contentRect, orientation),
                        lineColor,
                        stabilized ? 4f : 5f);
                }
            }

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = frame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasImagePosition)
                {
                    continue;
                }

                var color = stabilized
                    ? new Color(0.25f, 1f, 1f, 1f)
                    : IsDerivedJoint(joint.id)
                        ? new Color(1f, 0.3f, 0.95f, 1f)
                        : new Color(1f, 0.9f, 0.15f, 1f);
                DrawPoint(CanonicalImageToGuiScreen(joint.imagePosition, contentRect, orientation), color, stabilized ? 11f : 14f);
            }
        }

        private void DrawDiagnostics()
        {
            var panelHeight = 590f;
            var panel = new Rect(16f, 16f, previewPanelWidth, panelHeight);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.84f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var observation = canonicalSource == null ? null : canonicalSource.LatestObservation;
            var rawFrame = runtime == null ? null : runtime.RawCanonicalFrame;
            var stabilizedFrame = runtime == null ? null : runtime.StabilizedFrame;
            var calibration = runtime == null ? null : runtime.Calibration;
            var rotationFrame = runtime == null ? null : runtime.RotationFrame;
            var coordinateAgreement = CanonicalCoordinateAgreementEvaluator.Evaluate(rawFrame);
            var providerReady = provider != null && provider.Status == PoseProviderStatus.Ready;
            var proceduralRig = GetComponent<ProceduralDebugHumanoidRig>();
            var debugRigPresent = proceduralRig != null && proceduralRig.IsBuilt;
            var state = observation == null || !observation.hasPose
                ? providerReady ? "WAITING / UNAVAILABLE" : provider == null ? "SOURCE / UNAVAILABLE" : provider.Status.ToString().ToUpperInvariant()
                : observation.trustedCount > 0 ? "TRACKING" : "POSE / NO TRUSTED LANDMARKS";
            var cameraName = provider == null || string.IsNullOrEmpty(provider.SelectedCameraName) ? "(none)" : provider.SelectedCameraName;
            var resolution = provider != null && provider.ActualCameraWidth > 0 ? $"{provider.ActualCameraWidth}x{provider.ActualCameraHeight}" : "(not started)";
            var age = provider == null || double.IsInfinity(provider.LatestPoseAgeMilliseconds) ? "-" : $"{provider.LatestPoseAgeMilliseconds:0} ms";
            var rotationState = rotationFrame != null && rotationFrame.calibrationValid && rotationFrame.hasMeaningfulRotation ? "Live" : "Waiting Calibration";
            var retargeted = retargeter != null && retargeter.IsBound;
            var kinematicTargets = runtime == null ? null : runtime.KinematicTargets;
            var text =
                $"POSE TRACKING / MOTION ENGINE LAB\n" +
                $"State: {state}\n" +
                $"Camera: {cameraName}\n" +
                $"Capture: {resolution} @ {(provider == null ? 0f : provider.CameraFramesPerSecond):0.0} fps\n" +
                $"Render: {_renderFps:0.0} fps\n" +
                $"Requests: {(provider == null ? 0f : provider.InferenceRequestsPerSecond):0.0}/s   Results: {(provider == null ? 0f : provider.PoseResultsPerSecond):0.0}/s\n" +
                $"Latest pose age: {age}\n" +
                $"Raw trusted: {(observation == null ? 0 : observation.trustedCount)}/{PoseObservation.LandmarkCount}\n" +
                $"Canonical tracked: {(rawFrame == null ? 0 : rawFrame.trackedJointCount)}/{CanonicalPoseFrame.JointCount}\n" +
                  $"Stabilized tracked: {(stabilizedFrame == null ? 0 : stabilizedFrame.trackedJointCount)}/{CanonicalPoseFrame.JointCount}\n" +
                  $"Pelvis: {(rawFrame != null && rawFrame.hasCanonicalPelvis)}   3D: {(rawFrame != null && rawFrame.hasCanonical3D)}\n" +
                  $"2D↔3D X agreement: {AgreementStatus(coordinateAgreement.hasXEvidence, coordinateAgreement.xPass)}   X comparisons: {coordinateAgreement.xComparisons}\n" +
                  $"2D↔3D Y agreement: {AgreementStatus(coordinateAgreement.hasYEvidence, coordinateAgreement.yPass)}   Y comparisons: {coordinateAgreement.yComparisons}   mismatches: {coordinateAgreement.yMismatches}   N/A: {coordinateAgreement.yInsufficientComparisons}\n" +
                  $"Calibration: {(calibration == null ? "Unavailable" : calibration.State.ToString())}   usable={(calibration != null && calibration.IsValid)}\n" +
                 $"Body reference: {FormatBodyReferenceStatus(calibration)}\n" +
                 $"Left arm: {FormatCalibrationChainStatus(calibration, MotionCalibrationChainId.LeftArm)}\n" +
                 $"Right arm: {FormatCalibrationChainStatus(calibration, MotionCalibrationChainId.RightArm)}\n" +
                 $"Left leg: {FormatCalibrationChainStatus(calibration, MotionCalibrationChainId.LeftLeg)}\n" +
                 $"Right leg: {FormatCalibrationChainStatus(calibration, MotionCalibrationChainId.RightLeg)}\n" +
                 $"Calib dimensions: shoulder {(calibration == null ? 0f : calibration.Profile.shoulderWidth):0.00}   hip {(calibration == null ? 0f : calibration.Profile.hipWidth):0.00}   torso {(calibration == null ? 0f : calibration.Profile.torsoLength):0.00}\n" +
                 $"Rotation solve: {rotationState}\n" +
                 $"Valid bones: {(rotationFrame == null ? 0 : rotationFrame.validBoneCount)}/{GoldenNeedle.Core.Motion.Rotation.CanonicalRotationFrame.BoneCount}\n" +
                 $"Debug rig: {(debugRigPresent ? "Present" : "Missing")}   View: {(rigView != null && rigView.IsReady ? "Ready" : "Missing")}\n" +
                 $"Retargeter: {(retargeted ? "Bound" : "Unbound")}   Binding mode: {(retargeter == null ? "Unbound" : retargeter.BindingModeName)}\n" +
                 $"Driving: {(retargeter != null && retargeter.DriveRig ? "On" : "Off")}\n" +
                 $"Kinematic targets: {(retargeter != null && retargeter.KinematicTargetsLive && kinematicTargets != null ? "Live" : "Waiting")}\n" +
                 $"Source chains valid: {(retargeter == null ? 0 : retargeter.SourceChainsValid)}/{CanonicalKinematicTargets.ChainCount}\n" +
                 $"Targets generated: {(retargeter == null ? 0 : retargeter.TargetsGenerated)}/{CanonicalKinematicTargets.ChainCount}\n" +
                 $"IK chains solved: {(retargeter == null ? 0 : retargeter.IkChainsSolved)}/{CanonicalKinematicTargets.ChainCount}\n" +
                 $"Limb bones driven: {(retargeter == null ? 0 : retargeter.LimbBonesDriven)}/8\n" +
                 $"Max retarget fidelity error: {(retargeter == null ? 0f : retargeter.MaxNormalizedRetargetFidelityError * 100f):0.0}%\n" +
                 $"Max IK endpoint residual: {(retargeter == null ? 0f : retargeter.MaxNormalizedIkEndpointResidual * 100f):0.0}%\n" +
                 $"Max bend-plane error: {(retargeter == null ? 0f : retargeter.MaxBendPlaneErrorDegrees):0.0}°\n" +
                 $"Inference: {(provider == null ? 0f : provider.LastInferenceDurationMilliseconds):0.0} ms\n" +
                $"Canonical view: MediaPipe inference frame\n" +
                $"Sensor rotation: {(provider == null ? 0 : provider.Orientation.SensorRotationDegrees)}°\n" +
                $"Sensor V mirrored: {(provider != null && provider.Orientation.SensorVerticallyMirrored)}\n" +
                $"Inference prep H/V: {(provider != null && provider.Orientation.InferenceFlipHorizontally)}/{(provider != null && provider.Orientation.InferenceFlipVertically)}\n" +
                 $"Display rotation: {(provider == null ? 0 : provider.Orientation.DisplayRotationDegrees)}°\n" +
                 $"Display V correction: {(provider != null && provider.Orientation.DisplayVerticalCorrection)}\n" +
                $"Source H correction: {(provider != null && provider.Orientation.SourceTextureHorizontallyMirrored)}\n" +
                $"Display mirror: {(provider != null && provider.Orientation.DisplayMirrored)} (explicit only)\n" +
                $"Presentation H transform: {(provider != null && provider.Orientation.PresentationHorizontalMirror)}\n" +
                $"Status: {(provider == null ? "No MediaPipe provider" : provider.StatusMessage)}";
            GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f), text, _smallLabelStyle);

            var help = new Rect(16f, Screen.height - 36f, Mathf.Max(640f, Screen.width - 32f), 24f);
            GUI.Label(help, "R retry   C calibrate   X cancel/reset   K recenter locomotion   F1 raw   F2 canonical 2D   F3 3D   F4 stabilized 2D   F5 rig drive ON/OFF   F6 coordinate sample", _smallLabelStyle);
        }

        private void DrawLocomotionDiagnostics()
        {
            if (locomotion == null)
            {
                return;
            }

            var root = locomotion.RootSample;
            var cadenceSample = locomotion.CadenceSample;
            var heading = locomotion.HeadingSample;
            var fusionResult = locomotion.FusionResult;
            var width = Mathf.Min(360f, Mathf.Max(300f, Screen.width - previewPanelWidth - 64f));
            var panel = new Rect(previewPanelWidth + 32f, 16f, width, 205f);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.90f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var recenter = locomotion.RecenterPending
                ? "PENDING"
                : $"ready #{locomotion.RecenterCount}";
            var rootState = fusionResult.rootTrackingLive
                ? "Live"
                : root.hasOrigin ? "Holding" : "Waiting";
            var text =
                "PHASE 5A / EMBODIED LOCOMOTION\n" +
                $"Root tracking: {rootState}   conf={root.confidence:0.00}   scale={root.apparentScale:0.000}   yawCos={root.yawCosine:0.00}\n" +
                $"Physical displacement X/Z: {FormatLocomotionVector(root.displacementXZ)}\n" +
                $"Physical translation: {(fusionResult.physicalTranslationActive ? "ACTIVE" : "idle")}   activity={fusionResult.physicalActivity:0.00}\n" +
                $"Cadence: {(fusionResult.cadenceActive ? "ACTIVE" : "idle")}   conf={cadenceSample.confidence:0.00}   rate={cadenceSample.rateStepsPerSecond:0.00}/s\n" +
                $"Body heading world X/Z: {(heading.isValid ? FormatLocomotionVector(heading.worldHeadingXZ) : "unavailable")}\n" +
                $"Physical contribution: {FormatLocomotionVector(fusionResult.physicalContribution)}\n" +
                $"Cadence contribution vel: {FormatLocomotionVector(fusionResult.cadenceVelocity)}   blend={fusionResult.cadenceBlend:0.00}\n" +
                $"Final frame motion: {FormatLocomotionVector(locomotion.FinalFrameMotionXZ)}\n" +
                $"Recenter: {recenter}   K = set current physical position as origin";

            GUI.Label(
                new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 16f),
                text,
                _smallLabelStyle);
        }

        private void DrawLocomotionView()
        {
            if (locomotionView == null)
            {
                return;
            }

            var width = Mathf.Clamp(Screen.width * 0.34f, 320f, 500f);
            var height = width * 0.58f;
            var panel = new Rect(
                Screen.width - width - 16f,
                Screen.height - height - 48f,
                width,
                height);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 22f),
                "PHASE 5A / FIXED WORLD GRID",
                _smallLabelStyle);

            var view = new Rect(
                panel.x + 8f,
                panel.y + 30f,
                panel.width - 16f,
                panel.height - 38f);
            if (locomotionView.Texture != null)
            {
                GUI.DrawTexture(view, locomotionView.Texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
                GUI.DrawTexture(view, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private static string FormatLocomotionVector(Vector2 value)
        {
            return $"({value.x:+0.00;-0.00;0.00}, {value.y:+0.00;-0.00;0.00})";
        }

        private static string FormatBodyReferenceStatus(MotionCalibrationSession calibration)
        {
            if (calibration == null)
            {
                return "Unavailable";
            }

            if (calibration.Profile.bodyReferenceValid)
            {
                return "READY";
            }

            return calibration.State == MotionCalibrationState.SamplingBodyReference
                ? $"sampling {calibration.Progress01 * 100f:0}%"
                : "waiting for shoulders / hips";
        }

        private static string FormatCalibrationChainStatus(
            MotionCalibrationSession calibration,
            MotionCalibrationChainId id)
        {
            if (calibration == null)
            {
                return "Unavailable";
            }

            var progress = calibration.GetChainProgress(id);
            if (progress.IsReady)
            {
                return "READY";
            }

            var sampleText = progress.SampleCount > 0
                ? $"{progress.SampleCount}/{progress.RequiredSamples} samples"
                : string.Empty;
            string waitText;
            switch (progress.WaitReason)
            {
                case MotionCalibrationChainWaitReason.MissingJoint:
                    waitText = $"waiting for {progress.WaitingForJoint}";
                    break;
                case MotionCalibrationChainWaitReason.LowConfidence:
                    waitText = "waiting for confident tracking";
                    break;
                case MotionCalibrationChainWaitReason.InvalidGeometry:
                    waitText = "waiting for usable segment geometry";
                    break;
                default:
                    waitText = "waiting for samples";
                    break;
            }

            return string.IsNullOrEmpty(sampleText)
                ? waitText
                : $"{sampleText}; {waitText}";
        }

        private static string AgreementStatus(bool hasEvidence, bool pass)
        {
            return !hasEvidence ? "PENDING" : pass ? "PASS" : "FAIL";
        }

        private void DrawCoordinateDiagnostic()
        {
            var width = Mathf.Min(840f, Mathf.Max(620f, Screen.width - 32f));
            var height = Mathf.Min(780f, Mathf.Max(560f, Screen.height - 32f));
            var panel = new Rect(Screen.width - width - 16f, 16f, width, height);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.97f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var observation = canonicalSource == null ? null : canonicalSource.LatestObservation;
            var rawFrame = runtime == null ? null : runtime.RawCanonicalFrame;
            var stabilizedFrame = runtime == null ? null : runtime.StabilizedFrame;
            var calibration = runtime == null ? null : runtime.Calibration;
            var profile = calibration == null ? null : calibration.Profile;
            var orientation = provider == null
                ? default(CameraOrientationState)
                : provider.Orientation;

            var sourceReference = default(SignedAxisBasis);
            var hasSourceReference = profile != null &&
                HumanoidRetargetingMath.TryBuildSignedBasis(
                    profile.neutralBodyRight,
                    profile.neutralBodyUp,
                    profile.neutralBodyForward,
                    out sourceReference);

            var leftShoulder = Vector3.zero;
            var rightShoulder = Vector3.zero;
            var leftHip = Vector3.zero;
            var rightHip = Vector3.zero;
            var pelvis = Vector3.zero;
            var chest = Vector3.zero;
            var hasLivePositions =
                TryGetRetargetPosition(stabilizedFrame, CanonicalJointId.LeftShoulder, out leftShoulder) &&
                TryGetRetargetPosition(stabilizedFrame, CanonicalJointId.RightShoulder, out rightShoulder) &&
                TryGetRetargetPosition(stabilizedFrame, CanonicalJointId.LeftHip, out leftHip) &&
                TryGetRetargetPosition(stabilizedFrame, CanonicalJointId.RightHip, out rightHip) &&
                TryGetRetargetPosition(stabilizedFrame, CanonicalJointId.Pelvis, out pelvis) &&
                TryGetRetargetPosition(stabilizedFrame, CanonicalJointId.Chest, out chest);

            var targetReference = default(SignedAxisBasis);
            var hasTargetBasis = _rigBinding != null &&
                _rigBinding.TryGetReferenceBodyBasis(out targetReference);

            var axisMap = default(CanonicalToAvatarAxisMap);
            var liveSource = default(SignedAxisBasis);
            var hasMap = profile != null &&
                hasLivePositions &&
                _rigBinding != null &&
                HumanoidRetargetingMath.TryCreateCanonicalToAvatarMap(
                    profile,
                    _rigBinding,
                    out axisMap) &&
                HumanoidRetargetingMath.TryBuildLiveSourceBasisForDiagnostics(
                    axisMap,
                    rightShoulder - leftShoulder,
                    chest - pelvis,
                    out liveSource);

            var text =
                "F6 CANONICAL Z / BODY-YAW TRACE (READ-ONLY)\n" +
                "Yaw sign: 0 = reference Forward; + = Forward turns toward that basis +Right around +Up\n" +
                "Joint labels are MediaPipe/Golden Needle semantic Left/Right, not screen side.\n" +
                $"Display mirror: {orientation.DisplayMirrored}   Inference H flip: {orientation.InferenceFlipHorizontally}\n\n";

            text += "A. SOURCE CALIBRATION BASIS\n";
            if (hasSourceReference)
            {
                text +=
                    $"Source ref R = {FormatVector(sourceReference.Right)}\n" +
                    $"Source ref U = {FormatVector(sourceReference.Up)}\n" +
                    $"Source ref F = {FormatVector(sourceReference.Forward)}\n" +
                    $"Source handedness = {sourceReference.HandednessSign:+0;-0;0}\n\n";
            }
            else
            {
                text += "unavailable\n\n";
            }

            text += "B. LIVE SOURCE BODY BASIS (same handedness rule as production torso mapping)\n";
            if (hasMap)
            {
                text +=
                    $"Live R = {FormatVector(liveSource.Right)}\n" +
                    $"Live U = {FormatVector(liveSource.Up)}\n" +
                    $"Live F = {FormatVector(liveSource.Forward)}\n\n";
            }
            else
            {
                text += "unavailable until body calibration + stabilized torso + rig binding are valid\n\n";
            }

            text += "C. STABILIZED CANONICAL DEPTH (production retarget positions; +Z documented away from camera)\n";
            if (hasLivePositions)
            {
                text +=
                    $"L shoulder XYZ = {FormatVector(leftShoulder)}\n" +
                    $"R shoulder XYZ = {FormatVector(rightShoulder)}\n" +
                    $"Shoulder Z: L={leftShoulder.z:0.000}  R={rightShoulder.z:0.000}  dZ R-L={(rightShoulder.z - leftShoulder.z):+0.000;-0.000;0.000}\n" +
                    $"Hip Z:      L={leftHip.z:0.000}  R={rightHip.z:0.000}  dZ R-L={(rightHip.z - leftHip.z):+0.000;-0.000;0.000}\n\n";
            }
            else
            {
                text += "unavailable / missing stabilized torso joints\n\n";
            }

            text += "D. TARGET AVATAR REFERENCE BASIS (cached production basis)\n";
            if (hasTargetBasis)
            {
                text +=
                    $"Target R = {FormatVector(targetReference.Right)}\n" +
                    $"Target U = {FormatVector(targetReference.Up)}\n" +
                    $"Target F = {FormatVector(targetReference.Forward)}\n" +
                    $"Target handedness = {targetReference.HandednessSign:+0;-0;0}\n";
                if (_rigBinding.TryGetAnimatorFootForwardForDiagnostics(out var footForward))
                {
                    text +=
                        $"Target foot/toe forward = {FormatVector(footForward)}\n" +
                        $"Dot(Target F, foot forward) = {Vector3.Dot(targetReference.Forward, footForward):0.000}\n\n";
                }
                else
                {
                    text += "Target foot/toe forward = unavailable\n\n";
                }
            }
            else
            {
                text += "unavailable / rig not bound\n\n";
            }

            text += "E. SOURCE -> TARGET MAP\n";
            if (hasMap)
            {
                var mappedRefRight = axisMap.MapVector(axisMap.Source.Right);
                var mappedRefUp = axisMap.MapVector(axisMap.Source.Up);
                var mappedRefForward = axisMap.MapVector(axisMap.Source.Forward);
                var mappedLiveRight = axisMap.MapVector(liveSource.Right);
                var mappedLiveUp = axisMap.MapVector(liveSource.Up);
                var mappedLiveForward = axisMap.MapVector(liveSource.Forward);

                text +=
                    $"Map determinant sign = {axisMap.DeterminantSign:+0;-0;0}\n" +
                    $"Mapped ref R = {FormatVector(mappedRefRight)}\n" +
                    $"Mapped ref U = {FormatVector(mappedRefUp)}\n" +
                    $"Mapped ref F = {FormatVector(mappedRefForward)}\n" +
                    $"Mapped live R = {FormatVector(mappedLiveRight)}\n" +
                    $"Mapped live U = {FormatVector(mappedLiveUp)}\n" +
                    $"Mapped live F = {FormatVector(mappedLiveForward)}\n\n" +
                    "F. BODY YAW EVIDENCE\n" +
                    $"Source yaw from calibration = {TryFormatYaw(axisMap.Source, liveSource.Forward)}\n" +
                    $"Mapped target yaw = {TryFormatYaw(axisMap.Target, mappedLiveForward)}\n";

                var appliedYawText = "unavailable";
                if (HumanoidRetargetingMath.TryBuildMappedBodyRotation(
                        axisMap,
                        rightShoulder - leftShoulder,
                        chest - pelvis,
                        out var currentBodyRotation))
                {
                    var targetReferenceBodyRotation = Quaternion.LookRotation(
                        axisMap.Target.Forward,
                        axisMap.Target.Up);
                    var bodyDelta = currentBodyRotation *
                        Quaternion.Inverse(targetReferenceBodyRotation);
                    appliedYawText = TryFormatYaw(
                        axisMap.Target,
                        bodyDelta * axisMap.Target.Forward);
                }

                text += $"Applied torso delta yaw = {appliedYawText}\n\n";
            }
            else
            {
                text +=
                    "unavailable until body calibration + stabilized torso + rig binding are valid\n\n" +
                    "F. BODY YAW EVIDENCE\n" +
                    "unavailable\n\n";
            }

            text +=
                "EXISTING RAW/CANONICAL SAMPLE\n" +
                CanonicalCoordinateAgreementEvaluator.DescribeYComparisons(rawFrame) +
                "joint          image x,y       raw world x,y,z          canonical world x,y,z\n";
            for (var i = 0; i < CoordinateDiagnosticJointIds.Length; i++)
            {
                var raw = observation == null
                    ? default
                    : observation.GetLandmark(CoordinateDiagnosticSourceIndices[i]);
                var canonical = rawFrame == null
                    ? default
                    : rawFrame.GetJoint(CoordinateDiagnosticJointIds[i]);
                text += $"{CoordinateDiagnosticLabels[i],-12} {FormatImage(canonical),-15} {FormatRawWorld(raw),-24} {FormatWorld(canonical)}\n";
            }

            GUI.Label(
                new Rect(
                    panel.x + 10f,
                    panel.y + 8f,
                    panel.width - 20f,
                    panel.height - 16f),
                text,
                _smallLabelStyle);
        }

        private static bool TryGetRetargetPosition(
            CanonicalPoseFrame frame,
            CanonicalJointId id,
            out Vector3 position)
        {
            position = Vector3.zero;
            if (frame == null)
            {
                return false;
            }

            var joint = frame.GetJoint(id);
            if (!joint.IsTracked)
            {
                return false;
            }

            if (joint.hasLocalPosition && IsFinite(joint.localPosition))
            {
                position = joint.localPosition;
                return true;
            }

            if (joint.hasWorldPosition && IsFinite(joint.worldPosition))
            {
                position = joint.worldPosition;
                return true;
            }

            return false;
        }

        private static string TryFormatYaw(
            SignedAxisBasis referenceBasis,
            Vector3 liveForward)
        {
            return HumanoidRetargetingMath.TryCalculateSignedYawDegreesForDiagnostics(
                    referenceBasis,
                    liveForward,
                    out var yaw)
                ? $"{yaw:+0.0;-0.0;0.0} deg"
                : "unavailable";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:+0.000;-0.000;0.000}, {value.y:+0.000;-0.000;0.000}, {value.z:+0.000;-0.000;0.000})";
        }

        private static string FormatImage(CanonicalPoseJoint joint)
        {
            return joint.IsTracked && joint.hasImagePosition
                ? $"{joint.imagePosition.x:0.00},{joint.imagePosition.y:0.00}"
                : "--,--";
        }

        private static string FormatRawWorld(PoseLandmarkObservation landmark)
        {
            return landmark.IsTracked && landmark.hasWorldCoordinates &&
                   IsFinite(landmark.worldX) && IsFinite(landmark.worldY) && IsFinite(landmark.worldZ)
                ? $"{landmark.worldX:0.00},{landmark.worldY:0.00},{landmark.worldZ:0.00}"
                : "--,--,--";
        }

        private static string FormatWorld(CanonicalPoseJoint joint)
        {
            return joint.IsTracked && joint.hasWorldPosition
                ? $"{joint.worldPosition.x:0.00},{joint.worldPosition.y:0.00},{joint.worldPosition.z:0.00}"
                : "--,--,--";
        }

        private static Vector2 InferenceTopLeftToGuiScreen(
            Vector2 inferenceTopLeft,
            Rect contentRect,
            CameraOrientationState orientation)
        {
            var guiNormalized = orientation.InferenceTopLeftToGuiNormalized(inferenceTopLeft);
            return new Vector2(
                contentRect.x + guiNormalized.x * contentRect.width,
                contentRect.y + guiNormalized.y * contentRect.height);
        }

        private static Vector2 CanonicalImageToGuiScreen(
            Vector2 canonicalImage,
            Rect contentRect,
            CameraOrientationState orientation)
        {
            // Canonical image Y is bottom-up; MediaPipe inference input and the final
            // normalized IMGUI coordinate are top-down.
            return InferenceTopLeftToGuiScreen(
                new Vector2(canonicalImage.x, 1f - canonicalImage.y),
                contentRect,
                orientation);
        }

        private static void ApplyDisplayPreviewTransform(Rect previewRect, CameraOrientationState orientation)
        {
            var pivot = previewRect.center;
            GUIUtility.ScaleAroundPivot(
                new Vector2(
                    orientation.PresentationHorizontalMirror ? -1f : 1f,
                    orientation.DisplayVerticalCorrection ? -1f : 1f),
                pivot);
            GUIUtility.RotateAroundPivot(-orientation.DisplayRotationDegrees, pivot);
        }

        private void DrawDebugRigView()
        {
            if (rigView == null)
            {
                return;
            }

            var width = Mathf.Min(previewPanelWidth, Mathf.Max(220f, Screen.width - 32f));
            var panelHeight = Mathf.Clamp(Screen.height * 0.34f, 180f, 300f);
            var panelTop = Screen.height - panelHeight - 48f;
            if (panelTop < 388f)
            {
                panelTop = 388f;
                panelHeight = Mathf.Max(140f, Screen.height - panelTop - 48f);
            }

            var panel = new Rect(16f, panelTop, width, panelHeight);
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 7f, panel.width - 20f, 22f), "PROCEDURAL RIG / ACTUAL TRANSFORMS", _smallLabelStyle);

            var view = new Rect(panel.x + 8f, panel.y + 32f, panel.width - 16f, panel.height - 40f);
            if (rigView.Texture != null)
            {
                GUI.DrawTexture(view, rigView.Texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
                GUI.DrawTexture(view, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private void DrawCanonical3DView()
        {
            var canonicalFrame = runtime == null ? null : runtime.RawCanonicalFrame;
            var stabilizedFrame = runtime == null ? null : runtime.StabilizedFrame;
            if (canonicalFrame == null || stabilizedFrame == null)
            {
                return;
            }

            var width = Mathf.Clamp(previewPanelWidth, 280f, 420f);
            var panel = new Rect(Screen.width - width - 16f, 16f, width, Screen.height - 64f);
            GUI.color = new Color(0.015f, 0.02f, 0.035f, 0.92f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 22f), "CANONICAL 3D / LOCAL SPACE", _smallLabelStyle);

            var view = new Rect(panel.x + 16f, panel.y + 38f, panel.width - 32f, panel.height - 86f);
            var center = view.center;
            DrawLine(center + new Vector2(-view.width * 0.35f, 0f), center + new Vector2(view.width * 0.35f, 0f), new Color(0.95f, 0.25f, 0.25f, 0.55f), 1f);
            DrawLine(center + new Vector2(0f, view.height * 0.35f), center + new Vector2(0f, -view.height * 0.35f), new Color(0.25f, 1f, 0.35f, 0.55f), 1f);
            DrawLine(center + new Vector2(-view.width * 0.18f, view.height * 0.18f), center + new Vector2(view.width * 0.18f, -view.height * 0.18f), new Color(0.25f, 0.55f, 1f, 0.55f), 1f);

            for (var i = 0; i < CanonicalConnections.GetLength(0); i++)
            {
                var from = canonicalFrame.GetJoint(CanonicalConnections[i, 0]);
                var to = canonicalFrame.GetJoint(CanonicalConnections[i, 1]);
                if (from.IsTracked && to.IsTracked && from.hasLocalPosition && to.hasLocalPosition)
                {
                    DrawLine(Project3D(from.localPosition, view), Project3D(to.localPosition, view), new Color(1f, 0.72f, 0.12f, 0.9f), 3f);
                }
            }

            for (var i = 0; i < CanonicalConnections.GetLength(0); i++)
            {
                var from = stabilizedFrame.GetJoint(CanonicalConnections[i, 0]);
                var to = stabilizedFrame.GetJoint(CanonicalConnections[i, 1]);
                if (from.IsTracked && to.IsTracked && from.hasLocalPosition && to.hasLocalPosition)
                {
                    DrawLine(Project3D(from.localPosition, view), Project3D(to.localPosition, view), new Color(0.15f, 1f, 1f, 0.95f), 3f);
                }
            }

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = canonicalFrame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasLocalPosition)
                {
                    continue;
                }

                DrawPoint(Project3D(joint.localPosition, view), IsDerivedJoint(joint.id) ? new Color(1f, 0.3f, 0.95f) : new Color(1f, 0.9f, 0.15f), 9f);
            }

            for (var i = 0; i < CanonicalPoseFrame.JointCount; i++)
            {
                var joint = stabilizedFrame.GetJoint((CanonicalJointId)i);
                if (!joint.IsTracked || !joint.hasLocalPosition)
                {
                    continue;
                }

                DrawPoint(Project3D(joint.localPosition, view), new Color(0.25f, 1f, 1f), 7f);
            }

            GUI.Label(new Rect(panel.x + 12f, panel.yMax - 42f, panel.width - 24f, 34f), "+X right/red   +Y up/green   +Z away/blue\nYellow canonical   Cyan stabilized   Pelvis-relative when available", _smallLabelStyle);
        }

        private static Vector2 Project3D(Vector3 position, Rect view)
        {
            var perspective = 1f / Mathf.Max(0.45f, 1f + position.z * 0.45f);
            var scale = Mathf.Min(view.width, view.height) * 0.42f;
            return view.center + new Vector2(position.x * scale * perspective, -position.y * scale * perspective);
        }

        private static Rect GetContentRect(Rect target, int width, int height, int imageRotationDegrees)
        {
            if (width <= 0 || height <= 0)
            {
                return target;
            }

            if (imageRotationDegrees == 90 || imageRotationDegrees == 270)
            {
                var swapped = width;
                width = height;
                height = swapped;
            }

            var sourceAspect = width / (float)height;
            var targetAspect = target.width / target.height;
            if (sourceAspect > targetAspect)
            {
                var drawnWidth = target.height * sourceAspect;
                return new Rect(target.center.x - drawnWidth * 0.5f, target.y, drawnWidth, target.height);
            }

            var drawnHeight = target.width / sourceAspect;
            return new Rect(target.x, target.center.y - drawnHeight * 0.5f, target.width, drawnHeight);
        }

        private static bool IsDerivedJoint(CanonicalJointId id)
        {
            return id == CanonicalJointId.Pelvis || id == CanonicalJointId.Spine || id == CanonicalJointId.Chest;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DrawPoint(Vector2 position, Color color, float size)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color, float width)
        {
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            var midpoint = (from + to) * 0.5f;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, midpoint);
            GUI.color = color;
            GUI.DrawTexture(new Rect(midpoint.x - length * 0.5f, midpoint.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}
