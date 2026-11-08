using System;
using System.Collections;
using SignalLost.Combat;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.World
{
    /// <summary>
    /// Keeps the player out of deep water (they can't swim). While they walk around it remembers the last dry ground
    /// they stood on. If their feet go deeper than the <see cref="Ocean"/>'s wade depth, the screen fades to black,
    /// they lose <see cref="Ocean.DrownDamage"/> health and they are put back on the nearest dry land that connects to
    /// where they came from, facing inland, before the screen fades back in. If that damage kills them, the normal
    /// death and respawn takes over. Added to the player by <see cref="Ocean"/>; idle while the player's movement is
    /// switched off (cutscenes, respawning).
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class DeepWaterGuard : MonoBehaviour
    {
        private const float FadeOut = 0.5f, Hold = 0.35f, FadeIn = 0.6f;
        private const float DryMargin = 0.3f;     // land must be this far above the sea
        private const float SearchRadius = 60f;

        private FirstPersonController movement;
        private CharacterController body;
        private Health health;
        private PlayerGear gear;
        private PlayerLife life;
        private Vector3 lastLand;
        private bool haveLand;
        private float nextSample;
        private NavMeshPath path;

        /// <summary>True from the moment the player is caught in deep water until they are back on land.</summary>
        public bool Rescuing { get; private set; }

        /// <summary>Raised when the player has been put back on land (not when the damage killed them).</summary>
        public event Action<Vector3> Rescued;

        public bool HasSafeLand => haveLand;
        public Vector3 LastSafeLand => lastLand;

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            gear = GetComponent<PlayerGear>();
            life = GetComponent<PlayerLife>();
        }

        private void Update()
        {
            Ocean ocean = Ocean.Current;
            if (ocean == null || Rescuing || !movement.enabled || (life != null && life.IsRespawning) || (health != null && health.IsDead)) return;
            if (SignalLost.Core.CheatMode.Enabled) return; // flying for testing: anywhere goes
            Vector3 feet = Feet();
            if (ocean.IsTooDeep(feet))
            {
                StartCoroutine(Rescue(ocean));
                return;
            }
            if (Time.time < nextSample || !movement.IsGrounded) return;
            nextSample = Time.time + 0.2f;
            if (DrySpot(ocean, feet, 1f, out Vector3 spot))
            {
                lastLand = spot;
                haveLand = true;
            }
        }

        private Vector3 Feet()
        {
            Vector3 position = transform.position;
            return body != null && body.enabled ? new Vector3(position.x, body.bounds.min.y, position.z) : position;
        }

        private IEnumerator Rescue(Ocean ocean)
        {
            Rescuing = true;
            Vector3 caughtAt = transform.position;
            gear?.Holster(this);
            movement.enabled = false;
            ScreenFade fade = ScreenFade.Instance;
            fade.SetCaption("TOO DEEP", "You can't swim. Back to dry land.");
            yield return fade.FadeTo(1f, FadeOut);

            if (health != null && ocean.DrownDamage > 0f) health.TakeDamage(ocean.DrownDamage, ocean.gameObject);
            if (health != null && health.IsDead)
            {
                // PlayerLife takes it from here with its own fade and respawn; just get out of its way.
                fade.SetCaption("");
                gear?.Unholster(this);
                yield return fade.FadeTo(0f, 0.3f);
                Rescuing = false;
                yield break;
            }

            Vector3 land = NearestLand(ocean, caughtAt);
            Vector3 inland = land - caughtAt;
            inland.y = 0f;
            if (inland.sqrMagnitude < 0.25f) inland = ocean.IslandCentre - land;
            inland.y = 0f;
            Quaternion facing = inland.sqrMagnitude > 0.01f ? Quaternion.LookRotation(inland.normalized) : transform.rotation;
            if (body != null) body.enabled = false;
            transform.SetPositionAndRotation(land + Vector3.up * 0.05f, facing);
            if (body != null) body.enabled = true;
            movement.ResetMotion();
            lastLand = land;
            haveLand = true;

            yield return new WaitForSecondsRealtime(Hold);
            movement.enabled = true;
            gear?.Unholster(this);
            yield return fade.FadeTo(0f, FadeIn);
            fade.SetCaption("");
            Rescuing = false;
            Rescued?.Invoke(land);
        }

        /// <summary>
        /// The nearest dry NavMesh ground to <paramref name="from"/> that the player can walk back from to where they
        /// last stood on land (so not a rock top or an unreachable ledge); else that last spot; else the respawn point.
        /// </summary>
        public Vector3 NearestLand(Ocean ocean, Vector3 from)
        {
            for (float ring = 2f; ring <= SearchRadius; ring += 3f)
            {
                int tries = Mathf.Clamp(Mathf.RoundToInt(ring * 1.2f), 8, 48);
                bool found = false;
                Vector3 best = default;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < tries; i++)
                {
                    float angle = i * Mathf.PI * 2f / tries;
                    Vector3 probe = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                    probe.y = ocean.SeaLevel + 2f;
                    if (!DrySpot(ocean, probe, 6f, out Vector3 spot) || !Connected(spot)) continue;
                    float distance = (spot - from).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = spot;
                    found = true;
                }
                if (found) return best;
            }
            if (haveLand) return lastLand;
            PlayerSpawnPoint spawn = PlayerSpawnPoint.Current;
            return spawn != null ? spawn.transform.position : ocean.IslandCentre;
        }

        private static bool DrySpot(Ocean ocean, Vector3 near, float reach, out Vector3 spot)
        {
            spot = near;
            if (NavMesh.SamplePosition(near, out NavMeshHit hit, reach, NavMesh.AllAreas))
            {
                spot = hit.position;
                return spot.y > ocean.SeaLevel + DryMargin || !ocean.IsSea(spot.x, spot.z);
            }
            // A scene without a NavMesh: the ground under the probe will do.
            if (!Physics.Raycast(near + Vector3.up * 3f, Vector3.down, out RaycastHit ground, reach + 3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            spot = ground.point;
            return (spot.y > ocean.SeaLevel + DryMargin || !ocean.IsSea(spot.x, spot.z)) && !NavMesh.SamplePosition(spot, out _, 50f, NavMesh.AllAreas);
        }

        private bool Connected(Vector3 spot)
        {
            if (!haveLand) return true;
            path ??= new NavMeshPath();
            return NavMesh.CalculatePath(spot, lastLand, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
