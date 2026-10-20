using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.NPC;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Copy into Assets/Editor of a disposable matching project, never the real project's Assets.
[InitializeOnLoad]
public static class KnockbackSmoke
{
    private const string Key = "SignalLost.KnockbackSmoke";
    private static IEnumerator run;
    private static float resume;
    private static int frame = -1;
    private static readonly List<string> report = new List<string>();
    private static FirstPersonController player;
    private static Health hp, enemyHealth;
    private static NavMeshAgent enemy;
    private static GameObject source, wall, ceiling;
    private static Material material;
    private static readonly Knockback Shove = new Knockback(KnockbackMode.Shove, 6f);
    private static readonly Knockback Launch = new Knockback(KnockbackMode.Launch, 6f, 5f);

    static KnockbackSmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception) &&
                !trace.Contains("UnityEditor.Search.SearchDatabase")) Fail(new Exception(message + "\n" + trace));
        };
    }

    public static void Start()
    {
        try
        {
            TutorialEnemiesSetup.RequireTemp();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Cube("Floor", new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
            var p = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/FirstPersonPlayer.prefab"));
            p.AddComponent<Health>();
            p.transform.position = new Vector3(0, 0.05f, 0);
            var navigation = new GameObject("Navigation").AddComponent<NavMeshSurface>();
            navigation.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navigation.BuildNavMesh();
            wall = Cube("Knockback wall", new Vector3(1.5f, 2, 0), new Vector3(0.2f, 4, 10));
            ceiling = Cube("Knockback ceiling", new Vector3(0, 2.2f, 0), new Vector3(10, 0.2f, 10));
            wall.SetActive(false); ceiling.SetActive(false);
            SessionState.SetBool(Key + "Tutorial", false);
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    public static void StartTutorial()
    {
        try
        {
            TutorialEnemiesSetup.RequireTemp();
            EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity");
            SessionState.SetBool(Key + "Tutorial", true);
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 180)
        { Fail(new Exception("Knockback regression timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            run ??= SessionState.GetBool(Key + "Tutorial", false) ? RunTutorial() : Run();
            if (run.MoveNext()) { resume = Time.time + (run.Current is float seconds ? seconds : 0f); return; }
            SessionState.SetBool(Key, false);
            Debug.Log("KNOCKBACK_PLAY_MODE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        Time.captureDeltaTime = 1f / 60f;
        yield return 0.1f;
        player = Object.FindFirstObjectByType<FirstPersonController>(); hp = player.GetComponent<Health>();
        wall = GameObject.Find("Knockback wall"); // inactive objects need scene-root lookup
        foreach (var root in player.gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "Knockback wall") wall = root;
            if (root.name == "Knockback ceiling") ceiling = root;
        }
        source = new GameObject("Remote attack owner"); source.transform.position = new Vector3(25, 0, 25);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Check(default(Knockback).Velocity(Vector3.right) == Vector3.zero, "Default payload has no knockback.");
        Check(Shove.Velocity(new Vector3(3, 9, 0)) == Vector3.right * 6, "Shoves are planar; strength ignores direction length.");
        Check(Launch.Velocity(Vector3.zero) == Vector3.up * 5, "Centered blast launches up without an arbitrary facing fallback.");
        Check(Launch.Velocity(new Vector3(float.NaN, 0, 0)) == Vector3.zero, "Malformed directions cannot poison movement.");
        Check(Knockback.SlideAlongWall(Vector3.right, Vector3.right) == Vector3.right &&
            Knockback.SlideAlongWall(Vector3.right + Vector3.forward, Vector3.left) == Vector3.forward,
            "Departing contact retains impulse; blocking wall removes only the inward component.");

        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            Place(Vector3.zero); player.transform.rotation = Quaternion.Euler(0, 137, 0);
            Check(hp.TakeDamage(1, source, Shove, direction), "Directional hit accepted.");
            yield return 0.25f;
            Vector3 displacement = player.transform.position;
            Check(Vector3.Dot(displacement, direction) > 0.6f && Mathf.Abs(Vector3.Dot(displacement, Vector3.Cross(direction, Vector3.up))) < 0.05f,
                "Shove follows incoming direction, not player facing or remote owner: " + direction);
            Check(player.transform.position.y < 0.15f, "Shove does not launch.");
        }
        Place(Vector3.zero);
        hp.TakeDamage(1, source, Launch, Vector3.right);
        yield return 0.15f;
        Check(player.transform.position.y > 0.35f && player.transform.position.x > 0.3f, "Launch has both outward and upward motion.");
        yield return 1f;
        Check(player.transform.position.y < 0.15f, "Launch returns to ground under existing gravity.");

        Place(Vector3.zero); hp.Invulnerable = true;
        Check(!hp.TakeDamage(1, source, Launch, Vector3.right), "Invulnerable damage rejected.");
        hp.Invulnerable = false;
        Check(!hp.TakeDamage(0, source, Launch, Vector3.right) && !hp.TakeDamage(float.NaN), "Zero/NaN damage rejected.");
        yield return 0.2f;
        Check(Mathf.Abs(player.transform.position.x) < 0.01f, "Rejected damage produces no impulse.");
        hp.TakeDamage(1, source, Launch, Vector3.right); hp.Restore();
        yield return 0.2f;
        Check(Mathf.Abs(player.transform.position.x) < 0.01f && player.transform.position.y < 0.15f, "Restore clears pending launch motion.");
        hp.TakeDamage(999, source, Launch, Vector3.right);
        yield return 0.2f;
        Check(hp.IsDead && Mathf.Abs(player.transform.position.x) < 0.01f, "Lethal hit does not move a dead player.");
        Place(Vector3.zero); hp.TakeDamage(1, source, Launch, Vector3.right); player.enabled = false; player.enabled = true;
        yield return 0.2f;
        Check(Mathf.Abs(player.transform.position.x) < 0.01f, "Disabling controls clears momentum for respawn/blackout.");

        Place(Vector3.zero); wall.SetActive(true); Physics.SyncTransforms();
        hp.TakeDamage(1, source, new Knockback(KnockbackMode.Shove, 100), Vector3.right);
        yield return 0.2f;
        Check(player.transform.position.x > 0.5f && player.transform.position.x < 1.2f, "Swept player collision blocks high-speed wall tunneling.");
        wall.SetActive(false); Place(Vector3.zero); ceiling.SetActive(true); Physics.SyncTransforms();
        hp.TakeDamage(1, source, new Knockback(KnockbackMode.Launch, 0, 10), Vector3.zero);
        yield return 0.2f;
        Check(player.transform.position.y < 0.45f && Get<float>(player, "verticalSpeed") <= 0, "Ceiling stops upward launch velocity.");
        ceiling.SetActive(false);

        Place(Vector3.zero);
        LaserBolt.Spawn(material, new Vector3(-3, 0.9f, 0), Vector3.right, new Vector3(-3, 0.9f, 0), 1, 25, 0.08f, 10, ~0, source.transform, null);
        yield return 0.3f;
        Check(hp.Current == 99 && Mathf.Abs(player.transform.position.x) < 0.01f, "Default-off projectile damages without adding knockback.");
        Place(Vector3.zero);
        LaserBolt.Spawn(material, new Vector3(-3, 0.9f, 0), Vector3.right, new Vector3(-3, 0.9f, 0), 1, 25, 0.08f, 10, ~0, source.transform, null, Launch);
        yield return 0.25f;
        Check(hp.Current == 99 && player.transform.position.x > 0.3f && player.transform.position.y > 0.2f, "Opt-in real projectile carries launch in its travel direction.");
        Place(Vector3.zero);
        var bolt = LaserBolt.Spawn(material, new Vector3(3, 0.9f, 0), Vector3.right, new Vector3(3, 0.9f, 0), 1, 25, 0.08f, 10, ~0, player.transform, null, Shove);
        Call(bolt, "Reflect", new Vector3(3, 0.9f, 0), Vector3.left, source.transform, 1f);
        yield return 0.3f;
        Check(hp.Current == 99 && player.transform.position.x < -0.3f, "Reflected bolt uses its new direction and can push the original shooter.");

        enemy = new GameObject("Agent receiver").AddComponent<NavMeshAgent>();
        enemy.enabled = false;
        enemyHealth = enemy.gameObject.AddComponent<Health>();
        var capsule = enemy.gameObject.AddComponent<CapsuleCollider>(); capsule.height = 1.8f; capsule.radius = 0.35f; capsule.center = Vector3.up * 0.9f;
        enemy.transform.position = new Vector3(0, 0, 12); enemy.enabled = true; enemy.Warp(enemy.transform.position);
        enemy.isStopped = true;
        Vector3 initial = enemy.transform.position;
        enemyHealth.TakeDamage(1, source, Launch, Vector3.right);
        yield return 0.15f;
        Check(enemy.transform.position.y > initial.y + 0.25f && enemy.transform.position.x > initial.x + 0.3f,
            "Opt-in agent receiver physically launches instead of being pinned to navigation.");
        yield return 1.3f;
        Check(!KnockbackReceiver.IsMoving(enemy.gameObject) && enemy.isOnNavMesh && enemy.updatePosition && enemy.isStopped,
            "Agent lands, rebinds navigation, and restores prior movement settings.");
        Check(enemy.transform.position.y < 0.2f, "Enemy lands above ground.");
        initial = enemy.transform.position;
        LaserBolt.Spawn(material, initial + new Vector3(-3, 0.9f, 0), Vector3.right, initial + new Vector3(-3, 0.9f, 0), 1, 25, 0.08f, 10, ~0, player.transform, null, Shove);
        yield return 0.35f;
        Check(enemy.transform.position.x > initial.x + 0.4f, "Player-owned projectile can opt into enemy knockback.");
        yield return 0.6f;
        enemyHealth.TakeDamage(1, source, Launch, Vector3.left); enemyHealth.TakeDamage(1, source, Launch, Vector3.right);
        yield return 0.1f;
        Check(KnockbackReceiver.IsMoving(enemy.gameObject), "Repeated hits reuse one active receiver.");
        enemyHealth.Restore();
        Check(!KnockbackReceiver.IsMoving(enemy.gameObject) && enemy.isOnNavMesh, "Enemy restore clears active knockback and restores navigation.");

        // One damage application per Health even with multiple colliders; nested blasts have independent bookkeeping.
        Place(Vector3.zero);
        enemy.Warp(new Vector3(2.5f, 0, 0));
        Physics.SyncTransforms();
        var extra = new GameObject("Extra player hitbox"); extra.transform.SetParent(player.transform, false);
        extra.transform.localPosition = Vector3.up; extra.AddComponent<SphereCollider>().radius = 0.2f;
        enemyHealth.Restore();
        FodderExplosion.Explode(new Vector3(-1, 0, 0), 5, 10, 1, source, null, material, ~0, Launch);
        yield return 0.15f;
        Check(hp.Current == 90 && player.transform.position.x > 0.3f && player.transform.position.y > 0.2f,
            "Explosion hits multi-collider player once and launches away from blast, not owner.");
        Check(enemyHealth.Current == 90 && !KnockbackReceiver.IsMoving(enemy.gameObject), "Existing blast enemy damage preserved; enemy knockback not enabled by default. HP=" + enemyHealth.Current + " moving=" + KnockbackReceiver.IsMoving(enemy.gameObject));
        Place(Vector3.zero); enemyHealth.Restore();
        bool nested = false;
        Action<Health, float, GameObject> chain = (_, amount, owner) =>
        {
            if (nested) return;
            nested = true;
            FodderExplosion.Explode(Vector3.zero, 5, 10, 1, source, null, material, ~0);
        };
        enemyHealth.Damaged += chain;
        FodderExplosion.Explode(Vector3.zero, 5, 10, 1, source, null, material, ~0);
        enemyHealth.Damaged -= chain;
        Check(nested && hp.Current == 80 && enemyHealth.Current == 80, "Nested explosions deduplicate separately without corrupting outer iteration.");
        Place(new Vector3(-15, 0, 0)); enemyHealth.Restore();
        FodderExplosion.Explode(new Vector3(1.5f, 0, 0), 3, 1, 1, player.gameObject, null, material, ~0, Launch, knockbackEnemies: true);
        Check(KnockbackReceiver.IsMoving(enemy.gameObject), "Future player-owned blasts can explicitly opt into enemy knockback.");
        enemyHealth.Restore();
        enemy.gameObject.SetActive(false);
        Object.Destroy(extra);
        yield return 0.05f;

        // Exercise a real tutorial enemy AI, not just the shared damage backend.
        Place(new Vector3(10, 0, 0));
        var crabObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/HermitCrab.prefab"), new Vector3(10, 0, 1.1f), Quaternion.Euler(0, 180, 0));
        var crab = crabObject.GetComponent<HermitCrab>();
        float until = Time.time + 6;
        while (hp.Current == 100 && Time.time < until) yield return 0.05f;
        Check(hp.Current < 100, "Real crab slam damages the player.");
        // Inspect immediately at impact: editor asset imports can delay polling until a short launch has landed.
        crab.enabled = false; crab.GetComponent<NavMeshAgent>().isStopped = true;
        crab.transform.rotation = Quaternion.Euler(0, 180, 0);
        Place(crab.transform.position + Vector3.back * 1.7f);
        Check((bool)Call(crab, "TryHitTarget") && Get<float>(player, "verticalSpeed") > 0 && Get<Vector3>(player, "knockbackVelocity").z < 0,
            "Real crab heavy slam configures an outward launch on impact.");
        Place(new Vector3(-20, 0, -20));
        crab.enabled = true;
        var crabHealth = crab.GetComponent<Health>();
        Vector3 crabStart = crab.transform.position;
        crabHealth.TakeDamage(1, player.gameObject, Launch, Vector3.right);
        yield return 0.15f;
        Check(crab.transform.position.x > crabStart.x + 0.3f && crab.transform.position.y > crabStart.y + 0.2f,
            "Existing crab collider supports opt-in enemy launch without duplicate legacy knockback.");
        yield return 1.3f;
        Check(!KnockbackReceiver.IsMoving(crabObject) && crab.GetComponent<NavMeshAgent>().isOnNavMesh, "Real crab navigation restored after landing.");
        Object.Destroy(crabObject);

        var frogObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/MortarFrog.prefab"), new Vector3(0, 0, 15), Quaternion.identity);
        yield return 0.1f;
        var frog = frogObject.GetComponent<MortarFrog>();
        var can = Get<Health>(frog, "canHealth");
        Vector3 frogStart = frog.transform.position;
        can.TakeDamage(1, player.gameObject, Launch, Vector3.right);
        yield return 0.2f;
        Check(frog.GetComponent<Health>().Current == frog.GetComponent<Health>().Max - 1 && can.Current == can.Max &&
            frog.transform.position.x > frogStart.x + 0.3f && frog.transform.position.y > frogStart.y + 0.2f,
            "Frog weak-spot relay preserves damage, direction and launch; active hop AI cannot override it. HP=" + frog.GetComponent<Health>().Current + " can=" + can.Current + " from=" + frogStart + " now=" + frog.transform.position);
        Object.Destroy(frogObject);

        Place(new Vector3(20, 0, 21));
        var burnout = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/MiniBosses/Burnout.prefab"), new Vector3(20, 0, 20), Quaternion.identity).GetComponent<Burnout>();
        yield return 0.1f;
        burnout.enabled = false;
        Set(burnout, "dashDirection", Vector3.forward); Set(burnout, "dashLength", 30f); Set(burnout, "dashHit", false);
        Call(burnout, "UpdateDash", 0.01f);
        Check(hp.Current < 100 && Get<float>(player, "verticalSpeed") > 0 && Get<Vector3>(player, "knockbackVelocity").z > 0,
            "Actual Burnout dash impact launches along dash travel, not sideways or player facing.");
        Object.Destroy(burnout.gameObject);

        Place(new Vector3(11, 0, 20));
        var buzzard = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/MiniBosses/OldBuzzard.prefab"), new Vector3(10, 0, 20), Quaternion.identity).GetComponent<OldBuzzard>();
        yield return 0.1f;
        buzzard.enabled = false;
        Set(buzzard, "diveTarget", new Vector3(10, 0, 20)); Set(buzzard, "diveFrom", buzzard.transform.position);
        Set(buzzard, "stateTime", Get<float>(buzzard, "diveTime"));
        Call(buzzard, "UpdateDive");
        Check(hp.Current < 100 && Get<float>(player, "verticalSpeed") > 0 && Get<Vector3>(player, "knockbackVelocity").x > 0,
            "Actual Buzzard dive launches radially from its ground impact.");
        Place(new Vector3(11, 0, 20));
        Set(buzzard, "hopTo", new Vector3(10, 0, 20)); Set(buzzard, "stateTime", Get<float>(buzzard, "stompTime"));
        Call(buzzard, "UpdateStomp");
        Check(hp.Current < 100 && Get<float>(player, "verticalSpeed") > 0, "Actual Buzzard stomp launches on impact.");
        Object.Destroy(buzzard.gameObject);

        Place(new Vector3(0, 0, 0));
        var beetle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/JerryCanBeetle.prefab"), new Vector3(-1, 0, 0), Quaternion.identity).GetComponent<JerryCanBeetle>();
        yield return 0.1f;
        beetle.enabled = false; Call(beetle, "Detonate");
        Check(hp.Current < 100 && Get<float>(player, "verticalSpeed") > 0 && Get<Vector3>(player, "knockbackVelocity").x > 0,
            "Actual beetle explosion launches away from its blast center.");
        Object.Destroy(beetle.gameObject);
        Place(new Vector3(0, 0, 0));
        ScrapBomb.Drop(material, material, material, new Vector3(-1, 0.1f, 0), new Vector3(-1, 0, 0), 3, 10, source, null, ~0, Launch);
        yield return 0.15f;
        Check(hp.Current < 100 && player.transform.position.x > 0.2f && player.transform.position.y > 0.2f,
            "Actual falling scrap bomb carries its owner's configured launch profile.");

        // Nested bonus damage must raise death once, and hit context must unwind after callbacks.
        var nestedHealth = new GameObject("Nested damage").AddComponent<Health>();
        int deaths = 0; bool applying = false;
        nestedHealth.Died += _ => deaths++;
        nestedHealth.Damaged += (h, amount, owner) =>
        {
            if (applying) return;
            applying = true; h.TakeDamage(99); applying = false;
            Check(h.HitKnockback.mode == KnockbackMode.Launch, "Nested damage restores the enclosing hit context.");
        };
        nestedHealth.TakeDamage(2, source, Launch, Vector3.right);
        Check(deaths == 1 && nestedHealth.HitKnockback.mode == KnockbackMode.None, "Nested lethal damage raises death once and clears hit context.");
        Object.Destroy(material);
    }

    private static IEnumerator RunTutorial()
    {
        Time.captureDeltaTime = 1f / 60f;
        yield return 0.2f;
        player = Object.FindFirstObjectByType<FirstPersonController>(); hp = player.GetComponent<Health>();
        var tutorial = player.GetComponent<TutorialPlayer>();
        var merchant = Object.FindFirstObjectByType<MerchantCrab>();
        foreach (var crab in Object.FindObjectsByType<HermitCrab>()) crab.enabled = false;
        foreach (var frog in Object.FindObjectsByType<MortarFrog>()) frog.enabled = false;
        merchant.BeginQuest();
        yield return 0.1f;
        var merchantHealth = merchant.GetComponent<Health>();
        merchantHealth.TakeDamage(1, player.gameObject, Launch, Vector3.right);
        yield return 0.1f;
        Check(KnockbackReceiver.IsMoving(merchant.gameObject), "Tutorial merchant accepts opt-in enemy knockback while hostile.");
        merchantHealth.TakeDamage(999, player.gameObject);
        yield return 1.4f;
        Check(merchant.IsDefeated && merchantHealth.Invulnerable && merchantHealth.Current == merchantHealth.Max,
            "Merchant surrender restores friendly protected health.");
        Check(!KnockbackReceiver.IsMoving(merchant.gameObject) && !merchant.GetComponent<CharacterController>().enabled &&
            merchant.GetComponent<NavMeshAgent>().isOnNavMesh, "Surrender leaves knockback controller off and restores merchant navigation.");
        Vector3 spawn = Get<Vector3>(tutorial, "spawnPosition");
        hp.TakeDamage(1, merchant.gameObject, Launch, Vector3.right);
        hp.TakeDamage(999);
        Check(tutorial.IsRespawning, "Real tutorial death starts its existing respawn sequence.");
        yield return 2f;
        Vector3 offset = player.transform.position - spawn; offset.y = 0;
        Check(!tutorial.IsRespawning && hp.Current == hp.Max && offset.magnitude < 0.1f &&
            Get<Vector3>(player, "knockbackVelocity") == Vector3.zero,
            "Tutorial respawns at its checkpoint without stale knockback.");
        var terrain = Object.FindFirstObjectByType<Terrain>();
        foreach (var agent in Object.FindObjectsByType<NavMeshAgent>())
            Check(agent.transform.position.y >= terrain.SampleHeight(agent.transform.position) + terrain.transform.position.y - 0.3f,
                "Enemy stage reset remains above current terrain: " + agent.name);
        Check(merchant.IsDefeated && !merchant.GetComponent<CharacterController>().enabled,
            "Friendly merchant and disabled knockback controller survive stage reset.");
    }

    private static void Place(Vector3 point)
    {
        var body = player.GetComponent<CharacterController>();
        body.enabled = false; player.transform.position = point + Vector3.up * 0.05f; body.enabled = true;
        hp.Restore(); player.ResetMotion(); Physics.SyncTransforms();
    }
    private static GameObject Cube(string name, Vector3 p, Vector3 size)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
        obj.transform.position = p; obj.transform.localScale = size; return obj;
    }
    private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); report.Add(message); }
    private static void Fail(Exception ex)
    {
        if (SessionState.GetBool(Key + "Failed", false)) return;
        SessionState.SetBool(Key + "Failed", true);
        SessionState.SetBool(Key, false);
        Debug.LogError("KNOCKBACK_FAIL: " + ex + "\n" + string.Join("\n", report));
        EditorApplication.Exit(1);
    }
}
