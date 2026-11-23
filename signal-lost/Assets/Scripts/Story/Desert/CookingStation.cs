using System;
using System.Collections;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace SignalLost.Story
{
    /// <summary>
    /// "Omelet du Jour" (docs/desert-revamp.md, Act 1): the camp fire's cooking mini-game, with Cavendish as a fussy
    /// sous-chef. Three steps, each graded and scored together out of ten:
    /// <list type="number">
    /// <item><b>Crack</b>: four eggs; a marker sweeps across the bar, press E while it is in the green.</item>
    /// <item><b>Whisk</b>: alternate A and D to fill the bowl's bar before it drains (7 s).</item>
    /// <item><b>Flip</b>: the pan swings; press Space as it passes the sweet spot.</item>
    /// </list>
    /// A bad omelet is still an omelet. While cooking the player can't walk, shoot or use anything; the camera looks down
    /// at the pan. The story arms it (<see cref="Ready"/>) and listens for <see cref="Cooked"/>.
    /// </summary>
    public sealed class CookingStation : MonoBehaviour, IInteractable
    {
        private enum Phase { Idle, Crack, Whisk, Flip, Done }

        [Tooltip("Where the pan sits over the fire (the camera looks at it).")]
        [SerializeField] private Transform pan;

        public bool Ready { get; set; }
        public bool Cooking => phase != Phase.Idle;
        /// <summary>The finished omelet's score out of ten.</summary>
        public event Action<int> Cooked;
        public int LastScore { get; private set; }

        private Phase phase;
        private int egg;
        private float marker, zone, zoneWidth, bar, timer, needle;
        private float crackScore, whiskScore, flipScore;
        private bool lastWasA, anyWhisk;
        private string feedback = "";
        private float feedbackUntil;
        private Color feedbackColour = Color.white;
        private Behaviour[] frozen = Array.Empty<Behaviour>();
        private Transform eyes;
        private Quaternion eyesBefore;

        public string Prompt => Ready && !Cooking ? "Cook the omelet" : "";
        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && Ready && !Cooking;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            StartCoroutine(Cook(player.gameObject));
            return true;
        }

        /// <summary>Tests: cooks without anyone at the controls, scoring <paramref name="score"/>.</summary>
        public void CookForTests(int score)
        {
            LastScore = Mathf.Clamp(score, 1, 10);
            Ready = false;
            Cooked?.Invoke(LastScore);
        }

        private IEnumerator Cook(GameObject player)
        {
            Ready = false;
            Freeze(player, true);
            Sfx.Play("ui_open", 0.8f);

            // 1. Crack four eggs.
            phase = Phase.Crack;
            crackScore = 0f;
            for (egg = 0; egg < 4; egg++)
            {
                zoneWidth = 0.14f;
                zone = Random.Range(0.25f, 0.85f - zoneWidth);
                float sweep = Random.Range(1.0f, 1.3f);
                bool pressed = false;
                for (float t = 0f; t < sweep && !pressed; t += Time.deltaTime)
                {
                    marker = t / sweep;
                    if (Pressed(Keyboard.current?.eKey))
                    {
                        pressed = true;
                        float off = Mathf.Abs(marker - (zone + zoneWidth * 0.5f));
                        if (off < zoneWidth * 0.2f) { crackScore += 3f; Say("Perfect crack!", Good); }
                        else if (off < zoneWidth * 0.5f) { crackScore += 2f; Say("Clean.", Good); }
                        else { crackScore += 1f; Say("Bit of shell. Crunchy.", Meh); }
                        Sfx.Play("crab_snip", 0.9f, Random.Range(0.9f, 1.15f));
                    }
                    yield return null;
                }
                if (!pressed) { Say("Dropped it. Next!", Bad); Sfx.Play("mud_splat", 0.7f); }
                yield return new WaitForSeconds(0.45f);
            }
            crackScore /= 12f;

            // 2. Whisk.
            phase = Phase.Whisk;
            bar = 0f;
            anyWhisk = false;
            float best = 0f;
            for (timer = 7f; timer > 0f && bar < 1f; timer -= Time.deltaTime)
            {
                bool a = Pressed(Keyboard.current?.aKey), d = Pressed(Keyboard.current?.dKey);
                if ((a && !lastWasA) || (d && lastWasA) || (!anyWhisk && (a || d)))
                {
                    bar += 0.075f;
                    lastWasA = a;
                    anyWhisk = true;
                    if (Random.value < 0.35f) Sfx.Play("scrap_clink_" + Random.Range(1, 4), 0.35f, 1.4f);
                }
                bar = Mathf.Max(0f, bar - 0.2f * Time.deltaTime);
                best = Mathf.Max(best, bar);
                yield return null;
            }
            whiskScore = bar >= 1f ? Mathf.Lerp(0.6f, 1f, timer / 7f) : best * 0.6f;
            Say(bar >= 1f ? "Fluffy!" : "Lumpy. Rustic, we'll call it.", bar >= 1f ? Good : Meh);
            Sfx.PlayAt("fire_crackle", PanPosition, 1f, 1.2f, 15f);
            yield return new WaitForSeconds(0.8f);

            // 3. Flip.
            phase = Phase.Flip;
            float swing = 0f;
            bool flipped = false;
            for (float t = 0f; t < 6f && !flipped; t += Time.deltaTime)
            {
                swing += Time.deltaTime * Mathf.Lerp(1.6f, 2.6f, t / 6f);
                needle = 0.5f + 0.5f * Mathf.Sin(swing * Mathf.PI);
                if (Pressed(Keyboard.current?.spaceKey))
                {
                    flipped = true;
                    float off = Mathf.Abs(needle - 0.5f);
                    flipScore = off < 0.05f ? 1f : off < 0.14f ? 0.7f : 0.3f;
                    Say(off < 0.05f ? "A flawless flip!" : off < 0.14f ? "Flipped!" : "Half of it's on the fire.", off < 0.05f ? Good : off < 0.14f ? Good : Meh);
                    Sfx.Play("player_jump", 0.6f, 1.3f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, PanPosition + Vector3.up * 0.2f, Vector3.up, 12, 0.05f, 2f, 60f, new Color(1f, 0.8f, 0.4f));
                }
                yield return null;
            }
            if (!flipped) { flipScore = 0.15f; Say("It's... scrambled now.", Bad); }
            yield return new WaitForSeconds(1.2f);

            phase = Phase.Done;
            LastScore = Mathf.Clamp(Mathf.RoundToInt(10f * (0.4f * crackScore + 0.3f * whiskScore + 0.3f * flipScore)), 1, 10);
            Sfx.Play("quest_chime", 0.8f);
            yield return new WaitForSeconds(1.6f);
            phase = Phase.Idle;
            Freeze(player, false);
            Cooked?.Invoke(LastScore);
        }

        private Vector3 PanPosition => pan != null ? pan.position : transform.position + Vector3.up * 0.6f;

        private static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl key) => key != null && key.wasPressedThisFrame;

        private static readonly Color Good = new Color(0.6f, 1f, 0.6f), Meh = new Color(1f, 0.85f, 0.45f), Bad = new Color(1f, 0.5f, 0.45f);

        private void Say(string text, Color colour)
        {
            feedback = text;
            feedbackColour = colour;
            feedbackUntil = Time.time + 1.4f;
        }

        // While cooking: no walking, looking, shooting or using things; the camera looks down at the pan.
        private void Freeze(GameObject player, bool on)
        {
            if (on)
            {
                frozen = new Behaviour[] { player.GetComponent<FirstPersonController>(), player.GetComponent<MiningLaser>(), player.GetComponent<PlayerInteractor>() };
                foreach (Behaviour b in frozen) if (b != null) b.enabled = false;
                Camera cam = player.GetComponentInChildren<Camera>();
                eyes = cam != null ? cam.transform : null;
                if (eyes != null)
                {
                    eyesBefore = eyes.localRotation;
                    Vector3 flat = PanPosition - player.transform.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(flat);
                    eyes.localRotation = Quaternion.Euler(32f, 0f, 0f);
                }
            }
            else
            {
                foreach (Behaviour b in frozen) if (b != null) b.enabled = true;
                if (eyes != null) eyes.localRotation = eyesBefore;
                frozen = Array.Empty<Behaviour>();
            }
        }

        // ---------- The panel ----------

        private GUIStyle title, body, big;

        private void OnGUI()
        {
            if (phase == Phase.Idle) return;
            float s = Screen.height / 1080f;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
                body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            title.fontSize = Mathf.RoundToInt(34 * s);
            body.fontSize = Mathf.RoundToInt(24 * s);
            big.fontSize = Mathf.RoundToInt(54 * s);

            var panel = new Rect(Screen.width * 0.5f - 420f * s, Screen.height - 390f * s, 840f * s, 300f * s);
            Fill(panel, new Color(0.12f, 0.08f, 0.05f, 0.86f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f * s), new Color(1f, 0.7f, 0.3f));
            string heading = phase switch
            {
                Phase.Crack => $"OMELET DU JOUR  -  CRACK THE EGGS ({Mathf.Min(egg + 1, 4)} / 4)",
                Phase.Whisk => "OMELET DU JOUR  -  WHISK",
                Phase.Flip => "OMELET DU JOUR  -  FLIP",
                _ => "OMELET DU JOUR"
            };
            string help = phase switch
            {
                Phase.Crack => "Press <b>E</b> when the marker is in the green.",
                Phase.Whisk => $"Alternate <b>A</b> and <b>D</b> as fast as you can!   {Mathf.Max(0f, timer):0.0} s",
                Phase.Flip => "Press <b>Space</b> as the pan swings through the middle.",
                _ => ""
            };
            GUI.Label(new Rect(panel.x, panel.y + 16f * s, panel.width, 44f * s), heading, title);
            GUI.Label(new Rect(panel.x + 30f * s, panel.y + 62f * s, panel.width - 60f * s, 40f * s), help, body);

            var track = new Rect(panel.x + 60f * s, panel.y + 130f * s, panel.width - 120f * s, 46f * s);
            Fill(track, new Color(0.3f, 0.22f, 0.16f));
            switch (phase)
            {
                case Phase.Crack:
                    Fill(new Rect(track.x + track.width * zone, track.y, track.width * zoneWidth, track.height), new Color(0.35f, 0.75f, 0.35f));
                    Fill(new Rect(track.x + track.width * marker - 4f * s, track.y - 10f * s, 8f * s, track.height + 20f * s), new Color(1f, 0.95f, 0.8f));
                    break;
                case Phase.Whisk:
                    Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(bar), track.height), new Color(1f, 0.85f, 0.35f));
                    break;
                case Phase.Flip:
                    Fill(new Rect(track.x + track.width * 0.45f, track.y, track.width * 0.1f, track.height), new Color(0.35f, 0.75f, 0.35f));
                    Fill(new Rect(track.x + track.width * 0.36f, track.y, track.width * 0.09f, track.height), new Color(0.6f, 0.6f, 0.3f));
                    Fill(new Rect(track.x + track.width * 0.55f, track.y, track.width * 0.09f, track.height), new Color(0.6f, 0.6f, 0.3f));
                    Fill(new Rect(track.x + track.width * needle - 4f * s, track.y - 10f * s, 8f * s, track.height + 20f * s), new Color(1f, 0.95f, 0.8f));
                    break;
                case Phase.Done:
                    GUI.color = new Color(1f, 0.85f, 0.4f);
                    GUI.Label(new Rect(track.x, track.y - 10f * s, track.width, 70f * s), $"{LastScore} / 10", big);
                    GUI.color = Color.white;
                    break;
            }
            if (Time.time < feedbackUntil)
            {
                GUI.color = feedbackColour;
                GUI.Label(new Rect(panel.x, panel.y + 200f * s, panel.width, 50f * s), feedback, title);
                GUI.color = Color.white;
            }
        }

        private static void Fill(Rect rect, Color colour)
        {
            Color before = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
