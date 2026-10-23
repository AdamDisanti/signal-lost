using System;
using System.Collections;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Enemies.Meadow;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace SignalLost.Story
{
    /// <summary>
    /// The Chief Toad at the tutorial pond (meeting notes 10-23). He is not there until Cavendish gives the quest
    /// (<see cref="Arm"/>). When the player then comes within his arena of the pond, a short cutscene cuts to the
    /// bank: the toad rises out of the middle of the pond in a burst of water, his health bar appears, and control
    /// returns for the fight. While he fights, 3-5 frogs keep hopping in (<see cref="FrogWaves.Sustain"/>).
    /// Beating him leaves a pile of loot: his cooling system (the stage part), scrap, and his crown, the camp trophy.
    /// Space skips the cutscene.
    /// </summary>
    public sealed class ChiefToadEncounter : MonoBehaviour
    {
        [SerializeField] private ChiefToad toad;
        [SerializeField] private FrogWaves frogs;
        [Tooltip("Middle of the pond, at the water's surface: where he rises.")]
        [SerializeField] private Transform pondCentre;
        [Tooltip("The cutscene starts when the player is this close to the pond's middle.")]
        [SerializeField, Min(5f)] private float triggerRadius = 40f;
        [SerializeField, Min(0.5f)] private float riseSeconds = 2.4f;
        [SerializeField, Min(0.5f)] private float riseDepth = 6f;

        public enum State { Waiting, Armed, Cutscene, Fighting, Defeated }

        public State Current { get; private set; }
        public ChiefToad Toad => toad;
        public TrophyPickup Trophy { get; private set; }
        public event Action Started, Beaten;

        private PlayerLife player;

        private void Awake()
        {
            player = FindAnyObjectByType<PlayerLife>();
            if (toad != null) toad.gameObject.SetActive(false);
            Signal(false);
        }

        private void OnEnable() => DesertMiniBoss.Defeated += OnBossDefeated;
        private void OnDisable() => DesertMiniBoss.Defeated -= OnBossDefeated;

        /// <summary>Cavendish has given the quest: the toad waits under the pond for the player.</summary>
        public void Arm()
        {
            if (Current != State.Waiting) return;
            Current = State.Armed;
            Signal(true);
        }

        private void Update()
        {
            if (Current != State.Armed || player == null || player.IsRespawning) return;
            Vector3 offset = player.transform.position - pondCentre.position;
            offset.y = 0f;
            if (offset.magnitude <= triggerRadius) StartCoroutine(Cutscene());
        }

        // ---------- The rise ----------

        private IEnumerator Cutscene()
        {
            Current = State.Cutscene;
            Signal(false);
            var movement = player.GetComponent<FirstPersonController>();
            Camera view = player.GetComponentInChildren<Camera>();
            bool wasInvulnerable = player.Health.Invulnerable;
            player.Health.Invulnerable = true;
            if (movement != null) movement.enabled = false;

            // A camera on the bank between the player and the pond, low, looking at the middle.
            Vector3 centre = pondCentre.position;
            Vector3 fromPond = player.transform.position - centre;
            fromPond.y = 0f;
            fromPond = fromPond.sqrMagnitude > 1f ? fromPond.normalized : Vector3.back;
            Camera shot = new GameObject("Chief Toad Cutscene Camera").AddComponent<Camera>();
            shot.CopyFrom(view);
            shot.fieldOfView = 50f;
            shot.transform.position = ShotPosition(centre, fromPond);
            shot.transform.LookAt(centre + Vector3.up * 2.5f);
            view.enabled = false;
            SignalLost.UI.UiMenus.Open(this, true); // the HUD steps aside for the shot

            // He rises from under the water; his brain stays off until he is up.
            Vector3 top = NavMesh.SamplePosition(centre, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : centre;
            Quaternion facing = Quaternion.LookRotation(fromPond);
            var agent = toad.GetComponent<NavMeshAgent>();
            toad.enabled = false;
            if (agent != null) agent.enabled = false;
            toad.transform.SetPositionAndRotation(top + Vector3.down * riseDepth, facing);
            toad.gameObject.SetActive(true);
            yield return SkippableWait(0.5f);
            float nextSplash = 0f;
            for (float t = 0f; t < riseSeconds && !Skipped(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / riseSeconds);
                toad.transform.position = Vector3.Lerp(top + Vector3.down * riseDepth, top, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
                if (Time.time >= nextSplash)
                {
                    nextSplash = Time.time + 0.18f;
                    Vector3 rim = centre + UnityEngine.Random.insideUnitSphere.Flattened() * 2.5f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Water, rim, Vector3.up, 40, 0.35f, 9f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, centre, Vector3.up, 14, 1.2f, 3f);
                }
                yield return null;
            }
            toad.transform.SetPositionAndRotation(top, facing);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, centre, Vector3.up, 120, 0.45f, 12f);

            // He wakes: the boss bar appears as he engages; a beat to take it in, then back to the player.
            if (agent != null) agent.enabled = true;
            toad.enabled = true;
            yield return SkippableWait(1.1f);

            Destroy(shot.gameObject);
            view.enabled = true;
            SignalLost.UI.UiMenus.Close(this);
            if (movement != null)
            {
                movement.ResetMotion();
                movement.enabled = true;
            }
            player.Health.Invulnerable = wasInvulnerable;
            if (frogs != null) frogs.Sustain(3, 5);
            Current = State.Fighting;
            Started?.Invoke();
        }

        // A spot on the bank with a clear view of the pond's middle, starting on the player's side and working round.
        private static Vector3 ShotPosition(Vector3 centre, Vector3 fromPond)
        {
            Vector3 target = centre + Vector3.up * 2.5f;
            for (int i = 0; i < 12; i++)
            {
                float angle = (i % 2 == 0 ? 1f : -1f) * (i + 1) / 2 * 25f + 15f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * fromPond;
                Vector3 spot = centre + direction * 18f;
                spot.y = Mathf.Max(spot.y, GroundHeight(spot)) + 5f;
                Vector3 look = target - spot;
                if (!Physics.SphereCast(spot, 0.4f, look.normalized, out _, look.magnitude - 3f, ~0, QueryTriggerInteraction.Ignore)) return spot;
            }
            return centre + fromPond * 18f + Vector3.up * 8f;
        }

        private static float GroundHeight(Vector3 point) =>
            Physics.Raycast(point + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : point.y;

        private bool skipped;

        private bool Skipped()
        {
            if (!skipped && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) skipped = true;
            return skipped;
        }

        private IEnumerator SkippableWait(float seconds)
        {
            for (float t = 0f; t < seconds && !Skipped(); t += Time.deltaTime) yield return null;
        }

        /// <summary>Skips the cutscene (as if Space was pressed). For tests.</summary>
        public void Skip() => skipped = true;

        // ---------- Defeat and loot ----------

        private void OnBossDefeated(DesertMiniBoss boss)
        {
            if (boss != toad || Current == State.Defeated) return;
            Current = State.Defeated;
            if (frogs != null) frogs.StopSustain();
            // The loot pile: the base class drops the cooling system where he fell; add scrap and the crown beside it.
            Vector3 at = toad.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, (player != null ? player.transform.position - at : Vector3.forward).Flattened().normalized);
            Trophy = TrophyPickup.Create("chief_toad_crown", Ground(at + side * 2f));
            for (int i = 0; i < 14; i++)
            {
                ScrapType type = i < 6 ? ScrapType.ScrapMetal : i < 10 ? ScrapType.Wiring : i < 12 ? ScrapType.CircuitBoards : ScrapType.FuelCells;
                Vector2 spread = UnityEngine.Random.insideUnitCircle * 2.5f;
                ScrapPickup.Spawn(type, at + Vector3.up * 1.5f, new Vector3(spread.x, UnityEngine.Random.Range(4f, 6f), spread.y));
            }
            Beaten?.Invoke();
        }

        private static Vector3 Ground(Vector3 point) =>
            Physics.Raycast(point + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : point;

        private void Signal(bool on)
        {
            if (pondCentre != null && pondCentre.TryGetComponent(out QuestSignalSource signal)) signal.enabled = on;
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 Flattened(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
