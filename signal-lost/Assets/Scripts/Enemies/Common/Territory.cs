using System;
using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// The fighting-radius rule every enemy follows (meeting notes 10-22, the baseline unless an enemy needs
    /// something special):
    /// <list type="bullet">
    /// <item>It attacks only when the player is inside its fight radius; outside, it ignores them even in plain sight
    /// (being hit still provokes it).</item>
    /// <item>Once fighting, it chases until the player is <see cref="LeashMultiplier"/> times the radius from the
    /// centre, then walks home and heals to full, so the player cannot drag the whole map along or chip it down
    /// with hit-and-run.</item>
    /// </list>
    /// The centre is a shared <see cref="CombatZone"/> if one is assigned, otherwise where the enemy started.
    /// Every enemy AI owns one of these as a serialized field named "territory".
    /// </summary>
    [Serializable]
    public sealed class Territory
    {
        public const float LeashMultiplier = 2f;

        [Tooltip("Optional shared area (for a group of enemies). Empty: a circle around where this enemy starts.")]
        [SerializeField] private CombatZone zone;
        [Tooltip("Used when no zone is assigned: it attacks anyone this close to where it started.")]
        [SerializeField, Min(1f)] private float fightRadius = 12f;

        private Vector3 origin;

        public Territory(float radius) => fightRadius = radius;

        public CombatZone Zone
        {
            get => zone;
            set => zone = value;
        }

        public Vector3 Centre => zone != null ? zone.Centre : origin;
        public float Radius => zone != null ? zone.Radius : fightRadius;
        public float LeashRadius => Radius * LeashMultiplier;

        /// <summary>Call once the enemy knows where home is (after it has been placed on the NavMesh).</summary>
        public void SetHome(Vector3 home) => origin = home;

        /// <summary>True when the player is close enough for this enemy to start fighting.</summary>
        public bool Contains(Vector3 position) => FlatDistance(position) <= Radius;

        /// <summary>True when the player has run far enough away that the enemy gives up and goes home.</summary>
        public bool Escaped(Vector3 position) => FlatDistance(position) > LeashRadius;

        private float FlatDistance(Vector3 position)
        {
            Vector3 offset = position - Centre;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>Scene-view gizmo: the fight radius, and a fainter ring where the chase ends.</summary>
        public void DrawGizmos(Vector3 fallbackCentre)
        {
            if (zone != null) return; // the zone draws itself
            DrawRings(Application.isPlaying ? origin : fallbackCentre, fightRadius, new Color(1f, 0.35f, 0.2f, 0.7f));
        }

        internal static void DrawRings(Vector3 centre, float radius, Color color)
        {
            Gizmos.color = color;
            Ring(centre, radius);
            Gizmos.color = new Color(color.r, color.g, color.b, color.a * 0.35f);
            Ring(centre, radius * LeashMultiplier);
        }

        private static void Ring(Vector3 centre, float radius)
        {
            const int segments = 48;
            Vector3 previous = centre + new Vector3(radius, 0.1f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = centre + new Vector3(Mathf.Cos(angle) * radius, 0.1f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
