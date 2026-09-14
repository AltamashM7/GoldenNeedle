using System;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MinAttribute : Attribute
    {
        public MinAttribute(float min) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);
        public float sqrMagnitude => x * x + y * y;
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public float sqrMagnitude => x * x + y * y + z * z;

        public Vector3 normalized
        {
            get
            {
                var magnitude = MathF.Sqrt(sqrMagnitude);
                return magnitude > 0.000001f ? this / magnitude : zero;
            }
        }

        public static Vector3 Cross(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(
                lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.x * rhs.y - lhs.y * rhs.x);
        }

        public static Vector3 operator +(Vector3 lhs, Vector3 rhs) =>
            new Vector3(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);

        public static Vector3 operator -(Vector3 lhs, Vector3 rhs) =>
            new Vector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);

        public static Vector3 operator *(Vector3 value, float scalar) =>
            new Vector3(value.x * scalar, value.y * scalar, value.z * scalar);

        public static Vector3 operator /(Vector3 value, float scalar) =>
            new Vector3(value.x / scalar, value.y / scalar, value.z / scalar);
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Clamp(float value, float min, float max) => MathF.Min(MathF.Max(value, min), max);
        public static float Sqrt(float value) => MathF.Sqrt(value);
        public static float Exp(float power) => MathF.Exp(power);
    }
}
