using System;
using GoldenNeedle.Core.Motion.Retargeting;
using UnityEngine;

namespace GoldenNeedle.Core.Motion.Locomotion
{
    [Serializable]
    public sealed class AvatarRelativeCrouchGroundingSettings
    {
        [Tooltip("Dimensionless multiplier applied to normalized grounded body compression after scaling by the avatar's captured standing leg height.")]
        [InspectorName("Avatar Crouch Depth Multiplier")]
        [Min(0f)] public float depthMultiplier = 1.20f;

        [Tooltip("Maximum grounded crouch root descent as a fraction of the avatar's captured standing leg height.")]
        [InspectorName("Maximum Crouch / Leg Fraction")]
        [Min(0f)] public float maximumDepthFraction = 0.65f;

        public void Sanitize()
        {
            depthMultiplier = SafePositive(depthMultiplier, 1.20f);
            maximumDepthFraction = SafePositive(maximumDepthFraction, 0.65f);
        }

        private static float SafePositive(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? fallback
                : value;
        }
    }

    public struct AvatarRelativeCrouchGroundingSample
    {
        public bool hasAvatarLegScale;
        public bool active;
        public bool heldThroughTrackingGrace;
        public bool depthClamped;
        public float leftStandingLegScale;
        public float rightStandingLegScale;
        public float avatarStandingLegScale;
        public float effectiveCompression;
        public float primaryRootOffsetY;
        public float finalRootOffsetY;
        public float residualGroundCorrectionY;
    }

    /// <summary>
    /// Maps the VerticalLocomotionInterpreter's normalized grounded compression onto stable avatar
    /// reference geometry. The cached scale comes from HumanoidRigBinding's reference-pose leg
    /// chains, so it does not shrink as the avatar crouches. This component never rotates a leg and
    /// never feeds solved foot error back into crouch depth. Phase 4 therefore remains the sole
    /// owner of the visible knee/leg pose.
    /// </summary>
    public sealed class AvatarRelativeCrouchGrounding
    {
        private const float Epsilon = 0.00001f;

        private readonly AvatarRelativeCrouchGroundingSettings _settings;
        private HumanoidRigBinding _binding;
        private int _referencePoseVersion = -1;
        private bool _hasAvatarLegScale;
        private float _leftStandingLegScale;
        private float _rightStandingLegScale;
        private float _avatarStandingLegScale;
        private float _filteredRootOffsetY;
        private bool _hasFilteredOffset;

        public AvatarRelativeCrouchGrounding(AvatarRelativeCrouchGroundingSettings settings = null)
        {
            _settings = settings ?? new AvatarRelativeCrouchGroundingSettings();
            _settings.Sanitize();
            Reset();
        }

        public AvatarRelativeCrouchGroundingSample LatestSample { get; private set; }
        public bool HasAvatarLegScale => _hasAvatarLegScale;
        public float AvatarStandingLegScale => _avatarStandingLegScale;

        public void Reset()
        {
            _binding = null;
            _referencePoseVersion = -1;
            _hasAvatarLegScale = false;
            _leftStandingLegScale = 0f;
            _rightStandingLegScale = 0f;
            _avatarStandingLegScale = 0f;
            _filteredRootOffsetY = 0f;
            _hasFilteredOffset = true;
            LatestSample = default;
        }

