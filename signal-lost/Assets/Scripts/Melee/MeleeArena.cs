using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Finale;
using SignalLost.Player;
using SignalLost.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalLost.Melee
{
    /// <summary>
    /// The melee test arena (Adam, 11-10; docs/melee-rage.md): the finale's robot creatures (crabs, ticks, frogs,
    /// armadillos and mudpuppies with <see cref="Robotify"/> skins) keep spawning around the player, more and faster
    /// the longer it runs, so the fists, the knuckles and rage can be tried on a crowd. Number keys are testing tools:
    /// 1-4 send in a robot mini-boss, 5 a boss dummy that measures damage, 6 pauses the horde, 7 and 8 step through the
    /// knuckle and Moon Shoe tiers, 9 fills the rage meter and 0 clears the arena. A panel at the top left lists them.
    /// Built by the MeleeArenaBuilder editor script.
    /// </summary>
    public sealed class MeleeArena : MonoBehaviour
    {
        [SerializeField] private GameObject[] addPrefabs = System.Array.Empty<GameObject>();
        [SerializeField] private FinaleRobots.Sources robotSources = new FinaleRobots.Sources();
        [Tooltip("Half the width of the open floor: spawns stay inside it.")]
        [SerializeField, Min(5f)] private float halfSize = 27f;
        [SerializeField, Min(0.2f)] private float startGap = 2.6f, fastestGap = 0.7f;
        [SerializeField, Min(1)] private int startCap = 8, maxCap = 28;
        [Tooltip("Health of the robot mini-bosses compared with the originals (the finale's repair robots use 0.5).")]
        [SerializeField, Range(0.1f, 2f)] private float robotHealth = 0.5f;

        private readonly List<Health> adds = new List<Health>();
        private readonly List<GameObject> extras = new List<GameObject>();
        private PlayerLife player;
        private PlayerUpgrades upgrades;
        private CombatZone zone;
        private float started, nextSpawn;
        private bool paused;
        private int kills, knuckleLimit = 3, shoeLimit = 3;
        private TextMeshProUGUI help;
        private Canvas canvas;

        public int Alive { get { adds.RemoveAll(h => h == null || h.IsDead); return adds.Count; } }
        public int Kills => kills;
        public float Minutes => (Time.time - started) / 60f;
        public int Cap => Mathf.Min(maxCap, startCap + Mathf.FloorToInt(Minutes * 6f));
        public float Gap => Mathf.Max(fastestGap, startGap - Minutes * 0.6f);
        public bool Paused { get => paused; set => paused = value; }
        public IReadOnlyList<GameObject> Extras => extras;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerLife>();
            upgrades = player != null ? player.GetComponent<PlayerUpgrades>() : null;
            zone = GetComponent<CombatZone>();
            started = Time.time;
            nextSpawn = Time.time + 1.5f;
            Health.AnyKilled += OnKilled;
            if (player != null) player.Respawned += ClearArena;
            gameObject.AddComponent<DamageNumbers>();
            BuildHelp();
        }

        private void OnDestroy()
        {
            Health.AnyKilled -= OnKilled;
            if (player != null) player.Respawned -= ClearArena;
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void OnKilled(Health dead, GameObject killer)
        {
            if (adds.Contains(dead)) kills++;
        }

        private void Update()
        {
            TestKeys();
            if (!paused && player != null && !player.IsRespawning && Time.time >= nextSpawn)
            {
                nextSpawn = Time.time + Gap;
                int batch = 2 + Mathf.FloorToInt(Minutes);
                for (int i = 0; i < batch && Alive < Cap; i++) SpawnAdd();
            }
            UpdateHelp();
        }

        // ---------- The horde ----------

        /// <summary>Spawns one robot creature in a ring around the player, inside the arena. Returns it, or null.</summary>
        public Health SpawnAdd()
        {
            if (addPrefabs.Length == 0 || player == null) return null;
            GameObject prefab = addPrefabs[Random.Range(0, addPrefabs.Length)];
            if (prefab == null) return null;
            if (!Place(9f, 20f, out Vector3 point)) return null;
            Vector3 face = player.transform.position - point;
            face.y = 0f;
            GameObject spawned = Instantiate(prefab, point, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward));
            spawned.name = "Robo " + prefab.name;
            spawned.AddComponent<Robotify>().addParts = spawned.GetComponentInChildren<Renderer>() != null;
            if (zone != null)
                foreach (ITerritorial enemy in spawned.GetComponentsInChildren<ITerritorial>()) enemy.Territory.Zone = zone;
            Effects.ParticleBurst.Spawn(Effects.ParticleBurst.Kind.Sparks, point + Vector3.up * 0.3f, Vector3.up, 20, 0.06f, 5f, 50f, GaussFx.Energy);
            var health = spawned.GetComponent<Health>();
            if (health != null) adds.Add(health);
            return health;
        }

        // A safe spot between minRing and maxRing metres from the player, inside the walls.
        private bool Place(float minRing, float maxRing, out Vector3 point)
        {
            Vector3 centre = transform.position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 desired = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(minRing, maxRing);
                desired.x = Mathf.Clamp(desired.x, centre.x - halfSize, centre.x + halfSize);
                desired.z = Mathf.Clamp(desired.z, centre.z - halfSize, centre.z + halfSize);
                desired.y = centre.y;
                if (SpawnSafety.TryPlace(desired, 4f, out point)) return true;
            }
            point = default;
            return false;
        }

        // ---------- Testing keys ----------

        private static readonly FinaleRobots.Kind[] Robots =
            { FinaleRobots.Kind.BurnBot, FinaleRobots.Kind.WitchBot, FinaleRobots.Kind.MechaBuzzard, FinaleRobots.Kind.RoboToad };

        private void TestKeys()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null || player == null || UiMenus.AnyOpen) return;
            for (int i = 0; i < Robots.Length; i++)
                if (keys[Key.Digit1 + i].wasPressedThisFrame) SpawnRobot(Robots[i]);
            if (keys.digit5Key.wasPressedThisFrame) SpawnDummy();
            if (keys.digit6Key.wasPressedThisFrame) paused = !paused;
            if (keys.digit7Key.wasPressedThisFrame) SetKnuckleLimit((knuckleLimit + 1) % 4);
            if (keys.digit8Key.wasPressedThisFrame) SetShoeLimit((shoeLimit + 1) % 4);
            if (keys.digit9Key.wasPressedThisFrame && RageMeter.Instance != null) RageMeter.Instance.Add(RageMeter.MaxStacks);
            if (keys.digit0Key.wasPressedThisFrame) ClearArena();
        }

        public GameObject SpawnRobot(FinaleRobots.Kind kind)
        {
            if (!Place(12f, 18f, out Vector3 point)) return null;
            Vector3 face = player.transform.position - point;
            face.y = 0f;
            FinaleRobots.Robot robot = FinaleRobots.Spawn(kind, robotSources, point, Quaternion.LookRotation(face), robotHealth);
            if (robot == null) return null;
            extras.Add(robot.Go);
            return robot.Go;
        }

        public GameObject SpawnDummy()
        {
            if (!Place(6f, 9f, out Vector3 point)) return null;
            var dummy = new GameObject("Boss Dummy");
            dummy.transform.SetPositionAndRotation(point, Quaternion.LookRotation(Flat(player.transform.position - point)));
            var health = dummy.AddComponent<Health>();
            health.SetMax(20000f);
            health.Restore();
            dummy.AddComponent<ArenaBossDummy>();
            extras.Add(dummy);
            return dummy;
        }

        public void SetKnuckleLimit(int tier)
        {
            knuckleLimit = tier;
            if (upgrades == null) return;
            upgrades.KnuckleTierLimit = tier;
            upgrades.Apply();
        }

        public void SetShoeLimit(int tier)
        {
            shoeLimit = tier;
            if (upgrades == null) return;
            upgrades.ShoeTierLimit = tier;
            upgrades.Apply();
        }

        /// <summary>Removes every enemy, robot and dummy (and the horde starts over, as if just begun).</summary>
        public void ClearArena()
        {
            foreach (Health add in adds) if (add != null) Destroy(add.gameObject);
            adds.Clear();
            foreach (GameObject extra in extras) if (extra != null) Destroy(extra);
            extras.Clear();
            started = Time.time;
            nextSpawn = Time.time + 2f;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.01f ? v : Vector3.forward;
        }

        // ---------- The help panel ----------

        private void BuildHelp()
        {
            canvas = UiTheme.CreateCanvas("Melee Arena Help", 25);
            Image panel = UiTheme.Panel("Help", canvas.transform, UiTheme.PanelColor, new Color(GaussFx.Energy.r, GaussFx.Energy.g, GaussFx.Energy.b, 0.9f), 0.8f);
            RectTransform p = UiTheme.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(470f, 380f));
            help = UiTheme.Label("Text", p, 19f, UiTheme.Text, TextAlignmentOptions.TopLeft);
            UiTheme.Stretch(help.rectTransform, 16f);
        }

        private void UpdateHelp()
        {
            if (help == null) return;
            help.transform.parent.gameObject.SetActive(!UiMenus.FullScreenOpen);
            string Tier(int t) => t == 0 ? "off" : new[] { "", "I", "II", "III" }[t];
            string hex = UiTheme.Hex(GaussFx.Energy);
            help.text =
                $"<b><color={hex}>MELEE ARENA</color></b>   alive {Alive}/{Cap}   kills {kills}{(paused ? "   <color=#FF6A5A>PAUSED</color>" : "")}\n" +
                $"<color=#A9B4CC>Q cycle to fists   LMB punch   RMB magnet pull   V finisher   Space twice: double jump</color>\n\n" +
                "<b>1</b> Burn-Bot   <b>2</b> Witch-Bot   <b>3</b> Mecha-Buzzard   <b>4</b> Robo-Toad\n" +
                "<b>5</b> boss dummy (damage per second)\n" +
                $"<b>6</b> {(paused ? "resume" : "pause")} the horde\n" +
                $"<b>7</b> knuckle tier: <b>{Tier(knuckleLimit)}</b>   <b>8</b> Moon Shoe tier: <b>{Tier(shoeLimit)}</b>\n" +
                "<b>9</b> max rage   <b>0</b> clear the arena\n\n" +
                $"<color=#A9B4CC>Spawning every {Gap:0.0} s; the cap rises every 10 s.</color>";
        }
    }
}
