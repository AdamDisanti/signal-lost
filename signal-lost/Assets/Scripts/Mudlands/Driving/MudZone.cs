using System.Collections.Generic;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// A patch of thick mud or bog water (an ellipse on the ground). On foot it slows the player (<see cref="MudlandsPlayerSlow"/>);
    /// the car loses grip and speed in mud and stalls in bog water. Short-lived patches (the otter's mud balls, the
    /// biker boss's spray) are spawned with <see cref="Spawn"/>. Registered in a static list so lookups are cheap.
    /// </summary>
    public sealed class MudZone : MonoBehaviour
    {
        public enum Kind { Mud, BogWater }

        private static readonly List<MudZone> Active = new List<MudZone>();

        [SerializeField] private Kind kind = Kind.Mud;
        [Tooltip("Half-widths along local x and z.")]
        [SerializeField] private Vector2 radii = new Vector2(6f, 4f);
        [Tooltip("On-foot slowdown inside (0 to 1).")]
        [SerializeField, Range(0f, 0.9f)] private float footSlow = 0.45f;

        private float dieAt = float.PositiveInfinity, born;

        public Kind Type => kind;
        public float FootSlow => footSlow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public void Configure(Kind type, Vector2 halfWidths, float slow)
        {
            kind = type;
            radii = halfWidths;
            footSlow = slow;
        }

        /// <summary>A temporary round mud patch with a dark splat on the ground.</summary>
        public static MudZone Spawn(Vector3 point, float radius, float seconds, float slow = 0.4f)
        {
            var go = new GameObject("Mud Splat");
            go.transform.position = point + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var zone = go.AddComponent<MudZone>();
            zone.Configure(Kind.Mud, new Vector2(radius, radius * Random.Range(0.75f, 1f)), slow);
            zone.dieAt = Time.time + seconds;
            zone.born = Time.time;
            go.transform.localScale = Vector3.one * 0.05f;
            MudArt.Part(go.transform, "Splat", Vector3.zero, Quaternion.identity, "mud_splat_disc", k =>
            {
                const int sides = 11;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    float r0 = 0.82f + 0.18f * Mathf.Sin(i * 2.7f), r1 = 0.82f + 0.18f * Mathf.Sin((i + 1) * 2.7f);
                    k.Tri(Vector3.zero, new Vector3(Mathf.Cos(a1) * r1, 0f, Mathf.Sin(a1) * r1), new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0), MudPalette.WetMud);
                }
            }, false).localScale = new Vector3(zone.radii.x, 1f, zone.radii.y);
            return zone;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (float.IsPositiveInfinity(dieAt)) return;
            float left = dieAt - Time.time;
            if (left <= 0f) { Destroy(gameObject); return; }
            float grow = Mathf.Clamp01((Time.time - born) / 0.2f);
            transform.localScale = Vector3.one * Mathf.Max(0.05f, Mathf.Min(grow, Mathf.Clamp01(left / 0.6f)));
        }

        /// <summary>How deep into the patch a point is: 0 outside, 1 at the middle.</summary>
        public float Inside(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point); // the zone's scale (a splat growing or fading) is included
            float e = (local.x * local.x) / (radii.x * radii.x) + (local.z * local.z) / (radii.y * radii.y);
            if (Mathf.Abs(point.y - transform.position.y) > 3f) return 0f;
            return e >= 1f ? 0f : Mathf.Clamp01((1f - e) * 3f);
        }

        /// <summary>The strongest mud/water effect at a point: (kind, strength 0-1, foot slow at that strength).</summary>
        public static (Kind kind, float strength, float slow) At(Vector3 point)
        {
            Kind best = Kind.Mud;
            float strength = 0f, slow = 0f;
            foreach (MudZone zone in Active)
            {
                if (zone == null) continue;
                float inside = zone.Inside(point);
                if (inside <= 0f) continue;
                float zoneSlow = zone.footSlow * inside;
                if (zoneSlow > slow) slow = zoneSlow;
                if (inside > strength || (zone.kind == Kind.BogWater && inside > 0.5f))
                {
                    strength = inside;
                    best = zone.kind;
                }
            }
            return (best, strength, slow);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Mud ? new Color(0.5f, 0.3f, 0.1f, 0.8f) : new Color(0.2f, 0.6f, 0.7f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Vector3 previous = new Vector3(radii.x, 0.1f, 0f);
            for (int i = 1; i <= 32; i++)
            {
                float a = i * Mathf.PI * 2f / 32f;
                var next = new Vector3(Mathf.Cos(a) * radii.x, 0.1f, Mathf.Sin(a) * radii.y);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }

    /// <summary>
    /// Slows the player on foot without touching FirstPersonController (the same trick as the desert's
    /// PlayerSlowReceiver): after the player moves each frame, part of that move is undone. Sources: mud and bog
    /// water (not with Swamp Waders), the chug (half speed), and timed slows such as the witch's spores.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MudlandsPlayerSlow : MonoBehaviour
    {
        private CharacterController body;
        private Vector3 last;
        private bool hasLast;
        private float timedSlow, timedUntil;

        /// <summary>Swamp Waders: mud and bog water no longer slow the player.</summary>
        public bool Waders { get; set; }
        /// <summary>An extra slow set by other systems each frame (the chug).</summary>
        public float ExtraSlow { get; set; }
        public float CurrentSlow { get; private set; }

        public static MudlandsPlayerSlow Ensure(GameObject player)
        {
            var slow = player.GetComponent<MudlandsPlayerSlow>();
            return slow != null ? slow : player.AddComponent<MudlandsPlayerSlow>();
        }

        /// <summary>Slows the player by <paramref name="amount"/> (0-1) for a few seconds (spore clouds, mud in the face).</summary>
        public void SlowFor(float amount, float seconds)
        {
            if (Time.time >= timedUntil || amount >= timedSlow) timedSlow = amount;
            timedUntil = Mathf.Max(timedUntil, Time.time + seconds);
        }

        private void Awake() => body = GetComponent<CharacterController>();

        private void OnDisable() => hasLast = false;

        private void LateUpdate()
        {
            if (body == null || !body.enabled)
            {
                hasLast = false;
                CurrentSlow = 0f;
                return;
            }
            Vector3 now = transform.position;
            float slow = Waders ? 0f : MudZone.At(now).slow;
            if (Time.time < timedUntil) slow = Mathf.Max(slow, timedSlow);
            slow = Mathf.Max(slow, ExtraSlow);
            CurrentSlow = slow;
            if (hasLast && slow > 0f)
            {
                Vector3 moved = now - last;
                moved.y = 0f;
                if (moved.sqrMagnitude < 4f) body.Move(-moved * Mathf.Clamp01(slow));
            }
            last = transform.position;
            hasLast = true;
        }
    }
}
