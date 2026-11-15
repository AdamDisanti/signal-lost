using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Finale;
using SignalLost.Inventory;
using SignalLost.Melee;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch Play-mode check of the melee update (docs/melee-rage.md) in the melee arena: the loadout, Q cycling to the
// fists, punches and each knuckle tier (lunge; marks, chain lightning and the magnet pull; finishers), rage stacks and
// the damage they add, the Moon Shoes' double jump, the robot mini-bosses and the horde. Copy into an Editor folder of a
// disposable project copy:
//   Unity -batchmode -projectPath <temp-project> -executeMethod MeleeSmoke.Start -logFile <log>
// Omit -quit. Add -finaleRender (without -nographics) to save captures to Logs/Melee_*.png. Not a physical-input test:
// the moves are started through their methods, the way the buttons start them.
[InitializeOnLoad]
public static class MeleeSmoke
{
    private const string Key = "MeleeSmoke";
    private const string Scene = "Assets/Scenes/Initial Test Scenes/MeleeArena.unity";
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static readonly List<string> errors = new List<string>();
    private static double resume;
    private static bool started;

    private static PlayerLife life;
    private static PlayerFists fists;
    private static FirstPersonController movement;
    private static Camera view;

    static MeleeSmoke() { EditorApplication.update += Tick; }

