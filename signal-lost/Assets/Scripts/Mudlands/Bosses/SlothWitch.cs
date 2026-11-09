using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest line 3's mini-boss, Granny Drowse the Sloth Witch (Christian's design, docs/mudlands.md). She flies on a
    /// broom 6-9 m up, out of melee reach, and everything she does is lazy and slow except her magic.
    /// <list type="bullet">
    /// <item><b>Hex orbs:</b> three slow homing orbs (10 each); bolts pop them.</item>
    /// <item><b>Sleepy spores:</b> a purple cloud where you stand: half speed and a blur while you are in it.</item>
    /// <item><b>Cauldron rain:</b> the cauldron bubbles and spits five potion globs onto marked rings (12).</item>
    /// <item><b>Clone beam:</b> she splits into 3 (5 in phase 2) around you; all charge a beam for 3 s. Only the real
    /// one wears the hat and only she can be hurt; hit her to interrupt. Otherwise the beams fire at where you stood
    /// when they locked (80%), 15 each.</item>
    /// <item><b>Knocked off:</b> a full-charge bolt knocks her off the broom in a ragdoll tumble. She lands dazed, then
    /// runs (very slowly) back to the broom, taking 1.5x damage on the ground. Hit the broom to knock it further away.
    /// On the ground the car can ram her.</item>
    /// </list>
    /// 420 HP. Drops the Antigrav Coil and a Witch Ember (Bigger Boom).
    /// </summary>
    public sealed class SlothWitch : MudBoss
    {
        private enum State { Fly, Hex, Spores, Rain, Clones, Falling, Dazed, Walk, Mount, Wobble }

        private static readonly Color Fur = new Color(0.5f, 0.44f, 0.36f), Mask = new Color(0.88f, 0.82f, 0.68f),
            Patch = new Color(0.25f, 0.2f, 0.16f), Cloak = new Color(0.25f, 0.14f, 0.32f), HatColor = new Color(0.16f, 0.1f, 0.2f),
            Straw = new Color(0.8f, 0.68f, 0.38f), Hex = new Color(0.45f, 1f, 0.35f);

        private State state = State.Fly;
        private float stateTime, orbit, nextHex, nextSpores, nextRain, nextClones, hexFired;
        private Vector3 fallVelocity, broomSpot, beamLock;
        private bool beamLocked;
        private Transform body, hat, broom, armL, armR, eyelids, Head;
        private readonly List<Transform> clones = new List<Transform>();
        private GroundRing beamRing;
        private Health broomHealth;
        private PlayerFlask playerFlask;
        private bool dazedBySpores;

        public override string Subtitle => "Granny Drowse, the Sloth Witch";
        public override string Title => "Granny Drowse";

        // ---------- Model ----------

        protected override void BuildModel(Transform visual)
        {
            broom = MudArt.Pivot(visual, "Broom", new Vector3(0f, 0.6f, 0f));
            MudArt.Part(broom, "Broom Model", Vector3.zero, Quaternion.identity, "witch_broom", k =>
            {
                k.Rod(new Vector3(0f, 0f, 1.3f), new Vector3(0f, 0f, -1f), 0.05f, 6, MudPalette.Wood);
                k.Cone(new Vector3(0f, 0f, -0.95f), Quaternion.Euler(-90f, 0f, 0f), 0.32f, 0.7f, 8, Straw);
                k.Prism(new Vector3(0f, 0f, -0.95f), Quaternion.Euler(-90f, 0f, 0f), 0.2f, 0.2f, 0.08f, 8, new Color(0.5f, 0.15f, 0.12f));
            });
            // The ship's antigrav coil is what makes the broom fly.
            ShipPart = MudArt.Part(broom, "Antigrav Coil", new Vector3(0f, 0f, 0.6f), Quaternion.identity, "witch_coil", k =>
            {
                for (int i = 0; i < 3; i++) k.Prism(new Vector3(0f, 0f, -0.1f + i * 0.1f), Quaternion.Euler(90f, 0f, 0f), 0.13f, 0.13f, 0.04f, 10, MudPalette.Metal);
                k.Ball(Vector3.zero, 0.07f, new Color(0.6f, 0.4f, 1f), 0, 3f);
            });

            body = MudArt.Pivot(visual, "Body", new Vector3(0f, 0.75f, 0f));
            MudArt.Part(body, "Torso", Vector3.zero, Quaternion.identity, "witch_body", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(0f, 0.35f, 0f), new Vector3(0.38f, 0.48f, 0.34f), Quaternion.identity, Fur, 1);
                k.Lathe(new Vector3(0f, -0.05f, -0.05f), Quaternion.identity, new[] // the cloak, flaring at the hem
                {
                    new Vector2(0.6f, 0f), new Vector2(0.5f, 0.15f), new Vector2(0.4f, 0.45f), new Vector2(0.28f, 0.8f), new Vector2(0.12f, 0.98f), new Vector2(0.0001f, 1.02f)
                }, 12, Cloak);
                k.Ball(new Vector3(0f, 0.3f, 0.2f), new Vector3(0.25f, 0.3f, 0.18f), Quaternion.identity, new Color(0.6f, 0.53f, 0.43f), 1);
                for (int i = 0; i < 10; i++) // shaggy tufts
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    k.Ball(new Vector3(Mathf.Sin(a) * 0.33f, 0.08f, Mathf.Cos(a) * 0.28f), new Vector3(0.07f, 0.13f, 0.07f), Quaternion.Euler(Mathf.Cos(a) * 25f, 0f, -Mathf.Sin(a) * 25f), Fur, 1);
                }
            });
            Head = MudArt.Pivot(body, "Head", new Vector3(0f, 0.95f, 0.05f));
            MudArt.Part(Head, "Face", Vector3.zero, Quaternion.identity, "witch_head", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, new Vector3(0.3f, 0.27f, 0.28f), Quaternion.identity, Fur, 1);
                k.Ball(new Vector3(0f, -0.02f, 0.13f), new Vector3(0.23f, 0.18f, 0.17f), Quaternion.identity, Mask, 1);
                foreach (float x in new[] { -0.09f, 0.09f }) // the drooping dark patches round her eyes
                    k.Ball(new Vector3(x, 0.01f, 0.245f), new Vector3(0.085f, 0.045f, 0.035f), Quaternion.Euler(0f, 0f, x > 0 ? 25f : -25f), Patch, 1);
                k.Ball(new Vector3(0f, -0.05f, 0.3f), new Vector3(0.05f, 0.035f, 0.03f), Quaternion.identity, MudPalette.Black, 1);
                foreach (float x in new[] { -0.26f, 0.26f }) // fluffy cheeks
                    k.Ball(new Vector3(x, -0.08f, 0.05f), new Vector3(0.09f, 0.1f, 0.1f), Quaternion.identity, Fur, 1);
            });
            (Transform l, Transform r) = MudArt.Eyes(Head, new Vector3(0f, 0.03f, 0.255f), 0.09f, 0.032f, MudPalette.Black, MudPalette.Black, "witch");
            eyelids = MudArt.Part(Head, "Lids", new Vector3(0f, 0.05f, 0.262f), Quaternion.identity, "witch_lids", k =>
            {
                k.Smooth = true;
                k.Ball(new Vector3(-0.09f, 0f, 0f), new Vector3(0.05f, 0.018f, 0.02f), Quaternion.identity, Patch, 1);
                k.Ball(new Vector3(0.09f, 0f, 0f), new Vector3(0.05f, 0.018f, 0.02f), Quaternion.identity, Patch, 1);
            });
            Transform browL = BossFace.Brow(Head, "witch_brow", new Vector3(-0.1f, 0.11f, 0.24f), -1f, new Vector3(0.09f, 0.025f, 0.035f), new Color(0.72f, 0.66f, 0.55f), -6f);
            Transform browR = BossFace.Brow(Head, "witch_brow", new Vector3(0.1f, 0.11f, 0.24f), 1f, new Vector3(0.09f, 0.025f, 0.035f), new Color(0.72f, 0.66f, 0.55f), -6f);
            Transform mouth = BossFace.Mouth(Head, "witch_mouth", new Vector3(0f, -0.13f, 0.265f), 0.11f, 0.045f, new Color(0.5f, 0.42f, 0.34f), false, Quaternion.Euler(-20f, 0f, 0f));
            FaceParts = new BossFace.Rig
            {
                BrowL = browL, BrowR = browR, EyeL = l, EyeR = r, Lids = eyelids, Mouth = mouth,
                BrowTravel = 0.02f, Idle = Mood.Sleepy, Fight = Mood.Smug,
            };
            hat = MudArt.Part(Head, "Witch Hat", new Vector3(0f, 0.2f, -0.02f), Quaternion.Euler(-10f, 0f, 8f), "witch_hat", HatMesh);
            armL = Arm(body, -1f);
            armR = Arm(body, 1f);
        }

        private static void HatMesh(MeshKit k)
        {
            k.Prism(Vector3.zero, Quaternion.identity, 0.5f, 0.48f, 0.04f, 12, HatColor);
            k.Prism(new Vector3(0f, 0.04f, 0f), Quaternion.identity, 0.24f, 0.17f, 0.3f, 10, HatColor);
            k.Prism(new Vector3(0f, 0.34f, 0f), Quaternion.Euler(-14f, 0f, 0f), 0.17f, 0.06f, 0.35f, 8, HatColor);
            k.Cone(new Vector3(0f, 0.66f, -0.09f), Quaternion.Euler(-40f, 0f, 0f), 0.06f, 0.25f, 6, HatColor); // the crooked tip
            k.Prism(new Vector3(0f, 0.06f, 0f), Quaternion.identity, 0.245f, 0.24f, 0.07f, 10, new Color(0.45f, 0.75f, 0.3f));
            k.Box(new Vector3(0f, 0.09f, 0.24f), new Vector3(0.1f, 0.08f, 0.02f), new Color(0.85f, 0.75f, 0.3f)); // buckle
        }

        private static Transform Arm(Transform body, float side)
        {
            Transform shoulder = MudArt.Pivot(body, side < 0 ? "Arm L" : "Arm R", new Vector3(0.32f * side, 0.6f, 0.05f));
            MudArt.Part(shoulder, "Arm", Vector3.zero, Quaternion.identity, "witch_arm", k =>
            {
                k.Smooth = true;
                k.Ball(Vector3.zero, 0.1f, Fur, 1);
                k.Rod(Vector3.zero, new Vector3(0f, -0.65f, 0.1f), 0.09f, 8, Fur, 0f, 0.075f);
                k.Ball(new Vector3(0f, -0.65f, 0.1f), 0.08f, Fur, 1);
                k.Smooth = false;
                for (int i = -1; i <= 1; i++) // three long claws
                    k.Cone(new Vector3(i * 0.04f, -0.66f, 0.12f), Quaternion.Euler(150f, 0f, i * 10f), 0.02f, 0.2f, 4, MudPalette.Cream);
            });
            return shoulder;
        }

        protected override Action<MeshKit> KeyItemModel() => k =>
        {
            k.Ball(Vector3.zero, 0.15f, new Color(0.5f, 1f, 0.35f), 1, 3f);
            k.Cone(new Vector3(0f, 0.1f, 0f), Quaternion.identity, 0.1f, 0.25f, 5, new Color(0.4f, 0.9f, 0.3f), 2f);
        };

        // ---------- Entrance ----------

        public override IEnumerator Entrance(BossEntrance cine)
        {
            Vector3 home = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 toPlayer = (cine.PlayerPosition - home).Flatten().normalized;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.back;
            var lanterns = new List<Light>(transform.parent != null ? transform.parent.GetComponentsInChildren<Light>() : new Light[0]);
            cine.Cut(home + toPlayer * 16f + Vector3.up * 4f, home + Vector3.up * 2f);
            // The clearing's lanterns go out one by one.
            foreach (Light lantern in lanterns)
            {
                if (cine.Skipped) break;
                lantern.enabled = false;
                Sfx.Play("ui_close", 0.5f, 0.7f);
                yield return cine.Wait(0.25f);
            }
            // She waits inside the cauldron (its shell hides her), broom tucked away: it is longer than the pot is wide.
            transform.SetPositionAndRotation(home + Vector3.down * 1.7f, Quaternion.LookRotation(toPlayer));
            broom.gameObject.SetActive(false);
            Face?.Show(Mood.Sleepy, 8f);
            yield return cine.Move(home + toPlayer * 8f + Vector3.up * 2.5f, home + Vector3.up * 1.5f, 1f);
            // The cauldron bubbles harder and harder...
            Sfx.Play("magic_charge", 0.8f, 0.6f);
            Vector3 surface = home + Vector3.up * Surface;
            var glow = new GameObject("Cauldron Glow").AddComponent<Light>();
            glow.transform.position = surface + Vector3.up * 0.6f;
            glow.type = LightType.Point;
            glow.color = Hex;
            glow.range = 9f;
            glow.intensity = 0f;
            for (float t = 0f; t < 0.9f && !cine.Tick(); t += Time.deltaTime)
            {
                glow.intensity = t * 4f;
                if (Random.value < 0.6f) Bubble(surface, 0.5f + t);
                yield return null;
            }
            // ...and erupts: a column of green steam she rises through, very slowly, mid-yawn.
            Sfx.Play("mud_splat", 1f, 0.6f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, surface, Vector3.up, 45, 1.6f, 4f, 35f, new Color(0.55f, 1f, 0.5f, 0.75f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, surface, Vector3.up, 30, 0.25f, 6f, 40f, new Color(0.45f, 0.95f, 0.35f, 0.9f));
            cine.LookAt(Head, Vector3.zero);
            const float rise = 3f;
            for (float t = 0f; t < rise && !cine.Tick(); t += Time.deltaTime)
            {
                transform.position = home + Vector3.up * Mathf.Lerp(-1.7f, 3.5f, Mathf.SmoothStep(0f, 1f, t / rise));
                glow.intensity = Mathf.Lerp(3.6f, 1.2f, t / rise);
                // A thick collar of steam on the surface, so nothing is seen passing through it.
                if (Random.value < 0.85f) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, surface + Random.insideUnitSphere.Flatten() * 0.7f, Vector3.up, 3, 1.3f, 1.4f, 30f, new Color(0.55f, 1f, 0.5f, 0.6f));
                if (Random.value < 0.3f) Bubble(surface, 1.4f);
                armR.localRotation = Quaternion.Euler(-160f, 0f, 30f); // a long, lazy stretch
                yield return null;
            }
            Say("*yaaawn*", new Color(0.85f, 0.8f, 1f), 1.6f, 4f, 2.2f);
            // Her broom shoots up out of the pot on end (it only fits that way), flips flat and slides in under her.
            yield return BroomFromCauldron(cine, surface);
            yield return cine.Wait(0.5f);
            // A very slow blink...
            Face?.Show(Mood.Sleepy, 1.4f);
            yield return cine.Wait(1.2f);
            Face?.Show(Mood.Laugh, 2f);
            Destroy(glow.gameObject, 0.1f);
            // ...then a cackle at full speed, and the lanterns come back green.
            Sfx.Play("cackle", 1f);
            Say("HEHEHEHEHE!", Hex, 1.6f, 6f, 2.2f);
            foreach (Light lantern in lanterns)
            {
                lantern.color = Hex;
                lantern.enabled = true;
            }
            for (float t = 0f; t < 1.2f && !cine.Tick(); t += Time.deltaTime)
            {
                armL.localRotation = Quaternion.Euler(-120f + Mathf.Sin(t * 30f) * 20f, 0f, -30f);
                armR.localRotation = Quaternion.Euler(-120f - Mathf.Sin(t * 30f) * 20f, 0f, 30f);
                yield return null;
            }
            // A skip leaves her mid-rise: put her up where the fight starts, broom and all.
            if (cine.Skipped)
            {
                transform.position = FightStart(home);
                broom.gameObject.SetActive(true);
                broom.localPosition = BroomRest;
                broom.localRotation = Quaternion.identity;
            }
        }

        // The cauldron's liquid, above its base (MudModels.Kind.Cauldron).
        private const float Surface = 1.38f;
        private static readonly Vector3 BroomRest = new Vector3(0f, 0.6f, 0f);

        public override Vector3 FightStart(Vector3 home) => home + Vector3.up * 3.5f;
        public override bool WalksAfterEntrance => false; // she flies

        private static void Bubble(Vector3 surface, float size)
        {
            Vector3 at = surface + Random.insideUnitSphere.Flatten() * 0.8f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, Vector3.up, 2, 0.35f * size, 1.2f, 20f, new Color(0.5f, 1f, 0.45f, 0.7f));
            if (Random.value < 0.3f) ParticleBurst.Spawn(ParticleBurst.Kind.Water, at, Vector3.up, 4, 0.08f, 2.5f, 30f, new Color(0.45f, 0.95f, 0.35f, 0.9f));
        }

        private IEnumerator BroomFromCauldron(BossEntrance cine, Vector3 surface)
        {
            // Start deep in the pot, straight up and down, beside her (in her Visual's space, so the pot's middle is
            // under her and the broom rises past her side, inside the rim).
            Vector3 deep = Visual.InverseTransformPoint(surface + Vector3.down * 1.4f) + Vector3.right * 0.65f;
            Vector3 clear = new Vector3(0.65f, -0.6f, 0f);
            Quaternion upright = Quaternion.Euler(-90f, 0f, 0f);
            broom.localPosition = deep;
            broom.localRotation = upright;
            broom.gameObject.SetActive(true);
            Sfx.Play("chain_swish", 0.9f, 0.7f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, surface, Vector3.up, 25, 1.2f, 5f, 25f, new Color(0.55f, 1f, 0.5f, 0.7f));
            const float up = 0.6f, under = 0.5f;
            for (float t = 0f; t < up && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / up);
                broom.localPosition = Vector3.Lerp(deep, clear, k);
                broom.localRotation = upright * Quaternion.Euler(0f, 0f, 540f * k); // spinning on end as it shoots up
                yield return null;
            }
            // Clear of the rim: it flips flat and slides in under her.
            for (float t = 0f; t < under && !cine.Tick(); t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / under);
                broom.localPosition = Vector3.Lerp(clear, BroomRest, k) + Vector3.down * Mathf.Sin(k * Mathf.PI) * 0.4f;
                broom.localRotation = Quaternion.Slerp(upright, Quaternion.identity, k);
                yield return null;
            }
            broom.localPosition = BroomRest;
            broom.localRotation = Quaternion.identity;
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, broom.position, Vector3.zero, 12, 0.6f, 1.5f, 80f, new Color(0.7f, 0.5f, 1f, 0.6f));
            Sfx.Play("magic_charge", 0.5f, 1.4f);
        }

        // ---------- Base hooks ----------

        protected override void OnBossStarted()
        {
            if (Agent != null) Agent.enabled = false; // she flies
            broomHealth = MakeBroomTarget();
            var flaskOwner = Target != null ? Target.GetComponentInParent<PlayerFlask>() : null;
            playerFlask = flaskOwner;
            nextHex = Time.time + 1.5f;
            nextSpores = Time.time + 5f;
            nextRain = Time.time + 8f;
            nextClones = Time.time + 10f;
            Enter(State.Fly);
        }

        private Health MakeBroomTarget()
        {
            var hit = broom.gameObject.AddComponent<BoxCollider>();
            hit.size = new Vector3(0.4f, 0.4f, 2.4f);
            var health = broom.gameObject.AddComponent<Health>();
            health.SetMax(1000f);
            health.Restore();
            health.Damaged += (h, amount, source) =>
            {
                h.Restore();
                if (broom.parent == Visual || IsDead) return;
                // Knocked further away while she walks back to it.
                Vector3 away = (broom.position - transform.position).Flatten().normalized;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere.Flatten().normalized;
                broomSpot = ArenaPoint(broom.position + away * 8f);
                Say("MY BROOM!", Hex, 1.2f, 5f);
                Sfx.PlayAt("wood_smash", broom.position, 0.7f, 1.4f, 30f);
            };
            return health;
        }

        protected override void UpdatePatrol(float dt) => Hover(dt, 7f);

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        // ---------- Flying ----------

        private void Hover(float dt, float height)
        {
            orbit += dt * 0.25f; // lazy circles
            Vector3 centre = Home + new Vector3(Mathf.Cos(orbit), 0f, Mathf.Sin(orbit)) * 11f;
            Vector3 target = centre + Vector3.up * (height + Mathf.Sin(Time.time * 0.8f + BossSeed) * 1f);
            transform.position = Vector3.MoveTowards(transform.position, target, dt * 4f);
            Vector3 face = Target != null && Engaged ? (PlayerPosition - transform.position).Flatten() : new Vector3(-Mathf.Sin(orbit), 0f, Mathf.Cos(orbit));
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * 2f);
        }

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            UpdateSpores();
            switch (state)
            {
                case State.Fly:
                    Hover(dt, 7.5f);
                    if (Time.time >= nextClones) { StartClones(); break; }
                    if (Time.time >= nextRain) { Enter(State.Rain); Say("Stir, stir...", Hex, 1.2f, 4f); break; }
                    if (Time.time >= nextSpores) { Enter(State.Spores); break; }
                    if (Time.time >= nextHex) { Enter(State.Hex); hexFired = 0f; Sfx.PlayAt("magic_charge", transform.position, 0.5f, 1.5f, 40f); }
                    break;
                case State.Hex:
                    Hover(dt, 7.5f);
                    int count = Phase == 2 ? 4 : 3;
                    if (stateTime > 0.5f + hexFired * 0.4f && hexFired < count)
                    {
                        hexFired++;
                        HexOrb.Fire(armR.position + transform.forward * 0.5f, gameObject, Phase == 2 ? 8.5f : 7f);
                    }
                    if (hexFired >= count) { Enter(State.Fly); nextHex = Time.time + (Phase == 2 ? 3f : 4f); }
                    break;
                case State.Spores:
                    Hover(dt, 7.5f);
                    if (stateTime < 0.6f) break;
                    SporeCloud.Drop(transform.position, Ground(PlayerPosition), gameObject);
                    nextSpores = Time.time + 9f;
                    Enter(State.Fly);
                    break;
                case State.Rain: UpdateRain(dt); break;
                case State.Clones: UpdateClones(dt); break;
                case State.Wobble:
                    Hover(dt, 6f);
                    transform.rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(stateTime * 20f) * 2f);
                    if (stateTime > 1.2f) Enter(State.Fly);
                    break;
                case State.Falling: UpdateFall(dt); break;
                case State.Dazed:
                    Halt();
                    if (stateTime > (dazedLong ? 2.5f : 1.5f)) { Enter(State.Walk); Say("Oh, bother.", new Color(0.85f, 0.8f, 1f), 1.6f, 4f); }
                    break;
                case State.Walk:
                    Chase(broomSpot, 1.6f); // as fast as a sloth goes
                    TurnToward(broomSpot, 90f * dt);
                    if (Flat(broomSpot - transform.position) < 1.2f) Mount();
                    break;
                case State.Mount:
                    transform.position += Vector3.up * dt * 4f;
                    if (stateTime > 1.5f) { Enter(State.Fly); DamageTakenMultiplier = 1f; }
                    break;
            }
        }

        private bool dazedLong;

        // ---------- Attacks ----------

        private void UpdateRain(float dt)
        {
            Hover(dt, 7.5f);
            if (stateTime < 0.8f)
            {
                if (Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, Home + Vector3.up * 1.2f, Vector3.up, 4, 0.8f, 3f, 30f, new Color(0.55f, 1f, 0.5f, 0.7f));
                return;
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 5f;
                Vector3 land = Ground(PlayerPosition + new Vector3(spread.x, 0f, spread.y) * (i == 0 ? 0.1f : 1f));
                Transform glob = MudArt.Part(null, "Potion Glob", Vector3.zero, Quaternion.identity, "witch_glob", k => k.Ball(Vector3.zero, 0.3f, Hex, 1, 2f));
                GameObject owner = gameObject;
                Lob.Throw(glob, Home + Vector3.up * 1.5f, land, 1.2f + i * 0.12f, 7f, at =>
                {
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, Vector3.up, 14, 0.8f, 3f, 70f, new Color(0.55f, 1f, 0.45f, 0.8f));
                    Sfx.PlayAt("hex_zap", at, 0.6f, 0.8f, 30f);
                    if (TargetAlive && (PlayerPosition - at).Flatten().magnitude < 1.7f) HurtPlayer(12f);
                }, 1.6f, new Color(0.5f, 1f, 0.35f, 0.5f));
            }
            Sfx.PlayAt("mud_splat", Home, 1f, 0.7f, 40f);
            nextRain = Time.time + 12f;
            Enter(State.Fly);
        }

        private void StartClones()
        {
            Enter(State.Clones);
            beamLocked = false;
            int count = Phase == 2 ? 5 : 3;
            Sfx.PlayAt("cackle", transform.position, 1f, 1.2f, 60f);
            Say("Which one am I?", Hex, 1.4f, 4f);
            // The real witch takes one spot at random; the clones (no hats, can't be hurt) take the rest.
            int real = Random.Range(0, count);
            float start = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 spot = PlayerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 11f + Vector3.up * 4.5f;
                if (i == real)
                {
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position, Vector3.zero, 30, 1f, 3f, 80f, new Color(0.6f, 0.4f, 1f, 0.7f));
                    transform.position = spot;
                    continue;
                }
                Transform copy = Instantiate(Visual.gameObject, spot, Quaternion.identity).transform;
                copy.name = "Witch Clone";
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>()) Destroy(behaviour);
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>()) Destroy(collider);
                Transform copyHat = FindChild(copy, "Witch Hat");
                if (copyHat != null) Destroy(copyHat.gameObject);
                // A bolt that hits a clone fizzles: it has a body to hit, but can't be hurt.
                var shape = copy.gameObject.AddComponent<CapsuleCollider>();
                shape.center = new Vector3(0f, 1.2f, 0f);
                shape.height = 2.2f;
                shape.radius = 0.5f;
                var health = copy.gameObject.AddComponent<Health>();
                health.Invulnerable = true;
                health.Blocked += (h, a, s) => ParticleBurst.Spawn(ParticleBurst.Kind.Mist, h.transform.position + Vector3.up, Vector3.up, 10, 0.5f, 2f, 60f, new Color(0.6f, 0.4f, 1f, 0.7f));
                clones.Add(copy);
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, spot, Vector3.zero, 30, 1f, 3f, 80f, new Color(0.6f, 0.4f, 1f, 0.7f));
            }
            beamRing = GroundRing.Spawn(PlayerPosition, 2f, new Color(0.6f, 1f, 0.35f, 0.5f));
            Sfx.PlayAt("magic_charge", transform.position, 1f, 0.8f, 60f);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        private void UpdateClones(float dt)
        {
            const float charge = 3f;
            float k = stateTime / charge;
            Vector3 face = (PlayerPosition - transform.position).Flatten();
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            foreach (Transform clone in clones)
                if (clone != null)
                {
                    Vector3 cf = (PlayerPosition - clone.position).Flatten();
                    if (cf.sqrMagnitude > 0.01f) clone.rotation = Quaternion.LookRotation(cf);
                    FindChild(clone, "Arm R").localRotation = Quaternion.Euler(-100f, 0f, 20f);
                }
            if (!beamLocked)
            {
                beamLock = PlayerPosition;
                if (beamRing != null) beamRing.MoveTo(Ground(beamLock));
                if (k >= 0.8f) { beamLocked = true; Sfx.PlayAt("ui_click", transform.position, 1f, 1.6f, 60f); }
            }
            if (beamRing != null) beamRing.Progress = k;
            if (Random.value < 0.5f)
            {
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, armR.position, Vector3.zero, 2, 0.08f, 1f, 80f, Hex);
                foreach (Transform clone in clones) if (clone != null) ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, clone.position + Vector3.up * 1.4f, Vector3.zero, 2, 0.08f, 1f, 80f, Hex);
            }
            if (k < 1f) return;
            // Fire: every caster's beam lands on the locked spot.
            int beams = clones.Count + 1;
            Beam(armR.position, beamLock);
            foreach (Transform clone in clones) if (clone != null) Beam(clone.position + Vector3.up * 1.4f, beamLock);
            CameraShake.Add(0.4f);
            Sfx.PlayAt("hex_zap", beamLock, 1f, 0.6f, 60f);
            if (TargetAlive && (PlayerPosition - beamLock).Flatten().magnitude < 1.8f) HurtPlayer(15f * beams);
            EndClones(false);
        }

        private void EndClones(bool interrupted)
        {
            foreach (Transform clone in clones)
                if (clone != null)
                {
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, clone.position + Vector3.up, Vector3.zero, 25, 1f, 3f, 80f, new Color(0.6f, 0.4f, 1f, 0.7f));
                    Destroy(clone.gameObject);
                }
            clones.Clear();
            if (beamRing != null) { beamRing.Finish(); beamRing = null; }
            nextClones = Time.time + (Phase == 2 ? 11f : 14f);
            if (interrupted) { Enter(State.Wobble); Say("OW! My concentration!", Hex, 1.4f, 4f); }
            else Enter(State.Fly);
        }

        private static void Beam(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Hex Beam");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.5f;
            line.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Beam);
            line.startColor = line.endColor = Hex;
            Destroy(go, 0.35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, to, Vector3.up, 20, 0.1f, 6f, 80f, Hex);
        }

        // ---------- Falling off the broom ----------

        private void KnockOff(Vector3 push)
        {
            if (state == State.Falling || state == State.Dazed || state == State.Walk || state == State.Mount) return;
            if (state == State.Clones) EndClones(false);
            Enter(State.Falling);
            fallVelocity = push * 4f + Vector3.up * 3f;
            broom.SetParent(null, true);
            broomSpot = ArenaPoint(transform.position + Random.insideUnitSphere.Flatten().normalized * 9f);
            Say("WAAAH!", Hex, 1.2f, 5f);
            Sfx.PlayAt("cackle", transform.position, 0.8f, 0.6f, 50f);
        }

        private void UpdateFall(float dt)
        {
            fallVelocity += Physics.gravity * dt;
            transform.position += fallVelocity * dt;
            Visual.Rotate(new Vector3(400f, 60f, 200f) * dt, Space.Self); // the ragdoll tumble
            // The broom drifts down to hover by the ground somewhere nearby.
            broom.position = Vector3.MoveTowards(broom.position, broomSpot + Vector3.up * 1f, dt * 8f);
            Vector3 ground = Ground(transform.position);
            if (transform.position.y > ground.y + 0.05f || fallVelocity.y > 0f) return;
            transform.position = ground;
            Visual.localRotation = VisualRestRotation;
            Dust(ground, 35, 0.9f);
            CameraShake.Add(0.3f);
            Sfx.PlayAt("player_land", ground, 1f, 0.6f, 40f);
            AgentUtil.Reenable(Agent, 5f);
            DamageTakenMultiplier = 1.5f;
            dazedLong = false;
            Enter(State.Dazed);
            Say("@_@", new Color(0.85f, 0.8f, 1f), 1.4f, 5f);
        }

        private void Mount()
        {
            Halt();
            if (Agent != null) Agent.enabled = false;
            broom.SetParent(Visual, false);
            broom.localPosition = new Vector3(0f, 0.6f, 0f);
            broom.localRotation = Quaternion.identity;
            Enter(State.Mount);
            Say("Up we go...", new Color(0.85f, 0.8f, 1f), 1.4f, 4f);
        }

        private Vector3 ArenaPoint(Vector3 point)
        {
            Vector3 offset = (point - Home).Flatten();
            if (offset.magnitude > ArenaRadius * 0.7f) point = Home + offset.normalized * ArenaRadius * 0.7f;
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : Home;
        }

        // ---------- Spores ----------

        private void UpdateSpores()
        {
            bool inside = SporeCloud.Contains(PlayerPosition);
            if (inside)
            {
                if (Target != null) MudlandsPlayerSlow.Ensure(Target.gameObject).SlowFor(0.5f, 0.25f);
                if (playerFlask != null) playerFlask.ExtraDaze = Mathf.Max(playerFlask.ExtraDaze, 0.5f);
                dazedBySpores = true;
            }
            else if (dazedBySpores)
            {
                dazedBySpores = false;
                if (playerFlask != null) playerFlask.ExtraDaze = 0f;
            }
        }

        // ---------- Damage and the car ----------

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Clones && amount >= 5f) { EndClones(true); return; }
            bool flying = state == State.Fly || state == State.Hex || state == State.Spores || state == State.Rain || state == State.Wobble;
            if (flying && IsFullCharge(amount, source)) KnockOff(-away.Flatten().normalized * -1f);
        }

        public override VehicleResponse OnVehicleHit(VehicleHit hit) => VehicleResponse.Explode;

        public override float OnCarBlast(CarBlastInfo blast)
        {
            if (state == State.Dazed || state == State.Walk) { Enter(State.Dazed); dazedLong = true; Say("@_@", new Color(0.85f, 0.8f, 1f), 2f, 5f); }
            return blast.Damage;
        }

        protected override void OnPhaseTwo()
        {
            Say("Now I'm AWAKE!", Hex, 1.8f, 5f);
            Sfx.PlayAt("cackle", transform.position, 1f, 0.9f, 60f);
        }

        protected override void UpdatePhaseShift(float dt) { if (state == State.Fly) Hover(dt, 8f); }

        protected override void OnReset()
        {
            if (state == State.Clones) EndClones(false);
            if (broom.parent != Visual)
            {
                broom.SetParent(Visual, false);
                broom.localPosition = new Vector3(0f, 0.6f, 0f);
                broom.localRotation = Quaternion.identity;
            }
            if (Agent != null) Agent.enabled = false;
            transform.position = Home + Vector3.up * 7f;
            DamageTakenMultiplier = 1f;
            Enter(State.Fly);
            if (playerFlask != null) playerFlask.ExtraDaze = 0f;
        }

        protected override void OnMudBossDeath()
        {
            EndClones(false);
            SporeCloud.ClearAll();
            if (playerFlask != null) playerFlask.ExtraDaze = 0f;
            Sfx.PlayAt("cackle", transform.position, 0.8f, 0.5f, 50f);
        }

        // ---------- Animation ----------

        protected override void AnimateBoss(float dt)
        {
            float t = Time.time;
            bool riding = broom.parent == Visual;
            Quaternion left = Quaternion.Euler(-60f, 0f, -20f), right = Quaternion.Euler(-60f, 0f, 20f);
            float sit = riding ? 0.75f : 1f;
            switch (state)
            {
                case State.Fly:
                    left = Quaternion.Euler(-70f + Mathf.Sin(t * 0.8f) * 8f, 0f, -15f);
                    right = Quaternion.Euler(-70f + Mathf.Cos(t * 0.8f) * 8f, 0f, 15f);
                    break;
                case State.Hex:
                case State.Spores:
                    right = Quaternion.Euler(-120f, 0f, 20f + Mathf.Sin(t * 12f) * 10f);
                    break;
                case State.Rain:
                    right = Quaternion.Euler(-90f + Mathf.Sin(t * 8f) * 30f, Mathf.Sin(t * 8f) * 40f, 30f); // stirring
                    break;
                case State.Clones:
                    right = Quaternion.Euler(-100f, 0f, 20f);
                    left = Quaternion.Euler(-100f, 0f, -20f);
                    break;
                case State.Walk:
                    float swing = Mathf.Sin(t * 2.2f); // big, slow-motion arm swings
                    left = Quaternion.Euler(-40f + swing * 50f, 0f, -20f);
                    right = Quaternion.Euler(-40f - swing * 50f, 0f, 20f);
                    sit = 1f;
                    break;
                case State.Dazed:
                    left = Quaternion.Euler(-10f, 0f, -50f);
                    right = Quaternion.Euler(-10f, 0f, 50f);
                    break;
            }
            body.localPosition = new Vector3(0f, sit, riding ? -0.1f : 0f);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, left, dt * 6f); // sloth-slow, even in a fight
            armR.localRotation = Quaternion.Slerp(armR.localRotation, right, dt * 6f);
            // Heavy, half-shut eyes and the slow blinks are the face's (BossFace).
            hat.localRotation = Quaternion.Euler(-10f, 0f, 8f + Mathf.Sin(t * 1.2f) * 6f);
            if (riding) broom.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 4f, 0f, Mathf.Sin(t * 0.9f) * 5f);
        }

        protected override void UpdateDeath(float t)
        {
            // Floats down like a falling leaf and falls asleep. Snoring.
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, 80f * Mathf.Clamp01(t / 1.2f));
            Vector3 ground = Ground(transform.position);
            if (transform.position.y > ground.y) transform.position = Vector3.MoveTowards(transform.position, ground, Time.deltaTime * 3f);
            if (t > 1.2f && Mathf.Repeat(t, 1.5f) < Time.deltaTime) Emote.Show(transform, "zzz", new Color(0.85f, 0.8f, 1f), 1.5f, 1.2f, 4f);
        }
    }

    /// <summary>The witch's slow homing hex orb: 10 on contact; any bolt or swing pops it.</summary>
    public sealed class HexOrb : MonoBehaviour
    {
        private Vector3 velocity;
        private float speed, born;
        private Transform target;
        private GameObject owner;

        public static void Fire(Vector3 from, GameObject owner, float speed)
        {
            var go = new GameObject("Hex Orb");
            go.transform.position = from;
            MudArt.Part(go.transform, "Orb", Vector3.zero, Quaternion.identity, "hex_orb", k => k.Ball(Vector3.zero, 0.3f, new Color(0.5f, 1f, 0.35f), 1, 3f));
            go.AddComponent<SphereCollider>().radius = 0.35f;
            var health = go.AddComponent<Health>();
            health.SetMax(1f);
            health.Restore();
            var orb = go.AddComponent<HexOrb>();
            health.Died += _ => orb.Pop();
            orb.owner = owner;
            orb.speed = speed;
            orb.born = Time.time;
            var player = FindAnyObjectByType<FirstPersonController>();
            orb.target = player != null ? player.transform : null;
            orb.velocity = Vector3.down * speed * 0.5f;
            Sfx.PlayAt("hex_zap", from, 0.4f, 1.6f, 30f);
        }

        private void Update()
        {
            if (Time.time - born > 6f || target == null) { Pop(); return; }
            Vector3 aim = (target.position + Vector3.up * 1.2f - transform.position).normalized * speed;
            velocity = Vector3.RotateTowards(velocity, aim, 60f * Mathf.Deg2Rad * Time.deltaTime, speed * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(0f, 400f * Time.deltaTime, 0f);
            if ((target.position + Vector3.up * 1.2f - transform.position).sqrMagnitude < 0.8f)
            {
                var health = target.GetComponent<Health>();
                if (health != null) health.TakeDamage(10f, owner);
                Pop();
            }
        }

        private void Pop()
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position, Vector3.zero, 16, 0.08f, 4f, 80f, new Color(0.5f, 1f, 0.35f));
            Sfx.PlayAt("hex_zap", transform.position, 0.4f, 2f, 25f);
            Destroy(gameObject);
        }
    }

    /// <summary>The witch's sleepy spore cloud: anyone inside is slowed and drowsy (the witch applies it to the player).</summary>
    public sealed class SporeCloud : MonoBehaviour
    {
        private static readonly List<SporeCloud> Clouds = new List<SporeCloud>();
        private const float Radius = 3.5f;
        private float until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clouds.Clear();

        public static void Drop(Vector3 from, Vector3 at, GameObject owner)
        {
            Transform pod = MudArt.Part(null, "Spore Pod", Vector3.zero, Quaternion.identity, "spore_pod", k => k.Ball(Vector3.zero, 0.25f, MudPalette.Magic, 1, 1.5f));
            Lob.Throw(pod, from, at, 1f, 1f, landed =>
            {
                var go = new GameObject("Spore Cloud");
                go.transform.position = landed;
                var cloud = go.AddComponent<SporeCloud>();
                cloud.until = Time.time + 5f;
                Clouds.Add(cloud);
                Sfx.PlayAt("mud_splat", landed, 0.6f, 1.6f, 30f);
            }, Radius, new Color(0.6f, 0.4f, 1f, 0.45f));
        }

        public static bool Contains(Vector3 point)
        {
            foreach (SporeCloud cloud in Clouds)
                if (cloud != null && (cloud.transform.position - point).Flatten().magnitude < Radius) return true;
            return false;
        }

        public static void ClearAll()
        {
            foreach (SporeCloud cloud in Clouds) if (cloud != null) Destroy(cloud.gameObject);
            Clouds.Clear();
        }

        private void Update()
        {
            if (Time.time > until) { Clouds.Remove(this); Destroy(gameObject); return; }
            // A haze you can still see through from inside (it slows and dazes; it shouldn't blind).
            if (Random.value < 0.35f)
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Random.insideUnitSphere.Flatten() * Radius * 0.8f, Vector3.up, 2, 1.1f, 0.6f, 30f,
                    new Color(0.65f, 0.45f, 1f, 0.28f));
        }
    }
}
