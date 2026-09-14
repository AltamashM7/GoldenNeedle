using System;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    public enum CameraFocusSemantic
    {
        AvatarBody = 0,
        Hands = 1,
        LeftHand = 2,
        RightHand = 3,
    }

    [Serializable]
    public sealed class CameraViewPreset
    {
        [Tooltip("Unique user-facing preset name used by commands and speech parameters.")]
        public string presetName = "Back";

        [Tooltip("Semantic avatar target used as the base camera focus point.")]
        public CameraFocusSemantic focusSemantic = CameraFocusSemantic.AvatarBody;

        [Tooltip("Horizontal placement direction in the retained body-heading frame: X=avatar right, Y=avatar forward.")]
        public Vector2 placementDirection = new Vector2(0f, -1f);

        [Tooltip("Distance from the resolved focus point along Placement Direction.")]
        [Min(0f)] public float distance = 4f;

        [Tooltip("World-up camera height added after heading-relative placement.")]
        public float height = 2.2f;

        [Tooltip("Fine placement offset in heading-local coordinates: X=right, Y=up, Z=forward.")]
        public Vector3 placementOffset = Vector3.zero;

        [Tooltip("Look-target offset in heading-local coordinates: X=right, Y=up, Z=forward.")]
        public Vector3 focusOffset = new Vector3(0f, 1.15f, 0f);

        [Range(30f, 90f)] public float fieldOfView = 55f;
        [Min(0.1f)] public float positionResponse = 7f;
        [Min(0.1f)] public float orientationResponse = 7f;
        [Min(0.1f)] public float fieldOfViewResponse = 7f;

        public void Sanitize()
        {
            presetName = (presetName ?? string.Empty).Trim();
            if (!CameraViewPresetMath.IsFinite(placementDirection))
            {
                placementDirection = Vector2.zero;
            }
            if (!CameraViewPresetMath.IsFinite(placementOffset))
            {
                placementOffset = Vector3.zero;
            }
            if (!CameraViewPresetMath.IsFinite(focusOffset))
            {
                focusOffset = Vector3.zero;
            }

            distance = CameraViewPresetMath.SanitizeNonNegative(distance, 0f);
            height = CameraViewPresetMath.SanitizeFinite(height, 0f);
            fieldOfView = Mathf.Clamp(CameraViewPresetMath.SanitizeFinite(fieldOfView, 55f), 30f, 90f);
            positionResponse = Mathf.Max(0.1f, CameraViewPresetMath.SanitizeFinite(positionResponse, 7f));
            orientationResponse = Mathf.Max(0.1f, CameraViewPresetMath.SanitizeFinite(orientationResponse, 7f));
            fieldOfViewResponse = Mathf.Max(0.1f, CameraViewPresetMath.SanitizeFinite(fieldOfViewResponse, 7f));
        }

        public static CameraViewPreset[] CreateDefaults()
        {
            return CreateDefaults(4f, 2.2f, 1.15f, 7f, 55f);
        }

        public static CameraViewPreset[] CreateDefaults(
            float legacyBackDistance,
            float legacyBackHeight,
            float legacyBackLookHeight,
            float legacyBackResponse,
            float legacyBackFieldOfView)
        {
            return new[]
            {
                Create(
                    "Back",
                    CameraFocusSemantic.AvatarBody,
                    new Vector2(0f, -1f),
                    legacyBackDistance,
                    legacyBackHeight,
                    new Vector3(0f, legacyBackLookHeight, 0f),
                    legacyBackFieldOfView,
                    legacyBackResponse,
                    legacyBackResponse,
                    legacyBackResponse),
                Create(
                    "Front",
                    CameraFocusSemantic.AvatarBody,
                    new Vector2(0f, 1f),
                    4f,
                    2.2f,
                    new Vector3(0f, 1.15f, 0f),
                    55f,
                    7f,
                    7f,
                    7f),
                Create(
                    "Left",
                    CameraFocusSemantic.AvatarBody,
                    new Vector2(-1f, 0f),
                    4f,
                    2.2f,
                    new Vector3(0f, 1.15f, 0f),
                    55f,
                    7f,
                    7f,
                    7f),
                Create(
                    "Right",
                    CameraFocusSemantic.AvatarBody,
                    new Vector2(1f, 0f),
                    4f,
                    2.2f,
                    new Vector3(0f, 1.15f, 0f),
                    55f,
                    7f,
                    7f,
                    7f),
                Create(
                    "FullBody",
                    CameraFocusSemantic.AvatarBody,
                    new Vector2(0f, -1f),
                    5.2f,
                    2.4f,
                    new Vector3(0f, 1.05f, 0f),
                    60f,
                    6f,
                    6f,
                    6f),
                Create(
                    "Hands",
                    CameraFocusSemantic.Hands,
                    new Vector2(0f, -1f),
                    2.4f,
                    0.35f,
                    Vector3.zero,
                    45f,
                    8f,
                    8f,
                    8f),
                Create(
                    "LeftHand",
                    CameraFocusSemantic.LeftHand,
                    new Vector2(0f, -1f),
                    1.7f,
                    0.25f,
                    Vector3.zero,
                    40f,
                    9f,
                    9f,
                    9f),
                Create(
                    "RightHand",
                    CameraFocusSemantic.RightHand,
                    new Vector2(0f, -1f),
                    1.7f,
                    0.25f,
                    Vector3.zero,
                    40f,
                    9f,
                    9f,
                    9f),
            };
        }

        private static CameraViewPreset Create(
            string name,
            CameraFocusSemantic focus,
            Vector2 placementDirection,
            float distance,
            float height,
            Vector3 focusOffset,
            float fieldOfView,
            float positionResponse,
            float orientationResponse,
            float fieldOfViewResponse)
        {
            return new CameraViewPreset
            {
                presetName = name,
                focusSemantic = focus,
                placementDirection = placementDirection,
                distance = distance,
                height = height,
                placementOffset = Vector3.zero,
                focusOffset = focusOffset,
                fieldOfView = fieldOfView,
                positionResponse = positionResponse,
                orientationResponse = orientationResponse,
                fieldOfViewResponse = fieldOfViewResponse,
            };
        }
    }

    public struct CameraFocusCandidates
    {
        public bool hasAvatarRoot;
        public Vector3 avatarRoot;
        public bool hasChest;
        public Vector3 chest;
        public bool hasPelvis;
        public Vector3 pelvis;
        public bool hasLeftLowerArm;
        public Vector3 leftLowerArm;
        public bool hasRightLowerArm;
        public Vector3 rightLowerArm;
        public bool hasLeftHand;
        public Vector3 leftHand;
        public bool hasRightHand;
        public Vector3 rightHand;
    }

    public readonly struct CameraFocusResolution
    {
        public CameraFocusResolution(
            CameraFocusSemantic requestedSemantic,
            Vector3 position,
            string resolvedTarget,
            bool usedFallback)
        {
            RequestedSemantic = requestedSemantic;
            Position = position;
            ResolvedTarget = resolvedTarget ?? string.Empty;
            UsedFallback = usedFallback;
        }

        public CameraFocusSemantic RequestedSemantic { get; }
        public Vector3 Position { get; }
        public string ResolvedTarget { get; }
        public bool UsedFallback { get; }
    }

    public static class CameraViewPresetMath
    {
        private const float Epsilon = 0.000001f;

        public static bool TryFindUniquePreset(
            CameraViewPreset[] presets,
            string requestedName,
            out CameraViewPreset preset,
            out int index,
            out string failureReason)
        {
            preset = null;
            index = -1;
            failureReason = string.Empty;
            var normalized = (requestedName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                failureReason = "Camera preset name is blank";
                return false;
            }
            if (presets == null || presets.Length == 0)
            {
                failureReason = "No camera presets are configured";
                return false;
            }

            var matchCount = 0;
            for (var i = 0; i < presets.Length; i++)
            {
                var candidate = presets[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.presetName))
                {
                    continue;
                }
                if (!string.Equals(candidate.presetName.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchCount++;
                preset = candidate;
                index = i;
            }

            if (matchCount == 1)
            {
                return true;
            }

            preset = null;
            index = -1;
            failureReason = matchCount > 1
                ? $"Camera preset name is ambiguous: {normalized}"
                : $"Unknown camera preset: {normalized}";
            return false;
        }

        public static bool TryResolveFocus(
            CameraFocusSemantic semantic,
            CameraFocusCandidates candidates,
            out CameraFocusResolution resolution)
        {
            resolution = default;
            switch (semantic)
            {
                case CameraFocusSemantic.AvatarBody:
                    if (TryCandidate(candidates.hasAvatarRoot, candidates.avatarRoot, semantic, "AvatarRoot", false, out resolution))
                    {
                        return true;
                    }
                    if (TryCandidate(candidates.hasChest, candidates.chest, semantic, "Chest", true, out resolution))
                    {
                        return true;
                    }
                    return TryCandidate(candidates.hasPelvis, candidates.pelvis, semantic, "Pelvis", true, out resolution);

                case CameraFocusSemantic.Hands:
                    if (HasFinite(candidates.hasLeftHand, candidates.leftHand) &&
                        HasFinite(candidates.hasRightHand, candidates.rightHand))
                    {
                        resolution = new CameraFocusResolution(
                            semantic,
                            (candidates.leftHand + candidates.rightHand) * 0.5f,
                            "BothHands",
                            false);
                        return true;
                    }
                    if (TryCandidate(candidates.hasLeftHand, candidates.leftHand, semantic, "LeftHandOnly", true, out resolution))
                    {
                        return true;
                    }
                    if (TryCandidate(candidates.hasRightHand, candidates.rightHand, semantic, "RightHandOnly", true, out resolution))
                    {
                        return true;
                    }
                    return TryBodyFallback(semantic, candidates, out resolution);

                case CameraFocusSemantic.LeftHand:
                    if (TryCandidate(candidates.hasLeftHand, candidates.leftHand, semantic, "LeftHand", false, out resolution))
                    {
                        return true;
                    }
                    if (TryCandidate(candidates.hasLeftLowerArm, candidates.leftLowerArm, semantic, "LeftLowerArm", true, out resolution))
                    {
                        return true;
                    }
                    return TryBodyFallback(semantic, candidates, out resolution);

                case CameraFocusSemantic.RightHand:
                    if (TryCandidate(candidates.hasRightHand, candidates.rightHand, semantic, "RightHand", false, out resolution))
                    {
                        return true;
                    }
                    if (TryCandidate(candidates.hasRightLowerArm, candidates.rightLowerArm, semantic, "RightLowerArm", true, out resolution))
                    {
                        return true;
                    }
                    return TryBodyFallback(semantic, candidates, out resolution);

                default:
                    return false;
            }
        }

        public static bool TryCalculateView(
            Vector2 retainedHeadingXZ,
            Vector3 focusPosition,
            CameraViewPreset preset,
            out Vector3 targetPosition,
            out Vector3 lookTarget)
        {
            targetPosition = Vector3.zero;
            lookTarget = Vector3.zero;
            if (preset == null ||
                !IsFinite(retainedHeadingXZ) ||
                retainedHeadingXZ.sqrMagnitude <= Epsilon ||
                !IsFinite(focusPosition))
            {
                return false;
            }

            var headingMagnitude = Mathf.Sqrt(retainedHeadingXZ.sqrMagnitude);
            var forward = new Vector3(
                retainedHeadingXZ.x / headingMagnitude,
                0f,
                retainedHeadingXZ.y / headingMagnitude);
            var right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude <= Epsilon)
            {
                return false;
            }
            right = right.normalized;

            var orbitDirection =
                right * preset.placementDirection.x +
                forward * preset.placementDirection.y;
            if (orbitDirection.sqrMagnitude > Epsilon)
            {
                orbitDirection = orbitDirection.normalized;
            }
            else
            {
                orbitDirection = Vector3.zero;
            }

            targetPosition =
                focusPosition +
                orbitDirection * Mathf.Max(0f, preset.distance) +
                Vector3.up * preset.height +
                HeadingLocalToWorld(preset.placementOffset, right, forward);
            lookTarget =
                focusPosition +
                HeadingLocalToWorld(preset.focusOffset, right, forward);
            return IsFinite(targetPosition) && IsFinite(lookTarget);
        }

        public static float ResponseAlpha(float response, float deltaTime)
        {
            var safeResponse = Mathf.Max(0.1f, SanitizeFinite(response, 0.1f));
            var safeDeltaTime = Mathf.Max(0f, SanitizeFinite(deltaTime, 0f));
            return 1f - Mathf.Exp(-safeResponse * safeDeltaTime);
        }

        public static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        public static float SanitizeNonNegative(float value, float fallback)
        {
            return Mathf.Max(0f, SanitizeFinite(value, fallback));
        }

        public static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector3 HeadingLocalToWorld(Vector3 local, Vector3 right, Vector3 forward)
        {
            return right * local.x + Vector3.up * local.y + forward * local.z;
        }

        private static bool TryBodyFallback(
            CameraFocusSemantic semantic,
            CameraFocusCandidates candidates,
            out CameraFocusResolution resolution)
        {
            if (TryCandidate(candidates.hasChest, candidates.chest, semantic, "Chest", true, out resolution))
            {
                return true;
            }
            if (TryCandidate(candidates.hasPelvis, candidates.pelvis, semantic, "Pelvis", true, out resolution))
            {
                return true;
            }
            return TryCandidate(candidates.hasAvatarRoot, candidates.avatarRoot, semantic, "AvatarRoot", true, out resolution);
        }

        private static bool TryCandidate(
            bool available,
            Vector3 position,
            CameraFocusSemantic semantic,
            string resolvedTarget,
            bool usedFallback,
            out CameraFocusResolution resolution)
        {
            if (!HasFinite(available, position))
            {
                resolution = default;
                return false;
            }

            resolution = new CameraFocusResolution(
                semantic,
                position,
                resolvedTarget,
                usedFallback);
            return true;
        }

        private static bool HasFinite(bool available, Vector3 position)
        {
            return available && IsFinite(position);
        }
    }
}