    public static void Start()
    {
        EditorSceneManager.OpenScene(Scene);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Time", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 360) { Fail(new Exception("Timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < resume) return;
        try
        {
            if (!started) { started = true; steps.Push(Run()); }
            while (steps.Count > 0)
            {
                IEnumerator top = steps.Peek();
                if (!top.MoveNext()) { steps.Pop(); continue; }
                if (top.Current is IEnumerator nested) { steps.Push(nested); continue; }
                resume = EditorApplication.timeSinceStartup + (top.Current is float seconds ? seconds : 0);
                return;
            }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/MeleeSmoke.txt", report);
            Debug.Log("MELEE_SMOKE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if ((type == LogType.Exception || type == LogType.Error) && !(message + stack).Contains("SearchDatabase")) errors.Add(message + "\n" + stack);
        };
        yield return 3f;
        life = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
        var gear = life.GetComponent<PlayerGear>();
        var pockets = life.GetComponent<PlayerInventory>();
        fists = life.GetComponent<PlayerFists>();
        movement = life.GetComponent<FirstPersonController>();
        var rage = life.GetComponent<RageMeter>();
        var arena = UnityEngine.Object.FindAnyObjectByType<MeleeArena>();
        view = life.GetComponentInChildren<Camera>();
        arena.Paused = true;
        arena.ClearArena();
        life.Health.Invulnerable = true;

        // ---------- The loadout ----------
        var loadout = life.GetComponent<MeleeLoadout>();
        Check(loadout != null && fists != null && rage != null, "The playtest loadout switches melee on here: fists, a rage meter (added at runtime).");
        Check(loadout.KnuckleTier == 3 && loadout.ShoeTier == 3 && !pockets.Upgrades.Any(id => id.Contains("knuckle") || id.Contains("shoe")),
            "It hands out Gaussian Brass Knuckles III and Moon Shoes III directly (not shop upgrades).");
        Check(fists.KnuckleTier == 3 && Near(fists.DamageMultiplier, 1.75f) && Near(fists.SpeedBonus, 1.16f),
            $"Knuckles III: tier {fists.KnuckleTier}, punches x{fists.DamageMultiplier:0.00}, speed x{fists.SpeedBonus:0.00} with fists up.");
        Check(movement.DoubleJump && Near(movement.JumpHeightScale, 1.55f) && Near(movement.SpeedBonus, 1.22f),
            $"Moon Shoes III: double jump, jump x{movement.JumpHeightScale:0.00}, run x{movement.SpeedBonus:0.00}.");

        // ---------- Q: laser -> scanner -> fists -> laser ----------
        Check(gear.InHand == PlayerGear.Held.Laser, "The laser is out to start with.");
        gear.Scanner.SetRaised(true); // Q from the laser: the scanner raises itself
        yield return null;
        Check(gear.InHand == PlayerGear.Held.Scanner, "Q raises the scanner.");
        Check(gear.Scanner.SwapPressed(), "Q with the scanner up is taken by the gear...");
        yield return null;
        Check(gear.InHand == PlayerGear.Held.Fists && fists.enabled && !gear.Laser.enabled && !gear.Scanner.IsRaised, "...and puts the fists up.");
        Check(Near(movement.SpeedBonus, 1.22f * 1.16f), $"With the knuckles up the player runs faster (x{movement.SpeedBonus:0.00}).");
        Check(gear.Scanner.SwapPressed(), "Q with the fists up...");
        yield return null;
        Check(gear.InHand == PlayerGear.Held.Laser && gear.Laser.enabled && !fists.enabled && Near(movement.SpeedBonus, 1.22f), "...goes back to the laser.");
        gear.SetFistsOut(true);
        yield return 0.3f;
        Check(fists.GetComponent<FirstPersonHands>().RightHand.GetComponentsInChildren<Renderer>().Any(r => r.name == "Gauss Coil" && r.gameObject.activeInHierarchy),
            "The knuckles are on both gloves while the fists are up.");
        yield return Capture("Guard");

        // ---------- Bare fists ----------
        arena.SetKnuckleLimit(0);
        rage.ResetStacks();
        Check(fists.KnuckleTier == 0 && Near(fists.DamageMultiplier, 1f), "Testing key 7 can take the knuckles off (bare fists).");
        Health target = Dummy(2.3f);
        yield return 0.2f;
        float hp = target.Current;
        yield return PunchAndWait();
        Check(Near(hp - target.Current, 9f), $"A bare jab does 9 ({hp - target.Current:0.#}).");
        Check(!OverchargeMark.Has(target), "Bare fists leave no overcharge mark.");
        UnityEngine.Object.Destroy(target.gameObject);

        // ---------- Knuckles I: the lunge ----------
        arena.SetKnuckleLimit(1);
        yield return 1f; // the combo window closes: the next punch is a jab again
        target = Dummy(9f);
        yield return 0.2f;
        hp = target.Current;
        Vector3 from = life.transform.position;
        yield return PunchAndWait();
        float moved = Vector3.Distance(from, life.transform.position);
        Check(moved > 4.5f, $"Knuckles I: punching at an enemy 9 m away flings the player onto it ({moved:0.0} m).");
        Check(Near(hp - target.Current, 9f * 1.35f), $"...and the jab lands, 35% harder ({hp - target.Current:0.##}).");
        Check(!OverchargeMark.Has(target), "Tier I leaves no mark.");
        UnityEngine.Object.Destroy(target.gameObject);

        // ---------- Knuckles II: marks, chain lightning, the magnet pull ----------
        arena.SetKnuckleLimit(2);
        yield return 1f; // let the combo window close so the next punch is a jab
        Health a = Spawn("HermitCrab", 1.8f);
        Health b = Spawn("HermitCrab", 4.5f, 2.5f);
        Health c = Spawn("HermitCrab", 4.5f, -2.5f);
        yield return 0.3f;
        Hold(a, b, c);
        yield return PunchAndWait();
        Check(OverchargeMark.Has(a), "Knuckles II: a punch leaves an overcharge mark.");
        float hb = b.Current, hc = c.Current;
        Punch();
        yield return 0.25f;
        yield return Capture("ChainLightning");
        yield return Settle();
        Check(!OverchargeMark.Has(a) && (b.Current < hb || b.IsDead) && (c.Current < hc || c.IsDead),
            $"Punching the marked enemy uses the mark and chain lightning hits the two beside it ({hb - b.Current:0}, {hc - c.Current:0}).");
        foreach (Health h in new[] { a, b, c }) Kill(h);

        rage.ResetStacks();
        Health far = Spawn("HermitCrab", 12f);
        yield return 0.3f;
        Hold(far);
        Invoke(fists, "TryPull");
        yield return 0.1f;
        yield return Capture("MagnetPull");
        float gap = float.MaxValue;
        for (float t = 0f; t < 3f && (fists.IsBusy || t < 0.2f); t += 0.03f)
        {
            gap = Mathf.Min(gap, Flat(far.transform.position - life.transform.position).magnitude);
            yield return 0.03f;
        }
        yield return 0.3f;
        Check(gap < 3f, $"Right click yanks a small enemy 12 m away to the player (to {gap:0.0} m)...");
        Check(Mathf.Abs(far.Max - far.Current - 30f * 1.55f) < 0.1f || far.IsDead, $"...and pounds it: 30 x 1.55 = {far.Max - far.Current:0.#} damage.");
        Kill(far);

        GameObject dummy = arena.SpawnDummy();
        Health boss = dummy.GetComponent<Health>();
        yield return 0.2f;
        Face(boss.transform.position);
        Teleport(boss.transform.position - Flat(boss.transform.position - life.transform.position).normalized * 14f);
        Face(boss.transform.position);
        yield return 0.2f;
        from = life.transform.position;
        SetPrivate(fists, "pullReadyAt", 0f);
        Invoke(fists, "TryPull");
        yield return Settle();
        Check(Vector3.Distance(from, life.transform.position) > 8f && boss.Current < boss.Max,
            $"Pulling something big (the boss dummy) pulls the player to it instead ({Vector3.Distance(from, life.transform.position):0} m) and pounds it.");

        // ---------- Rage ----------
        OverchargeMark.Consume(boss); // (its mark from the pound would throw lightning and steal the kill)
        Face(life.transform.position - (boss.transform.position - life.transform.position));
        yield return 0.6f;
        rage.ResetStacks();
        Health r1 = Spawn("HermitCrab", 1.8f);
        yield return 0.2f;
        Hold(r1);
        r1.TakeDamage(r1.Current - 1f); // nearly dead (from nothing, so it isn't the player's kill)
        yield return PunchAndWait();
        Check(r1.IsDead && rage.Stacks == 2, $"A melee kill gives 2 rage stacks ({rage.Stacks}).");
        Health r2 = Spawn("HermitCrab", 6f);
        yield return 0.2f;
        r2.TakeDamage(r2.Max, life.gameObject);
        Check(rage.Stacks == 3, $"Any other kill gives 1 ({rage.Stacks}).");
        float bossHp = boss.Current;
        boss.TakeDamage(100f, life.gameObject);
        Check(Mathf.Abs(bossHp - boss.Current - 118f) < 0.05f, $"Every hit the player deals is boosted 6% a stack: 100 -> {bossHp - boss.Current:0.#} at 3 stacks (bosses included).");
        Check(Near(movement.SpeedBonus, 1.22f * fists.SpeedBonus * 1.09f), $"...and the player runs 3% faster a stack (x{movement.SpeedBonus:0.00}).");
        float speedAt3 = movement.SpeedBonus;
        rage.Add(RageMeter.MaxStacks);
        Check(rage.Stacks == RageMeter.MaxStacks && rage.Maxed, "Rage tops out at 10 stacks.");
        yield return Capture("RageMax");
        SetPrivate(rage, "lastKill", Time.time - RageMeter.HoldSeconds - 0.1f);
        SetPrivate(rage, "nextDrain", 0f);
        yield return 0.2f;
        Check(rage.Stacks == RageMeter.MaxStacks - 1, $"With no kills for {RageMeter.HoldSeconds:0} s the stacks start draining ({rage.Stacks}).");
        UnityEngine.Object.Destroy(dummy);

        // ---------- Knuckles III: finishers ----------
        arena.SetKnuckleLimit(3);
        rage.ResetStacks();
        yield return 0.5f;
        Health e = Spawn("HermitCrab", 3f);
        Health f = Spawn("HermitCrab", 5f, 2f);
        Health g = Spawn("HermitCrab", 5.5f, -2.5f);
        yield return 0.3f;
        Hold(e, f, g);
        Check(!fists.CanFinish(e), "A healthy enemy can't be finished.");
        e.TakeDamage(e.Max * 0.7f);
        OverchargeMark.Apply(f);
        OverchargeMark.Apply(g);
        yield return 0.4f;
        Check(fists.CanFinish(e) && fists.FinisherTarget() == e, "Below 35% health it can be finished, and V would go for it.");
        Check(e.GetComponent<FinisherGlow>() != null && RageHud.FinisherPrompt.Contains("FINISH"), "It glows, and the finisher prompt shows under the crosshair.");
        yield return CaptureFrom("FinisherGlow", life.transform.position + Vector3.up * 2.5f - life.transform.forward * 3f, e.transform.position);
        life.Health.Invulnerable = false;
        float hf = f.Current, hg = g.Current;
        Invoke(fists, "TryFinisher");
        yield return 0.1f;
        Check(life.Health.Invulnerable && fists.IsFinishing, "The finisher starts, and the player can't be hurt during it.");
        yield return 0.3f;
        yield return Capture("Finisher");
        yield return Settle();
        Check(e.IsDead, "The finisher kills its target.");
        Check(rage.Stacks >= 3, $"A finisher kill gives 3 rage stacks ({rage.Stacks} with the shockwave's).");
        Check(!OverchargeMark.Has(f) && !OverchargeMark.Has(g) && (f.Current < hf || f.IsDead) && (g.Current < hg || g.IsDead),
            $"The shockwave and the marks going off hurt the enemies around ({hf - f.Current:0}, {hg - g.Current:0}).");
        Check(!life.Health.Invulnerable && !fists.IsFinishing, "Afterwards the player can be hurt again.");
        life.Health.Invulnerable = true;
        foreach (Health h in new[] { f, g }) Kill(h);

        // ---------- Moon Shoes: the directional double jump ----------
        Teleport(arena.transform.position + new Vector3(0f, 0f, -10f));
        yield return 0.3f;
        var body = life.GetComponent<CharacterController>();
        body.enabled = false; // a CharacterController ignores being moved while it is on
        life.transform.position += Vector3.up * 3f;
        body.enabled = true;
        yield return 0.1f;
        var air = typeof(FirstPersonController).GetMethod("AirJump", BindingFlags.Instance | BindingFlags.NonPublic);
        bool jumped = (bool)air.Invoke(movement, new object[] { life.transform.right });
        float rise = (float)typeof(FirstPersonController).GetField("verticalSpeed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(movement);
        Vector3 carry = (Vector3)typeof(FirstPersonController).GetField("momentum", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(movement);
        Check(jumped && rise > 5f && Vector3.Angle(carry, life.transform.right) < 1f && carry.magnitude > 7f,
            $"Moon Shoes III: a second jump in mid-air ({rise:0.0} m/s up), launched the way the keys point ({carry.magnitude:0.0} m/s sideways).");
        Check(!(bool)air.Invoke(movement, new object[] { life.transform.forward }), "Only one extra jump per trip into the air.");
        yield return 1.5f;
        arena.SetShoeLimit(2);
        Check(!movement.DoubleJump && Near(movement.JumpHeightScale, 1.4f), "Moon Shoes II has no double jump (testing key 8).");
        arena.SetShoeLimit(3);

        // ---------- The robot mini-bosses ----------
        foreach (FinaleRobots.Kind kind in new[] { FinaleRobots.Kind.BurnBot, FinaleRobots.Kind.WitchBot, FinaleRobots.Kind.MechaBuzzard, FinaleRobots.Kind.RoboToad })
        {
            GameObject robot = arena.SpawnRobot(kind);
            Check(robot != null && robot.GetComponent<Robotify>() != null, $"Testing key: {FinaleRobots.Name(kind)} comes in.");
        }
        yield return 3f;
        Health witch = arena.Extras.Select(x => x != null ? x.GetComponent<Health>() : null).FirstOrDefault(h => h != null && h.name == "Witch-Bot");
        Check(witch != null && !witch.IsDead && !fists.CanFinish(witch), "Robot mini-bosses fight, and can't be finished while healthy.");
        yield return CaptureFrom("Robots", life.transform.position + Vector3.up * 5f - life.transform.forward * 7f, witch.transform.position);
        witch.TakeDamage(witch.Current - witch.Max * 0.2f);
        Teleport(witch.transform.position + Flat(life.transform.position - witch.transform.position).normalized * 7f);
        Face(witch.transform.position);
        yield return 0.3f;
        Face(witch.transform.position);
        Check(fists.CanFinish(witch), $"Below a quarter of its health a robot mini-boss can be finished ({witch.Current:0} / {witch.Max:0}).");
        Health aimed = fists.FinisherTarget();
        Check(aimed == witch, $"...from further off than a critter ({Flat(witch.transform.position - life.transform.position).magnitude:0.0} m; V picks {(aimed != null ? aimed.name : "nothing")}).");
        rage.ResetStacks();
        Invoke(fists, "TryFinisher");
        yield return 0.25f;
        yield return Capture("RobotFinisher");
        yield return Settle();
        Check(witch.IsDead && rage.Stacks >= 3, $"V lunges in and finishes it with a bigger slam ({rage.Stacks} rage).");
        arena.ClearArena();
        yield return 0.5f;

        // ---------- The horde ----------
        arena.Paused = false;
        yield return WaitFor(() => arena.Alive >= 6, 20f, "The robot horde starts pouring in.");
        int killsBefore = arena.Kills;
        rage.ResetStacks();
        for (int i = 0; i < 90 && arena.Kills - killsBefore < 4; i++)
        {
            Health nearest = UnityEngine.Object.FindObjectsByType<Robotify>().Select(r => r.GetComponent<Health>())
                .Where(h => h != null && !h.IsDead).OrderBy(h => Vector3.Distance(h.transform.position, life.transform.position)).FirstOrDefault();
            if (nearest == null) { yield return 0.3f; continue; }
            Face(nearest.transform.position);
            yield return PunchAndWait();
        }
        int kills = arena.Kills - killsBefore;
        Check(kills >= 3 && rage.Stacks >= 3, $"Brawling through the horde with the fists kills robots and builds rage ({kills} kills, {rage.Stacks} stacks).");
        yield return Capture("Horde");

        Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions." : $"{errors.Count} errors, first: {errors[0]}");
    }

    // ---------- Helpers ----------

    // A robot creature (the arena's kind) a set distance in front of the player, offset sideways, facing it.
    private static readonly Dictionary<Health, Vector3> spots = new Dictionary<Health, Vector3>();

    private static Health Spawn(string prefab, float ahead, float side = 0f)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + prefab + ".prefab");
        Vector3 forward = Flat(life.transform.forward), right = Vector3.Cross(Vector3.up, forward);
        Vector3 at = life.transform.position + forward * ahead + right * side;
        if (Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        GameObject go = UnityEngine.Object.Instantiate(source, at, Quaternion.LookRotation(-forward));
        spots[go.GetComponent<Health>()] = at;
        go.AddComponent<Robotify>().addParts = true;
        var zone = UnityEngine.Object.FindAnyObjectByType<SignalLost.Enemies.CombatZone>();
        foreach (SignalLost.Enemies.ITerritorial enemy in go.GetComponentsInChildren<SignalLost.Enemies.ITerritorial>()) enemy.Territory.Zone = zone;
        return go.GetComponent<Health>();
    }

    private static void Punch() => Invoke(fists, "StartPunch");

    // Stunned in place (after their first frame: a frog ignores stuns before its Start) so a hop doesn't spoil a check.
    // and put back where they were spawned (a frog may have hopped away first).
    private static void Hold(params Health[] enemies)
    {
        foreach (Health h in enemies)
        {
            h.GetComponent<IStunnable>()?.Stun(8f);
            var agent = h.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh && spots.TryGetValue(h, out Vector3 spot)) agent.Warp(spot);
        }
    }

