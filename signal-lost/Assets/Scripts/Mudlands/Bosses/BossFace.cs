using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>A boss's expression: what the brows, eyes and mouth are doing.</summary>
    public enum Mood { Neutral, Angry, Furious, Smug, Hurt, Shock, Sleepy, Laugh, Focus }

    /// <summary>
    /// The Mudlands bosses' faces. Each boss builds its brows, eyes (or eyelids) and mouth as separate parts and hands
    /// them here; this blends them between moods, blinks, flaps the mouth while the boss talks, turns the eyes to
    /// whoever it is looking at (the player in a fight, the camera in its entrance), squashes the whole body when it
    /// is hit and lets it breathe while it stands about. Moods come from the fight on their own (calm when idle, the
    /// boss's fighting face when engaged, angrier when nearly beaten, pained for a moment when hurt), and bosses push
    /// their own at the big moments with <see cref="Show"/>.
    /// It runs in <c>LateUpdate</c>, after the boss has posed its rig, and keeps running while the boss's own script
    /// is switched off for its entrance.
    /// </summary>
    public sealed class BossFace : MonoBehaviour
    {
        /// <summary>The parts a boss builds for its face. Any of them may be missing.</summary>
        public struct Rig
        {
            public Transform BrowL, BrowR, EyeL, EyeR, Lids, Mouth, Body;
            /// <summary>How far the brows travel up and down, in metres.</summary>
            public float BrowTravel;
            /// <summary>The mood when idle, and when fighting.</summary>
            public Mood Idle, Fight;
        }

        private struct Look
        {
            public float BrowAngle, BrowRaise, BrowAsym, EyeOpen, MouthOpen, MouthWide, MouthTilt;

            public static Look Lerp(Look a, Look b, float t) => new Look
            {
                BrowAngle = Mathf.Lerp(a.BrowAngle, b.BrowAngle, t),
                BrowRaise = Mathf.Lerp(a.BrowRaise, b.BrowRaise, t),
                BrowAsym = Mathf.Lerp(a.BrowAsym, b.BrowAsym, t),
                EyeOpen = Mathf.Lerp(a.EyeOpen, b.EyeOpen, t),
                MouthOpen = Mathf.Lerp(a.MouthOpen, b.MouthOpen, t),
                MouthWide = Mathf.Lerp(a.MouthWide, b.MouthWide, t),
                MouthTilt = Mathf.Lerp(a.MouthTilt, b.MouthTilt, t),
            };
        }

        // Brow angle: positive pulls the inner ends down (cross), negative lifts them (worried). Raise is in brow
        // travels. Eye open 1 is normal. Mouth open 0 is shut, 1 wide open; wide scales its width; tilt is a smirk.
        private static Look For(Mood mood) => mood switch
        {
            Mood.Angry => new Look { BrowAngle = 22f, BrowRaise = -0.4f, EyeOpen = 0.75f, MouthOpen = 0.3f, MouthWide = 1.15f },
            Mood.Furious => new Look { BrowAngle = 32f, BrowRaise = -0.6f, EyeOpen = 1.15f, MouthOpen = 0.95f, MouthWide = 1.3f },
            Mood.Smug => new Look { BrowAngle = 6f, BrowRaise = 0.1f, BrowAsym = 0.9f, EyeOpen = 0.65f, MouthOpen = 0.08f, MouthWide = 1.15f, MouthTilt = 12f },
            Mood.Hurt => new Look { BrowAngle = -24f, BrowRaise = 0.4f, EyeOpen = 0.15f, MouthOpen = 0.75f, MouthWide = 0.85f },
            Mood.Shock => new Look { BrowAngle = -10f, BrowRaise = 1f, EyeOpen = 1.4f, MouthOpen = 1f, MouthWide = 0.7f },
            Mood.Sleepy => new Look { BrowAngle = -6f, BrowRaise = -0.25f, EyeOpen = 0.3f, MouthOpen = 0.12f, MouthWide = 0.9f },
            Mood.Laugh => new Look { BrowAngle = -12f, BrowRaise = 0.6f, EyeOpen = 0.35f, MouthOpen = 0.9f, MouthWide = 1.3f },
            Mood.Focus => new Look { BrowAngle = 14f, BrowRaise = -0.2f, EyeOpen = 0.8f, MouthOpen = 0f, MouthWide = 0.85f },
            _ => new Look { EyeOpen = 1f, MouthOpen = 0.1f, MouthWide = 1f },
        };

        private Rig rig;
        private MudBoss boss;
        private Health health;
        private Look now;
        private Vector3 browLRest, browRRest, eyeLScale, eyeRScale, lidsScale, mouthScale, bodyScale;
        private Quaternion browLTurn, browRTurn, eyeLTurn, eyeRTurn, mouthTurn;
        private Mood shown = Mood.Neutral;
        private float shownUntil, talkUntil, nextBlink, blinkStart = -1f, squash, squashSpeed, flinchUntil;
        private bool ready;

        /// <summary>A face for <paramref name="owner"/> from the parts its model built.</summary>
        public static BossFace Attach(MudBoss owner, Rig parts)
        {
            var face = owner.gameObject.AddComponent<BossFace>();
            face.boss = owner;
            face.rig = parts;
            if (face.rig.BrowTravel <= 0f) face.rig.BrowTravel = 0.05f;
            face.Capture();
            return face;
        }

        private void Capture()
        {
            if (rig.BrowL != null) { browLRest = rig.BrowL.localPosition; browLTurn = rig.BrowL.localRotation; }
            if (rig.BrowR != null) { browRRest = rig.BrowR.localPosition; browRTurn = rig.BrowR.localRotation; }
            if (rig.EyeL != null) { eyeLScale = rig.EyeL.localScale; eyeLTurn = rig.EyeL.localRotation; }
            if (rig.EyeR != null) { eyeRScale = rig.EyeR.localScale; eyeRTurn = rig.EyeR.localRotation; }
            if (rig.Lids != null) lidsScale = rig.Lids.localScale;
            if (rig.Mouth != null) { mouthScale = rig.Mouth.localScale; mouthTurn = rig.Mouth.localRotation; }
            if (rig.Body != null) bodyScale = rig.Body.localScale;
            now = For(rig.Idle);
            nextBlink = Time.time + Random.Range(1.5f, 4f);
            ready = true;
        }

        private void Start()
        {
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        /// <summary>Holds <paramref name="mood"/> for <paramref name="seconds"/> (over whatever the fight would show).</summary>
        public void Show(Mood mood, float seconds = 1.5f)
        {
            shown = mood;
            shownUntil = Time.time + seconds;
        }

        /// <summary>Back to the fight's own moods at once.</summary>
        public void Clear() => shownUntil = 0f;

        /// <summary>Flaps the mouth for <paramref name="seconds"/> (speech bubbles call this).</summary>
        public void Talk(float seconds) => talkUntil = Mathf.Max(talkUntil, Time.time + seconds);

        /// <summary>A hit: a squash of the whole body, and a pained face for a moment.</summary>
        public void Flinch(float strength)
        {
            strength = Mathf.Clamp01(strength);
            squashSpeed += 2.5f + 6f * strength;
            flinchUntil = Time.time + 0.25f + 0.35f * strength;
        }

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            if (boss == null || boss.IsDead) return;
            Flinch(health.Max > 0f ? amount / (health.Max * 0.08f) : 0.5f);
        }

        private Mood Current()
        {
            if (boss == null) return Mood.Neutral;
            if (Time.time < shownUntil) return shown; // a boss can still pick its face after death (the witch nods off)
            if (boss.IsDead) return Mood.Shock;
            if (Time.time < flinchUntil) return Mood.Hurt;
            if (!boss.Engaged) return rig.Idle;
            if (health != null && health.Normalized < 0.33f && rig.Fight != Mood.Smug) return Mood.Furious;
            return rig.Fight;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            now = Look.Lerp(now, For(Current()), 1f - Mathf.Exp(-14f * dt));

            // Blinks every few seconds (not while the eyes are screwed shut anyway).
            float blink = 1f;
            if (blinkStart < 0f && Time.time >= nextBlink) blinkStart = Time.time;
            if (blinkStart >= 0f)
            {
                float t = (Time.time - blinkStart) / 0.16f;
                blink = 1f - Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.92f;
                if (t >= 1f)
                {
                    blinkStart = -1f;
                    nextBlink = Time.time + Random.Range(2f, 5f) * (Random.value < 0.2f ? 0.15f : 1f); // now and then a double
                }
            }

            // Brows: up/down by the raise, turned about the face's forward axis by the angle (inner ends down = cross).
            Vector3 up = Vector3.up * rig.BrowTravel;
            if (rig.BrowL != null)
            {
                rig.BrowL.localPosition = browLRest + up * (now.BrowRaise + now.BrowAsym);
                rig.BrowL.localRotation = browLTurn * Quaternion.Euler(0f, 0f, -now.BrowAngle + now.BrowAsym * 10f);
            }
            if (rig.BrowR != null)
            {
                rig.BrowR.localPosition = browRRest + up * now.BrowRaise;
                rig.BrowR.localRotation = browRTurn * Quaternion.Euler(0f, 0f, now.BrowAngle);
            }

            // Eyes: open by the mood and the blink, turned toward whoever the boss is looking at.
            float open = Mathf.Max(0.06f, now.EyeOpen * blink);
            Quaternion gazeL = Gaze(rig.EyeL), gazeR = Gaze(rig.EyeR);
            if (rig.EyeL != null)
            {
                rig.EyeL.localScale = new Vector3(eyeLScale.x, eyeLScale.y * open, eyeLScale.z);
                rig.EyeL.localRotation = Quaternion.Slerp(rig.EyeL.localRotation, eyeLTurn * gazeL, 1f - Mathf.Exp(-10f * dt));
            }
            if (rig.EyeR != null)
            {
                rig.EyeR.localScale = new Vector3(eyeRScale.x, eyeRScale.y * open, eyeRScale.z);
                rig.EyeR.localRotation = Quaternion.Slerp(rig.EyeR.localRotation, eyeRTurn * gazeR, 1f - Mathf.Exp(-10f * dt));
            }
            // Heavy lids (the witch): taller the sleepier, almost gone when wide awake.
            if (rig.Lids != null)
                rig.Lids.localScale = new Vector3(lidsScale.x, lidsScale.y * Mathf.Clamp(1f + 3f * (1f - open), 0.25f, 4.5f), lidsScale.z);

            // Mouth: opens with the mood and flaps while talking.
            if (rig.Mouth != null)
            {
                float mouthOpen = now.MouthOpen;
                if (Time.time < talkUntil) mouthOpen = Mathf.Max(mouthOpen, 0.15f + 0.75f * Mathf.Abs(Mathf.Sin(Time.time * 15f)));
                rig.Mouth.localScale = new Vector3(mouthScale.x * now.MouthWide, mouthScale.y * Mathf.Lerp(0.3f, 2.4f, mouthOpen), mouthScale.z);
                rig.Mouth.localRotation = mouthTurn * Quaternion.Euler(0f, 0f, now.MouthTilt);
            }

            // The body: a springy squash when hit, and slow breathing.
            if (rig.Body != null)
            {
                squashSpeed += (-squash * 160f - squashSpeed * 11f) * dt;
                squash += squashSpeed * dt;
                float s = Mathf.Clamp(squash, -0.25f, 0.25f) * 0.5f;
                float breath = Mathf.Sin(Time.time * 1.7f) * 0.012f;
                rig.Body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + s * 0.8f, 1f - s + breath, 1f + s * 0.8f));
            }
        }

        // The turn that points an eye at the target (clamped, so the pupils stay on the eyeballs).
        private Quaternion Gaze(Transform eye)
        {
            if (eye == null || eye.parent == null) return Quaternion.identity;
            Vector3? target = boss != null ? boss.FaceLookPoint : null;
            if (target == null) return Quaternion.identity;
            Vector3 local = eye.parent.InverseTransformPoint(target.Value) - eye.localPosition;
            if (local.sqrMagnitude < 0.0001f) return Quaternion.identity;
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -28f, 28f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -20f, 20f);
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        // ---------- Parts ----------

        /// <summary>A brow: a soft, slightly arched bar, its own pivot so it can tilt and lift.</summary>
        public static Transform Brow(Transform head, string key, Vector3 at, float side, Vector3 size, Color color, float tilt = 0f)
        {
            Transform pivot = MudArt.Pivot(head, side < 0f ? "Brow L" : "Brow R", at, Quaternion.Euler(0f, 0f, tilt * side));
            MudArt.Part(pivot, "Brow", Vector3.zero, Quaternion.identity, key, k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, size * 0.5f, Quaternion.identity, color, 1);
            });
            return pivot;
        }

        /// <summary>
        /// A mouth that can open: a dark, rounded slot with a tongue and, optionally, a row of teeth along the top,
        /// all squashed shut at rest (scale y) and stretched open by the face.
        /// </summary>
        public static Transform Mouth(Transform head, string key, Vector3 at, float width, float height, Color lips, bool teeth,
            Quaternion facing = default)
        {
            Transform pivot = MudArt.Pivot(head, "Mouth", at, facing == default ? Quaternion.identity : facing);
            MudArt.Part(pivot, "Mouth Shape", Vector3.zero, Quaternion.identity, key, k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(0f, 0f, -height * 0.15f), new Vector3(width * 0.5f + height * 0.12f, height * 0.62f, height * 0.4f), Quaternion.identity, lips, 1);
                k.Ball(Vector3.zero, new Vector3(width * 0.5f, height * 0.5f, height * 0.35f), Quaternion.identity, new Color(0.22f, 0.05f, 0.06f), 1);
                k.Ball(new Vector3(0f, -height * 0.25f, height * 0.08f), new Vector3(width * 0.28f, height * 0.2f, height * 0.25f), Quaternion.identity, new Color(0.85f, 0.35f, 0.4f), 1);
                if (teeth) k.Box(new Vector3(0f, height * 0.32f, height * 0.2f), new Vector3(width * 0.8f, height * 0.22f, height * 0.12f), MudPalette.White);
            });
            pivot.localScale = new Vector3(1f, 0.6f, 1f);
            return pivot;
        }
    }
}
