using SignalLost.Combat;
using SignalLost.Player;
using SignalLost.World;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Decides whether an enemy may appear at a spot (11-07: no more enemies inside the wreck, under the map or
    /// somewhere the player can't get to). A spot is safe when all of these hold:
    /// <list type="bullet">
    /// <item>it is on the NavMesh;</item>
    /// <item>nothing solid is above it, so it is not under a roof, inside a hull or below the terrain;</item>
    /// <item>it is not below a terrain's surface, or under the sea around an island (<see cref="Ocean"/>);</item>
    /// <item>a body-sized capsule standing there touches nothing but the ground, so it is not inside a prop;</item>
    /// <item>the NavMesh has a complete walking path from it to the respawn point or to where the player stands, so it
    /// is not on a cut-off island, roof or cliff top. Either will do: the respawn point keeps spawns on the level's main
    /// ground, and the player's spot covers areas they dropped into that don't connect back.</item>
    /// </list>
    /// Spawners call <see cref="TryPlace"/>; enemies placed by hand call <see cref="Settle"/> when they start.
    /// </summary>
    public static class SpawnSafety
    {
        /// <summary>How far above a spot to look for a roof. Tree canopies higher than this don't count.</summary>
        public const float HeadroomCheck = 7f;
        private const float BodyRadius = 0.35f, BodyHeight = 1.3f, GroundTolerance = 0.45f;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[16];
        private static NavMeshPath path;

        /// <summary>Why <paramref name="point"/> is unsafe, or null if an enemy may appear there. For tests and logs.</summary>
        public static string WhyUnsafe(Vector3 point, bool needPath = true)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit onMesh, 0.6f, NavMesh.AllAreas)) return "off the NavMesh";
            point = onMesh.position;
            if (Ocean.Current != null && Ocean.Current.IsUnderwater(point, 0.2f)) return "under the sea";

            // A roof, a hull or the terrain overhead.
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.25f, Vector3.up, Hits, HeadroomCheck, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Solid(Hits[i].collider)) return "under " + Hits[i].collider.name;

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 local = point - terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
                if (point.y < terrain.SampleHeight(point) + terrain.GetPosition().y - GroundTolerance) return "under " + terrain.name;
            }

            // Inside a prop: the ground under the feet doesn't count, so the capsule starts just above it.
            Vector3 bottom = point + Vector3.up * (BodyRadius + GroundTolerance), top = point + Vector3.up * (BodyHeight - BodyRadius);
            count = Physics.OverlapCapsuleNonAlloc(bottom, top, BodyRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Solid(Overlaps[i]) && !(Overlaps[i] is TerrainCollider)) return "inside " + Overlaps[i].name;

            if (needPath && !Reachable(point)) return "cut off from the player";
            return null;
        }

        public static bool IsSafe(Vector3 point, bool needPath = true) => WhyUnsafe(point, needPath) == null;

        /// <summary>
        /// A safe spot near <paramref name="desired"/>: the spot itself if it is safe, otherwise the nearest of a few
        /// tries in widening rings out to <paramref name="searchRadius"/>. False if there is none.
        /// </summary>
        public static bool TryPlace(Vector3 desired, float searchRadius, out Vector3 point, int attempts = 16)
        {
            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = desired;
                if (i > 0)
                {
                    float ring = searchRadius * Mathf.Sqrt(i / (float)(attempts - 1));
                    float angle = i * 2.39996f; // golden angle: spreads the tries round the circle
                    candidate += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                }
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, Mathf.Max(1.5f, searchRadius * 0.5f), NavMesh.AllAreas)) continue;
                if (WhyUnsafe(hit.position) != null) continue;
                point = hit.position;
                return true;
            }
            point = desired;
            return false;
        }

        /// <summary>
        /// For an enemy that starts where a designer (or a builder script) put it: if that spot is unsafe it moves to
        /// the nearest safe one within <paramref name="searchRadius"/>. Returns where it ended up, or null if nowhere
        /// near is safe (the enemy is then left where it was).
        /// </summary>
        public static Vector3? Settle(Transform enemy, float searchRadius = 8f)
        {
            Vector3 position = enemy.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas) && IsSafe(hit.position)) return hit.position;
            if (!TryPlace(position, searchRadius, out Vector3 safe)) return null;
            enemy.position = safe;
            return safe;
        }

        /// <summary>Somewhere the player can certainly walk: the respawn point, or else the player's own spot on the NavMesh.</summary>
        public static bool ReachAnchor(out Vector3 anchor)
        {
            if (SpawnAnchor(out anchor)) return true;
            return PlayerAnchor(out anchor);
        }

        // True with no anchors at all (nothing to compare against), so scenes without a player still spawn.
        private static bool Reachable(Vector3 point)
        {
            bool haveSpawn = SpawnAnchor(out Vector3 spawn), havePlayer = PlayerAnchor(out Vector3 player);
            if (!haveSpawn && !havePlayer) return true;
            return (haveSpawn && Connected(point, spawn)) || (havePlayer && Connected(point, player));
        }

        private static bool SpawnAnchor(out Vector3 anchor)
        {
            PlayerSpawnPoint spawn = PlayerSpawnPoint.Current;
            anchor = default;
            if (spawn == null || !NavMesh.SamplePosition(spawn.transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;
            anchor = hit.position;
            return true;
        }

        // Only when the player is actually standing on the NavMesh (not mid-jump, not inside the wreck under its roof).
        private static bool PlayerAnchor(out Vector3 anchor)
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            anchor = default;
            if (player == null || !NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) return false;
            anchor = hit.position;
            return true;
        }

        private static bool Connected(Vector3 from, Vector3 to)
        {
            path ??= new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        // Level geometry: not the player, not creatures, not see-through triggers.
        private static bool Solid(Collider collider) =>
            collider != null && !(collider is CharacterController) && collider.GetComponentInParent<Health>() == null &&
            collider.GetComponentInParent<NavMeshAgent>() == null;
    }
}