    // A boss dummy (it never moves or heals mid-test) a set distance in front of the player, for exact damage numbers.
    private static Health Dummy(float ahead)
    {
        Vector3 forward = Flat(life.transform.forward).normalized;
        var go = new GameObject("Test Dummy");
        go.transform.SetPositionAndRotation(life.transform.position + forward * (ahead + 1.1f), Quaternion.LookRotation(-forward));
        var health = go.AddComponent<Health>();
        health.SetMax(20000f);
        health.Restore();
        go.AddComponent<ArenaBossDummy>();
        return health;
    }

    private static IEnumerator PunchAndWait()
    {
        Punch();
        yield return Settle();
    }

    // Until the move (and any lunge or pull) is over, and the chain lightning has run (the batch clock is uneven).
    private static IEnumerator Settle()
    {
        yield return 0.05f;
        for (float t = 0f; t < 4f && (fists.IsBusy || movement.IsLunging); t += 0.05f) yield return 0.05f;
        yield return 0.35f;
    }

    private static void Kill(Health h)
    {
        if (h != null && !h.IsDead) h.TakeDamage(h.Current + 1f);
        if (h != null) UnityEngine.Object.Destroy(h.gameObject, 0.1f);
    }

    private static void Face(Vector3 point)
    {
        Vector3 to = Flat(point - life.transform.position);
        if (to.sqrMagnitude > 0.01f) life.transform.rotation = Quaternion.LookRotation(to);
        for (Transform t = view.transform; t != null && t != life.transform; t = t.parent) t.localRotation = Quaternion.identity; // level gaze
    }

