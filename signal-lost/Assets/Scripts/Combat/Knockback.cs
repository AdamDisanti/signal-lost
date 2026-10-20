using System;
using UnityEngine;

namespace SignalLost.Combat
{
    public enum KnockbackMode { None, Shove, Launch }

    /// <summary>Per-attack velocity in metres/second; None keeps existing damage-only calls unchanged.</summary>
    [Serializable]
    public struct Knockback
    {
        public KnockbackMode mode;
        [Min(0f)] public float speed;
        [Min(0f)] public float upwardSpeed;
        [Min(0.1f)] public float deceleration;

        public Knockback(KnockbackMode mode, float speed, float upwardSpeed = 0f, float deceleration = 18f)
        {
            this.mode = mode;
            this.speed = speed;
            this.upwardSpeed = upwardSpeed;
            this.deceleration = deceleration;
        }

        public Vector3 Velocity(Vector3 incomingDirection)
        {
            if (mode == KnockbackMode.None || !Finite(speed) || !Finite(upwardSpeed) ||
                !Finite(deceleration) || deceleration <= 0f || !Finite(incomingDirection.sqrMagnitude))
                return Vector3.zero;
            incomingDirection.y = 0f;
            return incomingDirection.normalized * Mathf.Max(0f, speed) +
                Vector3.up * (mode == KnockbackMode.Launch ? Mathf.Max(0f, upwardSpeed) : 0f);
        }

        public static Vector3 SlideAlongWall(Vector3 velocity, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f) return velocity;
            normal.y = 0f;
            normal.Normalize();
            // Contact behind a departing target must not cancel an outward impulse.
            return Vector3.Dot(velocity, normal) < 0f ? Vector3.ProjectOnPlane(velocity, normal) : velocity;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
