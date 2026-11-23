using System.Collections;
using SignalLost.Audio;
using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Briarwing, the Thorn Wyvern (the Wreck Field; docs/desert-bosses.md). Mostly grounded; it takes off only for its
    /// signature dives.
    ///
    /// Phase 1 (Briar): wing-claw swipes (left, right), tail lash when you're beside or behind it, the briar lunge (a
    /// biting lunge at range), and the thorn barrage (rears up, three volleys of five thorns that stick in the ground).
    /// Phase 2 (Bristling): its thorns glow; the swipes end in a delayed two-handed slam, the barrage tracks you, and the
    /// spinning dive (a hop, a hang, then a corkscrew dive along a line, trailing thorns) is added.
    /// Phase 3 (Thornstorm): the spike dive bomb (it climbs high, its shadow hunts you, it plunges and spikes burst out in
    /// two expanding rings) and longer combos.
    ///
    /// Openings: after the slam, the lunge, the spinning dive (it lands tangled) and the dive bomb (it's stuck in the
    /// sand). Its throat glow is a weak spot, doubly so while it barrages.
    ///
    /// Flight (11-22, the user: real flight, not a stiff, over-powered lift): the body rises on each wing's downstroke
    /// and barely on the folded upstroke (<see cref="BeatLift"/>, timed by the clips' "downstroke"/"upstroke" moments),
    /// it climbs at a heavy flyer's pace, it flies where it is heading and banks into its turns and pitches with its
    /// climb (<see cref="Lean"/>), it glides when it is up to speed and hovers when it holds still, it stoops with its
    /// wings folding, and if it loses its prey in the air it flares and lands (<see cref="Landing"/>).
    /// </summary>
    public sealed class Briarwing : DuelBoss
    {
        [Header("Briarwing")]
        [SerializeField] private Material thornMaterial;
        [Tooltip("The throat's hitbox (a weak spot).")]
        [SerializeField] private Health throat;
        [SerializeField, Min(0f)] private float swipeDamage = 22f;
        [SerializeField, Min(0f)] private float slamDamage = 32f;
        [SerializeField, Min(0f)] private float tailDamage = 24f;
        [SerializeField, Min(0f)] private float lungeDamage = 28f;
        [SerializeField, Min(0f)] private float thornDamage = 13f;
        [SerializeField, Min(0f)] private float diveDamage = 30f;
        [SerializeField, Min(0f)] private float bombDamage = 35f;
        [SerializeField, Min(0f)] private float spikeDamage = 22f;

        [Header("Flight")]
        [Tooltip("Metres a second it climbs on average while taking off (faster on the downstroke, slower on the upstroke).")]
        [SerializeField, Min(1f)] private float climbSpeed = 5.5f;
        [Tooltip("How high the dive bomb climbs above the ground, in metres.")]
        [SerializeField, Min(4f)] private float diveBombHeight = 13f;
        [Tooltip("Top speed while hunting from the air, metres a second.")]
        [SerializeField, Min(1f)] private float flySpeed = 9f;
        [Tooltip("Most it can bank in a turn, degrees.")]
        [SerializeField, Range(0f, 60f)] private float maxBank = 35f;

        private Material thornGlow;
        private bool barraging;
        private Color glow;
        private Transform visual;
        private Vector3 bodyCentre;
        private float roll, pitch;

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            RelayPart(throat, 0);
            visual = transform.Find("Visual");
            // The body's middle (the spine), about which it banks and pitches in the air.
            bodyCentre = visual != null ? visual.parent.InverseTransformPoint(BonePosition("Spine")) : Vector3.up * 2.8f;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                    if (m.name.StartsWith("BriarwingThorn")) thornGlow = m;
            SetGlow(Color.black);
        }

        protected override void DefineAttacks()
        {
            Attacks.Add(new Attack { Name = "Swipes", Run = Swipes, MaxRange = 7f, MaxAngle = 65f, Weight = 3f, Cooldown = 2.5f });
            Attacks.Add(new Attack { Name = "TailLash", Run = TailLash, MaxRange = 9f, MinAngle = 75f, Weight = 5f, Cooldown = 4f });
            Attacks.Add(new Attack { Name = "Lunge", Run = Lunge, MinRange = 5f, MaxRange = 13f, MaxAngle = 45f, Weight = 2.5f, Cooldown = 4f });
            Attacks.Add(new Attack { Name = "Barrage", Run = Barrage, MinRange = 9f, MaxRange = 32f, Weight = 2f, Cooldown = 10f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "SpinDive", Run = SpinDive, MinRange = 7f, MaxRange = 26f, MinPhase = 2, Weight = 2.2f, Cooldown = 9f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "DiveBomb", Run = DiveBomb, MinRange = 0f, MinPhase = 3, Weight = 2.5f, Cooldown = 15f, HyperArmor = true });
        }

        protected override void OnDuelPhase(int phase)
        {
            SetGlow(phase >= 3 ? new Color(0.6f, 1.6f, 0.35f) : phase == 2 ? new Color(0.3f, 0.8f, 0.18f) : Color.black);
        }

        protected override void OnDuelReset()
        {
            SetGlow(Color.black);
            Level();
        }

        protected override void OnMoveEnded()
        {
            barraging = false;
            SetGlow(glow);
            Level();
        }

        protected override float OnPartHit(int part, float amount, GameObject source) => amount * (barraging ? 2f : 1.25f);

        private void SetGlow(Color color, bool remember = true)
        {
            if (remember) glow = color;
            if (thornGlow == null) return;
            thornGlow.EnableKeyword("_EMISSION");
            thornGlow.SetColor("_EmissionColor", color);
        }

        private bool Close(float range) => Target != null && FlatDistance(Target.position) <= range;

        // ---------- Wing-claw swipes, the delayed slam ----------

        private IEnumerator Swipes()
        {
            LookDuringMove(true);
            bool slam = DuelPhase >= 2;
            yield return Swipe("SwipeL", "WingHandL", false);
            if (!Close(8f)) yield break;
            yield return Swipe("SwipeR", "WingHandR", !slam);
            if (!slam) yield break;
            if (!Close(9f)) yield break;
            yield return Slam();
            if (DuelPhase >= 3 && Random.value < 0.45f && Target != null && FlatDistance(Target.position) > 6f)
                yield return SpinDive();
        }

        private IEnumerator Swipe(string clip, string hand, bool last)
        {
            Play(clip);
            float move = Moment("move"), hit = Moment("hit");
            yield return UntilTime(move, () => CloseIn(4.5f, 170f));
            Sfx.PlayAt("chain_swish", transform.position, 0.9f, 0.7f, 40f);
            yield return StrikeWindow(hit - 0.1f, hit + 0.12f, () => BonePosition(hand), 2.1f, swipeDamage, 2.5f, false, () => Lurch(5f));
            if (last) yield return Finish(() => Track(40f));
            else yield return UntilTime(hit + 0.3f, () => Track(60f));
        }

        private IEnumerator Slam()
        {
            Play("Slam");
            float move = Moment("move"), hit = Moment("hit");
            // The delay: it hangs at the top far longer than the swipes, to catch an early dodge.
            yield return UntilTime(move, () => Track(120f));
            yield return UntilTime(hit, () => Lurch(4f));
            Vector3 centre = (BonePosition("WingHandL") + BonePosition("WingHandR")) * 0.5f;
            Strike(centre, 3.2f, slamDamage, 4f);
            Sfx.PlayAt("laser_slam", centre, 1f, 0.6f, 50f);
            if (DuelPhase >= 3) Keep(SpikeRing.Spawn(spikeMesh, thornMaterial, GroundBelow(centre), 1.5f, 6f, 9f, spikeDamage * 0.7f, gameObject, 1.3f, 0.6f, 1.2f));
            yield return Finish();
        }

        // ---------- Tail lash ----------

        private IEnumerator TailLash()
        {
            Play("TailLash");
            float from = Moment("hit_start"), to = Moment("hit_end");
            yield return UntilTime(from - 0.1f);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.5f, 40f);
            yield return StrikeWindow(from, to, ClosestTail, 2.4f, tailDamage, 5f, true);
            yield return Finish();
        }

        private Vector3 ClosestTail()
        {
            Vector3 best = BonePosition("Tail4");
            float d = BossFx.DistanceToPlayer(best);
            foreach (string bone in new[] { "Tail2", "Tail3", "Tail5" })
            {
                Vector3 p = BonePosition(bone);
                float pd = BossFx.DistanceToPlayer(p);
                if (pd < d)
                {
                    d = pd;
                    best = p;
                }
            }
            return best;
        }

        // ---------- Briar lunge ----------

        private IEnumerator Lunge()
        {
            LookDuringMove(true);
            Play("Lunge");
            float start = Moment("move_start"), end = Moment("move_end"), hit = Moment("hit");
            yield return UntilTime(start, () => Track(220f));
            LookDuringMove(false);
            Sfx.PlayAt("crab_snip", transform.position, 1f, 0.5f, 40f);
            float reach = Target != null ? Mathf.Clamp(FlatDistance(Target.position) - 3.6f, 1.5f, 9f) : 6f;
            var landed = new Ref<bool>();
            IEnumerator travel = Travel(transform.forward, reach, (end - start) * 1.1f);
            while (travel.MoveNext())
            {
                if (!landed.Value && ClipTime >= hit - 0.08f && Strike(BonePosition("Mouth"), 2.0f, lungeDamage, 3f)) landed.Value = true;
                yield return travel.Current;
            }
            yield return StrikeWindow(hit - 0.05f, hit + 0.15f, () => BonePosition("Mouth"), 2.0f, landed.Value ? 0f : lungeDamage, 3f);
            yield return Finish();
        }

        // ---------- Thorn barrage ----------

        private IEnumerator Barrage()
        {
            barraging = true;
            Play("Barrage");
            float flare = Moment("flare");
            yield return UntilTime(flare, () => Track(150f));
            SetGlow(new Color(1.2f, 2.4f, 0.6f), false);
            Sfx.PlayAt("magic_charge", transform.position, 0.9f, 0.7f, 50f);
            float[] volleys = Table.Times(Playing, "volley");
            for (int v = 0; v < volleys.Length; v++)
            {
                yield return UntilTime(volleys[v], () => Track(DuelPhase >= 2 ? 160f : 40f));
                Volley(v);
            }
            SetGlow(glow);
            yield return Finish();
        }

        private void Volley(int index)
        {
            if (Target == null) return;
            Vector3 from = BonePosition("Mouth");
            Vector3 aim = BossFx.PlayerChest;
            if (DuelPhase >= 2 && BossFx.PlayerBody != null) aim += BossFx.PlayerBody.velocity * 0.35f; // leads you
            Vector3 dir = (aim - from).normalized;
            int count = DuelPhase >= 3 ? 7 : 5;
            float spread = DuelPhase >= 3 ? 9f : 11f;
            float offset = index % 2 == 1 ? spread * 0.5f : 0f; // alternate volleys interleave
            for (int i = 0; i < count; i++)
            {
                float yaw = (i - (count - 1) * 0.5f) * spread + offset;
                Vector3 d = Quaternion.AngleAxis(yaw, Vector3.up) * dir;
                Keep(ThornShot.Fire(spikeMesh, thornMaterial, from + d * 1.2f, d, 28f, thornDamage, gameObject, DuelPhase >= 3 ? 6f : 4f));
            }
            Sfx.PlayAt("laser_shot_weak", from, 1f, 0.45f, 60f);
        }

        // ---------- Flight ----------

        /// <summary>
        /// How hard the wings are lifting right now, as a multiple of a steady climb: up to about 2 through a
        /// downstroke (shaped like the beat), about 0.4 on the folded upstroke, 1 when gliding, and 0 before the
        /// first beat of a takeoff. Read from the playing clip's "downstroke" and "upstroke" moments.
        /// </summary>
        private float BeatLift()
        {
            float[] downs = Table.Times(Playing, "downstroke"), ups = Table.Times(Playing, "upstroke");
            if (downs.Length == 0 || ups.Length == 0) return 1f;
            float t = ClipTime;
            if (Playing == "Flap" || Playing == "Hover") t %= Table.Length(Playing);
            float down = -1f, up = -1f, nextUp = -1f;
            foreach (float d in downs) if (d <= t) down = d;
            foreach (float u in ups)
            {
                if (u <= t) up = u;
                else if (nextUp < 0f && u > down) nextUp = u;
            }
            if (down < 0f) return 0f;
            if (up > down) return 0.4f;
            float length = (nextUp > 0f ? nextUp : down + 0.45f) - down;
            return 0.4f + 1.6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - down) / length));
        }

        /// <summary>Banks into a turn (degrees a second of yaw) and pitches with a climb (metres a second), about the body's middle.</summary>
        private void Lean(float yawRate, float climb)
        {
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            roll = Mathf.Lerp(roll, Mathf.Clamp(-yawRate * 0.32f, -maxBank, maxBank), k);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(-climb * 2.2f, -16f, 14f), k);
            ApplyLean();
        }

        private void Level()
        {
            roll = pitch = 0f;
            ApplyLean();
        }

        private void ApplyLean()
        {
            if (visual == null) return;
            Quaternion lean = Quaternion.Euler(pitch, 0f, roll);
            visual.localRotation = lean;
            visual.localPosition = bodyCentre - lean * bodyCentre;
        }

        /// <summary>
        /// One frame of flight towards a point: turns (at most <paramref name="turnRate"/> degrees a second) to head
        /// for it, flies the way it is heading at up to <paramref name="speed"/> (gaining at most
        /// <paramref name="accel"/>), climbs or sinks at <paramref name="climb"/> and banks and pitches to suit.
        /// Returns its new horizontal speed.
        /// </summary>
        private float Fly(Vector3 toward, float speed, float accel, float turnRate, float climb, float currentSpeed)
        {
            Vector3 to = toward - transform.position;
            to.y = 0f;
            float before = transform.eulerAngles.y;
            if (to.sqrMagnitude > 0.25f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnRate * Time.deltaTime);
            float yawRate = Mathf.DeltaAngle(before, transform.eulerAngles.y) / Mathf.Max(Time.deltaTime, 1e-4f);
            float want = Mathf.Min(speed, to.magnitude * 1.2f);
            currentSpeed = Mathf.MoveTowards(currentSpeed, want, accel * Time.deltaTime);
            Vector3 step = transform.forward * (currentSpeed * Time.deltaTime) + Vector3.up * (climb * Time.deltaTime);
            Vector3 next = transform.position + step;
            Vector3 fromHome = next - Home;
            fromHome.y = 0f;
            if (fromHome.magnitude > ArenaRadius) next -= fromHome.normalized * (fromHome.magnitude - ArenaRadius);
            transform.position = next;
            Lean(yawRate, climb);
            return currentSpeed;
        }

        /// <summary>Lost its prey in the air: flares, beats twice to brake and lands just ahead, then folds its wings.</summary>
        private IEnumerator Landing()
        {
            Play("Land", 0.25f);
            Vector3 from = transform.position;
            Vector3 ground = ClampToArena(from + transform.forward * 5f, 2f);
            float touch = Moment("touchdown");
            yield return UntilTime(touch, () =>
            {
                float u = Mathf.Clamp01(ClipTime / touch);
                float eased = 1f - (1f - u) * (1f - u); // fast at first, slowing as the flare bites
                float before = transform.position.y;
                transform.position = Vector3.Lerp(from, ground, eased);
                Lean(0f, (transform.position.y - before) / Mathf.Max(Time.deltaTime, 1e-4f) * 0.4f);
            });
            Level();
            SetAirborne(false);
            Sfx.PlayAt("laser_slam", transform.position, 0.5f, 0.5f, 50f); // the thump of a heavy landing
            yield return Finish();
        }

        // ---------- Spinning dive ----------

        private IEnumerator SpinDive()
        {
            Play("Hop");
            yield return Until("launch", 0, () => Track(220f));
            SetAirborne(true);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.4f, 50f);
            Vector3 start = transform.position;
            Vector3 apex = start + Vector3.up * 5.5f - transform.forward * 1.5f;
            // The spring and one full downstroke carry it up; then it hovers, rising a little on each beat, and slows to a
            // hang at the top rather than stopping dead.
            void Rise(float most)
            {
                Vector3 p = transform.position;
                // A leap this high starts at about 10 m/s; the wings add to it on the downstroke, less on the upstroke.
                float climb = Mathf.Clamp((apex.y - p.y) * 3f, -1f, most) * (0.6f + 0.4f * BeatLift());
                Vector3 flat = Vector3.MoveTowards(new Vector3(p.x, 0f, p.z), new Vector3(apex.x, 0f, apex.z), 3f * Time.deltaTime);
                transform.position = new Vector3(flat.x, p.y + climb * Time.deltaTime, flat.z);
                Lean(0f, climb);
                Track(300f);
            }
            yield return UntilTime(Table.Length("Hop") - 0.08f, () => Rise(7f));
            Play("Hover", 0.15f);
            yield return Seconds(0.45f / Tempo, () => Rise(5f));
            // Hang: it locks on, then commits.
            Vector3 target = BossFx.PlayerFeet;
            GroundMarker marker = Marker(GroundBelow(target), 2.6f);
            yield return Seconds(0.4f / Tempo, () =>
            {
                Rise(3f);
                target = BossFx.PlayerFeet;
                marker.MoveTo(GroundBelow(target));
                marker.Progress += Time.deltaTime * 2f;
            });
            if (Target == null)
            {
                Destroy(marker.gameObject);
                yield return Landing();
                yield break;
            }
            Level(); // the corkscrew spins the body itself
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            Vector3 end = ClampToArena(target + flat.normalized * 3f, 2f);
            Play("SpinDive", 0.08f);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.3f, 60f);
            Vector3 from = transform.position;
            float length = Vector3.Distance(from, end), travelled = 0f, nextThorn = 1.5f, speed = 9f;
            bool hit = false;
            while (travelled < length)
            {
                speed = Mathf.Min(25f * Tempo, speed + 60f * Time.deltaTime); // it gathers speed into the dive
                travelled = Mathf.Min(length, travelled + speed * Time.deltaTime);
                transform.position = Vector3.Lerp(from, end, travelled / length);
                if (!hit && Strike(transform.position + Vector3.up * 2.4f, 2.8f, diveDamage, 5f)) hit = true;
                if (travelled >= nextThorn && transform.position.y - GroundBelow(transform.position).y < 3.5f)
                {
                    nextThorn += 1.8f;
                    Vector3 g = GroundBelow(transform.position);
                    Keep(GroundThorn.Spawn(spikeMesh, thornMaterial, g + Vector3.up * 0.2f, (Vector3.down + Random.insideUnitSphere * 0.4f).normalized,
                        DuelPhase >= 3 ? 5f : 3.5f, 8f, gameObject, 1.4f));
                }
                yield return null;
            }
            SetAirborne(false);
            Sfx.PlayAt("car_crash", transform.position, 0.8f, 0.7f, 60f);
            if (!hit) Strike(transform.position + Vector3.up, 3.5f, diveDamage * 0.7f, 5f);
            DamageTakenMultiplier = 1.35f;
            Play("SpinRecover", 0.1f);
            yield return Finish();
        }

        // ---------- Spike dive bomb ----------

        private IEnumerator DiveBomb()
        {
            Play("Takeoff");
            yield return Until("launch", 0, () => Track(200f));
            SetAirborne(true);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.35f, 70f);
            float high = GroundBelow(transform.position).y + diveBombHeight;
            float speed = 2f, since = Time.time;
            bool flapping = false;
            // The climb: two takeoff beats, then steady flapping, each downstroke lifting it, while it circles out over
            // the arena towards the player, banking as it turns.
            while (transform.position.y < high - 0.4f && Time.time - since < 6f)
            {
                if (!flapping && ClipTime >= Table.Length("Takeoff") - 0.2f)
                {
                    Play("Flap", 0.25f);
                    flapping = true;
                }
                float left = high - transform.position.y;
                float climb = climbSpeed * Tempo * BeatLift() * Mathf.Clamp(left / 3f, 0.3f, 1f);
                Vector3 aim = Target != null ? Target.position + Target.right * 8f : Home;
                speed = Fly(aim, 5f, 3f, 50f, climb, speed);
                yield return null;
            }
            GroundMarker marker = Marker(GroundBelow(BossFx.PlayerFeet), 4.8f, true);
            float hunt = 2.4f / Tempo;
            float t = 0f, held = 0f;
            string flight = Playing;
            // Its shadow hunts the player, lagging a little: keep moving. It flaps to catch up, glides once it's up to
            // speed, and hovers when it's over you.
            yield return Seconds(hunt, () =>
            {
                t += Time.deltaTime;
                held += Time.deltaTime;
                Vector3 over = Target != null ? Target.position : transform.position + transform.forward;
                float gap = Vector3.Distance(new Vector3(over.x, 0f, over.z), new Vector3(transform.position.x, 0f, transform.position.z));
                string want = gap < 3f && speed < 3f ? "Hover" : speed < flySpeed * Tempo * 0.8f || transform.position.y < high - 1f ? "Flap" : "Glide";
                if (want != flight && held > 1.1f)
                {
                    Play(want, 0.3f);
                    flight = want;
                    held = 0f;
                }
                float climb = (high - transform.position.y) * 1.5f;
                climb = flight == "Glide" ? climb - 0.5f : (climb + 0.3f) * BeatLift();
                speed = Fly(over, flySpeed * Tempo, 7f, 110f, climb, speed);
                marker.MoveTo(GroundBelow(transform.position));
                marker.Progress = t / hunt * 0.8f;
            });
            if (Target == null)
            {
                Destroy(marker.gameObject);
                yield return Landing();
                yield break;
            }
            // Over its prey it stops and hangs on beating wings (a hover), then folds them and drops.
            Play("Hover", 0.25f);
            Sfx.PlayAt("magic_charge", transform.position, 1f, 0.5f, 80f);
            yield return Seconds(0.55f / Tempo, () =>
            {
                speed = Fly(transform.position + transform.forward, 0f, 12f, 0f, (high - transform.position.y) * 1.5f * BeatLift(), speed);
                marker.Progress = Mathf.Min(1f, marker.Progress + Time.deltaTime * 0.5f);
            });
            Play("Plunge", 0.08f);
            Level();
            Vector3 ground = GroundBelow(transform.position);
            float fall = 4f;
            // The stoop: it falls faster and faster as the wings fold (not a constant slide).
            while (transform.position.y > ground.y + 0.05f)
            {
                fall = Mathf.Min(36f, fall + 55f * Time.deltaTime);
                transform.position = Vector3.MoveTowards(transform.position, ground, fall * Time.deltaTime);
                yield return null;
            }
            SetAirborne(false);
            Sfx.PlayAt("car_explode", ground, 1f, 0.6f, 80f);
            Strike(ground + Vector3.up, 4.8f, bombDamage, 6f);
            Keep(SpikeRing.Spawn(spikeMesh, thornMaterial, ground, 3f, 15f, 11f, spikeDamage, gameObject));
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 0.2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Keep(GroundThorn.Spawn(spikeMesh, thornMaterial, ground + d * 2.2f + Vector3.up * 0.4f, (-Vector3.up - d * 0.5f).normalized, 4f, 10f,
                    gameObject, 2.2f));
            }
            DamageTakenMultiplier = 1.4f;
            Play("Impact", 0.05f);
            yield return Seconds(0.65f / Tempo);
            Keep(SpikeRing.Spawn(spikeMesh, thornMaterial, ground, 3f, 15f, 11f, spikeDamage, gameObject));
            yield return Finish();
        }
    }
}
