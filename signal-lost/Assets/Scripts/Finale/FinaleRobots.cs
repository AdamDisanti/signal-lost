using System;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Mudlands;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace SignalLost.Finale
{
    /// <summary>
    /// Cavendish's robot copies of the bosses the player beat on the way (Adam, 11-09), sent in while the mech kneels to
    /// repair itself between phases: Burn-Bot and Witch-Bot at the first repair, Mecha-Worm and Mecha-Buzzard at the
    /// second (Adam, 11-10: two at a time, a fair 2v1). Each is the real boss (its own script, moves and attacks) given
    /// a robot skin by <see cref="Robotify"/>, a robot name, and these changes for the finale: no ship part or key item
    /// drop, no top-of-screen bar of its own (the finale HUD lists them), a wide fight radius so it engages at once,
    /// half health, and its hits on the player scaled down (<see cref="FinaleEncounter"/>). They can be finished off
    /// with the knuckles below a quarter of their health (Adam, 11-10; <see cref="SignalLost.Melee.Finishable"/>).
    /// Robo-Toad and Silt-Bot are built too but not in the line-up at the moment.
    /// </summary>
    public static class FinaleRobots
    {
        public enum Kind { RoboToad, MechaBuzzard, BurnBot, MechaWorm, WitchBot, SiltBot }

        public static readonly Kind[] FirstShield = { Kind.BurnBot, Kind.WitchBot };
        public static readonly Kind[] SecondShield = { Kind.MechaWorm, Kind.MechaBuzzard };

        public static string Name(Kind kind) => kind switch
        {
            Kind.RoboToad => "Robo-Toad",
            Kind.MechaBuzzard => "Mecha-Buzzard",
            Kind.BurnBot => "Burn-Bot",
            Kind.MechaWorm => "Mecha-Worm",
            Kind.WitchBot => "Witch-Bot",
            _ => "Silt-Bot"
        };

        /// <summary>The prefabs the copies are made from (set by the level builder).</summary>
        [Serializable]
        public sealed class Sources
        {
            public GameObject chiefToad, oldBuzzard, burnout, coreworm;
        }

        /// <summary>A robot in the fight: what the HUD and the encounter track.</summary>
        public sealed class Robot
        {
            public Kind Kind;
            public string Name;
            public GameObject Go;
            public Health Health;
            public DesertMiniBoss MiniBoss; // the desert-style ones, to keep their own bar hidden
            public bool Alive => Go != null && Health != null && !Health.IsDead;
        }

        private const float FightRadius = 48f;
        /// <summary>Share of its health at or below which a robot can be finished with the knuckles (V).</summary>
        public const float FinishBelow = 0.25f;

        public static Robot Spawn(Kind kind, Sources sources, Vector3 at, Quaternion facing, float healthScale)
        {
            var holder = new GameObject("Robot Holder");
            holder.SetActive(false); // nothing wakes up until it is configured
            GameObject go;
            switch (kind)
            {
                case Kind.RoboToad: go = FromPrefab(sources.chiefToad, holder, at, facing); break;
                case Kind.MechaBuzzard: go = FromPrefab(sources.oldBuzzard, holder, at, facing); break;
                case Kind.BurnBot: go = FromPrefab(sources.burnout, holder, at, facing); break;
                case Kind.MechaWorm: go = FromPrefab(sources.coreworm, holder, at, facing); break;
                case Kind.WitchBot: go = Build<SlothWitch>(holder, at, facing, 0.6f, 2.2f, 420f); break;
                default: go = Build<MudbenderOtter>(holder, at, facing, 0.55f, 1.8f, 480f); break;
            }
            if (go == null)
            {
                Object.Destroy(holder);
                return null;
            }
            string name = Name(kind);
            go.name = name;
            var robot = new Robot { Kind = kind, Name = name, Go = go, Health = go.GetComponent<Health>() };
            var miniBoss = go.GetComponent<DesertMiniBoss>();
            if (miniBoss != null)
            {
                robot.MiniBoss = miniBoss;
                Set(miniBoss, "displayName", name);
                Set(miniBoss, "partName", "");
                Set(miniBoss, "shipPart", null);
                Set(miniBoss, "arenaRadius", FightRadius);
                Set(miniBoss, "fodderSpawner", null);
                Set(miniBoss, "fodderWhileEngaged", false);
                if (miniBoss is MudBoss) Set(miniBoss, "keyItemDrop", "");
            }
            var worm = go.GetComponent<Coreworm>();
            if (worm != null)
            {
                Set(worm, "displayName", name.ToUpperInvariant());
                Set(worm, "arena", null);
            }
            go.AddComponent<SignalLost.Melee.Finishable>().Below = FinishBelow;
            var skin = go.AddComponent<Robotify>();
            skin.accent = kind == Kind.BurnBot || kind == Kind.MechaWorm ? FinalePalette.EngineGlow : FinalePalette.Teal;

            go.transform.SetParent(null, true); // it wakes up now
            Object.Destroy(holder);
            if (robot.Health != null) robot.Health.SetMax(robot.Health.Max * healthScale);
            if (worm != null)
            {
                float ground = at.y;
                worm.HideAt(at);
                worm.BeginFight();
                worm.UseGroundHeight(ground); // its own fight assumes its lair's floor
                CorewormHud.ShowBoss(null);  // the finale HUD lists it instead
                worm.Defeated += () =>
                {
                    // The real worm drops the Engine Core; this copy doesn't.
                    foreach (EngineCorePickup pickup in Object.FindObjectsByType<EngineCorePickup>(FindObjectsInactive.Include))
                        Object.Destroy(pickup.gameObject);
                };
            }
            return robot;
        }

        private static GameObject FromPrefab(GameObject prefab, GameObject holder, Vector3 at, Quaternion facing) =>
            prefab != null ? Object.Instantiate(prefab, at, facing, holder.transform) : null;

        // The Mudlands bosses are put together the way the Mudlands builder does it; their models build in Awake.
        private static GameObject Build<T>(GameObject holder, Vector3 at, Quaternion facing, float radius, float height, float hp) where T : MudBoss
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(holder.transform, false);
            go.transform.SetPositionAndRotation(at, facing);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = 4f;
            agent.angularSpeed = 360f;
            agent.acceleration = 20f;
            agent.enabled = false;
            Health health = go.AddComponent<Health>();
            health.SetMax(hp);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            go.AddComponent<T>();
            return go;
        }

        // Sets a private serialized field, looking up the class chain (most live on DesertMiniBoss or MudBoss).
        private static void Set(object target, string field, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            Debug.LogWarning($"FinaleRobots: no field {field} on {target.GetType().Name}");
        }
    }
}
