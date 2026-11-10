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

    private static IEnumerator Run()
    {
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
        Check(pockets.SupplyCount("hangover_pills") == 2 && pockets.SupplyCount("mystery_snack") == 2, "Two of each supply.");
        Check(mech.Current == CavendishMech.State.Dormant && mech.Health.Invulnerable, "The mech kneels by the wreck, powered down.");
        yield return Capture("Arrival", view);
        Transform look = mech.Rig.Cockpit;
        yield return CaptureFrom("MechDormant", mech.transform.position + mech.transform.forward * 16f + Vector3.up * 7f, look.position - Vector3.up * 2f);

        // ---------- The intro ----------
        Teleport(life, encounter.transform.position + (life.transform.position - encounter.transform.position).normalized * 25f);
        yield return WaitFor(() => encounter.Current == FinaleEncounter.State.Intro, 3f, "Walking into the arena starts the intro.");
        encounter.SkipIntro();
        yield return WaitFor(() => mech.Current == CavendishMech.State.Fighting, 5f, "The mech stands up and the fight begins.");
        Check(!mech.Health.Invulnerable && mech.Phase == 1 && encounter.Crew.Enabled, "Phase 1: it can be hurt, and the crew call-in is live.");
        yield return CaptureFrom("MechAwake", mech.transform.position + mech.transform.forward * 18f + mech.transform.right * 6f + Vector3.up * 4f, mech.Rig.Chest.position);

        // ---------- Its attacks land ----------
        int hits = 0;
        var seen = new HashSet<string>();
        Action<Health, float, GameObject> counter = (h, a, s) => { if (s == mech.gameObject) hits++; };
        life.Health.Damaged += counter;
        bool telegraphed = false, captured = false;
        for (float t = 0f; t < 34f; t += 0.25f)
        {
            if (!string.IsNullOrEmpty(mech.AttackName)) seen.Add(mech.AttackName);
            if (UnityEngine.Object.FindObjectsByType<Telegraph>().Length > 0) telegraphed = true;
            if (life.Health.Current < 50f) life.Health.Restore();
            if (!captured && mech.AttackName == "Freezing sweep" && UnityEngine.Object.FindObjectsByType<Telegraph>().Length == 0)
            {
                captured = true;
                yield return CaptureFrom("FreezingSweep", mech.transform.position - mech.transform.forward * 6f + mech.transform.right * 18f + Vector3.up * 9f, mech.transform.position + mech.transform.forward * 12f);
            }
            if (seen.Count >= 2 && hits > 0 && t > 12f) break;
            yield return 0.25f;
        }
        life.Health.Damaged -= counter;
        Check(telegraphed, "Every attack is painted on the ground first.");
        Check(seen.Count >= 2, "It uses more than one weapon: " + string.Join(", ", seen) + ".");
        Check(hits > 0, $"Its attacks hurt the player ({hits} hits).");
        life.Health.Restore();

        // ---------- The reactor weak spot ----------
        float before = mech.Health.Current;
        mech.Rig.Reactor.GetComponent<Health>().TakeDamage(40f, life.gameObject);
        Check(Mathf.Approximately(before - mech.Health.Current, 60f), $"The Reactor Core takes x1.5 ({before - mech.Health.Current:0} from a 40 hit).");

        // ---------- Phase 1 shield and its conduits ----------
        mech.Health.TakeDamage(mech.Health.Max, life.gameObject);
        Check(mech.Shielded && Mathf.Abs(mech.HealthFraction - 0.66f) < 0.005f && mech.Health.Invulnerable, "At 66% the shield goes up; the huge hit stopped there.");
        Check(!mech.Health.TakeDamage(100f, life.gameObject), "Shielded, it takes no damage.");
        Check(encounter.ConduitsTotal == 3 && encounter.ConduitsLeft == 3, "Three conduits come alive.");
        var live = UnityEngine.Object.FindObjectsByType<ShieldConduit>().Where(c => c.Live).ToArray();
        Check(live.Length == 3 && live.All(c => c.GetComponent<QuestSignalSource>().enabled), "Each live conduit shows on the scanner.");
        Check(QuestSignalSource.Nearest(life.transform.position) != null && live.Contains(QuestSignalSource.Nearest(life.transform.position).GetComponent<ShieldConduit>()),
            "The scanner selects the nearest live conduit.");
        yield return 4f;
        Check(FinaleEncounter.Adds.Count > 0, $"Adds come in while the shield is up ({FinaleEncounter.Adds.Count}).");
        yield return CaptureFrom("Shielded", mech.transform.position + mech.transform.forward * 20f + Vector3.up * 6f, mech.transform.position + Vector3.up * 5f);
        ShieldConduit first = live.OrderBy(c => Vector3.Distance(c.transform.position, life.transform.position)).First();
        Teleport(life, first.transform.position + (encounter.transform.position - first.transform.position).normalized * 6f);
        view.transform.LookAt(first.transform.position + Vector3.up);
        yield return Capture("Conduit", view);

        // ---------- The crew call-in ----------
        Check(encounter.Crew.Ready && encounter.Crew.Call() && AllyCrab.Live.Count == 4, "G calls in Eugene and three cousins.");
        yield return 2.5f;
        yield return CaptureFrom("Crew", life.transform.position - life.transform.forward * 4f + Vector3.up * 5f, AllyCrab.Live.Count > 0 ? AllyCrab.Live[0].transform.position : mech.transform.position);
        Check(!encounter.Crew.Ready && encounter.Crew.CooldownRemaining > CrewCallIn.Cooldown, "Then it cools down.");

        foreach (ShieldConduit c in live) c.GetComponent<Health>().TakeDamage(500f, life.gameObject);
        Check(!mech.Shielded && mech.Current == CavendishMech.State.Staggered, "Breaking all three drops the shield; the mech staggers.");
        yield return WaitFor(() => mech.Phase == 2 && mech.Current == CavendishMech.State.Fighting, 6f, "Phase 2 starts.");

        // ---------- A death retries the phase ----------
        mech.Health.TakeDamage(mech.Health.Max * 0.1f, life.gameObject);
        Check(mech.HealthFraction < 0.6f, "Phase 2 damage lands.");
        life.Health.Invulnerable = false;
        life.Health.TakeDamage(9999f, null);
        yield return WaitFor(() => !life.IsRespawning && mech.Current == CavendishMech.State.Staggered, 12f, "The player respawns at the arena edge.");
        Check(mech.Phase == 2 && Mathf.Abs(mech.HealthFraction - 0.66f) < 0.005f && encounter.Retries == 1, "Dying puts the mech back to the start of phase 2, not the whole fight.");
        Check(AllyCrab.Live.Count == 0 && encounter.Crew.Ready && FinaleEncounter.Adds.Count == 0, "The crew, the call-in cooldown and the adds reset.");
        yield return WaitFor(() => mech.Current == CavendishMech.State.Fighting, 6f, "The fight resumes.");

        // ---------- The car: it hurts, it never stuns ----------
        before = mech.Health.Current;
        CarBlast.Explode(mech.transform.position + mech.transform.forward * 3f + Vector3.up, 6f, 80f, 3f, life.gameObject, mech.Health);
        Check(mech.Health.Current < before && mech.Current == CavendishMech.State.Fighting, $"A car ram hurts the mech ({before - mech.Health.Current:0}) and doesn't stun it.");

        // ---------- Phase 2's placeholder task, phase 3, the end ----------
        mech.Health.TakeDamage(mech.Health.Max, life.gameObject);
        Check(mech.Shielded && Mathf.Abs(mech.HealthFraction - 0.33f) < 0.005f && encounter.ConduitsTotal == 4, "At 33%: shielded again; the placeholder task uses four more conduits.");
        foreach (ShieldConduit c in UnityEngine.Object.FindObjectsByType<ShieldConduit>().Where(c => c.Live).ToArray()) c.GetComponent<Health>().TakeDamage(500f, life.gameObject);
        yield return WaitFor(() => mech.Phase == 3 && mech.Current == CavendishMech.State.Fighting, 6f, "Phase 3 starts.");
        mech.Health.TakeDamage(mech.Health.Max, life.gameObject);
        yield return WaitFor(() => encounter.Current == FinaleEncounter.State.Won, 3f, "Phase 3 fights to zero: the mech goes down.");
        Check(StageProgress.HasMainPart("Reactor Core") && StageProgress.IsCleared(StageProgress.Finale), "The Reactor Core is recovered and the finale counts as cleared.");
        yield return 2f;
        yield return CaptureFrom("Defeated", mech.transform.position + mech.transform.forward * 16f + Vector3.up * 5f, mech.transform.position + Vector3.up * 3f);
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
