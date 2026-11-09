using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest line 1's mini-boss (Christian's design, docs/mudlands.md): a huge, grinning gorilla squashed into a
    /// comically small go-kart at the Banana Speedway.
    /// <list type="bullet">
    /// <item><b>Happy:</b> drives slow laps, waves, beeps, and steers into you for a bump (10). The kart soaks up 75%
    /// of damage.</item>
    /// <item><b>Breaking the kart:</b> a charged bolt (20+), the combo finisher, a heavy jump smash or the car. The kart
    /// spins out and falls apart, he stares at it, and screams "MY GO-KART!!!". Enraged from then on.</item>
    /// <item><b>Enraged:</b> barrel toss (rolling barrels to jump or shoot; red ones explode), a telegraphed swipe, a
    /// charge rush (arm windmill and blinking; dazed if he hits a tyre wall), and a banana snack that heals 12% unless
    /// a heavy hit interrupts it; the peel it leaves slips him if he charges over it.</item>
    /// <item><b>Furious</b> (base phase 2, half health): quicker wind-ups, two barrels at once.</item>
    /// </list>
    /// 520 HP. Drops the Auxiliary Starter Motor and the Kart Wheel (Mud Tires).
    /// </summary>
    public sealed class KartKong : MudBoss
    {
        private enum State { Laps, Bump, Breaking, Idle, Swipe, Rush, Dazed, Toss, Snack, Choke, Slip }

        private static readonly Color Fur = new Color(0.3f, 0.2f, 0.15f), Skin = new Color(0.82f, 0.64f, 0.48f),
            KartYellow = new Color(0.98f, 0.78f, 0.15f), KartRed = new Color(0.85f, 0.18f, 0.12f), Banana = new Color(1f, 0.88f, 0.25f);

        private State state = State.Laps;
        private float stateTime, nextBump, nextRush, nextToss, nextSnack, nextLapEmote, eaten;
        private bool kartBroken, interrupted;
        private int lap;
        private readonly List<Vector3> laps = new List<Vector3>();
        private Vector3 rushDirection;
        private bool rushHit;
        private Transform body, armL, armR, legL, legR, kart, mouth, held;
        private readonly Transform[] kartWheels = new Transform[4];

        public override string Subtitle => "Speedway champion (self-declared)";
        public override int DisplayPhase => !kartBroken ? 1 : Phase == 2 ? 3 : 2;
        public bool KartBroken => kartBroken;

        // ---------- Model ----------

        protected override void BuildModel(Transform visual)
        {
            kart = MudArt.Pivot(visual, "Kart", Vector3.zero);
            MudArt.Part(kart, "Kart Body", Vector3.zero, Quaternion.identity, "kong_kart", k =>
            {
                k.Box(new Vector3(0f, 0.28f, 0f), new Vector3(1.1f, 0.18f, 1.7f), KartYellow);
                k.Box(new Vector3(0f, 0.38f, 0.8f), new Vector3(0.9f, 0.16f, 0.3f), Quaternion.Euler(-20f, 0f, 0f), KartRed);
                k.Box(new Vector3(0f, 0.3f, -0.85f), new Vector3(1.2f, 0.12f, 0.15f), KartRed);
                k.Box(new Vector3(0f, 0.48f, -0.3f), new Vector3(0.6f, 0.08f, 0.5f), MudPalette.Black); // the seat, much too small
                k.Prism(new Vector3(0f, 0.38f, 0.92f), Quaternion.Euler(90f, 0f, 0f), 0.13f, 0.13f, 0.03f, 10, MudPalette.White);
                k.Box(new Vector3(0f, 0.38f, 0.94f), new Vector3(0.05f, 0.16f, 0.01f), MudPalette.Black); // "1"
                k.Rod(new Vector3(0f, 0.4f, 0.45f), new Vector3(0f, 0.75f, 0.3f), 0.03f, 5, MudPalette.DarkMetal);
                k.Prism(new Vector3(0f, 0.75f, 0.3f), Quaternion.Euler(-60f, 0f, 0f), 0.15f, 0.15f, 0.03f, 10, MudPalette.Black);
                // A bicycle horn with a red bulb.
                k.Cone(new Vector3(0.35f, 0.6f, 0.55f), Quaternion.Euler(80f, 0f, 0f), 0.06f, 0.15f, 7, MudPalette.Metal);
                k.Ball(new Vector3(0.35f, 0.62f, 0.48f), 0.06f, KartRed, 0);
                // A checkered flag on a whip antenna.
                k.Rod(new Vector3(-0.5f, 0.35f, -0.8f), new Vector3(-0.5f, 1.6f, -0.85f), 0.012f, 4, MudPalette.Metal);
                for (int x = 0; x < 3; x++)
                    for (int y = 0; y < 2; y++)
                        k.Box(new Vector3(-0.45f + x * 0.1f, 1.52f - y * 0.1f, -0.85f), new Vector3(0.1f, 0.1f, 0.01f), (x + y) % 2 == 0 ? MudPalette.Black : MudPalette.White);
            });
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.62f : 0.62f, z = i < 2 ? 0.6f : -0.6f;
                kartWheels[i] = MudArt.Part(kart, "Kart Wheel " + i, new Vector3(x, 0.2f, z), Quaternion.identity, "kong_kart_wheel", k =>
                {
                    k.Prism(new Vector3(0.1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.2f, 0.2f, 0.2f, 10, MudPalette.Rubber);
                    k.Prism(new Vector3(0.105f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.09f, 0.09f, 0.21f, 6, KartRed);
                });
            }

            body = MudArt.Pivot(visual, "Body", new Vector3(0f, 1.2f, -0.2f));
            MudArt.Part(body, "Torso", Vector3.zero, Quaternion.identity, "kong_torso", k =>
            {
                k.Smooth = true;
                // A barrel chest over a round belly, with a pale chest plate.
                k.Ball(new Vector3(0f, 0.3f, 0f), new Vector3(1.05f, 0.95f, 0.8f), Quaternion.identity, Fur, 1);
                k.Ball(new Vector3(0f, 0.55f, 0.2f), new Vector3(0.95f, 0.6f, 0.62f), Quaternion.identity, Fur, 1); // shoulders
                k.Ball(new Vector3(0f, 0.12f, 0.42f), new Vector3(0.66f, 0.62f, 0.4f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(0f, -0.42f, 0.1f), new Vector3(0.78f, 0.5f, 0.72f), Quaternion.identity, Fur, 1);
                // Tufts of fur at the shoulders and back.
                foreach (float x in new[] { -0.7f, 0.7f })
                    k.Ball(new Vector3(x, 0.85f, -0.1f), new Vector3(0.3f, 0.2f, 0.32f), Quaternion.Euler(0f, 0f, x * 30f), Fur, 1);
                // A checkered racing scarf.
                k.Smooth = false;
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    k.Box(new Vector3(Mathf.Sin(a) * 0.52f, 1.02f, Mathf.Cos(a) * 0.44f), new Vector3(0.27f, 0.16f, 0.12f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), i % 2 == 0 ? MudPalette.Black : MudPalette.White);
                }
                k.Box(new Vector3(0.25f, 0.8f, 0.5f), new Vector3(0.16f, 0.4f, 0.05f), Quaternion.Euler(10f, 0f, -12f), MudPalette.Black); // the loose end
            });
            // The ship's auxiliary starter motor, strapped on his back like a backpack.
            Transform part = MudArt.Part(body, "Auxiliary Starter Motor", new Vector3(0f, 0.4f, -0.85f), Quaternion.Euler(0f, 0f, 90f), "kong_motor", k =>
            {
                k.Prism(new Vector3(0f, -0.35f, 0f), Quaternion.identity, 0.28f, 0.28f, 0.7f, 10, MudPalette.Metal);
                k.Prism(new Vector3(0f, 0.35f, 0f), Quaternion.identity, 0.15f, 0.1f, 0.2f, 8, MudPalette.DarkMetal);
                for (int i = 0; i < 4; i++) k.Prism(new Vector3(0f, -0.25f + i * 0.15f, 0f), Quaternion.identity, 0.3f, 0.3f, 0.04f, 10, new Color(0.25f, 0.6f, 0.9f));
                k.Ball(new Vector3(0f, 0.1f, 0.3f), 0.06f, new Color(0.3f, 0.9f, 1f), 0, 3f);
            });
            ShipPart = part;

            Head = MudArt.Pivot(body, "Head", new Vector3(0f, 1.3f, 0.25f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "kong_head", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(0f, 0.05f, -0.05f), new Vector3(0.55f, 0.52f, 0.5f), Quaternion.identity, Fur, 1);
                k.Ball(new Vector3(0f, 0.32f, -0.2f), new Vector3(0.38f, 0.3f, 0.36f), Quaternion.identity, Fur, 1); // the crown
                // The face: a heart-shaped mask (two brow lobes) over a big round muzzle.
                foreach (float x in new[] { -0.15f, 0.15f })
                    k.Ball(new Vector3(x, 0.14f, 0.3f), new Vector3(0.24f, 0.2f, 0.22f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(0f, -0.17f, 0.34f), new Vector3(0.43f, 0.3f, 0.32f), Quaternion.identity, Skin, 1); // muzzle
                k.Ball(new Vector3(0f, 0.27f, 0.38f), new Vector3(0.36f, 0.08f, 0.14f), Quaternion.identity, Fur, 1); // the heavy brow ridge
                foreach (float x in new[] { -0.08f, 0.08f }) // nostrils
                    k.Ball(new Vector3(x, -0.06f, 0.64f), new Vector3(0.045f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, x * 200f), new Color(0.2f, 0.12f, 0.1f), 1);
                foreach (float x in new[] { -0.47f, 0.47f }) // ears
                {
                    k.Ball(new Vector3(x, 0.06f, -0.05f), new Vector3(0.1f, 0.14f, 0.07f), Quaternion.identity, Skin, 1);
                    k.Ball(new Vector3(x * 1.02f, 0.06f, -0.03f), new Vector3(0.05f, 0.08f, 0.04f), Quaternion.identity, new Color(0.62f, 0.45f, 0.33f), 1);
                }
                // A racing helmet pushed back on his head, much too small.
                k.Ball(new Vector3(0f, 0.48f, -0.24f), new Vector3(0.3f, 0.2f, 0.3f), Quaternion.Euler(-20f, 0f, 0f), KartRed, 1);
                k.Smooth = false;
                k.Box(new Vector3(0f, 0.55f, -0.1f), new Vector3(0.06f, 0.18f, 0.4f), Quaternion.Euler(-20f, 0f, 0f), MudPalette.White);
            });
            mouth = BossFace.Mouth(Head, "kong_mouth", new Vector3(0f, -0.27f, 0.6f), 0.42f, 0.16f, new Color(0.55f, 0.36f, 0.28f), true,
                Quaternion.Euler(-12f, 0f, 0f));
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.13f, 0.47f), 0.15f, 0.085f, MudPalette.White, MudPalette.Black, "kong");
            eyeL = l;
            eyeR = r;
            Transform browL = BossFace.Brow(Head, "kong_brow", new Vector3(-0.15f, 0.27f, 0.53f), -1f, new Vector3(0.24f, 0.07f, 0.09f), new Color(0.2f, 0.13f, 0.1f), 8f);
            Transform browR = BossFace.Brow(Head, "kong_brow", new Vector3(0.15f, 0.27f, 0.53f), 1f, new Vector3(0.24f, 0.07f, 0.09f), new Color(0.2f, 0.13f, 0.1f), 8f);
            armL = Arm(body, -1f);
            armR = Arm(body, 1f);
            legL = Leg(body, -1f);
            legR = Leg(body, 1f);
            held = MudArt.Pivot(body, "Held", new Vector3(0f, 2.2f, 0.5f));
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, EyeL = eyeL, EyeR = eyeR, Mouth = mouth,
                BrowTravel = 0.05f, Idle = Mood.Laugh, Fight = Mood.Angry,
            };
        }

        private Transform eyeL, eyeR, Head;

        private static Transform Arm(Transform body, float side)
        {
            Transform shoulder = MudArt.Pivot(body, side < 0 ? "Arm L" : "Arm R", new Vector3(0.95f * side, 0.8f, 0.1f));
            MudArt.Part(shoulder, "Arm", Vector3.zero, Quaternion.identity, "kong_arm", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, Fur, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -1.0f, 0.1f), 0.33f, 12, Fur, 0f, 0.27f);
                k.Ball(new Vector3(0f, -1.0f, 0.1f), 0.29f, Fur, 1); // elbow
                k.Rod(new Vector3(0f, -1.0f, 0.1f), new Vector3(0f, -1.68f, 0.25f), 0.31f, 12, Fur, 0f, 0.3f);
                // A big knuckly fist.
                k.Ball(new Vector3(0f, -1.86f, 0.3f), new Vector3(0.38f, 0.3f, 0.36f), Quaternion.identity, Skin, 1);
                for (int i = 0; i < 4; i++)
                    k.Ball(new Vector3((i - 1.5f) * 0.15f, -2.0f, 0.46f), new Vector3(0.09f, 0.08f, 0.09f), Quaternion.identity, Skin, 1);
                k.Ball(new Vector3(0f, -1.6f, 0.2f), new Vector3(0.36f, 0.16f, 0.36f), Quaternion.identity, Fur, 1); // shaggy cuff
            });
            return shoulder;
        }

        private static Transform Leg(Transform body, float side)
        {
            Transform hip = MudArt.Pivot(body, side < 0 ? "Leg L" : "Leg R", new Vector3(0.45f * side, -0.6f, 0.3f));
            MudArt.Part(hip, "Leg", Vector3.zero, Quaternion.identity, "kong_leg", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.3f, Fur, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -0.62f, 0.1f), 0.29f, 12, Fur, 0f, 0.24f);
                k.Ball(new Vector3(0f, -0.7f, 0.25f), new Vector3(0.27f, 0.15f, 0.37f), Quaternion.identity, Skin, 1);
                for (int i = 0; i < 3; i++)
                    k.Ball(new Vector3((i - 1f) * 0.12f, -0.72f, 0.55f), new Vector3(0.07f, 0.07f, 0.08f), Quaternion.identity, Skin, 1);
            });
            return hip;
        }

        protected override Action<MeshKit> KeyItemModel() => k =>
        {
            k.Prism(new Vector3(0.1f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.3f, 0.3f, 0.2f, 10, MudPalette.Rubber);
            k.Prism(new Vector3(0.105f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.13f, 0.13f, 0.21f, 6, KartRed);
        };

        // ---------- Entrance ----------

        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = Home0;
            BuildLaps(home);
            Vector3 start = laps[0], beforeStart = laps[laps.Count - 3];
            Vector3 side = Vector3.Cross(Vector3.up, (start - beforeStart).normalized);
            // A traffic light on the start line: red, red, green.
            Transform light = MudArt.Part(null, "Start Light", start + side * 5f, Quaternion.LookRotation(-side), "kong_light", k =>
            {
                k.Rod(Vector3.zero, new Vector3(0f, 3f, 0f), 0.08f, 6, MudPalette.DarkMetal);
                k.Box(new Vector3(0f, 3.4f, 0f), new Vector3(0.5f, 1.3f, 0.3f), MudPalette.Black);
            });
            var lamps = new Transform[3];
            for (int i = 0; i < 3; i++)
                lamps[i] = MudArt.Part(light, "Lamp " + i, new Vector3(0f, 3.8f - i * 0.4f, 0.16f), Quaternion.identity, i < 2 ? "kong_lamp_red" : "kong_lamp_green",
                    k => k.Ball(Vector3.zero, 0.14f, new Color(0.15f, 0.15f, 0.15f), 1));
            cine.Cut(start + side * 9f + Vector3.up * 2.5f, light.position + Vector3.up * 3.4f);
            transform.position = beforeStart;
            transform.rotation = Quaternion.LookRotation(start - beforeStart);
            for (int i = 0; i < 3 && !cine.Skipped; i++)
            {
                yield return cine.Wait(0.6f);
                Color lit = i < 2 ? new Color(1f, 0.2f, 0.15f) : new Color(0.3f, 1f, 0.35f);
                lamps[i].GetComponent<MeshRenderer>().sharedMaterial = MudArt.Glow(lit, 3f);
                Sfx.Play(i < 2 ? "ui_click" : "quest_chime", 0.8f, i < 2 ? 0.7f : 1f);
            }
            // He tears round the bend honking, past the camera, and skids onto the line.
            cine.LookAt(transform, Vector3.up * 1.5f);
            Sfx.Play("kart_horn", 1f);
            float speed = 14f;
            int index = laps.Count - 3;
            for (float t = 0f; t < 3.2f && !cine.Tick(); t += Time.deltaTime)
            {
                Vector3 target = laps[(index + 1) % laps.Count];
                Vector3 to = (target - transform.position).Flatten();
                if (to.magnitude < 1.5f) index = (index + 1) % laps.Count;
                transform.position += to.normalized * speed * Time.deltaTime;
                transform.position = Ground(transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6f);
                SpinWheels(speed);
                if (t > 2.6f) speed = Mathf.MoveTowards(speed, 0f, Time.deltaTime * 25f);
                yield return null;
            }
            transform.position = home;
            transform.rotation = Quaternion.LookRotation((cine.PlayerPosition - home).Flatten().normalized);
            cine.Cut(home + transform.forward * 6f + Vector3.up * 2.2f, home + Vector3.up * 2f);
            Dust(home, 30);
            Sfx.Play("car_skid", 0.8f);
            yield return cine.Wait(0.4f);
            // A big wave and two happy beeps.
            for (float t = 0f; t < 1.6f && !cine.Tick(); t += Time.deltaTime)
            {
                armR.localRotation = Quaternion.Euler(-170f, 0f, 20f + Mathf.Sin(t * 14f) * 25f);
                if (t < Time.deltaTime || Mathf.Abs(t - 0.8f) < Time.deltaTime) Sfx.Play("kart_horn", 0.9f, 1.15f);
                yield return null;
            }
            Say("BEEP BEEP!", new Color(1f, 0.9f, 0.4f), 1.2f, 6f);
            yield return cine.Wait(0.8f);
            Destroy(light.gameObject, 6f);
        }

        private Vector3 Home0 => transform.parent != null ? transform.parent.position : transform.position;

        private void BuildLaps(Vector3 centre)
        {
            laps.Clear();
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * 22f, 0f, Mathf.Sin(a) * 13f);
                laps.Add(NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p);
            }
        }

        // ---------- Base hooks ----------

        protected override void OnBossStarted()
        {
            if (AgentReady) Agent.updateRotation = false;
            BuildLaps(Home);
            Enter(State.Laps);
            nextSnack = Time.time + 12f;
        }

        protected override void OnEngaged()
        {
            nextBump = Time.time + 2f;
            if (!kartBroken) Say("WHEEE!", new Color(1f, 0.9f, 0.4f), 1.2f, 6f);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            if (held.childCount > 0 && next != State.Toss) Destroy(held.GetChild(0).gameObject);
        }

        protected override void UpdatePatrol(float dt) => DriveLaps(dt);

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            if (!kartBroken) { KartPhase(dt); return; }
            float quick = Phase == 2 ? 0.75f : 1f;
            switch (state)
            {
                case State.Breaking: UpdateBreaking(); break;
                case State.Idle: Decide(dt, quick); break;
                case State.Swipe:
                    Halt();
                    if (stateTime < 0.45f * quick) TurnToward(PlayerPosition, 300f * dt);
                    if (stateTime >= 0.6f * quick && stateTime - dt < 0.6f * quick)
                    {
                        Sfx.PlayAt("chain_swish", transform.position, 1f, 0.6f, 35f);
                        CameraShake.Add(0.15f);
                        if (PlayerDistance < 3.8f && AngleToTarget() < 75f && HurtPlayer(20f)) ShovePlayer(PlayerPosition - transform.position, 5f);
                    }
                    if (stateTime > 1.1f * quick) Enter(State.Idle);
                    break;
                case State.Rush: UpdateRush(dt, quick); break;
                case State.Dazed:
                case State.Choke:
                case State.Slip:
                    Halt();
                    if (stateTime > (state == State.Slip ? 2.5f : state == State.Choke ? 1.5f : 2f)) Enter(State.Idle);
                    break;
                case State.Toss: UpdateToss(dt, quick); break;
                case State.Snack: UpdateSnack(dt); break;
            }
        }

        private void Decide(float dt, float quick)
        {
            float distance = PlayerDistance;
            TurnToward(PlayerPosition, 200f * dt);
            if (Health.Normalized < 0.7f && Time.time >= nextSnack && distance > 7f)
            {
                Enter(State.Snack);
                eaten = 0f;
                interrupted = false;
                Say("*munch*", new Color(1f, 0.9f, 0.4f), 1.2f, 5f);
                return;
            }
            if (distance < 3.6f) { Enter(State.Swipe); Say("!", new Color(1f, 0.6f, 0.3f), 0.6f, 6f); return; }
            if (distance > 6f && distance < 24f && Time.time >= nextRush && Random.value < 0.6f)
            {
                Enter(State.Rush);
                rushHit = false;
                return;
            }
            if (distance > 5f && Time.time >= nextToss) { Enter(State.Toss); return; }
            Chase(PlayerPosition, 4.2f);
            FaceMovement(dt);
        }

        // ---------- The kart ----------

        private void KartPhase(float dt)
        {
            if (state == State.Bump)
            {
                Chase(PlayerPosition, 8.5f);
                FaceMovement(dt);
                SpinWheels(8.5f);
                if (PlayerDistance < 2f)
                {
                    if (HurtPlayer(10f)) ShovePlayer(PlayerPosition - transform.position, 4f);
                    Sfx.PlayAt("kart_horn", transform.position, 1f, 1.1f, 35f);
                    Say("BONK!", new Color(1f, 0.9f, 0.4f), 0.9f, 5f);
                    nextBump = Time.time + Random.Range(4f, 6f);
                    Enter(State.Laps);
                }
                else if (stateTime > 2.6f) { nextBump = Time.time + 3f; Enter(State.Laps); }
                return;
            }
            DriveLaps(dt);
            if (Time.time >= nextBump && PlayerDistance < 15f)
            {
                Enter(State.Bump);
                Sfx.PlayAt("kart_horn", transform.position, 1f, 1f, 35f);
                Say("BEEP BEEP!", new Color(1f, 0.9f, 0.4f), 1f, 5f);
            }
            if (Time.time >= nextLapEmote)
            {
                nextLapEmote = Time.time + Random.Range(3f, 6f);
                string[] lines = { "WHEEE!", ":D", "VROOM!", "*waves*", "LAP " + (lap + 1) + "!" };
                Say(lines[Random.Range(0, lines.Length)], new Color(1f, 0.9f, 0.4f), 1.1f, 4.5f);
            }
        }

        private void DriveLaps(float dt)
        {
            if (kartBroken || laps.Count == 0) return;
            Vector3 target = laps[lap % laps.Count];
            if (Flat(target - transform.position) < 2.5f) lap++;
            Chase(laps[lap % laps.Count], 5.5f);
            FaceMovement(dt);
            SpinWheels(5.5f);
        }

        private void FaceMovement(float dt)
        {
            if (!AgentReady) return;
            Vector3 v = Agent.velocity.Flatten();
            if (v.sqrMagnitude > 0.2f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v), 360f * dt);
        }

        private void SpinWheels(float speed)
        {
            foreach (Transform wheel in kartWheels)
                if (wheel != null) wheel.Rotate(speed / 0.2f * Mathf.Rad2Deg * Time.deltaTime, 0f, 0f, Space.Self);
        }

        private void BreakKart()
        {
            if (kartBroken || IsDead) return;
            kartBroken = true;
            Enter(State.Breaking);
            Halt();
            CameraShake.Add(0.4f);
            Sfx.PlayAt("car_crash", transform.position, 1f, 1.2f, 50f);
            Sfx.PlayAt("kart_horn", transform.position, 1f, 0.7f, 50f);
            // The kart's bits fly off; the original hides (it comes back if the fight resets).
            foreach (MeshRenderer piece in kart.GetComponentsInChildren<MeshRenderer>())
            {
                GameObject copy = Instantiate(piece.gameObject, piece.transform.position, piece.transform.rotation);
                var rb = copy.AddComponent<Rigidbody>();
                copy.AddComponent<BoxCollider>();
                rb.linearVelocity = (Random.insideUnitSphere + Vector3.up * 1.5f) * 6f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                copy.AddComponent<Debris>().Begin(4f);
            }
            kart.gameObject.SetActive(false);
        }

        private void UpdateBreaking()
        {
            Halt();
            float t = stateTime;
            if (t > 2.2f && t - Time.deltaTime <= 2.2f)
            {
                MudHud.Shout("MY GO-KART!!!", new Color(1f, 0.85f, 0.3f), 2.4f);
                Sfx.PlayAt("kong_roar", transform.position, 1f, 0.9f, 80f);
                CameraShake.Add(0.6f);
                Say("MY GO-KART!!!", new Color(1f, 0.5f, 0.3f), 2.4f, 7f);
            }
            if (t > 4.2f)
            {
                Enter(State.Idle);
                nextRush = Time.time + 1.5f;
                nextToss = Time.time + 0.5f;
            }
        }

        // ---------- Attacks ----------

        private void UpdateRush(float dt, float quick)
        {
            float windup = 1f * quick;
            if (stateTime < windup)
            {
                Halt();
                if (stateTime < windup * 0.75f) TurnToward(PlayerPosition, 400f * dt);
                rushDirection = transform.forward;
                if (Mathf.Repeat(stateTime, 0.2f) < dt) Say(stateTime < 0.2f ? "!" : "*blink*", new Color(1f, 0.5f, 0.3f), 0.25f, 5f);
                return;
            }
            float run = stateTime - windup;
            if (AgentReady) Agent.Move(rushDirection * 16f * dt);
            transform.rotation = Quaternion.LookRotation(rushDirection);
            if (Mathf.Repeat(run, 0.1f) < dt) Dust(transform.position, 6, 0.5f);
            if (!rushHit && PlayerDistance < 2.2f)
            {
                rushHit = true;
                if (HurtPlayer(28f)) ShovePlayer(rushDirection, 7f);
            }
            if (BananaPeel.SlipsAt(transform.position))
            {
                Enter(State.Slip);
                Say("WHOOPS!", new Color(1f, 0.9f, 0.4f), 1.6f, 6f);
                MudHud.Toast("He slipped on his own peel!", new Color(1f, 0.9f, 0.4f));
                CameraShake.Add(0.3f);
                Dust(transform.position, 40, 1f);
                return;
            }
            if (Physics.Raycast(transform.position + Vector3.up * 1.2f, rushDirection, 1.6f, sightMask, QueryTriggerInteraction.Ignore))
            {
                Enter(State.Dazed);
                Say("@_@", new Color(1f, 1f, 0.6f), 2f, 6f);
                CameraShake.Add(0.35f);
                Sfx.PlayAt("car_crash", transform.position, 0.8f, 0.7f, 40f);
                nextRush = Time.time + 6f;
                return;
            }
            if (run > 1.3f) { Enter(State.Idle); nextRush = Time.time + 5f; }
        }

        private void UpdateToss(float dt, float quick)
        {
            Halt();
            TurnToward(PlayerPosition, 300f * dt);
            if (held.childCount == 0 && stateTime < 0.1f)
                MudArt.Part(held, "Barrel", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), "kong_barrel", RollingBarrel.Mesh(false));
            if (stateTime < 0.8f * quick) return;
            if (held.childCount > 0) Destroy(held.GetChild(0).gameObject);
            int count = Phase == 2 ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                bool red = Random.value < 0.25f;
                Vector3 side = transform.right * (count == 1 ? 0f : i == 0 ? -2.5f : 2.5f);
                Vector3 land = Vector3.Lerp(transform.position, PlayerPosition, 0.45f) + side;
                Transform model = MudArt.Part(null, "Barrel", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), red ? "kong_barrel_red" : "kong_barrel", RollingBarrel.Mesh(red));
                Vector3 aim = PlayerPosition + side * 0.5f;
                GameObject owner = gameObject;
                Lob.Throw(model, held.position, Ground(land), 0.6f, 2f, at => RollingBarrel.Spawn(at, (aim - at).Flatten().normalized, red, owner), 1.2f);
            }
            Sfx.PlayAt("chain_swish", transform.position, 0.9f, 0.5f, 35f);
            nextToss = Time.time + (Phase == 2 ? 2.6f : 3.6f);
            Enter(State.Idle);
        }

        private void UpdateSnack(float dt)
        {
            // Backs off a little, sits, peels, munches. A heavy hit stops it.
            if (stateTime < 0.8f)
            {
                Vector3 away = (transform.position - PlayerPosition).Flatten().normalized;
                Chase(transform.position + away * 3f, 4f);
                return;
            }
            Halt();
            eaten = Mathf.Clamp01((stateTime - 0.8f) / 2.8f);
            MudHud.WorldBar(transform, eaten, Banana, 4.2f);
            if (Mathf.Repeat(stateTime, 0.6f) < dt) Sfx.PlayAt("crab_snip", transform.position, 0.5f, 0.6f, 25f);
            if (eaten < 1f) return;
            Health.Heal(Health.Max * 0.12f);
            Say("*BURP*", new Color(0.8f, 1f, 0.5f), 1.4f, 6f);
            Sfx.PlayAt("flask_burp", transform.position, 1f, 0.5f, 40f);
            BananaPeel.Drop(transform.position + transform.forward * 1.2f);
            nextSnack = Time.time + 18f;
            Enter(State.Idle);
        }

        private void InterruptSnack()
        {
            if (state != State.Snack || stateTime < 0.8f) return;
            Enter(State.Choke);
            nextSnack = Time.time + 10f;
            Say("HKK!", new Color(1f, 0.6f, 0.4f), 1.4f, 6f);
            MudHud.Toast("Snack interrupted!", new Color(1f, 0.85f, 0.4f));
        }

        // ---------- Damage ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            bool heavy = IsHeavyRanged(amount, source) || IsFinisher(source);
            if (!kartBroken)
            {
                if (heavy) { BreakKart(); return; }
                if (!IsDead) Health.Heal(amount * 0.75f); // the kart takes most of it
                Say("*clang*", new Color(0.85f, 0.85f, 0.85f), 0.5f, 4f);
                return;
            }
            if (heavy) InterruptSnack();
        }

        public override float OnCarBlast(CarBlastInfo blast)
        {
            if (!kartBroken) BreakKart();
            else if (state == State.Snack) InterruptSnack();
            else if (state != State.Breaking) { Enter(State.Dazed); Say("@_@", new Color(1f, 1f, 0.6f), 2f, 6f); }
            return blast.Damage;
        }

        protected override void OnPhaseTwo()
        {
            Say("GRAAAH!", new Color(1f, 0.4f, 0.3f), 1.6f, 7f);
            Sfx.PlayAt("kong_roar", transform.position, 1f, 0.8f, 60f);
        }

        protected override void UpdatePhaseShift(float dt) => Halt();

        protected override void OnReset()
        {
            kartBroken = false;
            kart.gameObject.SetActive(true);
            lap = 0;
            Enter(State.Laps);
            BananaPeel.ClearAll();
        }

        protected override void OnMudBossDeath()
        {
            Sfx.PlayAt("kart_horn", transform.position, 1f, 0.5f, 50f);
            BananaPeel.ClearAll();
        }

        // ---------- Animation ----------

        protected override void AnimateBoss(float dt)
        {
            float t = Time.time;
            float seated = kartBroken && state != State.Snack ? 0f : 1f;
            body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0f, Mathf.Lerp(1.5f, 1.2f, seated), seated * -0.2f), dt * 6f);
            Quaternion left = Quaternion.Euler(-50f, 0f, -15f), right = Quaternion.Euler(-50f, 0f, 15f), lean = Quaternion.identity;
            Quaternion legs = Quaternion.Euler(seated > 0.5f ? -80f : 0f, 0f, 0f);
            float walk = AgentReady ? Agent.velocity.magnitude : 0f;
            Mood? mood = null;
            switch (state)
            {
                case State.Laps:
                case State.Bump:
                    left = Quaternion.Euler(-60f, 0f, -25f);
                    right = Quaternion.Euler(-60f, 0f, 25f);
                    if (Mathf.Repeat(t * 0.3f + BossSeed, 1f) > 0.85f && state == State.Laps) right = Quaternion.Euler(-170f, 0f, 20f + Mathf.Sin(t * 14f) * 25f);
                    lean = Quaternion.Euler(state == State.Bump ? 12f : 0f, 0f, Mathf.Sin(t * 3f) * 4f);
                    mood = Mood.Laugh; // the big grin
                    break;
                case State.Breaking:
                    lean = stateTime < 2.2f ? Quaternion.Euler(20f, Mathf.Sin(stateTime * 2f) * 20f, 0f) : Quaternion.Euler(-15f, 0f, 0f);
                    if (stateTime > 2.2f)
                    {
                        float beat = Mathf.Sin(t * 22f);
                        left = Quaternion.Euler(-80f + beat * 20f, 0f, -60f);
                        right = Quaternion.Euler(-80f - beat * 20f, 0f, 60f);
                        mood = Mood.Furious;
                    }
                    else mood = Mood.Shock;
                    break;
                case State.Idle:
                    float gait = Mathf.Sin(t * 8f) * Mathf.Clamp01(walk / 3f);
                    left = Quaternion.Euler(-20f + gait * 40f, 0f, -10f);
                    right = Quaternion.Euler(-20f - gait * 40f, 0f, 10f);
                    lean = Quaternion.Euler(20f, 0f, 0f);
                    legs = Quaternion.Euler(gait * 30f, 0f, 0f);
                    break;
                case State.Swipe:
                    float wind = stateTime < 0.55f ? Mathf.SmoothStep(0f, 1f, stateTime / 0.55f) : 1f - Mathf.Clamp01((stateTime - 0.55f) / 0.2f);
                    right = Quaternion.Euler(-90f, Mathf.Lerp(-80f, 70f, stateTime < 0.55f ? 0f : 1f - wind), 60f * wind);
                    mood = Mood.Furious;
                    lean = Quaternion.Euler(10f, stateTime < 0.55f ? -25f * wind : 30f, 0f);
                    break;
                case State.Rush:
                    if (stateTime < 1f)
                    {
                        right = Quaternion.Euler(-200f + Mathf.Sin(stateTime * 25f) * 120f, 0f, 30f); // the windmill
                        mood = Mood.Furious;
                    }
                    else
                    {
                        float gallop = Mathf.Sin(t * 20f);
                        left = Quaternion.Euler(-40f + gallop * 70f, 0f, -10f);
                        right = Quaternion.Euler(-40f - gallop * 70f, 0f, 10f);
                        lean = Quaternion.Euler(45f, 0f, 0f);
                    }
                    break;
                case State.Dazed:
                case State.Choke:
                    lean = Quaternion.Euler(-10f, Mathf.Sin(t * 6f) * 20f, Mathf.Sin(t * 4f) * 12f);
                    mood = state == State.Choke ? Mood.Hurt : Mood.Sleepy;
                    left = Quaternion.Euler(-10f, 0f, -40f);
                    right = state == State.Choke ? Quaternion.Euler(-140f, 0f, 30f) : Quaternion.Euler(-10f, 0f, 40f);
                    break;
                case State.Slip:
                    lean = Quaternion.Euler(-80f * Mathf.Clamp01(stateTime / 0.3f), 0f, 0f);
                    mood = Mood.Shock;
                    left = Quaternion.Euler(-150f, 0f, -60f);
                    right = Quaternion.Euler(-150f, 0f, 60f);
                    break;
                case State.Toss:
                    float lift = Mathf.SmoothStep(0f, 1f, stateTime / 0.6f);
                    left = Quaternion.Euler(Mathf.Lerp(-20f, -175f, lift), 0f, -15f);
                    right = Quaternion.Euler(Mathf.Lerp(-20f, -175f, lift), 0f, 15f);
                    lean = Quaternion.Euler(-12f * lift, 0f, 0f);
                    mood = Mood.Furious;
                    break;
                case State.Snack:
                    left = Quaternion.Euler(-120f, 0f, -40f);
                    right = Quaternion.Euler(-130f + Mathf.Sin(t * 10f) * 15f, 0f, 45f);
                    mood = Mood.Laugh;
                    if (Face != null) Face.Talk(0.1f); // munching
                    legs = Quaternion.Euler(-80f, 0f, 0f);
                    break;
            }
            if (mood.HasValue && Face != null) Face.Show(mood.Value, 0.15f);
            body.localRotation = Quaternion.Slerp(body.localRotation, lean, dt * 8f);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, left, dt * 12f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, right, dt * (state == State.Swipe ? 25f : 12f));
            legL.localRotation = Quaternion.Slerp(legL.localRotation, legs, dt * 10f);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Inverse(legs) * Quaternion.Euler(seated > 0.5f ? -160f : 0f, 0f, 0f), dt * 10f);
            if (Head != null && Target != null && state != State.Breaking)
            {
                Vector3 local = transform.InverseTransformPoint(PlayerPosition + Vector3.up * 1.5f);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -50f, 50f);
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(0f, yaw, 0f), dt * 6f);
            }
            else if (Head != null && state == State.Breaking) Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(25f, 120f, 0f), dt * 3f);
        }

        protected override void UpdateDeath(float t)
        {
            // Keels over backwards with a thump.
            float fall = Mathf.Clamp01(t / 1f);
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(-85f * fall * fall, 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.up * 0.2f * Mathf.Sin(fall * Mathf.PI);
        }
    }

    /// <summary>Kart Kong's barrel, rolling across the ground at the player: jump it or break it. Red ones explode.</summary>
    public sealed class RollingBarrel : MonoBehaviour
    {
        private Vector3 direction;
        private bool red, done;
        private float born;
        private GameObject owner;
        private Health health;
        private GameObject lastHitBy;

        public static Action<MeshKit> Mesh(bool red) => k =>
        {
            Color wood = red ? new Color(0.75f, 0.18f, 0.12f) : new Color(0.55f, 0.36f, 0.2f);
            k.Lathe(new Vector3(0f, -0.45f, 0f), Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(0.32f, 0f), new Vector2(0.38f, 0.45f), new Vector2(0.32f, 0.9f), new Vector2(0.0001f, 0.9f) }, 10, wood);
            k.Prism(new Vector3(0f, -0.3f, 0f), Quaternion.identity, 0.36f, 0.36f, 0.05f, 10, MudPalette.DarkMetal);
            k.Prism(new Vector3(0f, 0.25f, 0f), Quaternion.identity, 0.36f, 0.36f, 0.05f, 10, MudPalette.DarkMetal);
            if (red) k.Box(new Vector3(0f, 0f, 0.38f), new Vector3(0.25f, 0.25f, 0.02f), MudPalette.White);
        };

        public static RollingBarrel Spawn(Vector3 at, Vector3 direction, bool red, GameObject owner)
        {
            var go = new GameObject(red ? "Red Barrel" : "Barrel");
            go.transform.position = at + Vector3.up * 0.4f;
            go.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.01f ? direction : Vector3.forward);
            MudArt.Part(go.transform, "Model", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), red ? "kong_barrel_red" : "kong_barrel", Mesh(red));
            var shape = go.AddComponent<CapsuleCollider>();
            shape.direction = 0;
            shape.radius = 0.38f;
            shape.height = 0.9f;
            var barrel = go.AddComponent<RollingBarrel>();
            barrel.direction = direction.Flatten().normalized;
            barrel.red = red;
            barrel.owner = owner;
            barrel.born = Time.time;
            barrel.health = go.AddComponent<Health>();
            barrel.health.SetMax(10f);
            barrel.health.Restore();
            barrel.health.Damaged += (h, amount, source) => barrel.lastHitBy = source;
            barrel.health.Died += _ => barrel.Burst(barrel.lastHitBy);
            Sfx.PlayAt("crab_block", at, 0.6f, 0.5f, 30f);
            return barrel;
        }

        private void Update()
        {
            if (done) return;
            float t = Time.time - born;
            if (t > 4f) { Burst(null, false); return; }
            transform.position += direction * 9f * Time.deltaTime;
            if (GroundProbe.Find(transform.position, 1f, 2f, out Vector3 ground))
                transform.position = new Vector3(transform.position.x, ground.y + 0.4f, transform.position.z);
            transform.GetChild(0).Rotate(Vector3.up, 9f / 0.38f * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            Vector3 to = player.transform.position - transform.position;
            float feet = player.transform.position.y;
            // Jumping clears it: the player's feet above the barrel's top.
            if (to.Flatten().magnitude < 0.9f && feet < transform.position.y + 0.3f)
            {
                var hp = player.GetComponent<Health>();
                if (hp != null && !red && hp.TakeDamage(18f, owner))
                    MudHud.Toast("Jump the barrels!", new Color(1f, 0.85f, 0.5f), 1.5f);
                Burst(owner, true);
            }
        }

        private void Burst(GameObject by, bool explodeRed = true)
        {
            if (done) return;
            done = true;
            Vector3 at = transform.position;
            Sfx.PlayAt("wood_smash", at, 0.8f, 0.9f, 35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at, Vector3.up, 16, 0.5f, 3f, 70f, new Color(0.5f, 0.4f, 0.3f, 0.7f));
            if (red && explodeRed)
            {
                ParticleBurst.Explosion(at, 4f);
                CameraShake.Add(0.3f);
                Sfx.PlayAt("car_explode", at, 0.7f, 1.3f, 50f);
                foreach (Collider hit in Physics.OverlapSphere(at, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Health target = hit.GetComponentInParent<Health>();
                    if (target == null || target == health || target.IsDead || !EnemyAllegiance.CanHurt(by, target)) continue;
                    // A red barrel the player broke counts as the player's: it hurts Kong.
                    if (target.GetComponentInParent<KartKong>() != null && by == owner) continue;
                    target.TakeDamage(25f, by);
                }
            }
            Destroy(gameObject);
        }
    }

    /// <summary>A banana peel left on the ground after Kart Kong's snack: he slips on it if he charges over it.</summary>
    public sealed class BananaPeel : MonoBehaviour
    {
        private static readonly List<BananaPeel> Peels = new List<BananaPeel>();
        private float until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Peels.Clear();

        public static void Drop(Vector3 at)
        {
            var go = new GameObject("Banana Peel");
            if (GroundProbe.Find(at, 2f, 3f, out Vector3 ground)) at = ground;
            go.transform.position = at + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            MudArt.Part(go.transform, "Peel", Vector3.zero, Quaternion.identity, "banana_peel", k =>
            {
                Color yellow = new Color(1f, 0.88f, 0.25f);
                for (int i = 0; i < 4; i++)
                {
                    Quaternion r = Quaternion.Euler(0f, i * 90f + 20f, 0f);
                    k.Box(r * new Vector3(0f, 0.03f, 0.22f), new Vector3(0.12f, 0.03f, 0.35f), r, yellow);
                }
                k.Prism(Vector3.zero, Quaternion.identity, 0.08f, 0.06f, 0.12f, 6, yellow);
                k.Prism(new Vector3(0f, 0.12f, 0f), Quaternion.identity, 0.03f, 0.02f, 0.06f, 5, new Color(0.3f, 0.22f, 0.1f));
            });
            var peel = go.AddComponent<BananaPeel>();
            peel.until = Time.time + 25f;
            Peels.Add(peel);
        }

        public static bool SlipsAt(Vector3 point)
        {
            foreach (BananaPeel peel in Peels)
            {
                if (peel == null) continue;
                if ((peel.transform.position - point).Flatten().sqrMagnitude < 1.6f * 1.6f)
                {
                    Peels.Remove(peel);
                    Destroy(peel.gameObject);
                    return true;
                }
            }
            return false;
        }

        public static void ClearAll()
        {
            foreach (BananaPeel peel in Peels) if (peel != null) Destroy(peel.gameObject);
            Peels.Clear();
        }

        private void Update()
        {
            if (Time.time < until) return;
            Peels.Remove(this);
            Destroy(gameObject);
        }
    }

    internal static class MudVectorExtensions
    {
        public static Vector3 Flatten(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
