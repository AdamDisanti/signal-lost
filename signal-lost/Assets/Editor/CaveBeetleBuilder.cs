using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using CrystalColor = SignalLost.Editor.CrystalAssetBuilder.CrystalColor;
using Hitbox = SignalLost.Editor.DesertDuelBossBuilder.Hitbox;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the four Crystal Caves beetle mini-bosses and their test scene.
    ///
    /// Built like the other mini-bosses (<see cref="DesertDuelBossBuilder"/>, <see cref="ShardMoleBuilder"/>): one
    /// Blender model and clip set from tools/blender/cave_bosses.py -- beetle (a round glossy shell with crystal
    /// growths, big eyes, stubby legs, a mic pack and a headset), bone hitboxes, the belly weak spot and the hidden
    /// signal amplifier. The four differ only in their shell, belly and crystal colours.
    ///
    /// Files: Prefabs/Enemies/Caves/{BugLennon, PaulArthropodney, GeorgeHarrison, ScarabStarr}.prefab (rebuilt in place,
    /// same GUIDs), the model in Prefabs/Enemies/Caves/Bosses/CaveBeetle, colour materials in Materials/Enemies/Caves,
    /// and Scenes/Bosses/CaveBeetlesTest.unity. Menu: Signal Lost > Crystal Caves Enemies > Beetles.
    /// </summary>
    public static class CaveBeetleBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bosses/CaveBeetlesTest.unity";

        private sealed class Member
        {
            public string Name, File;
            public Color Shell;
            public string[] Lines;
            public string LastWords;
            public string PrefabPath => CaveEnemyKit.PrefabFolder + "/" + File + ".prefab";
        }

        // Their dreams of being a band, a line per hit, each true to their place in it: Bug Lennon on rhythm guitar,
        // Paul Arthropodney on bass, George Harrison on lead guitar, Scarab Starr on drums (all four sing). They started
        // out at the Cavern Club, which comes up now and then.
        private static readonly Member[] Members =
        {
            new Member
            {
                Name = "Bug Lennon", File = "BugLennon", Shell = new Color(0.78f, 0.14f, 0.12f),
                Lines = new[]
                {
                    "Oi! Do you know who I am? Rhythm guitar, lead mouth, future frontman of the biggest band under the mountain!",
                    "We started at the Cavern Club, mate. Proper cavern. Proper club. Proper damp.",
                    "Rock and roll, mate. Emphasis on the ROCK. Six strings, six legs, and I strum with all of them.",
                    "Imagine it: a world tour. Every cave. Every tunnel. Every crevice.",
                    "Mind the guitar! I only just tuned it to the drip in the ceiling!",
                    "I write the songs, I keep the rhythm, and I do NOT get hit in the face mid-chorus!",
                },
                LastWords = "Tell the fans... it was all... for the rock...",
            },
            new Member
            {
                Name = "Paul Arthropodney", File = "PaulArthropodney", Shell = new Color(0.94f, 0.74f, 0.16f),
                Lines = new[]
                {
                    "Careful! These legs hold down the low end!",
                    "Nobody notices the bass player until the bass stops. Then EVERYBODY notices.",
                    "I keep telling them: underground pop. It's catchy AND it's literally underground.",
                    "Back at the Cavern Club my bass shook dust off the ceiling every night. Health hazard. Worth it.",
                    "Yellow Sub-terrain was going to be our first single! I wrote the bassline!",
                    "Four strings, six legs, one dream. Please don't dent the cute one.",
                },
                LastWords = "Let it... beetle...",
            },
            new Member
            {
                Name = "George Harrison", File = "GeorgeHarrison", Shell = new Color(0.18f, 0.38f, 0.86f),
                Lines = new[]
                {
                    "I'm the quiet one. The lead guitar does my talking. Please stop hitting the quiet one.",
                    "I just want to play crystal sitar in peace.",
                    "Here comes the... sun? No. There's no sun down here. Never mind.",
                    "My guitar gently weeps. So do I, a bit.",
                    "Every solo I played at the Cavern Club, the stalactites wept. Well. Dripped.",
                    "You can't rush a guitar solo! ...You can rush a guitarist, apparently.",
                },
                LastWords = "All things must... pass...",
            },
            new Member
            {
                Name = "Scarab Starr", File = "ScarabStarr", Shell = new Color(0.22f, 0.66f, 0.32f),
                Lines = new[]
                {
                    "Oi! I'm the drummer! Hitting things is MY job!",
                    "Ba-dum-TSS! ...That one was on your head, sorry.",
                    "Stone-age rock! Boulders for drums! Nobody's done it since the actual Stone Age!",
                    "I get by with a little help from my friends! LADS! HELP!",
                    "Four on the floor! Well. Six. I've got six feet.",
                    "They put me at the back of the Cavern Club, behind a stalagmite. Best seat in the house.",
                },
                LastWords = "Peace and... love... ow.",
            },
        };

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Crystal Caves Enemies/Beetles/Build Prefabs")]
        public static void BuildPrefabsMenu()
        {
            BuildPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("CAVE_BEETLE_PREFABS_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Beetles/Create Test Scene")]
        public static void CreateTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log(ScenePath + " already exists; skipped (edit it in Unity, or delete it to rebuild).");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (Member member in Members)
                if (CaveEnemyKit.Load(member.PrefabPath) == null) { BuildPrefabs(); break; }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform room = CaveEnemyKit.BuildCaveRoom("Beetles Test Cave", 64f, 76f, 14f);
            Vector3 centre = new Vector3(0f, 0f, 6f);
            const float zone = 24f;

            // Like the level's beetle zone: flat and empty, ringed by purple and pink crystals.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + 8f;
                Vector3 at = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (zone + 2.5f);
                if (Mathf.Abs(at.x) > 30f || at.z > 36f || at.z < -36f) continue;
                CaveEnemyKit.PlaceCrystalBatch(room, at, angle + 180f, 0.8f + 0.15f * (i % 3), i % 2 == 0 ? CrystalColor.Purple : CrystalColor.Pink);
            }

            var bandObject = new GameObject("The Beetles (band)");
            bandObject.transform.position = centre;
            var band = bandObject.AddComponent<BeetleBand>();
            var beetles = new CaveBeetle[Members.Length];
            Vector3[] spots = { new Vector3(-4f, 0f, 4f), new Vector3(4f, 0f, 4f), new Vector3(-4f, 0f, -4f), new Vector3(4f, 0f, -4f) };
            for (int i = 0; i < Members.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(CaveEnemyKit.Load(Members[i].PrefabPath), scene);
                item.name = Members[i].Name;
                item.transform.SetPositionAndRotation(centre + spots[i], Quaternion.Euler(0f, 180f, 0f));
                beetles[i] = item.GetComponent<CaveBeetle>();
            }
            var data = new SerializedObject(band);
            SerializedProperty list = data.FindProperty("members");
            list.arraySize = beetles.Length;
            for (int i = 0; i < beetles.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = beetles[i];
            data.FindProperty("zoneRadius").floatValue = zone;
            data.FindProperty("ringMaterial").objectReferenceValue = RingMaterial();
            SetLines(data.FindProperty("openingLines"), "An audience! Just like the Cavern Club! Places, everyone!", "Our first gig since the Cavern Club! Somebody check the mics!");
            SetLines(data.FindProperty("boxInLines"), "From the top, lads! Five, six, seven, EIGHT!", "Box formation! Like we rehearsed!", "Big finish! Everybody in!", "Harmonies, boys! Close ranks!");
            SetLines(data.FindProperty("dodgedLines"), "OW! Who was on tempo?!", "That's why we need a manager!", "My SHELL! My beautiful shell!", "Who choreographed this?!");
            SetLines(data.FindProperty("landedLines"), "Nailed the landing! Encore!", "And THAT'S how you hit the big note!", "Did you see that? We were tight!");
            SetLines(data.FindProperty("memberDownLines"), "We're down a member! The show must go on!", "Keep playing! Keep PLAYING!", "No! The band's breaking up!");
            data.ApplyModifiedPropertiesWithoutUndo();

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -30f), 0f);
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("CAVE_BEETLES_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Beetles/Validate")]
        public static void Validate()
        {
            foreach (Member member in Members)
            {
                GameObject prefab = CaveEnemyKit.Load(member.PrefabPath);
                Require(prefab != null, member.PrefabPath + " is missing (Build Prefabs).");
                var beetle = prefab.GetComponent<CaveBeetle>();
                Require(beetle != null && prefab.GetComponent<ScrapDropper>() == null, member.Name + " needs CaveBeetle and no ScrapDropper (the last beetle drops the band's one loot pile).");
                var data = new SerializedObject(beetle);
                Require(data.FindProperty("displayName").stringValue == member.Name, member.Name + ": wrong display name.");
                Require(data.FindProperty("partName").stringValue == "Signal amplifier", member.Name + ": the band's part is the signal amplifier.");
                foreach (string field in new[] { "visual", "clipTable", "animator", "shipPart", "crystalMaterial", "belly", "bodyHitbox", "markerMaterial", "shadowMaterial", "spikeMesh" })
                    Require(data.FindProperty(field).objectReferenceValue != null, member.Name + ": " + field + " is not set.");
                Require(data.FindProperty("phaseAt").arraySize == 2 && data.FindProperty("hitLines").arraySize > 0, member.Name + " needs three phases and its lines.");
                Require(!data.FindProperty("fodderWhileEngaged").boolValue, member.Name + ": no adds, like the other mini-bosses.");
                var table = BossClipTable.Parse((TextAsset)data.FindProperty("clipTable").objectReferenceValue);
                foreach (string clip in new[] { "Idle", "Walk", "Run", "Roar", "Headbutt", "Brace", "Charge", "FlipOver", "OnBack", "FlipUp", "Stomp", "Dive", "Stagger", "Death" })
                    Require(table.Has(clip), "CaveBeetleClips.json has no " + clip + " clip.");
                Require(!prefab.GetComponent<NavMeshAgent>().enabled, member.Name + ": the NavMeshAgent must start disabled.");
                Require(prefab.GetComponent<EnemySignalSource>() != null, member.Name + " needs an EnemySignalSource.");
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        Require(m != null, member.Name + " has a missing material on " + r.name + ".");
                Require(prefab.GetComponentInChildren<SignalLost.Enemies.Desert.JerryCanBeetle>() == null, member.Name + " must not carry the Jerry-Can Beetle's bomb behaviour.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<CaveBeetle>() == 4 && Count<BeetleBand>() == 1, ScenePath + ": expected the four beetles and their band.");
            var band = new SerializedObject(All<BeetleBand>()[0]);
            Require(band.FindProperty("members").arraySize == 4 && band.FindProperty("ringMaterial").objectReferenceValue != null, ScenePath + ": the band needs its four members and ring material.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("CAVE_BEETLES_VALIDATED");
        }

        private static void SetLines(SerializedProperty property, params string[] lines)
        {
            property.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) property.GetArrayElementAtIndex(i).stringValue = lines[i];
        }

        private static Material RingMaterial()
        {
            string path = CaveEnemyKit.MaterialFolder + "/BeetleBoxInRing.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "BeetleBoxInRing" };
            material.SetColor("_BaseColor", new Color(1f, 0.72f, 0.15f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------- Model ----------

        internal static void BuildPrefabs()
        {
            CaveEnemyKit.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(ShardMoleBuilder.Root)) AssetDatabase.CreateFolder(CaveEnemyKit.PrefabFolder, "Bosses");
            foreach (Member member in Members) BuildBeetle(member);
        }

        // One Blender model and clip set (tools/blender/cave_beetle.py) for all four; only the shell, belly and crystal
        // colours differ. About 2.4 m long and 1.3 m to the top of the shell.
        private static GameObject BuildBeetle(Member member)
        {
            GameObject root = DesertDuelBossBuilder.Begin("CaveBeetle", out Transform model, out BossClipTable table, out _, ShardMoleBuilder.Root);
            root.name = member.Name;
            Transform head = DesertDuelBossBuilder.FindDeep(model, "Head"), body = DesertDuelBossBuilder.FindDeep(model, "Body");
            if (head.position.z < body.position.z) model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation; // face +Z

            string key = member.File;
            Material growth = CaveEnemyKit.Glowing("Beetle" + key + "Crystal", Color.Lerp(member.Shell, Color.white, 0.25f), 1.2f);
            var swap = new Dictionary<string, Material>
            {
                ["CaveBeetleShell"] = CaveEnemyKit.Lit("Beetle" + key + "Shell", member.Shell, 0.3f, 0.82f),
                ["CaveBeetleBelly"] = CaveEnemyKit.Lit("Beetle" + key + "Underside", member.Shell * 0.45f + new Color(0.1f, 0.08f, 0.07f, 0.55f), 0f, 0.35f),
                ["CaveBeetleCrystal"] = growth,
            };
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] list = skin.sharedMaterials;
                for (int i = 0; i < list.Length; i++)
                    if (list[i] != null && swap.TryGetValue(list[i].name, out Material mine)) list[i] = mine;
                skin.sharedMaterials = list;
            }

            var beetle = root.AddComponent<CaveBeetle>();
            // The belly: tucked inside the shell's hitbox while upright, on top when it is flipped onto its back.
            Health belly = DesertDuelBossBuilder.WeakSpot(model, "Body", null, 0f, 0.75f, member.Name + " (belly)", Vector3.down * 0.45f);
            DesertDuelBossBuilder.Hitboxes(model, new[] { new Hitbox("Body", 0.9f), new Hitbox("Head", 0.5f) });
            Collider shellHitbox = body.Find("Hitbox Body").GetComponent<Collider>();

            // The signal amplifier rides hidden in its mic pack; only the last beetle standing drops it.
            Transform part = DesertDuelBossBuilder.HiddenPart(root, model, "MicPack", "Signal amplifier", go =>
            {
                Material metal = CaveEnemyKit.Lit("BeetleAmplifierMetal", new Color(0.55f, 0.56f, 0.6f), 0.7f, 0.5f);
                Material light = CaveEnemyKit.Glowing("BeetleAmplifierLight", new Color(0.3f, 0.9f, 1f), 2f);
                Mesh box = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("BeetleAmplifierBox", 4, 1f));
                Mesh rod = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("ShardMoleRod", 4, 0.5f));
                Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
                DesertDuelBossBuilder.AddMesh(go, box, metal, Vector3.zero, Quaternion.Euler(0f, 45f, 0f), new Vector3(0.45f, 0.3f, 0.45f));
                DesertDuelBossBuilder.AddMesh(go, rod, metal, new Vector3(0.12f, 0.15f, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.5f));
                DesertDuelBossBuilder.AddMesh(go, gem, light, new Vector3(0.12f, 0.65f, 0f), Quaternion.identity, Vector3.one * 0.06f);
                DesertDuelBossBuilder.AddMesh(go, gem, light, new Vector3(-0.1f, 0.16f, 0.1f), Quaternion.identity, Vector3.one * 0.05f);
            });

            var data = DesertDuelBossBuilder.Common(beetle, root, model, table, member.Name, "Signal amplifier", part, 400f, 2.6f, 6.5f,
                new[] { 0.66f, 0.33f }, new[] { "Rehearsal", "Performance", "Encore" }, new[] { "Head" }, 0.95f, ShardMoleBuilder.Root);
            var agent = root.GetComponent<NavMeshAgent>();
            agent.height = 1.4f;
            agent.angularSpeed = 540f;
            data.FindProperty("arenaRadius").floatValue = 24f;
            data.FindProperty("patrolRadius").floatValue = 3.5f;
            data.FindProperty("patrolSpeed").floatValue = 1.6f;
            data.FindProperty("poise").floatValue = 100f;
            data.FindProperty("stalkDistance").floatValue = 6f;
            data.FindProperty("turnSpeed").floatValue = 220f;
            data.FindProperty("markerMaterial").objectReferenceValue = RingMaterial();
            data.FindProperty("crystalMaterial").objectReferenceValue = growth;
            data.FindProperty("belly").objectReferenceValue = belly;
            data.FindProperty("bodyHitbox").objectReferenceValue = shellHitbox;
            SetLines(data.FindProperty("hitLines"), member.Lines);
            data.FindProperty("lastWords").stringValue = member.LastWords;
            data.ApplyModifiedPropertiesWithoutUndo();
            return DesertDuelBossBuilder.Save(root, member.PrefabPath);
        }
    }
}