        public AvatarRelativeCrouchGroundingSample Update(
            HumanoidRigBinding binding,
            bool calibrationValid,
            VerticalLocomotionSample verticalSample,
            VerticalLocomotionSettings verticalSettings,
            float deltaTime)
        {
            verticalSettings = verticalSettings ?? new VerticalLocomotionSettings();
            verticalSettings.Sanitize();
            _settings.Sanitize();

            if (!calibrationValid || !EnsureAvatarLegScale(binding))
            {
                if (!calibrationValid)
                {
                    Reset();
                }
                else
                {
                    ApplyFilter(0f, verticalSettings.verticalResponse, deltaTime);
                }
                return Publish(false, false, false, 0f, 0f, false);
            }

            // Jump remains the exclusive positive-Y authority. Clear any stale crouch descent
            // immediately so takeoff/landing can never be pinned by grounded state.
            if (verticalSample.IsJumpActive)
            {
                _filteredRootOffsetY = 0f;
                _hasFilteredOffset = true;
                return Publish(true, false, false, 0f, 0f, false);
            }

            if (!verticalSample.referenceReady)
            {
                ApplyFilter(0f, verticalSettings.verticalResponse, deltaTime);
                return Publish(true, false, false, 0f, 0f, false);
            }

            // The interpreter intentionally holds its last semantic sample during its short tracking
            // grace interval. Mirror that hold without inventing new geometry. Once it publishes
            // Unavailable, recover toward standing instead.
            if (!verticalSample.isAvailable &&
                verticalSample.state != VerticalLocomotionState.Unavailable)
            {
                return Publish(
                    true,
                    _filteredRootOffsetY < -Epsilon,
                    true,
                    0f,
                    _filteredRootOffsetY,
                    false);
            }

            var target = 0f;
            var effectiveCompression = 0f;
            var depthClamped = false;
            if (verticalSample.groundedBendActive)
            {
                effectiveCompression = EffectiveCompression(
                    verticalSample.crouchCompression,
                    verticalSettings.crouchReleaseThreshold);
                target = CalculateAvatarRelativeRootOffset(
                    effectiveCompression,
                    _avatarStandingLegScale,
                    _settings.depthMultiplier,
                    _settings.maximumDepthFraction,
                    out depthClamped);
            }

            ApplyFilter(target, verticalSettings.verticalResponse, deltaTime);
            return Publish(
                true,
                target < -Epsilon || _filteredRootOffsetY < -Epsilon,
                false,
                effectiveCompression,
                target,
                depthClamped);
        }

        public static float EffectiveCompression(float crouchCompression, float crouchReleaseThreshold)
        {
            if (!IsFinite(crouchCompression) || !IsFinite(crouchReleaseThreshold))
            {
                return 0f;
            }

            var motionDeadband = Mathf.Clamp(
                Mathf.Max(0f, crouchReleaseThreshold) * 0.25f,
                0.01f,
                0.05f);
            return Mathf.Max(0f, crouchCompression - motionDeadband);
        }

        public static float CalculateAvatarRelativeRootOffset(
            float effectiveCompression,
            float avatarStandingLegScale,
            float depthMultiplier,
            float maximumDepthFraction,
            out bool depthClamped)
        {
            depthClamped = false;
            if (!IsFinite(effectiveCompression) ||
                !IsFinite(avatarStandingLegScale) ||
                !IsFinite(depthMultiplier) ||
                !IsFinite(maximumDepthFraction) ||
                effectiveCompression <= 0f ||
                avatarStandingLegScale <= Epsilon ||
                depthMultiplier <= 0f ||
                maximumDepthFraction <= 0f)
            {
                return 0f;
            }

            var requestedDepth = effectiveCompression * avatarStandingLegScale * depthMultiplier;
            var maximumDepth = avatarStandingLegScale * maximumDepthFraction;
            var depth = Mathf.Min(requestedDepth, maximumDepth);
            depthClamped = requestedDepth > maximumDepth + Epsilon;
            return -depth;
        }

        private bool EnsureAvatarLegScale(HumanoidRigBinding binding)
        {
            if (binding == null || !binding.IsBound)
            {
                ClearScaleReference();
                return false;
            }

            if (_binding == binding &&
                _referencePoseVersion == binding.ReferencePoseVersion &&
                _hasAvatarLegScale)
            {
                return true;
            }

            _binding = binding;
            _referencePoseVersion = binding.ReferencePoseVersion;
            _hasAvatarLegScale = TryGetStandingLegScale(
                    binding,
                    CanonicalKinematicChainId.LeftLeg,
                    out _leftStandingLegScale) &&
                TryGetStandingLegScale(
                    binding,
                    CanonicalKinematicChainId.RightLeg,
                    out _rightStandingLegScale);

            if (!_hasAvatarLegScale)
            {
                _leftStandingLegScale = 0f;
                _rightStandingLegScale = 0f;
                _avatarStandingLegScale = 0f;
                _filteredRootOffsetY = 0f;
                _hasFilteredOffset = true;
                return false;
            }

            _avatarStandingLegScale =
                (_leftStandingLegScale + _rightStandingLegScale) * 0.5f;
            if (!IsFinite(_avatarStandingLegScale) || _avatarStandingLegScale <= Epsilon)
            {
                ClearScaleReference();
                return false;
            }

            // A new binding/reference pose defines a new vertical scale session. Do not carry a
            // crouch offset from the previous avatar/reference into the new one.
            _filteredRootOffsetY = 0f;
            _hasFilteredOffset = true;
            return true;
        }

