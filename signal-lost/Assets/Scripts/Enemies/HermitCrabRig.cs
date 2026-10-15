using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// The hermit crab's body and its procedural animation: scuttling legs, eye stalks, claws, hiding in its
    /// shell, flinching and flipping over when it dies. It also mounts the shell, which can be any model.
    ///
    /// Shell prefabs follow one convention: the prefab's origin is where it attaches to the crab, +Z points
    /// the way the crab faces (out of the shell's opening) and +Y is up. Arrange any model inside its prefab
    /// to match, then drop it into Shell Prefab. Shell colliders are removed; the crab's own collider is used.
    ///
    /// HermitCrab drives this component by setting its public fields each frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HermitCrabRig : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Shell")]
        [Tooltip("Model the crab wears as its shell. Origin = attach point, +Z = facing direction, +Y = up.")]
        [SerializeField] private GameObject shellPrefab;
        [Tooltip("Extra adjustment for this crab's shell, on top of the shell prefab's own layout.")]
        [SerializeField] private Vector3 shellOffset;
        [SerializeField] private Vector3 shellRotation;
        [SerializeField, Min(0.01f)] private float shellScale = 1f;

        [Header("Shell hide (bucket flips face down over the crab)")]
        [Tooltip("Where the shell's origin (the centre of its opening) sits when flipped over the crab, relative to the " +
                 "Shell Socket. The default puts the bucket's mouth on the ground over the tucked-in crab.")]
        [SerializeField] private Vector3 hideShellPosition = new Vector3(0f, -0.41f, -0.13f);
        [Tooltip("Shell rotation when flipped over the crab, relative to the Shell Socket. 105 stands the bucket mouth-down.")]
        [SerializeField] private Vector3 hideShellRotation = new Vector3(105f, 0f, 0f);
        [Tooltip("Seconds for the shell to flip down over the crab, and to lift back off.")]
        [SerializeField, Min(0.05f)] private float shellFlipTime = 0.3f;

        [Header("Body parts (set by the prefab builder)")]
        [Tooltip("Everything visible. Rotated for sideways scuttling and flipped over on death.")]
        [SerializeField] private Transform visual;
        [SerializeField] private Transform body;
        [SerializeField] private Transform shellSocket;
        [SerializeField] private Transform[] eyeStalks = new Transform[0];
        [Tooltip("The big claw; it does the attacking.")]
        [SerializeField] private Transform bigClaw;
        [SerializeField] private Transform smallClaw;
        [Tooltip("The moving finger of each claw, opened and snapped shut.")]
        [SerializeField] private Transform bigPincer;
        [SerializeField] private Transform smallPincer;
        [Tooltip("Leg pivots at the hips. Legs on the crab's left have a negative local X position.")]
        [SerializeField] private Transform[] legs = new Transform[0];

        // ---------- Set by HermitCrab every frame ----------
        /// <summary>Ground speed in metres per second, for the scuttle.</summary>
        [System.NonSerialized] public float MoveSpeed;
        /// <summary>True to scuttle sideways, like a real crab, instead of walking face first.</summary>
        [System.NonSerialized] public bool Sideways;
        /// <summary>Raising the claw before an attack, 0 to 1. Above 0 means the attack is telegraphed.</summary>
        [System.NonSerialized] public float Windup;
        /// <summary>The claw slamming down, 0 to 1.</summary>
        [System.NonSerialized] public float Strike;
        /// <summary>True while the crab hides in its shell (staggered by a heavy hit, or hiding from a charged shot).</summary>
        [System.NonSerialized] public bool Hidden;
        /// <summary>True to hide under the shell: the crab tucks in and the shell flips face down over it.</summary>
        [System.NonSerialized] public bool ShellDown;
        /// <summary>True while the claw is stuck in the ground after a missed slam: it wiggles, trying to pull free.</summary>
        [System.NonSerialized] public bool ClawStuck;

        [Header("Attack warning")]
        [Tooltip("Colour the big claw heats up to while winding up an attack.")]
        [SerializeField] private Color clawGlowColor = new Color(1f, 0.85f, 0.35f);

        private GameObject shellInstance;
        private Renderer[] renderers = new Renderer[0];
        private MaterialPropertyBlock block;

        private Vector3 visualPosition, bodyPosition;
        private Quaternion[] legRest, eyeRest;
        private Quaternion bigClawRest, smallClawRest, bigPincerRest, smallPincerRest, socketRest;
        private Vector3 eyeScale = Vector3.one;

        private float walkPhase, walkWeight, sideAngle, hideAmount, legTuck, eyeTuck;
        private float shellFlip, extraTuck; // 0 = shell on its back, 1 = face down over the crab
        private bool shellLanded, concealed;
        private readonly System.Collections.Generic.List<Renderer> bodyRenderers = new System.Collections.Generic.List<Renderer>();
        private Material sparkMaterial, glowMaterial, dirtMaterial;

        // Stuck claw: when it got stuck, when it yanked free, and the next dirt kick on a tug.
        private bool wasStuck;
        private float stuckStart = float.NegativeInfinity, yankStart = float.NegativeInfinity, nextTugDirt;
        private const float TugRate = 7f; // radians per second of the tug cycle (one tug every ~0.9 s)
        private const float StumbleTime = 0.6f;

        private struct Chunk
        {
            public Transform Body;
            public Vector3 Velocity, Spin;
            public float Born, Life, Size;
        }
        private readonly System.Collections.Generic.List<Chunk> chunks = new System.Collections.Generic.List<Chunk>();
        private readonly System.Collections.Generic.Dictionary<Renderer, Color> clawColors =
            new System.Collections.Generic.Dictionary<Renderer, Color>();
        private Transform glowOrb;
        private Renderer glowOrbRenderer;
        private float flinchStart = float.NegativeInfinity, flinchStrength, flashUntil;
        private float alertStart = float.NegativeInfinity;
        private float nextSnap, snapStart = float.NegativeInfinity;
        private float deathStart = float.NegativeInfinity;
        private bool dead;

        public GameObject Shell => shellInstance;
        /// <summary>True once the flipped shell actually covers the crab (bolts should bounce off).</summary>
        public bool IsShellCovering => shellFlip >= 0.7f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (visual == null) visual = transform;
            visualPosition = visual.localPosition;
            if (body != null) bodyPosition = body.localPosition;
            if (shellSocket != null) socketRest = shellSocket.localRotation;

            legRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;
            eyeRest = new Quaternion[eyeStalks.Length];
            for (int i = 0; i < eyeStalks.Length; i++) eyeRest[i] = eyeStalks[i] != null ? eyeStalks[i].localRotation : Quaternion.identity;
            if (eyeStalks.Length > 0 && eyeStalks[0] != null) eyeScale = eyeStalks[0].localScale;
            if (bigClaw != null) bigClawRest = bigClaw.localRotation;
            if (smallClaw != null) smallClawRest = smallClaw.localRotation;
            if (bigPincer != null) bigPincerRest = bigPincer.localRotation;
            if (smallPincer != null) smallPincerRest = smallPincer.localRotation;

            SetShell(shellPrefab);
            nextSnap = Time.time + Random.Range(1.5f, 4f);
            BuildClawGlow();
        }

        /// <summary>Swaps the crab's shell for another model, or removes it when given null.</summary>
        public void SetShell(GameObject prefab)
        {
            shellPrefab = prefab;
            if (shellInstance != null) Destroy(shellInstance);
            shellInstance = null;

            if (prefab != null && shellSocket != null)
            {
                shellInstance = Instantiate(prefab, shellSocket, false);
                shellInstance.name = prefab.name + " (shell)";
                Transform shell = shellInstance.transform;
                shell.localPosition = shellOffset;
                shell.localRotation = Quaternion.Euler(shellRotation);
                shell.localScale *= shellScale;
                foreach (Collider collider in shellInstance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    Destroy(collider);
                }
            }
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            // The crab's own parts (not the shell), hidden while it is under a face-down shell.
            bodyRenderers.Clear();
            foreach (Renderer item in renderers)
                if (shellInstance == null || !item.transform.IsChildOf(shellInstance.transform)) bodyRenderers.Add(item);
        }

        /// <summary>Quick shell wobble and white flash. Heavy hits wobble harder.</summary>
        public void Flinch(bool heavy)
        {
            flinchStart = Time.time;
            flinchStrength = heavy ? 1f : 0.45f;
            flashUntil = Time.time + 0.07f;
        }

        /// <summary>
        /// A bolt bouncing off the shell: a hard wobble and a spark on the side of the shell facing
        /// <paramref name="towardShooter"/>, but no white flash on the crab, which is safe inside.
        /// </summary>
        public void Clang(Vector3 towardShooter)
        {
            flinchStart = Time.time;
            flinchStrength = 1.2f;
            Transform shell = shellInstance != null ? shellInstance.transform : shellSocket;
            if (shell == null) return;
            if (sparkMaterial == null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null) return;
                sparkMaterial = new Material(unlit) { name = "Shell Spark (runtime)" };
                sparkMaterial.SetColor(BaseColor, new Color(1f, 0.95f, 0.75f));
            }
            towardShooter.y = 0f;
            Vector3 centre = shell.position + shell.rotation * new Vector3(0f, -0.05f, -0.3f); // roughly the middle of the bucket
            Vector3 point = centre + (towardShooter.sqrMagnitude > 0.001f ? towardShooter.normalized : Vector3.back) * 0.38f;
            ImpactFlash.Spawn(sparkMaterial, point, 0.22f, 0.1f);
        }

        private void OnDestroy()
        {
            if (sparkMaterial != null) Destroy(sparkMaterial);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (dirtMaterial != null) Destroy(dirtMaterial);
            foreach (Chunk chunk in chunks) if (chunk.Body != null) Destroy(chunk.Body.gameObject);
        }

        /// <summary>A startled hop with eyes popping up, when the crab first notices the player.</summary>
        public void Alert() => alertStart = Time.time;

        public void PlayDeath()
        {
            dead = true;
            deathStart = Time.time;
            flashUntil = Time.time + 0.1f;
        }

        /// <summary>Back to a living, standing pose (used when a test crab respawns).</summary>
        public void ResetPose()
        {
            dead = false;
            deathStart = float.NegativeInfinity;
            Hidden = false;
            hideAmount = legTuck = eyeTuck = 0f;
            shellFlip = extraTuck = 0f;
            ShellDown = shellLanded = concealed = false;
            Windup = Strike = 0f;
            SetVisible(true);
        }

        public void SetVisible(bool show)
        {
            foreach (Renderer item in renderers)
                if (item != null) item.enabled = show;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time;

            // Scuttle: legs cycle with ground speed.
            walkWeight = Mathf.Lerp(walkWeight, Mathf.Clamp01(MoveSpeed / 2.5f), 1f - Mathf.Exp(-10f * dt));
            float deathAge = time - deathStart;
            bool stumbleLegs = time - yankStart < StumbleTime;
            float legRate = dead ? Mathf.Lerp(30f, 0f, Mathf.Clamp01(deathAge / 2.5f)) : ClawStuck ? 24f : stumbleLegs ? 28f : 6f + MoveSpeed * 4f;
            walkPhase += dt * legRate;

            sideAngle = Mathf.Lerp(sideAngle, Sideways && !dead ? -70f : 0f, 1f - Mathf.Exp(-6f * dt));
            // Hiding tucks the body, claws, legs and eyes into the shell. Under a flipped shell they tuck in further,
            // and stay tucked until the shell has lifted mostly clear, so nothing pokes through it.
            shellFlip = Mathf.MoveTowards(shellFlip, ShellDown && !dead ? 1f : 0f, dt / shellFlipTime);
            bool underShell = (ShellDown || shellFlip > 0.35f) && !dead;
            extraTuck = Mathf.MoveTowards(extraTuck, underShell ? 1f : 0f, dt / 0.1f);
            float tucked = (Hidden || underShell) && !dead ? 1f : 0f;
            hideAmount = Mathf.MoveTowards(hideAmount, tucked, dt / 0.12f);
            legTuck = Mathf.MoveTowards(legTuck, tucked, dt / 0.12f);
            eyeTuck = Mathf.MoveTowards(eyeTuck, tucked, dt / 0.12f);

            // Once the shell has landed over it, the crab is out of sight: switch its parts off so no claw or leg can
            // poke through the shell, whatever shape the shell is. Switched back on as the shell lifts.
            bool hide = shellFlip >= 0.85f && !dead;
            if (hide != concealed)
            {
                concealed = hide;
                foreach (Renderer item in bodyRenderers)
                    if (item != null) item.enabled = !hide;
            }

            UpdateStuckEvents(time);
            UpdateChunks(dt);

            AnimateVisual(time, deathAge);
            AnimateLegs();
            AnimateEyes(time);
            AnimateClaws(time);
            AnimateShell(time);
            ApplyFlash(time);
        }

        private void AnimateVisual(float time, float deathAge)
        {
            Vector3 position = visualPosition;
            Quaternion rotation = Quaternion.Euler(0f, sideAngle, 0f);

            // Bob while walking, hop when startled, lean back while winding up, lunge into a strike.
            position.y += Mathf.Abs(Mathf.Sin(walkPhase)) * 0.025f * walkWeight;
            float alert = Mathf.Clamp01((time - alertStart) / 0.35f);
            if (alert < 1f) position.y += Mathf.Sin(alert * Mathf.PI) * 0.18f;
            rotation *= Quaternion.Euler(-10f * Windup + 12f * Strike, 0f, 0f);
            if (ClawStuck)
            {
                // Heaving on the buried claw: lurch forward, then throw its weight back on each tug, trembling with
                // effort and twisting side to side. The claw is counter-rotated so it stays planted in the ground.
                float tug = Tug(time);
                rotation *= Quaternion.Euler(8f - 18f * tug, Mathf.Sin(time * 5f) * 6f, Mathf.Sin(time * 13f) * 5f);
                position += Random.insideUnitSphere * (0.008f + 0.01f * tug);
            }
            // Yanked free: the pull throws it backwards (HermitCrab moves it ~0.8 m). It rears up, wobbles side to
            // side as it backpedals, and bounces as it lands, settling over 0.6 s.
            float yank = Mathf.Clamp01((time - yankStart) / StumbleTime);
            if (yank < 1f)
            {
                float rear = Mathf.Sin(yank * Mathf.PI);
                float settle = 1f - yank;
                rotation *= Quaternion.Euler(-24f * rear, Mathf.Sin(yank * Mathf.PI * 2f) * 10f * settle,
                    Mathf.Sin(yank * Mathf.PI * 4f) * 12f * settle);
                float hop = Mathf.Abs(Mathf.Sin(yank * Mathf.PI * 2f)) * 0.09f * settle; // two little bounces
                position += new Vector3(0f, hop, 0f);
            }
            position += rotation * new Vector3(0f, 0f, 0.12f * Strike - 0.05f * Windup);
            if (Windup > 0.6f) position += Random.insideUnitSphere * 0.006f; // trembling with effort

            if (dead)
            {
                // Hop, flip onto its back, wiggle, then sink away.
                float flip = Mathf.Clamp01(deathAge / 0.45f);
                position.y += Mathf.Sin(flip * Mathf.PI) * 0.4f + flip * 0.72f; // upside down it needs lifting to rest on the ground
                rotation *= Quaternion.Euler(0f, 0f, 180f * Mathf.SmoothStep(0f, 1f, flip));
                float sink = Mathf.Clamp01((deathAge - 2.5f) / 1f);
                position.y -= sink * 1.1f;
            }

            visual.localPosition = position;
            visual.localRotation = rotation;

            if (body != null) body.localPosition = bodyPosition + new Vector3(0f, -0.04f, -0.2f) * hideAmount + new Vector3(0f, -0.06f, 0f) * extraTuck;
        }

        private void AnimateLegs()
        {
            for (int i = 0; i < legs.Length; i++)
            {
                Transform leg = legs[i];
                if (leg == null) continue;
                bool left = leg.localPosition.x < 0f;
                float offset = i * 2.1f + (left ? Mathf.PI : 0f);
                // Stuck: legs scrabble fast and wide, digging in for grip.
                bool stumbling = Time.time - yankStart < StumbleTime;
                float stride = ClawStuck ? 1.4f : stumbling ? 1.3f : walkWeight;
                float swing = Mathf.Sin(walkPhase + offset) * 22f * stride;
                float lift = Mathf.Max(0f, Mathf.Cos(walkPhase + offset)) * 20f * stride;
                if (dead) { swing = Mathf.Sin(walkPhase * 1.3f + offset) * 35f; lift = 30f; }
                lift += 55f * legTuck; // tuck up into the shell
                swing -= 25f * legTuck * (left ? -1f : 1f);
                leg.localRotation = legRest[i]
                    * Quaternion.AngleAxis(swing, Vector3.up)
                    * Quaternion.AngleAxis(lift, Vector3.forward);
                leg.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, legTuck) * Mathf.Lerp(1f, 0.6f, extraTuck);
            }
        }

        private void AnimateEyes(float time)
        {
            float alert = Mathf.Clamp01((time - alertStart) / 0.5f);
            float pop = (alert < 1f ? Mathf.Sin(alert * Mathf.PI) * 0.35f : 0f) + Windup * 0.25f;
            if (ClawStuck) pop = 0.3f + 0.2f * Tug(time); // bulging with effort
            for (int i = 0; i < eyeStalks.Length; i++)
            {
                Transform eye = eyeStalks[i];
                if (eye == null) continue;
                float wobbleX = Mathf.Sin(time * 2.3f + i * 1.7f) * 8f;
                float wobbleZ = Mathf.Sin(time * 1.7f + i * 2.9f) * 10f;
                if (dead) { wobbleX *= 3f; wobbleZ *= 3f; }
                if (ClawStuck) { wobbleX = Mathf.Sin(time * 11f + i * 1.7f) * 22f; wobbleZ = Mathf.Sin(time * 8f + i * 2.9f) * 25f; }
                if (time - yankStart < StumbleTime)
                {
                    // Dizzy: eyes swing round in circles, in opposite directions.
                    float spin = (time - yankStart) * 25f * (i == 0 ? 1f : -1f);
                    wobbleX = Mathf.Sin(spin) * 30f;
                    wobbleZ = Mathf.Cos(spin) * 30f;
                }
                eye.localRotation = eyeRest[i] * Quaternion.Euler(wobbleX - 20f * Windup, 0f, wobbleZ);
                eye.localScale = eyeScale * (1f + pop) * Mathf.Lerp(1f, 0.15f, eyeTuck);
            }
        }

        private void AnimateClaws(float time)
        {
            // Idle: every few seconds the big claw gives a little snap.
            if (time >= nextSnap && Windup <= 0f && Strike <= 0f && !dead)
            {
                snapStart = time;
                nextSnap = time + Random.Range(2f, 5f);
            }
            float snap = Mathf.Clamp01((time - snapStart) / 0.3f);
            float idleOpen = snap < 1f ? Mathf.Sin(snap * Mathf.PI) * 30f : 0f;

            // Attack: raise the big claw and open it wide, then slam it down shut.
            float raise = -75f * EaseOut(Windup) + 100f * Strike;
            float open = Mathf.Max(idleOpen, 40f * Windup * (1f - Strike));
            float smallRaise = raise * 0.4f;
            if (ClawStuck)
            {
                // Buried about 18 cm deep (42 degrees down). Counter the body's lean so the tip stays in the ground,
                // and shudder as it strains. The small claw hammers the ground in frustration.
                float tug = Tug(time);
                raise = 42f + 18f * tug + Mathf.Sin(time * 31f) * 2f;
                open = 0f;
                smallRaise = -35f + Mathf.Max(0f, Mathf.Sin(time * 9f)) * 70f;
            }
            float tuck = 50f * hideAmount;
            float deadFlail = dead ? Mathf.Sin(walkPhase) * 25f : 0f;

            if (bigClaw != null)
            {
                bigClaw.localRotation = bigClawRest * Quaternion.Euler(raise + tuck + deadFlail, -15f * Strike, 0f);
                bigClaw.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, hideAmount) * Mathf.Lerp(1f, 0.55f, extraTuck);
            }
            if (smallClaw != null)
            {
                smallClaw.localRotation = smallClawRest * Quaternion.Euler(smallRaise + tuck - deadFlail, 0f, 0f);
                smallClaw.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, hideAmount) * Mathf.Lerp(1f, 0.55f, extraTuck);
            }
            if (bigPincer != null) bigPincer.localRotation = bigPincerRest * Quaternion.Euler(-open, 0f, 0f);
            if (smallPincer != null) smallPincer.localRotation = smallPincerRest * Quaternion.Euler(-open * 0.6f, 0f, 0f);
        }

        private void AnimateShell(float time)
        {
            if (shellSocket == null) return;

            // Landing face down: a thump wobble the moment it hits the ground.
            if (shellFlip >= 1f && !shellLanded)
            {
                shellLanded = true;
                flinchStart = time;
                flinchStrength = 0.7f;
            }
            else if (shellFlip < 1f) shellLanded = false;

            float age = time - flinchStart;
            float wobble = age < 0.4f ? Mathf.Sin(age * 45f) * Mathf.Exp(-age * 9f) * 14f * flinchStrength : 0f;
            // Less wobble when face down, so the rim stays on the ground.
            float wobbleScale = Mathf.Lerp(1f, 0.35f, shellFlip);
            shellSocket.localRotation = socketRest * Quaternion.Euler(wobble * 0.4f * wobbleScale, 0f, wobble * wobbleScale);

            // Flip the shell between riding on the crab's back and face down over it. It swings up and over in an
            // arc so it never cuts through the crab or the ground, speeding up as it falls.
            if (shellInstance == null) return;
            float t = shellFlip;
            float ease = ShellDown ? t * t : Mathf.SmoothStep(0f, 1f, t); // falls faster and faster; lifts smoothly
            Transform shell = shellInstance.transform;
            shell.localPosition = Vector3.Lerp(shellOffset, hideShellPosition, ease) + Vector3.up * Mathf.Sin(ease * Mathf.PI) * 0.25f;
            shell.localRotation = Quaternion.Slerp(Quaternion.Euler(shellRotation), Quaternion.Euler(hideShellRotation), ease);
        }

        private void ApplyFlash(float time)
        {
            bool flash = time < flashUntil;
            float glow = ClawGlow(time);
            foreach (Renderer item in renderers)
            {
                if (item == null) continue;
                if (flash)
                {
                    item.GetPropertyBlock(block);
                    block.SetColor(BaseColor, Color.white);
                    item.SetPropertyBlock(block);
                }
                else if (glow > 0f && clawColors.TryGetValue(item, out Color baseColor))
                {
                    item.GetPropertyBlock(block);
                    block.SetColor(BaseColor, Color.Lerp(baseColor, clawGlowColor, glow));
                    item.SetPropertyBlock(block);
                }
                else
                {
                    item.SetPropertyBlock(null);
                }
            }

            // A spark between the pincers that swells and pulses faster as the strike gets close.
            if (glowOrb != null)
            {
                glowOrbRenderer.enabled = glow > 0.05f;
                float pulse = 1f + 0.25f * Mathf.Sin(time * Mathf.Lerp(12f, 40f, Windup));
                glowOrb.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.11f, glow) * pulse;
            }
        }

        /// <summary>0 to 1: how hot the big claw glows. Builds through the wind-up and goes out when it strikes.</summary>
        private float ClawGlow(float time)
        {
            // Only while winding up: the glow goes out as soon as the claw comes down, and stays out while it recovers.
            if (dead || Windup <= 0f || Strike > 0.001f) return 0f;
            float heat = Mathf.Clamp01(Windup);
            float flicker = 0.85f + 0.15f * Mathf.Sin(time * Mathf.Lerp(10f, 35f, Windup));
            return Mathf.Clamp01(heat * flicker);
        }

        /// <summary>Remembers the big claw's colours and adds the warning spark between its pincers.</summary>
        private void BuildClawGlow()
        {
            if (bigClaw == null) return;
            foreach (Renderer item in bigClaw.GetComponentsInChildren<Renderer>(true))
            {
                Material material = item.sharedMaterial;
                clawColors[item] = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return;
            glowMaterial = new Material(unlit) { name = "Claw Glow (runtime)" };
            glowMaterial.SetColor(BaseColor, Color.Lerp(clawGlowColor, Color.white, 0.4f));
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Claw Glow";
            Collider collider = orb.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            glowOrb = orb.transform;
            glowOrb.SetParent(bigClaw, false);
            glowOrb.localPosition = new Vector3(0.065f, -0.045f, 0.36f); // between the two fingers
            glowOrbRenderer = orb.GetComponent<Renderer>();
            glowOrbRenderer.sharedMaterial = glowMaterial;
            glowOrbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowOrbRenderer.enabled = false;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // ---------- Stuck claw ----------

        /// <summary>0 to 1: the tug cycle while stuck, peaking each time it heaves back on the claw.</summary>
        private float Tug(float time) => Mathf.Max(0f, Mathf.Sin((time - stuckStart) * TugRate));

        /// <summary>Dirt bursts when the claw sinks in, on each tug, and when it rips free.</summary>
        private void UpdateStuckEvents(float time)
        {
            if (ClawStuck && !wasStuck)
            {
                stuckStart = time;
                nextTugDirt = stuckStart + (Mathf.PI * 0.5f) / TugRate; // first tug peak
                SpawnDirt(ClawTip(), 12, 2.2f, 0.35f);
                flinchStart = time;
                flinchStrength = 0.8f; // the shell rattles from the impact
            }
            else if (!ClawStuck && wasStuck && !dead)
            {
                yankStart = time;
                SpawnDirt(ClawTip(), 10, 3f, 0.45f);
            }
            else if (ClawStuck && time >= nextTugDirt)
            {
                SpawnDirt(ClawTip(), 3, 1.4f, 0.2f);
                nextTugDirt += Mathf.PI * 2f / TugRate;
            }
            wasStuck = ClawStuck;
        }

        /// <summary>World position of the big claw's fixed fingertip, clamped to the ground.</summary>
        private Vector3 ClawTip()
        {
            Vector3 tip = bigClaw != null ? bigClaw.TransformPoint(new Vector3(0.05f, -0.075f, 0.43f)) : transform.position;
            tip.y = Mathf.Max(tip.y, transform.position.y + 0.02f);
            return tip;
        }

        /// <summary>Kicks up low-poly dirt chunks that arc, tumble, bounce once and shrink away.</summary>
        private void SpawnDirt(Vector3 at, int count, float speed, float upward)
        {
            if (dirtMaterial == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) return;
                dirtMaterial = new Material(lit) { name = "Claw Dirt (runtime)" };
                dirtMaterial.SetColor(BaseColor, new Color(0.4f, 0.32f, 0.24f));
            }
            for (int i = 0; i < count; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Dirt";
                Collider collider = piece.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var renderer = piece.GetComponent<Renderer>();
                renderer.sharedMaterial = dirtMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float size = Random.Range(0.025f, 0.06f);
                piece.transform.SetPositionAndRotation(at + Random.insideUnitSphere * 0.05f, Random.rotation);
                piece.transform.localScale = Vector3.one * size;
                Vector3 outward = Random.insideUnitSphere;
                outward.y = Mathf.Abs(outward.y) + upward * 3f;
                chunks.Add(new Chunk
                {
                    Body = piece.transform,
                    Velocity = outward.normalized * speed * Random.Range(0.6f, 1.2f),
                    Spin = Random.insideUnitSphere * 720f,
                    Born = Time.time,
                    Life = Random.Range(0.5f, 0.9f),
                    Size = size,
                });
            }
        }

        private void UpdateChunks(float dt)
        {
            float ground = transform.position.y;
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                Chunk chunk = chunks[i];
                float age = Time.time - chunk.Born;
                if (chunk.Body == null || age >= chunk.Life)
                {
                    if (chunk.Body != null) Destroy(chunk.Body.gameObject);
                    chunks.RemoveAt(i);
                    continue;
                }
                chunk.Velocity.y -= 9.8f * dt;
                Vector3 position = chunk.Body.position + chunk.Velocity * dt;
                if (position.y < ground + chunk.Size * 0.5f)
                {
                    position.y = ground + chunk.Size * 0.5f;
                    chunk.Velocity = new Vector3(chunk.Velocity.x * 0.4f, -chunk.Velocity.y * 0.3f, chunk.Velocity.z * 0.4f);
                    chunk.Spin *= 0.4f;
                }
                chunk.Body.position = position;
                chunk.Body.rotation *= Quaternion.Euler(chunk.Spin * dt);
                float fade = Mathf.Clamp01((chunk.Life - age) / 0.25f); // shrink away at the end
                chunk.Body.localScale = Vector3.one * chunk.Size * fade;
                chunks[i] = chunk;
            }
        }

#if UNITY_EDITOR
        // The shell is attached when the game starts. Outside Play mode, draw it in the Scene view so crabs
        // can be placed and shells checked without pressing Play.
        private void OnDrawGizmos()
        {
            if (Application.isPlaying || shellPrefab == null || shellSocket == null) return;
            Matrix4x4 mount = shellSocket.localToWorldMatrix
                * Matrix4x4.TRS(shellOffset, Quaternion.Euler(shellRotation), Vector3.one * shellScale);
            Matrix4x4 toShell = shellPrefab.transform.worldToLocalMatrix;
            Gizmos.color = new Color(0.7f, 0.72f, 0.76f, 1f);
            foreach (MeshFilter part in shellPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null) continue;
                Gizmos.matrix = mount * toShell * part.transform.localToWorldMatrix;
                Gizmos.DrawMesh(part.sharedMesh);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
