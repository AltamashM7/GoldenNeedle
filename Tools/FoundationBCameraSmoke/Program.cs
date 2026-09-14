using System;
using System.Linq;
using GoldenNeedle.Debug.PoseTrackingSpike;
using UnityEngine;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var presets = CameraViewPreset.CreateDefaults();
            var names = presets.Select(preset => preset.presetName).OrderBy(name => name).ToArray();
            Equal(
                "Back,Front,FullBody,Hands,Left,LeftHand,Right,RightHand",
                string.Join(',', names),
                "required preset names");

            True(
                CameraViewPresetMath.TryFindUniquePreset(
                    presets,
                    "  lEfT hAnD  ",
                    out var leftHand,
                    out _,
                    out _),
                "case-insensitive preset lookup");
            Equal("LeftHand", leftHand.presetName, "preset canonical name");

            var duplicate = new[]
            {
                new CameraViewPreset { presetName = "Hands" },
                new CameraViewPreset { presetName = " hands " },
            };
            True(
                !CameraViewPresetMath.TryFindUniquePreset(
                    duplicate,
                    "Hands",
                    out _,
                    out _,
                    out var duplicateReason) &&
                duplicateReason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase),
                "duplicate preset rejected");
            True(
                !CameraViewPresetMath.TryFindUniquePreset(
                    presets,
                    "   ",
                    out _,
                    out _,
                    out _),
                "blank preset rejected");

            var back = Find(presets, "Back");
            True(
                CameraViewPresetMath.TryCalculateView(
                    new Vector2(0f, 1f),
                    new Vector3(1f, 0f, 2f),
                    back,
                    out var backPosition,
                    out var backLook),
                "Back geometry resolves");
            Vector(backPosition, new Vector3(1f, 2.2f, -2f), "Back position baseline");
            Vector(backLook, new Vector3(1f, 1.15f, 2f), "Back look baseline");

            var root = new Vector3(10f, 0f, 20f);
            Position(presets, "Front", root, new Vector3(10f, 2.2f, 24f));
            Position(presets, "Left", root, new Vector3(6f, 2.2f, 20f));
            Position(presets, "Right", root, new Vector3(14f, 2.2f, 20f));

            var bothHands = new CameraFocusCandidates
            {
                hasLeftHand = true,
                leftHand = new Vector3(-1f, 2f, 3f),
                hasRightHand = true,
                rightHand = new Vector3(1f, 4f, 5f),
            };
            True(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.Hands,
                    bothHands,
                    out var bothResolution),
                "both-hands focus resolves");
            Vector(bothResolution.Position, new Vector3(0f, 3f, 4f), "both-hands midpoint");
            True(!bothResolution.UsedFallback, "both hands is preferred focus");

            var oneHand = new CameraFocusCandidates
            {
                hasRightHand = true,
                rightHand = new Vector3(1f, 2f, 3f),
            };
            True(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.Hands,
                    oneHand,
                    out var oneResolution),
                "single-hand focus resolves");
            Equal("RightHandOnly", oneResolution.ResolvedTarget, "single-hand fallback target");
            True(oneResolution.UsedFallback, "single hand is marked fallback");

            var bodyFallback = new CameraFocusCandidates
            {
                hasChest = true,
                chest = new Vector3(0f, 1.4f, 0f),
                hasAvatarRoot = true,
                avatarRoot = Vector3.zero,
            };
            True(
                CameraViewPresetMath.TryResolveFocus(
                    CameraFocusSemantic.LeftHand,
                    bodyFallback,
                    out var bodyResolution),
                "missing hand body fallback resolves");
            Equal("Chest", bodyResolution.ResolvedTarget, "body fallback target");

            var half = CameraViewPresetMath.ResponseAlpha(7f, 0.01f);
            var full = CameraViewPresetMath.ResponseAlpha(7f, 0.02f);
            Near(1f - (1f - half) * (1f - half), full, 0.000001f, "exponential response composition");

            Console.WriteLine("FOUNDATION_B_CAMERA_SMOKE=PASS");
            Console.WriteLine("DEFAULT_PRESETS_AND_LOOKUP=PASS");
            Console.WriteLine("HEADING_RELATIVE_GEOMETRY=PASS");
            Console.WriteLine("FOCUS_FALLBACK_POLICY=PASS");
            Console.WriteLine("EXPONENTIAL_RESPONSE=PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static CameraViewPreset Find(CameraViewPreset[] presets, string name)
    {
        True(
            CameraViewPresetMath.TryFindUniquePreset(
                presets,
                name,
                out var preset,
                out _,
                out var reason),
            reason);
        return preset;
    }

    private static void Position(
        CameraViewPreset[] presets,
        string name,
        Vector3 focus,
        Vector3 expected)
    {
        True(
            CameraViewPresetMath.TryCalculateView(
                new Vector2(0f, 1f),
                focus,
                Find(presets, name),
                out var position,
                out _),
            $"{name} geometry resolves");
        Vector(position, expected, $"{name} heading-relative position");
    }

    private static void True(bool value, string label)
    {
        if (!value)
        {
            throw new InvalidOperationException($"FAILED: {label}");
        }
    }

    private static void Equal(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"FAILED: {label}; expected='{expected}' actual='{actual}'");
        }
    }

    private static void Near(float actual, float expected, float tolerance, string label)
    {
        if (MathF.Abs(actual - expected) > tolerance)
        {
            throw new InvalidOperationException(
                $"FAILED: {label}; expected={expected} actual={actual}");
        }
    }

    private static void Vector(Vector3 actual, Vector3 expected, string label)
    {
        Near(actual.x, expected.x, 0.0001f, label + " x");
        Near(actual.y, expected.y, 0.0001f, label + " y");
        Near(actual.z, expected.z, 0.0001f, label + " z");
    }
}
