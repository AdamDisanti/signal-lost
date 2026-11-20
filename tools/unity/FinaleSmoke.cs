using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Finale;
using SignalLost.Inventory;
using SignalLost.Mudlands;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch Play-mode check of the final boss slice (docs/final-boss.md) in the Wasteland scene: the playtest loadout,
// the intro, the mech's attacks, the phase 1 shield and conduits, the crew call-in, a death and phase retry, the car
// blast, the phase 2 placeholder task, phase 3 and the end. Copy into an Editor folder of a disposable project copy:
//   Unity -batchmode -projectPath <temp-project> -executeMethod FinaleSmoke.Start -logFile <log>
// Omit -quit. Add -finaleRender (without -nographics) to save captures to Logs/Finale_*.png. Not a physical-input test.
[InitializeOnLoad]
public static class FinaleSmoke
{
    private const string Key = "FinaleSmoke";
    private const string Scene = "Assets/Scenes/Large Areas/Wasteland.unity";
    private static readonly List<string> report = new List<string>();
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static double resume;
    private static bool started;

    static FinaleSmoke() { EditorApplication.update += Tick; }

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
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 420) { Fail(new Exception("Timed out.")); return; }
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
            File.WriteAllLines("Logs/FinaleSmoke.txt", report);
            Debug.Log("FINALE_SMOKE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static readonly List<string> errors = new List<string>();

    private static IEnumerator Run()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if ((type == LogType.Exception || type == LogType.Error) && !(message + stack).Contains("SearchDatabase")) errors.Add(message + "\n" + stack);
        };
        yield return 3f;
        var life = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
        var pockets = life.GetComponent<PlayerInventory>();
        var encounter = UnityEngine.Object.FindAnyObjectByType<FinaleEncounter>();
        CavendishMech mech = encounter.Mech;
        Camera view = life.GetComponentInChildren<Camera>();

        // ---------- The playtest loadout ----------
        Check(pockets.Has(PlayerItem.MiningLaser) && pockets.Has(PlayerItem.Scanner) && pockets.Has(PlayerItem.Flask), "The player arrives with the laser, scanner and flask.");
        int lines = UpgradeCatalog.All.Count(u => u.OneOff);
        Check(UpgradeCatalog.All.Where(u => u.OneOff).All(u => pockets.Owns(u.Id)), $"Every upgrade tier that exists is installed ({lines}).");
        Check(Mathf.Approximately(life.Health.Max, 130f) && Mathf.Approximately(life.Health.Current, 130f), $"End-game health: {life.Health.Current:0} / {life.Health.Max:0}.");
        Check(pockets.HasKeyItem(KeyItems.LearnersPermit), "The Learner's Permit is in the pockets (Drunk Driving works here).");
        Check(mech.Current == CavendishMech.State.Dormant && mech.Health.Invulnerable && Mathf.Approximately(mech.Health.Max, 6000f), "The mech (6000 health) kneels by the wreck, powered down.");
        yield return Capture("Arrival", view);

        // ---------- The intro ----------
        Teleport(life, encounter.transform.position + (life.transform.position - encounter.transform.position).normalized * 25f);
        yield return WaitFor(() => encounter.Current == FinaleEncounter.State.Intro, 3f, "Walking into the arena starts the intro.");
        encounter.SkipIntro();
        yield return WaitFor(() => mech.Current == CavendishMech.State.Fighting, 5f, "The mech stands up and the fight begins.");
        life.Health.Invulnerable = true;

        // ---------- The scrap magnet (phase 2's, checked first while the arena is still clear) ----------
        Teleport(life, mech.transform.position + mech.transform.forward * 18f);
        yield return 0.3f;
        bool forced = false;
        for (int tries = 0; tries < 200 && !forced; tries++) { forced = mech.ForceAttack("Scrap magnet"); if (!forced) yield return 0.05f; }
        float before = Vector3.Distance(life.transform.position, mech.transform.position), closest = before;
        for (float t = 0f; t < 12f && mech.AttackName == "Scrap magnet"; t += 0.05f)
        {
            closest = Mathf.Min(closest, Vector3.Distance(life.transform.position, mech.transform.position));
            yield return 0.05f;
        }
        Check(forced && closest < before - 6f, $"The scrap magnet drags a player who stands still in ({before:0} m -> {closest:0} m).");

        // ---------- Phase 1: each attack, aimed at a player standing still ----------
        // (The player can only be hurt while an attack is being measured, so the robots can't end the test early.)
        foreach (string attack in new[] { "Thruster lunge", "Rocket barrage", "Freezing sweep", "Thruster charge" })
            yield return Attack(mech, life, attack, view);
        Check(UnityEngine.Object.FindObjectsByType<SignalLost.Enemies.Desert.FireZone>().Length >= 8,
            $"Fire stays on the field ({UnityEngine.Object.FindObjectsByType<SignalLost.Enemies.Desert.FireZone>().Length} patches burning).");

        // ---------- The horde ----------
        yield return WaitFor(() => FinaleEncounter.Adds.Count >= 2, 16f, "Robot creatures keep spawning during the fight.");
        Check(FinaleEncounter.Adds.All(a => a.Go.GetComponent<Robotify>() != null), "They are Cavendish's robot versions.");
        int earlyCap = encounter.HordeCap;
        yield return CaptureFrom("Horde", life.transform.position + Vector3.up * 6f - life.transform.forward * 6f, FinaleEncounter.Adds[0].Go.transform.position);

        // ---------- First repair: Burn-Bot and Witch-Bot, and the conduits ----------
        mech.Health.TakeDamage(mech.Health.Max, life.gameObject);
        Check(mech.Shielded && Mathf.Abs(mech.HealthFraction - 0.66f) < 0.005f && mech.Health.Invulnerable, "At 66% the shield goes up and the mech is immune.");
        Check(encounter.HordeCap > earlyCap, $"The horde keeps coming, faster while it repairs (cap {earlyCap} -> {encounter.HordeCap}).");
        Vector3 kneelAt = mech.transform.position;
        yield return 3f;
        Check(Vector3.Distance(mech.transform.position, kneelAt) < 0.5f && (mech.AttackName == "" || mech.AttackName == "Rocket volley"),
            $"It kneels in place to repair, only sending the odd light rocket volley (moved {Vector3.Distance(mech.transform.position, kneelAt):0.00} m, attack '{mech.AttackName}').");
        string[] first = encounter.Robots.Select(r => r.Name).ToArray();
        Check(first.SequenceEqual(new[] { "Burn-Bot", "Witch-Bot" }) && encounter.Robots.All(r => r.Alive && r.Go.GetComponent<Robotify>() != null),
            "Burn-Bot and Witch-Bot come in, in robot skins: " + string.Join(", ", first) + ".");
        Check(encounter.Robots.All(r => r.MiniBoss == null || !BarShown(r.MiniBoss)), "Their own boss bars stay hidden (the finale HUD lists them).");
        life.Health.Invulnerable = false;
        float hp = life.Health.Current;
        life.Health.TakeDamage(20f, encounter.Robots[0].Go);
        Check(Mathf.Abs(hp - life.Health.Current - 14f) < 0.01f, $"The robots hit the player at 70% (a 20 hit took {hp - life.Health.Current:0.#}).");
        life.Health.Invulnerable = true;
        life.Health.Restore();
        yield return 4f;
        foreach (FinaleRobots.Robot robot in encounter.Robots)
            yield return CaptureFrom("Robot_" + robot.Name, robot.Go.transform.position + robot.Go.transform.forward * 9f + Vector3.up * 4f, robot.Go.transform.position + Vector3.up * 1.5f);
        Check(encounter.Robots.Any(r => r.Alive), "They fight on.");
        yield return CaptureFrom("Repairing", mech.transform.position + mech.transform.forward * 16f + Vector3.up * 5f, mech.transform.position + Vector3.up * 5f);

        // ---------- The whole crew ----------
        Check(encounter.Crew.Ready && encounter.Crew.Call() && AllyCrab.Live.Count == 11, $"G calls in Eugene and all ten cousins ({AllyCrab.Live.Count}).");
        yield return 2.5f;
        yield return Capture("Crew", view);

        foreach (ShieldConduit c in UnityEngine.Object.FindObjectsByType<ShieldConduit>().Where(c => c.Live).ToArray()) c.GetComponent<Health>().TakeDamage(500f, life.gameObject);
        yield return 1f;
        Check(mech.Shielded && mech.Phase == 1, "Breaking the conduits isn't enough while a robot still stands.");
        // Finish one robot with the knuckles (the end-game loadout has Knuckles III), the other the plain way.
        FinaleRobots.Robot finished = encounter.Robots.First(r => r.Alive);
        var fists = life.GetComponent<SignalLost.Melee.PlayerFists>();
        life.GetComponent<PlayerGear>().SetFistsOut(true);
        finished.Health.TakeDamage(finished.Health.Current - finished.Health.Max * 0.2f);
        Vector3 robotAt = finished.Go.transform.position;
        Teleport(life, robotAt + (life.transform.position - robotAt).normalized * 6f);
        Vector3 toRobot = robotAt - life.transform.position;
        toRobot.y = 0f;
        life.transform.rotation = Quaternion.LookRotation(toRobot);
        for (Transform t = view.transform; t != null && t != life.transform; t = t.parent) t.localRotation = Quaternion.identity;
        yield return 0.3f;
        Check(fists.CanFinish(finished.Health), $"Below a quarter of its health {finished.Name} can be finished with the knuckles.");
        typeof(SignalLost.Melee.PlayerFists).GetMethod("TryFinisher", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(fists, null);
        yield return WaitFor(() => !finished.Alive, 4f, $"V finishes {finished.Name} off.");
        life.GetComponent<PlayerGear>().SetFistsOut(false);
        foreach (FinaleRobots.Robot robot in encounter.Robots) if (robot.Alive) robot.Health.TakeDamage(robot.Health.Max * 10f, life.gameObject);
        for (float t = 0f; t < 10f && !(mech.Phase == 2 && mech.Current == CavendishMech.State.Fighting); t += 0.1f) yield return 0.1f;
        Check(mech.Phase == 2 && mech.Current == CavendishMech.State.Fighting,
            $"With both robots beaten and the conduits broken, phase 2 starts (phase {mech.Phase}, {mech.Current}, shielded {mech.Shielded}, robots left {encounter.RobotsLeft}, conduits left {encounter.ConduitsLeft}).");

        // ---------- Phase 2 attacks ----------
        yield return Attack(mech, life, "Coolant tidal wave", view);
        // ---------- A death retries the phase ----------
        life.Health.Invulnerable = false;
        life.Health.TakeDamage(9999f, null);
        yield return WaitFor(() => !life.IsRespawning && mech.Current == CavendishMech.State.Staggered, 12f, "The player respawns at the arena edge.");
        Check(mech.Phase == 2 && Mathf.Abs(mech.HealthFraction - 0.66f) < 0.005f && encounter.Robots.Count == 0 && FinaleEncounter.Adds.Count == 0 && AllyCrab.Live.Count == 0,
            "Dying restarts phase 2: the robots, the horde and the crew are cleared.");
        yield return WaitFor(() => mech.Current == CavendishMech.State.Fighting, 6f, "The fight resumes.");
        life.Health.Invulnerable = true;

        // ---------- The car: it hurts, it never stuns ----------
        before = mech.Health.Current;
        CarBlast.Explode(mech.transform.position + mech.transform.forward * 3f + Vector3.up, 6f, 80f, 3f, life.gameObject, mech.Health);
        Check(mech.Health.Current < before && mech.Current == CavendishMech.State.Fighting, $"A car ram hurts the mech ({before - mech.Health.Current:0}) and doesn't stun it.");

        // ---------- Second repair: Mecha-Worm and Mecha-Buzzard ----------
        mech.Health.TakeDamage(mech.Health.Max, life.gameObject);
        Check(mech.Shielded && Mathf.Abs(mech.HealthFraction - 0.33f) < 0.005f, "At 33% the shield goes up again.");
        yield return 4f;
        string[] second = encounter.Robots.Skip(Mathf.Max(0, encounter.Robots.Count - 2)).Select(r => r.Name).ToArray();
        Check(second.SequenceEqual(new[] { "Mecha-Worm", "Mecha-Buzzard" }), "Mecha-Worm and Mecha-Buzzard come in: " + string.Join(", ", second) + ".");
        yield return 5f;
        Check(encounter.Robots.All(r => r.Go != null), "Both are still in the fight a few seconds later.");
        foreach (FinaleRobots.Robot robot in encounter.Robots.Where(r => r.Go != null))
        {
            Vector3 at = robot.Go.transform.position;
            yield return CaptureFrom("Robot_" + robot.Name, at + (life.transform.position - at).normalized * 10f + Vector3.up * 5f, at + Vector3.up * 1.5f);
        }
        foreach (ShieldConduit c in UnityEngine.Object.FindObjectsByType<ShieldConduit>().Where(c => c.Live).ToArray()) c.GetComponent<Health>().TakeDamage(500f, life.gameObject);
        foreach (FinaleRobots.Robot robot in encounter.Robots) if (robot.Alive) robot.Health.TakeDamage(robot.Health.Max * 10f, life.gameObject);
        yield return WaitFor(() => mech.Phase == 3 && mech.Current == CavendishMech.State.Fighting, 8f, "With both beaten and the conduits broken, phase 3 starts.");

        // ---------- Phase 3: the napalm flyover ----------
        // (The robots are checked; clear them so they can't knock the "standing still" player off the line.)
        foreach (FinaleRobots.Robot robot in encounter.Robots) if (robot.Go != null) UnityEngine.Object.Destroy(robot.Go);
        foreach (FinaleEncounter.Add add in FinaleEncounter.Adds.ToArray()) if (add.Go != null) UnityEngine.Object.Destroy(add.Go);
        yield return 0.2f;
        int fires = UnityEngine.Object.FindObjectsByType<SignalLost.Enemies.Desert.FireZone>().Length;
        yield return Attack(mech, life, "Napalm flyover", view);
        int burning = UnityEngine.Object.FindObjectsByType<SignalLost.Enemies.Desert.FireZone>().Length;
        Check(burning >= fires + 5, $"The flyover carpets a line of fire ({fires} -> {burning} patches; older ones burn out meanwhile).");
        var legs = mech.GetComponent<UnityEngine.AI.NavMeshAgent>();
        Check(legs.enabled && legs.isOnNavMesh, "The mech lands back on the NavMesh, free to move.");

        mech.Health.TakeDamage(mech.Health.Max, life.gameObject);
        yield return WaitFor(() => encounter.Current == FinaleEncounter.State.Won, 3f, "Phase 3 fights to zero: the mech goes down.");
        yield return 1f;
        Check(FinaleEncounter.Adds.Count == 0 && encounter.Robots.All(r => !r.Alive), "With Cavendish down, his robots power off (the horde and any robot mini-bosses).");
        Check(StageProgress.HasMainPart("Reactor Core") && StageProgress.IsCleared(StageProgress.Finale), "The Reactor Core is recovered and the finale counts as cleared.");
        Check(errors.Count == 0, errors.Count == 0 ? "No errors or exceptions during the fight." : "Errors during the fight:\n" + string.Join("\n---\n", errors.Take(5)));
    }

    // A spot this far from the mech with nothing solid in between (the magnet test needs a straight pull).
    private static Vector3 ClearSpot(CavendishMech mech, float distance)
    {
        Vector3 centre = UnityEngine.Object.FindAnyObjectByType<FinaleEncounter>().transform.position;
        for (int a = 0; a < 360; a += 15)
        {
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * mech.transform.forward;
            Vector3 spot = mech.transform.position + dir * distance;
            Vector3 fromCentre = spot - centre;
            fromCentre.y = 0f;
            if (fromCentre.magnitude > 34f) continue; // on the levelled arena, not the hills round it
            bool blocked = false;
            foreach (RaycastHit hit in Physics.RaycastAll(spot + Vector3.up * 0.8f, -dir, distance - 5f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(mech.transform) && hit.collider.GetComponentInParent<Health>() == null && !(hit.collider is CharacterController)) blocked = true;
            if (!blocked) return spot;
        }
        return mech.transform.position + mech.transform.forward * distance;
    }

    private static bool BarShown(SignalLost.Enemies.Desert.DesertMiniBoss boss)
    {
        var list = typeof(SignalLost.Enemies.Desert.MiniBossHud).GetField("Engaged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as System.Collections.IList;
        return list != null && list.Contains(boss);
    }

    // Forces one attack at a player standing still (healed between attacks), and checks it lands.
    private static IEnumerator Attack(CavendishMech mech, PlayerLife life, string attack, Camera view)
    {
        // On the arena side of the mech (its leaps and flyovers stay inside the arena).
        Vector3 centre = UnityEngine.Object.FindAnyObjectByType<FinaleEncounter>().transform.position;
        Vector3 inward = centre - mech.transform.position;
        inward.y = 0f;
        Vector3 front = mech.transform.position + (inward.magnitude > 6f ? inward.normalized : mech.transform.forward) * 16f;
        Teleport(life, front);
        view.transform.rotation = Quaternion.LookRotation(mech.transform.position + Vector3.up * 6f - view.transform.position);
        life.Health.Restore();
        life.Health.Invulnerable = false;
        yield return 0.2f;
        int hits = 0;
        Action<Health, float, GameObject> count = (h, a, src) => { if (src == mech.gameObject) hits++; };
        life.Health.Damaged += count;
        for (int tries = 0; tries < 40 && !mech.ForceAttack(attack); tries++) yield return 0.1f;
        bool captured = false;
        for (float t = 0f; t < 14f && !string.IsNullOrEmpty(mech.AttackName); t += 0.1f)
        {
            if (life.Health.Current < 40f) life.Health.Restore();
            bool ready = attack == "Coolant tidal wave" ? GameObject.Find("Coolant Wave") != null : t > 0.6f;
            if (!captured && ready && attack == "Coolant tidal wave") yield return 0.6f; // let it get out across the arena
            if (!captured && ready)
            {
                captured = true;
                yield return CaptureFrom("Attack_" + attack.Replace(" ", ""), mech.transform.position + mech.transform.right * 22f + mech.transform.forward * 10f + Vector3.up * 10f, mech.transform.position + mech.transform.forward * 10f);
            }
            yield return 0.1f;
        }
        life.Health.Damaged -= count;
        life.Health.Invulnerable = true;
        Check(hits > 0, $"{attack}: it lands on a player who stands still ({hits} hits).");
        life.Health.Restore();
    }

    // ---------- Helpers ----------

    private static void Teleport(PlayerLife life, Vector3 point)
    {
        var body = life.GetComponent<CharacterController>();
        body.enabled = false;
        if (Physics.Raycast(point + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) point = hit.point;
        life.transform.position = point + Vector3.up * 0.1f;
        body.enabled = true;
    }

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
        File.WriteAllBytes("Logs/Finale_" + name + ".png", image.EncodeToPNG());
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
        File.WriteAllLines("Logs/FinaleSmoke.txt", report.Concat(new[] { "FAILED: " + ex.Message }));
        Debug.LogError("FINALE_SMOKE_FAIL " + ex + "\n" + string.Join("\n", report));
        EditorApplication.Exit(1);
    }
}