        private static bool TryGetStandingLegScale(
            HumanoidRigBinding binding,
            CanonicalKinematicChainId chainId,
            out float scale)
        {
            scale = 0f;
            if (binding == null || !binding.IsChainAvailable(chainId))
            {
                return false;
            }

            var reach = binding.GetChainTotalReach(chainId);
            if (!IsFinite(reach) || reach <= Epsilon)
            {
                return false;
            }

            if (binding.TryGetChainReference(
                    chainId,
                    out _,
                    out _,
                    out _,
                    out var rootToTipParentLocal,
                    out _) &&
                IsFinite(rootToTipParentLocal))
            {
                // Parent-reference local Y is the stable anatomical Up axis captured at bind time.
                // Prefer the actual standing hip-to-foot vertical span; fall back to direct span,
                // then authored two-segment reach for unusual rigs whose reference leg is not
                // predominantly vertical.
                var verticalSpan = Mathf.Abs(rootToTipParentLocal.y);
                if (verticalSpan > Epsilon)
                {
                    scale = Mathf.Min(verticalSpan, reach);
                    return true;
                }

                var directSpan = rootToTipParentLocal.magnitude;
                if (IsFinite(directSpan) && directSpan > Epsilon)
                {
                    scale = Mathf.Min(directSpan, reach);
                    return true;
                }
            }

            scale = reach;
            return true;
        }

        private void ApplyFilter(float target, float response, float deltaTime)
        {
            target = IsFinite(target) ? Mathf.Min(0f, target) : 0f;
            if (!_hasFilteredOffset)
            {
                _filteredRootOffsetY = target;
                _hasFilteredOffset = true;
                return;
            }

            var dt = Mathf.Max(0.0001f, deltaTime);
            var alpha = 1f - Mathf.Exp(-Mathf.Max(0.1f, response) * dt);
            _filteredRootOffsetY = Mathf.Lerp(_filteredRootOffsetY, target, alpha);
            if (Mathf.Abs(_filteredRootOffsetY) < Epsilon && Mathf.Abs(target) < Epsilon)
            {
                _filteredRootOffsetY = 0f;
            }
        }

        private AvatarRelativeCrouchGroundingSample Publish(
            bool hasScale,
            bool active,
            bool heldThroughGrace,
            float effectiveCompression,
            float primaryTargetY,
            bool depthClamped)
        {
            LatestSample = new AvatarRelativeCrouchGroundingSample
            {
                hasAvatarLegScale = hasScale,
                active = active,
                heldThroughTrackingGrace = heldThroughGrace,
                depthClamped = depthClamped,
                leftStandingLegScale = _leftStandingLegScale,
                rightStandingLegScale = _rightStandingLegScale,
                avatarStandingLegScale = _avatarStandingLegScale,
                effectiveCompression = effectiveCompression,
                primaryRootOffsetY = primaryTargetY,
                finalRootOffsetY = _filteredRootOffsetY,
                residualGroundCorrectionY = 0f,
            };
            return LatestSample;
        }

        private void ClearScaleReference()
        {
            _binding = null;
            _referencePoseVersion = -1;
            _hasAvatarLegScale = false;
            _leftStandingLegScale = 0f;
            _rightStandingLegScale = 0f;
            _avatarStandingLegScale = 0f;
            _filteredRootOffsetY = 0f;
            _hasFilteredOffset = true;
            LatestSample = default;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
