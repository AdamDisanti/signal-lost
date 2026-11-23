using System;
using System.Collections;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Story
{
    /// <summary>
    /// The end of "Omelet du Jour" (docs/desert-revamp.md, Act 1): mid-breakfast, a screech and a shadow, and Briarwing
    /// snatches the player off their seat and carries them over the dunes to its Roost, where it drops them by its empty
    /// nest, lands and the fight begins. Those were its eggs.
    ///
    /// The wyvern in the air is a stand-in: a copy of the boss's own model and animator (its Hover flap and Plunge clips)
    /// with every collider and script stripped, so the boss itself stays asleep until it lands; then the stand-in is swapped
    /// for the real Briarwing at its home. The player can't move while carried; nothing else is going on at the time.
    /// Space (Jump) skips it: the player lands by the nest and Briarwing is home, as at the end of the flight.
    /// </summary>
    public sealed class WyvernSnatch : MonoBehaviour
    {
        [Tooltip("Briarwing (kept inactive until it lands).")]
        [SerializeField] private DesertMiniBoss boss;
        [Tooltip("Where the player is dropped, near the nest inside the Roost.")]
        [SerializeField] private Transform dropPoint;
        [Tooltip("Where the player hangs under the stand-in, in its own space.")]
        [SerializeField] private Vector3 talons = new Vector3(0f, -2.6f, 0.9f);
        [SerializeField, Min(5f)] private float carrySpeed = 20f;

        public bool Running { get; private set; }
        private bool skip;
        /// <summary>Raised once the player is on the ground at the Roost and Briarwing has landed.</summary>
        public event Action Landed;

        public void Configure(DesertMiniBoss wyvern, Transform drop)
        {
            boss = wyvern;
            dropPoint = drop;
        }

        public void Begin(GameObject player)
        {
            if (Running || boss == null || dropPoint == null) return;
            skip = false;
            StartCoroutine(Snatch(player));
        }

        private void Update()
        {
            if (Running && !skip && SignalLost.Core.KeyBindings.WasPressed("jump")) skip = true;
        }

        /// <summary>Tests: puts the player at the drop point and wakes Briarwing, without the flight.</summary>
        public void SkipForTests(GameObject player)
        {
            Teleport(player, dropPoint.position + Vector3.up * 0.2f);
            WakeBoss();
            Landed?.Invoke();
        }

        private IEnumerator Snatch(GameObject player)
        {
            Running = true;
            Transform eyes = player.GetComponentInChildren<Camera>()?.transform;
            Quaternion eyesBefore = eyes != null ? eyes.localRotation : Quaternion.identity;
            Behaviour[] frozen = { player.GetComponent<FirstPersonController>(), player.GetComponent<MiningLaser>(), player.GetComponent<PlayerInteractor>() };
            foreach (Behaviour b in frozen) if (b != null) b.enabled = false;
            var body = player.GetComponent<CharacterController>();

            Sfx.Play("kong_roar", 1f, 1.7f);
            MiniBossHud.Message("SCREEEEEECH!", new Color(1f, 0.6f, 0.4f), 1.8f);
            SignalLost.UI.DialogueUi.Instance.Subtitle("", $"{SignalLost.Core.KeyBindings.Display("jump")} to skip", 3f);
            for (float t = 0f; t < 0.9f && !skip; t += Time.deltaTime) yield return null;

            // The stand-in swoops in from behind, out of the sun.
            GameObject standIn = StandIn();
            Animator animator = standIn.GetComponentInChildren<Animator>();
            Play(animator, "Hover");
            Vector3 grab = player.transform.position - Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f) * talons;
            Vector3 back = -player.transform.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
            Vector3 start = player.transform.position + back * 70f + Vector3.up * 30f;
            Vector3 bend = player.transform.position + back * 18f + Vector3.up * 14f;
            float flapAt = 0f;
            for (float t = 0f; t < 1f && !skip; t += Time.deltaTime / 2.4f)
            {
                MoveAlong(standIn.transform, start, bend, grab, t);
                if (eyes != null) eyes.localRotation = Quaternion.Slerp(eyesBefore, Quaternion.Euler(-35f, 0f, 0f), t * 0.6f);
                if (Time.time > flapAt) { Sfx.PlayAt("blackout_whoosh", standIn.transform.position, 0.8f, 0.7f, 80f); flapAt = Time.time + 0.7f; }
                yield return null;
            }

            // Snatched. Carried high over the dunes to the Roost.
            if (body != null) body.enabled = false;
            Sfx.Play("player_hurt_2", 0.6f);
            Vector3 from = standIn.transform.position;
            Vector3 to = dropPoint.position + Vector3.up * 7f - Quaternion.Euler(0f, standIn.transform.eulerAngles.y, 0f) * talons;
            Vector3 high = Vector3.Lerp(from, to, 0.45f) + Vector3.up * 28f;
            float seconds = Mathf.Max(3f, (Vector3.Distance(from, high) + Vector3.Distance(high, to)) / carrySpeed);
            Quaternion lookBefore = eyes != null ? eyes.localRotation : Quaternion.identity;
            for (float t = 0f; t < 1f && !skip; t += Time.deltaTime / seconds)
            {
                MoveAlong(standIn.transform, from, high, to, SmoothStep(t));
                player.transform.SetPositionAndRotation(standIn.transform.TransformPoint(talons / Mathf.Max(0.01f, standIn.transform.lossyScale.y)),
                    Quaternion.Euler(0f, standIn.transform.eulerAngles.y, 0f));
                if (eyes != null) eyes.localRotation = Quaternion.Slerp(lookBefore, Quaternion.Euler(38f, 0f, 0f), Mathf.Clamp01(t * 3f));
                if (Time.time > flapAt) { Sfx.PlayAt("blackout_whoosh", standIn.transform.position, 0.7f, 0.65f, 80f); flapAt = Time.time + 0.75f; }
                yield return null;
            }

            // Dropped by the nest (a skip puts the player straight there).
            Teleport(player, skip ? dropPoint.position + Vector3.up * 0.2f : player.transform.position);
            foreach (Behaviour b in frozen) if (b != null) b.enabled = true;
            if (eyes != null) eyes.localRotation = eyesBefore;
            MiniBossHud.Message("Those were ITS eggs!", new Color(1f, 0.75f, 0.4f), 2.6f);

            // It wheels round and lands at home, and becomes the real boss.
            Vector3 home = boss.transform.position;
            Vector3 circle = standIn.transform.position + Vector3.up * 6f;
            Vector3 above = home + Vector3.up * 9f;
            for (float t = 0f; t < 1f && !skip; t += Time.deltaTime / 1.8f)
            {
                MoveAlong(standIn.transform, circle - Vector3.up * 6f, circle, above, t);
                yield return null;
            }
            Play(animator, "Plunge");
            Vector3 landFrom = standIn.transform.position;
            for (float t = 0f; t < 1f && !skip; t += Time.deltaTime / 0.5f)
            {
                standIn.transform.position = Vector3.Lerp(landFrom, home, t * t);
                yield return null;
            }
            Destroy(standIn);
            WakeBoss();
            Running = false;
            Landed?.Invoke();
        }

        private void WakeBoss()
        {
            boss.gameObject.SetActive(true);
            Vector3 at = boss.transform.position;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at + Vector3.up, Vector3.up, 50, 2.5f, 7f, 80f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at + Vector3.up, Vector3.up, 20, 2.5f, 2.5f, 80f);
        }

        // A copy of the boss's model only: no colliders, no scripts, just the mesh, its rig and its animator.
        private GameObject StandIn()
        {
            Transform visual = boss.transform.Find("Visual") ?? boss.transform;
            GameObject copy = Instantiate(visual.gameObject);
            copy.name = "Briarwing (in flight)";
            copy.transform.localScale = visual.lossyScale;
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (MonoBehaviour m in copy.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
            foreach (Rigidbody r in copy.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
            copy.SetActive(true);
            Animator animator = copy.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            return copy;
        }

        private static void Play(Animator animator, string clip)
        {
            if (animator != null && animator.runtimeAnimatorController != null) animator.CrossFadeInFixedTime(clip, 0.15f);
        }

        // Along a quadratic curve, facing the way it is going (level, banking a little into turns).
        private static void MoveAlong(Transform flyer, Vector3 a, Vector3 b, Vector3 c, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);
            Vector3 ahead = Vector3.Lerp(Vector3.Lerp(a, b, Mathf.Min(1f, t + 0.02f)), Vector3.Lerp(b, c, Mathf.Min(1f, t + 0.02f)), Mathf.Min(1f, t + 0.02f));
            Vector3 heading = ahead - p;
            heading.y = 0f;
            flyer.position = p;
            if (heading.sqrMagnitude > 1e-5f) flyer.rotation = Quaternion.Slerp(flyer.rotation, Quaternion.LookRotation(heading), 0.2f);
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private static void Teleport(GameObject player, Vector3 at)
        {
            var body = player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            player.transform.position = at;
            if (body != null) body.enabled = true;
        }
    }
}
