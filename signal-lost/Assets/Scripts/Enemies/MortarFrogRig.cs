using System.Collections.Generic;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies
{
    /// <summary>
    /// The mortar frog's body and its procedural animation: hopping (crouch, leap, land), throat breathing, the
    /// loading telegraph (it rears back, aims the can up, throat swells, can rattles and glows), the hop it does
    /// when it fires, the backfire stun (it slips onto its side dripping goo, legs flailing, then scrambles up),
    /// clangs and sparks when the can is hit, flinching away from hits, the white hit flash, and the death flop.
    /// MortarFrog drives it by setting its public fields and calling its methods.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MortarFrogRig : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private const float FireHopTime = 0.45f;

        [Header("Body parts (set by the prefab builder)")]
        [Tooltip("Everything visible. Lifted for hops and flipped on death.")]
        [SerializeField] private Transform visual;
        [SerializeField] private Transform body;
        [SerializeField] private Transform throat;
        [SerializeField] private Transform[] eyes = new Transform[0];
        [Tooltip("Front leg pivots at the shoulders.")]
        [SerializeField] private Transform[] frontLegs = new Transform[0];
        [Tooltip("Hind leg pivots at the hips. They kick back on take-off.")]
        [SerializeField] private Transform[] hindLegs = new Transform[0];
        [Tooltip("The tin can's pivot on the frog's back.")]
        [SerializeField] private Transform canMount;
        [Tooltip("The can's open end, where goo comes out.")]
        [SerializeField] private Transform canMouth;

        [Header("Look")]
        [SerializeField] private Color gooColor = new Color(0.45f, 1f, 0.25f);
        [SerializeField, Min(0f)] private float hopHeight = 1.4f;
        [Tooltip("How high it hops off the ground from the can's kick when it fires.")]
        [SerializeField, Min(0f)] private float fireHopHeight = 0.55f;
        [Tooltip("How far it leans back while loading, in degrees, so the can points up for the lob.")]
        [SerializeField, Range(0f, 40f)] private float loadRear = 24f;
        [Tooltip("Scales every squash and stretch. 0 turns it off, 1 is the default cartoon bounce.")]
        [SerializeField, Range(0f, 2f)] private float squashAmount = 1f;

        // ---------- Set by MortarFrog every frame ----------
        /// <summary>0 to 1 through a hop, or below 0 when not hopping.</summary>
        [System.NonSerialized] public float Hop = -1f;
        /// <summary>0 to 1 while crouching to spring, just before a hop leaves the ground.</summary>
        [System.NonSerialized] public float Crouch;
        /// <summary>0 to 1 while loading a shot: it rears back, aims the can up, throat swells, can rattles and glows.</summary>
        [System.NonSerialized] public float Load;
        /// <summary>True while stunned and covered in its own goo after a backfire.</summary>
        [System.NonSerialized] public bool Gooed;

        public Transform CanMouth => canMouth != null ? canMouth : transform;
        public Material GooMaterial => gooMaterial;

        private sealed class Drip
        {
            public Transform Body;
            public Vector3 Velocity;
            public float Age;
        }

        private readonly List<Drip> drips = new List<Drip>();
        private Renderer[] renderers = new Renderer[0];
        private MaterialPropertyBlock block;
        private Material gooMaterial, sparkMaterial, dustMaterial;
        private Transform glowOrb;
        private Renderer glowOrbRenderer;

        private Vector3 visualPosition, visualScale = Vector3.one, throatScale, canPosition, bodyScale = Vector3.one;
        private Quaternion canRest;
        private Quaternion[] frontRest, hindRest, eyeRest;
        private Vector3 eyeScale = Vector3.one;
        private Vector3 flinchAway = Vector3.back; // local direction the last hit pushed it

        private float landTime = float.NegativeInfinity, fireTime = float.NegativeInfinity, flashUntil;
        private float flinchStart = float.NegativeInfinity, deathStart = float.NegativeInfinity;
        private float gooStart = float.NegativeInfinity, ungooTime = float.NegativeInfinity, clangTime = float.NegativeInfinity;
        private float nextBlink, blinkStart = float.NegativeInfinity, nextDrip;
        private float fall; // 0 upright, 1 lying on its side (backfire stun)
        private float fallSide = 1f;
        private bool wasHopping, dead, wasGooed, fireHopLanded = true;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (visual == null) visual = transform;
            visualPosition = visual.localPosition;
            visualScale = visual.localScale;
            if (throat != null) throatScale = throat.localScale;
            if (body != null) bodyScale = body.localScale;
            if (canMount != null)
            {
                canRest = canMount.localRotation;
                canPosition = canMount.localPosition;
            }
            frontRest = Rest(frontLegs);
            hindRest = Rest(hindLegs);
            eyeRest = Rest(eyes);
            if (eyes.Length > 0 && eyes[0] != null) eyeScale = eyes[0].localScale;
            renderers = visual.GetComponentsInChildren<Renderer>(true);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                gooMaterial = new Material(unlit) { name = "Frog Goo (runtime)" };
                gooMaterial.SetColor(BaseColor, gooColor);
                sparkMaterial = new Material(unlit) { name = "Can Spark (runtime)" };
                sparkMaterial.SetColor(BaseColor, new Color(1f, 0.95f, 0.75f));
                dustMaterial = new Material(unlit) { name = "Frog Dust (runtime)" };
                dustMaterial.SetColor(BaseColor, new Color(0.62f, 0.56f, 0.48f));
                BuildGlow();
            }
            nextBlink = Time.time + Random.Range(1f, 3f);
        }

        private static Quaternion[] Rest(Transform[] parts)
        {
            var rest = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++) rest[i] = parts[i] != null ? parts[i].localRotation : Quaternion.identity;
            return rest;
        }

        private void OnDestroy()
        {
            ClearDrips();
            if (gooMaterial != null) Destroy(gooMaterial);
            if (sparkMaterial != null) Destroy(sparkMaterial);
            if (dustMaterial != null) Destroy(dustMaterial);
        }

        /// <summary>The can kicks back, a puff of goo bursts from its mouth, and the frog hops off the ground.</summary>
        public void Fire()
        {
            fireTime = Time.time;
            fireHopLanded = false;
            if (gooMaterial != null) ImpactFlash.Spawn(gooMaterial, CanMouth.position, 0.3f, 0.12f);
        }

        /// <summary>Hit flash and a flinch straight back.</summary>
        public void Flinch() => Flinch(-transform.forward);

        /// <summary>Hit flash, eyes squeezed shut, and a lean away along <paramref name="worldAway"/>.</summary>
        public void Flinch(Vector3 worldAway)
        {
            flinchStart = Time.time;
            flashUntil = Time.time + 0.07f;
            Vector3 local = transform.InverseTransformDirection(worldAway);
            local.y = 0f;
            flinchAway = local.sqrMagnitude > 0.0001f ? local.normalized : Vector3.back;
        }

        /// <summary>Something hit the tin can: it rings, wobbles hard and throws sparks toward the shooter.</summary>
        public void Clang(Vector3 towardShooter)
        {
            clangTime = Time.time;
            if (sparkMaterial == null || canMount == null) return;
            towardShooter.y = 0f;
            Vector3 side = towardShooter.sqrMagnitude > 0.0001f ? towardShooter.normalized : transform.forward;
            Vector3 point = canMount.position + Vector3.up * 0.12f + side * 0.14f;
            ImpactFlash.Spawn(sparkMaterial, point, 0.22f, 0.1f);
            ImpactFlash.Spawn(sparkMaterial, point + Vector3.up * 0.1f + Random.insideUnitSphere * 0.08f, 0.1f, 0.14f);
        }

        /// <summary>The can backfires: goo bursts out over the frog and splatters around it.</summary>
        public void Backfire()
        {
            if (gooMaterial == null) return;
            ImpactFlash.Spawn(gooMaterial, CanMouth.position, 0.7f, 0.25f);
            ImpactFlash.Spawn(gooMaterial, transform.position + Vector3.up * 0.4f, 0.9f, 0.3f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 spray = Random.onUnitSphere;
                spray.y = Mathf.Abs(spray.y) + 0.6f;
                SpawnDrip(CanMouth.position, spray * Random.Range(1.8f, 3.2f), Random.Range(0.06f, 0.11f));
            }
        }

        public void PlayDeath()
        {
            dead = true;
            deathStart = Time.time;
            flashUntil = Time.time + 0.1f;
        }

        public void ResetPose()
        {
            dead = false;
            Gooed = false;
            wasGooed = false;
            Load = 0f;
            Crouch = 0f;
            Hop = -1f;
            fall = 0f;
            deathStart = float.NegativeInfinity;
            ungooTime = float.NegativeInfinity;
            fireHopLanded = true;
            ClearDrips();
            SetVisible(true);
        }

        public void SetVisible(bool show)
        {
            foreach (Renderer item in renderers) if (item != null) item.enabled = show;
            if (!show && glowOrbRenderer != null) glowOrbRenderer.enabled = false;
        }

        private void BuildGlow()
        {
            if (canMouth == null) return;
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Can Glow";
            Collider collider = orb.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            glowOrb = orb.transform;
            glowOrb.SetParent(canMouth, false);
            glowOrbRenderer = orb.GetComponent<Renderer>();
            glowOrbRenderer.sharedMaterial = gooMaterial;
            glowOrbRenderer.shadowCastingMode = ShadowCastingMode.Off;
            glowOrbRenderer.enabled = false;
        }

        private void Update()
        {
            float time = Time.time;
            float dt = Time.deltaTime;
            bool hopping = Hop >= 0f && !dead;
            if (wasHopping && !hopping) Land(time);
            wasHopping = hopping;
            if (Gooed && !wasGooed)
            {
                gooStart = time;
                fallSide = Random.value < 0.5f ? -1f : 1f;
            }
            if (!Gooed && wasGooed) ungooTime = time;
            wasGooed = Gooed;

            // Slip over fast, get back up a little slower.
            bool down = Gooed && !dead;
            fall = Mathf.MoveTowards(fall, down ? 1f : 0f, dt / (down ? 0.22f : 0.35f));

            // The hop from firing lands like any other hop.
            if (!fireHopLanded && time - fireTime >= FireHopTime)
            {
                fireHopLanded = true;
                Land(time);
            }

            AnimateBody(time, hopping);
            AnimateLegs(time, hopping);
            AnimateThroatAndEyes(time);
            AnimateCan(time);
            AnimateDrips(time, dt);
            ApplyColors(time);
        }

        /// <summary>Touch-down: start the landing squash and kick up a puff of dust.</summary>
        private void Land(float time)
        {
            landTime = time;
            if (dustMaterial == null || dead) return;
            Vector3 feet = transform.position + Vector3.up * 0.05f;
            ImpactFlash.Spawn(dustMaterial, feet + transform.right * 0.25f, 0.35f, 0.2f);
            ImpactFlash.Spawn(dustMaterial, feet - transform.right * 0.25f, 0.35f, 0.2f);
            ImpactFlash.Spawn(dustMaterial, feet - transform.forward * 0.2f, 0.3f, 0.16f);
        }

        /// <summary>A springy landing: squashes flat, overshoots tall, settles.</summary>
        private static float Bounce(float time, float start)
        {
            float t = time - start;
            if (t < 0f || t > 0.6f) return 0f;
            return Mathf.Exp(-t * 7f) * Mathf.Cos(t * 20f);
        }

        private static float Pulse(float time, float start, float duration)
        {
            float t = Mathf.Clamp01((time - start) / duration);
            return t < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
        }

        private void AnimateBody(float time, bool hopping)
        {
            Vector3 position = visualPosition;
            Quaternion rotation = Quaternion.identity;
            float squash = 0f; // above 0 squashes flat and wide, below 0 stretches tall and thin
            float puff = 0f;   // uniform swelling

            if (hopping)
            {
                // Arc up and down; nose up on take-off, nose down coming in to land, a little roll in the air.
                float p = Mathf.Clamp01(Hop);
                position.y += Mathf.Sin(p * Mathf.PI) * hopHeight;
                rotation *= Quaternion.Euler(Mathf.Lerp(-28f, 22f, Mathf.SmoothStep(0f, 1f, p)), 0f, Mathf.Sin(p * Mathf.PI * 2f) * 6f);
                // Stretch hard on take-off, round out at the top, stretch again diving in.
                squash -= 0.28f * Mathf.Sin(Mathf.Clamp01(p / 0.35f) * Mathf.PI) + 0.12f * Mathf.Clamp01((p - 0.6f) / 0.4f);
            }
            else
            {
                // Breathing: a slow bob and swell.
                float breath = Mathf.Sin(time * 2.4f);
                position.y += breath * 0.015f;
                squash -= breath * 0.03f;

                // Crouch to spring: squat low and wide, nose down.
                squash += 0.3f * Crouch;
                rotation *= Quaternion.Euler(10f * Crouch, 0f, 0f);

                // Loading: squat, rear back so the can points up, swell, and quiver with effort.
                squash += 0.14f * Load;
                puff += 0.1f * Load + Mathf.Sin(time * 32f) * 0.025f * Load;
                rotation *= Quaternion.Euler(-loadRear * Load, Mathf.Sin(time * 41f) * 3f * Load, Mathf.Sin(time * 37f) * 3f * Load);
                position.x += Mathf.Sin(time * 47f) * 0.015f * Load;
            }

            // Landing bounce (from hops and from the firing hop).
            if (!hopping) squash += 0.32f * Bounce(time, landTime);

            // Fire: the can's kick throws the frog up off the ground and jolts it back.
            float fireHop = Pulse(time, fireTime, FireHopTime);
            position.y += fireHop * fireHopHeight;
            float firing = Mathf.Clamp01((time - fireTime) / FireHopTime);
            if (firing < 1f) squash -= 0.25f * Mathf.Sin(Mathf.Clamp01(firing / 0.4f) * Mathf.PI);
            float kick = Pulse(time, fireTime, 0.3f);
            rotation *= Quaternion.Euler(-26f * kick, 0f, 0f);
            position.z -= 0.18f * kick;

            // Flinch: lean away from the hit, slide that way, and squash from the impact.
            float flinch = Mathf.Clamp01((time - flinchStart) / 0.35f);
            if (flinch < 1f)
            {
                float lean = Mathf.Sin(flinch * Mathf.PI) * (1f - 0.4f * flinch);
                rotation = Quaternion.AngleAxis(26f * lean, Vector3.Cross(Vector3.up, flinchAway)) * rotation;
                position += flinchAway * 0.12f * lean;
                squash += 0.15f * lean;
            }

            // Backfire stun: a startled bounce, then it slips onto its side and rocks there, dazed.
            float f = Mathf.SmoothStep(0f, 1f, fall);
            if (f > 0f)
            {
                float rock = Mathf.Sin((time - gooStart) * 6f) * 10f;
                rotation *= Quaternion.Euler(Mathf.Sin((time - gooStart) * 4f) * 6f * f, 0f, (100f + rock) * fallSide * f);
                position.y += 0.32f * f;
            }
            if (Gooed) position.y += Pulse(time, gooStart, 0.3f) * 0.4f;
            // Getting back up: a quick scramble hop.
            position.y += Pulse(time, ungooTime, 0.35f) * 0.25f;

            if (dead)
            {
                // Flop onto its back and twitch, then sink away.
                float age = time - deathStart;
                float flip = Mathf.Clamp01(age / 0.4f);
                position.y += Mathf.Sin(flip * Mathf.PI) * 0.45f + flip * 0.55f;
                rotation *= Quaternion.Euler(0f, 0f, 180f * Mathf.SmoothStep(0f, 1f, flip));
                position.y -= Mathf.Clamp01((age - 2.5f) / 1f) * 1.1f;
                squash = 0f;
                puff = 0f;
            }

            visual.localPosition = position;
            visual.localRotation = rotation;

            // Squash and stretch the whole frog from its feet, roughly keeping its volume.
            float tall = Mathf.Clamp(1f - squash * squashAmount, 0.55f, 1.5f);
            float wide = 1f / Mathf.Sqrt(tall);
            visual.localScale = Vector3.Scale(visualScale, new Vector3(wide, tall, wide)) * (1f + puff * squashAmount);

            // The back swells as it breathes in, and bulges with the shot it is loading.
            if (body != null)
                body.localScale = bodyScale * (1f + (dead ? 0f : Mathf.Max(0f, Mathf.Sin(time * 2.4f)) * 0.05f + 0.12f * Load));
        }

        private void AnimateLegs(float time, bool hopping)
        {
            float p = hopping ? Mathf.Clamp01(Hop) : 0f;
            // Hind legs snap straight out behind on take-off, trail through the air, then fold to land.
            float extend = hopping ? Mathf.Clamp01(p / 0.12f) * (1f - Mathf.SmoothStep(0.55f, 0.95f, p)) : 0f;
            // Front legs push back on take-off, then swing forward to catch the landing.
            float front = hopping ? Mathf.Lerp(25f, -60f, Mathf.SmoothStep(0.1f, 0.8f, p)) : 0f;
            float splay = Pulse(time, fireTime, FireHopTime); // legs fling out on the firing hop
            float brace = Pulse(time, fireTime, 0.25f);
            float landing = hopping ? 0f : Bounce(time, landTime);
            float flail = Mathf.SmoothStep(0f, 1f, fall);
            float deadTwitch = dead ? Mathf.Sin(time * 25f) * Mathf.Lerp(35f, 0f, Mathf.Clamp01((time - deathStart) / 2.5f)) : 0f;

            // Hind legs: positive swings the foot back. Coil forward to crouch and while loading.
            for (int i = 0; i < hindLegs.Length; i++)
            {
                if (hindLegs[i] == null) continue;
                float angle = 95f * extend - 25f * Crouch - 18f * Load + 70f * splay
                              + Mathf.Sin(time * 16f + i * 1.7f) * 45f * flail
                              + Mathf.Sin(time * 50f + i) * 4f * Load
                              + deadTwitch * (i == 0 ? 1f : -1f);
                hindLegs[i].localRotation = hindRest[i] * Quaternion.Euler(angle, 0f, 0f);
            }
            // Front legs: negative reaches forward. Plant and push while loading, brace when the can kicks.
            for (int i = 0; i < frontLegs.Length; i++)
            {
                if (frontLegs[i] == null) continue;
                float spread = (i == 0 ? 1f : -1f) * (12f * landing + 15f * flail);
                float angle = front + 15f * Crouch - 28f * Load + 18f * brace - 45f * splay - 20f * landing
                              + Mathf.Sin(time * 19f + i * 2.3f) * 40f * flail
                              - deadTwitch;
                frontLegs[i].localRotation = frontRest[i] * Quaternion.Euler(angle, 0f, spread);
            }
        }

        private void AnimateThroatAndEyes(float time)
        {
            if (throat != null)
            {
                // Breathing pulse; balloons while loading; empties with a snap when it fires.
                float breathe = 1f + Mathf.Max(0f, Mathf.Sin(time * 3f)) * 0.3f;
                float swell = 1f + Load * 1.9f + Mathf.Sin(time * 20f) * 0.12f * Load;
                float fire = Mathf.Clamp01((time - fireTime) / 0.2f);
                float empty = fire < 1f ? Mathf.Lerp(0.6f, 1f, fire) : 1f;
                throat.localScale = throatScale * breathe * swell * empty;
            }

            // Blink every few seconds, and squeeze the eyes shut when hit.
            if (time >= nextBlink)
            {
                blinkStart = time;
                nextBlink = time + Random.Range(2f, 5f);
            }
            float lid = Mathf.Max(Pulse(time, blinkStart, 0.15f), Pulse(time, flinchStart, 0.25f));
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] == null) continue;
                Quaternion look = Quaternion.identity;
                if (Gooed)
                {
                    // Dizzy: eyes roll round in opposite directions.
                    float spin = (time - gooStart) * 14f * (i == 0 ? 1f : -1f);
                    look = Quaternion.Euler(Mathf.Sin(spin) * 35f, Mathf.Cos(spin) * 35f, 0f);
                }
                eyes[i].localRotation = eyeRest[i] * look;
                Vector3 scale = eyeScale * (1f + 0.45f * Load + 0.5f * Pulse(time, fireTime, 0.3f) + 0.35f * Pulse(time, gooStart, 0.4f));
                scale.y *= 1f - 0.85f * lid;
                eyes[i].localScale = scale;
            }
        }

        private void AnimateCan(float time)
        {
            if (canMount == null) return;
            // Tilts up to aim and rattles harder as it loads, kicks back when it fires, wobbles when struck.
            Quaternion aim = Quaternion.Euler(-24f * Load, 0f, 0f);
            Quaternion rattle = Quaternion.Euler(Random.Range(-1f, 1f) * 13f * Load, 0f, Random.Range(-1f, 1f) * 13f * Load);
            float clang = Mathf.Clamp01((time - clangTime) / 0.45f);
            float ring = clang < 1f ? Mathf.Sin(clang * Mathf.PI * 6f) * (1f - clang) * 28f : 0f;
            Quaternion wobble = Quaternion.Euler(ring, 0f, ring * 0.6f);
            float recoil = Pulse(time, fireTime, 0.3f);
            canMount.localRotation = canRest * aim * rattle * wobble * Quaternion.Euler(-40f * recoil, 0f, 0f);
            canMount.localPosition = canPosition + canMount.localRotation * Vector3.down * 0.13f * recoil;

            // Goo glowing in the can's mouth while loading.
            if (glowOrb != null)
            {
                glowOrbRenderer.enabled = Load > 0.05f && !dead;
                float pulse = 1f + 0.2f * Mathf.Sin(time * Mathf.Lerp(10f, 30f, Load));
                glowOrb.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.22f, Load) * pulse;
            }
        }

        // ---------- Goo drips ----------

        private void AnimateDrips(float time, float dt)
        {
            // Drip goo while stunned and for a moment after getting up.
            bool dripping = !dead && (Gooed || time - ungooTime < 0.6f);
            if (dripping && time >= nextDrip && gooMaterial != null)
            {
                nextDrip = time + Random.Range(0.08f, 0.2f);
                Vector3 from = visual.position + Vector3.up * Random.Range(0.15f, 0.45f) + Random.insideUnitSphere * 0.25f;
                SpawnDrip(from, Random.insideUnitSphere * 0.3f, Random.Range(0.035f, 0.065f));
            }

            float floor = transform.position.y + 0.02f;
            for (int i = drips.Count - 1; i >= 0; i--)
            {
                Drip drip = drips[i];
                if (drip.Body == null)
                {
                    drips.RemoveAt(i);
                    continue;
                }
                drip.Age += dt;
                drip.Velocity += Vector3.down * 9.8f * dt;
                drip.Body.position += drip.Velocity * dt;
                if (drip.Body.position.y > floor && drip.Age < 3f) continue;
                if (drip.Age < 3f) GooPuddle.Spawn(gooMaterial, drip.Body.position, drip.Body.localScale.x * 1.4f, 1.5f);
                Destroy(drip.Body.gameObject);
                drips.RemoveAt(i);
            }
        }

        private void SpawnDrip(Vector3 position, Vector3 velocity, float size)
        {
            if (gooMaterial == null) return;
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "Goo Drip";
            Collider collider = drop.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = drop.GetComponent<Renderer>();
            renderer.sharedMaterial = gooMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            drop.transform.position = position;
            drop.transform.localScale = Vector3.one * size;
            drips.Add(new Drip { Body = drop.transform, Velocity = velocity });
        }

        private void ClearDrips()
        {
            foreach (Drip drip in drips) if (drip.Body != null) Destroy(drip.Body.gameObject);
            drips.Clear();
        }

        private void ApplyColors(float time)
        {
            bool flash = time < flashUntil;
            foreach (Renderer item in renderers)
            {
                if (item == null) continue;
                if (flash)
                {
                    item.GetPropertyBlock(block);
                    block.SetColor(BaseColor, Color.white);
                    item.SetPropertyBlock(block);
                }
                else if (Gooed && item.sharedMaterial != null && item.sharedMaterial.HasProperty(BaseColor))
                {
                    // Covered in its own goo.
                    item.GetPropertyBlock(block);
                    Color own = item.sharedMaterial.GetColor(BaseColor);
                    block.SetColor(BaseColor, Color.Lerp(own, gooColor * 0.8f, 0.7f));
                    item.SetPropertyBlock(block);
                }
                else
                {
                    item.SetPropertyBlock(null);
                }
            }
        }
    }
}
