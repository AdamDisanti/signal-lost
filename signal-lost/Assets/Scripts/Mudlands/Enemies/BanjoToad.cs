using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Inventory;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Support enemy: a round alien toad (orange, with glowing cyan spots and a throat sac that puffs up and glows on
    /// the beat) in a vest and bow tie, with a banjo made from a riveted hull plate and a ship's antenna. In a fight it
    /// plays a tune that makes nearby
    /// allies 25% faster and heals them 3 a second (rings pulse out on the beat, notes float up), while it keeps
    /// 9-14 m from you and tucks itself behind the biggest ally it can find. Get within 3.5 m and it winds up and
    /// twangs a shockwave (6 and a big shove), then hops away. When it dies its string snaps and the allies near it
    /// stop for a moment to mourn, which is your opening. Kill it first. Runs off clutching the banjo when a car comes.
    /// 50 HP.
    /// </summary>
    public sealed class BanjoToad : MudCritter
    {
        public static readonly CritterSpec Defaults = new CritterSpec("Banjo Toad", 50f, 0.45f, 1.1f, 3.4f, 16f,
            (ScrapType.Wiring, 1, 2), (ScrapType.CircuitBoards, 1, 1));

        private enum State { Idle, Play, Twang, Hop, Flee }

        private const float AuraRadius = 9f, HealPerSecond = 3f;
        // Alien colours: orange skin, a pale yellow belly, and cyan spots and throat sac that glow.
        private static readonly Color Skin = new Color(0.9f, 0.55f, 0.18f), Belly = new Color(0.98f, 0.88f, 0.6f),
            Vest = new Color(0.55f, 0.18f, 0.16f), Glow = new Color(0.3f, 0.9f, 1f);

        private State state = State.Idle;
        private float stateTime, nextBeat, nextTwang, nextTune;
        private Vector3 hopTo;
        private Transform torso, banjo, strumArm, footL, footR, throatSac;

        protected override void BuildModel(Transform visual)
        {
            torso = MudArt.Pivot(visual, "Torso", new Vector3(0f, 0.4f, 0f));
            MudArt.Part(torso, "Body", Vector3.zero, Quaternion.identity, "toad_body", k =>
            {
                k.Ball(new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.38f, 0.4f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(0f, -0.02f, 0.16f), new Vector3(0.36f, 0.3f, 0.28f), Quaternion.identity, Belly, 1);
                k.Box(new Vector3(-0.2f, 0.05f, 0.27f), new Vector3(0.18f, 0.42f, 0.14f), Quaternion.Euler(0f, 20f, -6f), Vest);
                k.Box(new Vector3(0.2f, 0.05f, 0.27f), new Vector3(0.18f, 0.42f, 0.14f), Quaternion.Euler(0f, -20f, 6f), Vest);
                for (int i = 0; i < 6; i++) k.Ball(new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.1f, 0.3f), -0.3f), 0.05f, Glow, 0, 1.2f);
            });
            Head = MudArt.Pivot(torso, "Head", new Vector3(0f, 0.35f, 0.1f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "toad_head", k =>
            {
                k.Ball(new Vector3(0f, 0.02f, 0.05f), new Vector3(0.38f, 0.2f, 0.3f), Quaternion.identity, Skin, 1);
                k.Box(new Vector3(0f, -0.07f, 0.3f), new Vector3(0.42f, 0.02f, 0.04f), Quaternion.identity, new Color(0.4f, 0.18f, 0.1f));
                // A bow tie.
                k.Cone(new Vector3(0f, -0.2f, 0.28f), Quaternion.Euler(0f, 0f, 90f), 0.06f, 0.12f, 3, new Color(0.15f, 0.15f, 0.4f));
                k.Cone(new Vector3(0f, -0.2f, 0.28f), Quaternion.Euler(0f, 0f, -90f), 0.06f, 0.12f, 3, new Color(0.15f, 0.15f, 0.4f));
            });
            throatSac = MudArt.Part(Head, "Throat Sac", new Vector3(0f, -0.12f, 0.25f), Quaternion.identity, "toad_sac",
                k => k.Ball(Vector3.zero, new Vector3(0.14f, 0.07f, 0.09f), Quaternion.identity, Glow, 1, 0.8f));
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.17f, 0.12f), 0.17f, 0.075f, new Color(0.95f, 0.85f, 0.3f), MudPalette.Black, "toad");
            EyeL = l;
            EyeR = r;
            banjo = MudArt.Pivot(torso, "Banjo", new Vector3(-0.05f, 0.02f, 0.42f), Quaternion.Euler(0f, 0f, -35f));
            // The banjo is ship salvage: a round hull plate for the drum (rivets round the rim, the orange stripe) and
            // a whip antenna for the neck, its tip light still blinking red.
            MudArt.Part(banjo, "Banjo Model", Vector3.zero, Quaternion.identity, "toad_banjo", k =>
            {
                k.Prism(new Vector3(0f, 0f, -0.04f), Quaternion.Euler(90f, 0f, 0f), 0.2f, 0.2f, 0.08f, 12, MudPalette.HullDark);
                k.Prism(new Vector3(0f, 0f, 0.041f), Quaternion.Euler(90f, 0f, 0f), 0.18f, 0.18f, 0.01f, 12, MudPalette.Hull);
                k.Box(new Vector3(0.07f, 0f, 0.05f), new Vector3(0.05f, 0.33f, 0.01f), MudPalette.HullStripe);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f + 0.2f;
                    k.Ball(new Vector3(Mathf.Cos(a) * 0.16f, Mathf.Sin(a) * 0.16f, 0.05f), 0.013f, MudPalette.HullDark, 0);
                }
                k.Rod(new Vector3(-0.15f, 0f, 0.04f), new Vector3(-0.7f, 0f, 0.04f), 0.026f, 6, MudPalette.Metal, 0f, 0.018f);
                foreach (float x in new[] { -0.3f, -0.5f, -0.66f }) k.Rod(new Vector3(x, 0f, 0.04f), new Vector3(x - 0.03f, 0f, 0.04f), 0.032f, 6, MudPalette.HullDark);
                k.Ball(new Vector3(-0.73f, 0f, 0.04f), 0.035f, new Color(1f, 0.25f, 0.2f), 0, 3f);
                for (int i = -1; i <= 1; i++) k.Box(new Vector3(-0.3f, i * 0.015f, 0.065f), new Vector3(0.8f, 0.004f, 0.004f), MudPalette.Metal);
            });
            strumArm = MudArt.Pivot(torso, "Strum Arm", new Vector3(0.35f, 0.1f, 0.2f));
            MudArt.Part(strumArm, "Arm", Vector3.zero, Quaternion.identity, "toad_arm", k =>
            {
                k.Rod(Vector3.zero, new Vector3(-0.15f, -0.15f, 0.2f), 0.06f, 5, Skin);
                k.Ball(new Vector3(-0.16f, -0.16f, 0.22f), 0.06f, Skin, 0);
            });
            footL = MudArt.Part(visual, "Foot L", new Vector3(-0.25f, 0.05f, 0.1f), Quaternion.identity, "toad_foot", k => k.Ball(Vector3.zero, new Vector3(0.14f, 0.06f, 0.2f), Quaternion.identity, Skin, 0));
            footR = MudArt.Part(visual, "Foot R", new Vector3(0.25f, 0.05f, 0.1f), Quaternion.identity, "toad_foot", k => k.Ball(Vector3.zero, new Vector3(0.14f, 0.06f, 0.2f), Quaternion.identity, Skin, 0));
        }

        protected override void OnCritterStarted() => state = State.Idle;

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Think(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle:
                    Halt();
                    if (IdleEmoteDue()) Say("~la la~", new Color(1f, 0.9f, 0.5f), 1.2f, 3.5f);
                    if (Notices()) Enter(State.Play);
                    break;
                case State.Play:
                    if (GaveUp() || !TargetAlive) { Enter(State.Idle); break; }
                    Reposition();
                    TurnToward(PlayerPosition, 240f * dt);
                    PlayTune();
                    if (PlayerDistance < 3.5f && Time.time >= nextTwang) { Enter(State.Twang); Halt(); Say("!", new Color(1f, 0.9f, 0.4f), 0.6f); }
                    break;
                case State.Twang:
                    TurnToward(PlayerPosition, 400f * dt);
                    if (stateTime >= 0.5f)
                    {
                        Twang();
                        nextTwang = Time.time + 3.5f;
                        Vector3 away = (transform.position - PlayerPosition).Flat().normalized;
                        hopTo = transform.position + away * 5f;
                        Enter(State.Hop);
                    }
                    break;
                case State.Hop:
                    MoveTo(hopTo, 6f);
                    if (stateTime > 0.8f) Enter(State.Play);
                    break;
                case State.Flee:
                    MoveTo(hopTo, 6f);
                    if (stateTime > 1.6f) Enter(Notices() ? State.Play : State.Idle);
                    break;
            }
            WatchPlayer(55f);
        }

        // Keep 9-14 m away, behind the biggest friend nearby if there is one.
        private void Reposition()
        {
            MudCritter shield = null;
            float best = float.MaxValue;
            foreach (MudCritter other in Living)
            {
                if (other == this || other.IsDead || other is Mudpuppy) continue;
                float d = (other.transform.position - transform.position).sqrMagnitude;
                if (d < best && d < 15f * 15f) { best = d; shield = other; }
            }
            Vector3 goal;
            Vector3 fromPlayer = (transform.position - PlayerPosition).Flat();
            if (shield != null)
                goal = shield.transform.position + (shield.transform.position - PlayerPosition).Flat().normalized * 3f;
            else
            {
                float distance = fromPlayer.magnitude;
                if (distance >= 9f && distance <= 14f) { Halt(); return; }
                goal = PlayerPosition + fromPlayer.normalized * 11.5f;
            }
            MoveTo(goal, Defaults.Speed);
        }

        private void PlayTune()
        {
            if (Time.time >= nextTune)
            {
                nextTune = Time.time + 1.8f;
                Sfx.PlayAt("banjo_tune", transform.position, 0.45f, Random.Range(0.95f, 1.05f), 30f);
            }
            if (Time.time < nextBeat) return;
            nextBeat = Time.time + 0.5f;
            Squash(0.12f);
            ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 0.2f, AuraRadius, 0.5f, 0.12f);
            Emote.Show(banjo, Random.value < 0.5f ? "~twang~" : "~plink~", new Color(1f, 0.9f, 0.5f), 0.5f, 1f, 3f);
            foreach (Collider hit in Physics.OverlapSphere(transform.position, AuraRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health ally = hit.GetComponentInParent<Health>();
                if (ally == null || ally.IsDead || ally.gameObject == gameObject || !EnemyAllegiance.IsEnemy(ally.gameObject)) continue;
                ally.Heal(HealPerSecond * 0.5f);
                ally.GetComponentInParent<MudCritter>()?.Buff(1.25f, 1.2f);
            }
        }

        private void Twang()
        {
            Sfx.PlayAt("banjo_twang", transform.position, 1f, 0.8f, 30f);
            ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), transform.position + Vector3.up * 0.4f, 4f, 0.35f, 0.4f);
            CameraShake.Add(0.12f);
            if (TargetAlive && PlayerDistance < 4f) Strike(4f, 360f, 6f, 5f);
        }

        // ---------- Reactions ----------

        protected override void OnSpotted()
        {
            Say("~TWANG~!", new Color(1f, 0.9f, 0.5f), 1f, 4f);
            Squash(-0.3f);
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Idle) Enter(State.Play);
            Say(Random.value < 0.5f ? "*twang*" : "!", new Color(1f, 0.8f, 0.5f), 0.6f, 2.5f);
        }

        public override void OnCarApproaching(DrunkCar car, float secondsAway)
        {
            if (state == State.Flee || secondsAway > 2f) return;
            Vector3 path = car.Velocity.Flat();
            Vector3 side = Vector3.Cross(Vector3.up, path.normalized);
            if (Vector3.Dot(side, transform.position - car.transform.position) < 0f) side = -side;
            hopTo = transform.position + side * 6f;
            Enter(State.Flee);
            Say("NOT THE BANJO!", new Color(1f, 0.6f, 0.5f), 1.2f, 2.8f);
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit)
        {
            if (hit.Speed >= 6f) Sfx.PlayAt("banjo_twang", transform.position, 1f, 1.4f, 30f);
            return base.OnVehicleHit(hit);
        }

        protected override void OnCritterDeath()
        {
            Sfx.PlayAt("banjo_twang", transform.position, 1f, 0.5f, 35f);
            // The band mourns: everything nearby stops for a moment.
            foreach (MudCritter other in Living)
            {
                if (other == this || other.IsDead || (other.transform.position - transform.position).sqrMagnitude > 144f) continue;
                Emote.Show(other.transform, "T_T", new Color(0.6f, 0.8f, 1f), 1.8f, 1.6f, 3f);
                other.Stun(1.2f);
            }
        }

        protected override void Animate(float dt)
        {
            if (IsStunned || Airborne) return;
            float t = Time.time;
            bool playing = state == State.Play;
            strumArm.localRotation = Quaternion.Euler(playing ? Mathf.Sin(t * 22f) * 25f : state == State.Twang ? -60f * (stateTime / 0.5f) : 0f, 0f, 0f);
            float tap = playing ? Mathf.Max(0f, Mathf.Sin(t * 12.6f)) * 0.06f : 0f;
            footR.localPosition = new Vector3(0.25f, 0.05f + tap, 0.1f);
            float hop = state == State.Hop || state == State.Flee ? Mathf.Abs(Mathf.Sin(stateTime * 9f)) * 0.35f : 0f;
            Visual.localPosition = VisualRest + Vector3.up * hop;
            torso.localRotation = Quaternion.Euler(state == State.Twang ? -15f : 0f, 0f, playing ? Mathf.Sin(t * 6.3f) * 6f : 0f);
            banjo.localRotation = state == State.Flee ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(0f, 0f, -35f);
            // The throat sac puffs up on the beat while it plays.
            throatSac.localScale = Vector3.one * (playing ? 1f + 0.4f * Mathf.Abs(Mathf.Sin(t * 6.3f)) : 0.75f);
        }

        protected override void DeathPose(float t)
        {
            // Flops forward onto its banjo.
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(80f * Mathf.Clamp01(t / 0.4f), 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.down * 0.5f * Mathf.Clamp01((t - 2f) / 1f);
        }
    }
}