    private static void Teleport(Vector3 point)
    {
        var body = life.GetComponent<CharacterController>();
        body.enabled = false;
        if (Physics.Raycast(point + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) point = hit.point;
        life.transform.position = point + Vector3.up * 0.1f;
        body.enabled = true;
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.002f;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);

    private static void SetPrivate(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
    {
        for (float t = 0f; t < seconds && !condition(); t += 0.1f) yield return 0.1f;
        Check(condition(), message);
    }

    private static IEnumerator CaptureFrom(string name, Vector3 at, Vector3 target)
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-finaleRender") < 0) yield break;
        var go = new GameObject("Capture Camera");
        var cam = go.AddComponent<Camera>();
        Camera main = Camera.main;
        if (main != null) cam.CopyFrom(main);
        cam.fieldOfView = 55f;
        go.transform.position = at;
        go.transform.LookAt(target);
        yield return Capture(name, cam);
        UnityEngine.Object.Destroy(go);
    }

    private static IEnumerator Capture(string name) => Capture(name, view);

    // Renders through the camera with the HUD canvases drawn into it (the same trick as CoreSystemsSmoke).
    private static IEnumerator Capture(string name, Camera cam)
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-finaleRender") < 0) yield break;
        ShaderUtil.allowAsyncCompilation = false;
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24);
        texture.Create();
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        RenderTexture old = cam.targetTexture;
        cam.targetTexture = texture;
        foreach (Canvas canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = cam.nearClipPlane + 0.02f; }
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = texture });
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/Melee_" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = active;
        foreach (Canvas canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        cam.targetTexture = old;
        UnityEngine.Object.Destroy(texture);
        UnityEngine.Object.Destroy(image);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report.Add(message);
    }

    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/MeleeSmoke.txt", report.Concat(new[] { "FAILED: " + ex.Message }));
        Debug.LogError("MELEE_SMOKE_FAIL " + ex + "\n" + string.Join("\n", report));
        EditorApplication.Exit(1);
    }
}
