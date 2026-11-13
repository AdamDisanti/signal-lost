using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using CrystalColor = SignalLost.Editor.CrystalAssetBuilder.CrystalColor;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the four Crystal Caves beetle mini-bosses and their test scene.
    ///
    /// The model starts from the Jerry-Can Beetle (same round shell, head and big eyes) without the fuel can, its
    /// straps or its explosion: it is bigger, glossier, has small crystal growths on its shoulders, short stubby legs
    /// tucked under the shell and no antennae or mandibles (nothing leggy or creepy), a microphone pack strapped to its
    /// back and a little headset mic by its mouth. The four are identical except for their colour.
    ///
    /// Files: Prefabs/Enemies/Caves/{BugLennon, PaulArthropodney, GeorgeHarrison, ScarabStarr}.prefab (rebuilt in place,
    /// same GUIDs), meshes in Prefabs/Enemies/Caves/Meshes, materials in Materials/Enemies/Caves, and
    /// Scenes/Bosses/CaveBeetlesTest.unity. Menu: Signal Lost > Crystal Caves Enemies > Beetles.
    /// </summary>
    public static class CaveBeetleBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bosses/CaveBeetlesTest.unity";
        private const float Size = 1.9f; // times the Jerry-Can Beetle

        private sealed class Member
        {
            public string Name, File;
            public Color Shell;
            public string[] Lines;
            public string LastWords;
            public string PrefabPath => CaveEnemyKit.PrefabFolder + "/" + File + ".prefab";
        }

        // Their dreams of being a band, a line per hit. Cave genres all round: rock and roll, underground pop, heavy
        // metal, stone-age rock, crystal-clear vocals.
        private static readonly Member[] Members =
        {
            new Member
            {
                Name = "Bug Lennon", File = "BugLennon", Shell = new Color(0.78f, 0.14f, 0.12f),
                Lines = new[]
                {
                    "Oi! Do you know who I am? Future frontman of the biggest band under the mountain!",
                    "We're going to be bigger than the cave! ...Well. It's a very big cave.",
                    "Rock and roll, mate. Emphasis on the ROCK. We've got loads of it.",
                    "Imagine it: a world tour. Every cave. Every tunnel. Every crevice.",
                    "Mind the headset! That's our only good microphone!",
                    "Crystal-clear vocals. That's the sound. That's the whole brand.",
                },
                LastWords = "Tell the fans... it was all... for the rock...",
            },
            new Member
            {
                Name = "Paul Arthropodney", File = "PaulArthropodney", Shell = new Color(0.94f, 0.74f, 0.16f),
                Lines = new[]
                {
                    "Careful! This shell is a precision instrument!",
                    "I keep telling them: underground pop. It's catchy AND it's literally underground.",
                    "I wrote a ballad about a stalactite. It really hangs with you.",
                    "Listen to that echo! Free reverb, every single night!",
                    "Yellow Sub-terrain was going to be our first single!",
                    "Every great band needs a cute one. Please don't dent the cute one.",
                },
                LastWords = "Let it... beetle...",
            },
            new Member
            {
                Name = "George Harrison", File = "GeorgeHarrison", Shell = new Color(0.18f, 0.38f, 0.86f),
                Lines = new[]
                {
                    "I'm the quiet one. Please stop hitting the quiet one.",
                    "I just want to play crystal sitar in peace.",
                    "Here comes the... sun? No. There's no sun down here. Never mind.",
                    "My guitar gently weeps. So do I, a bit.",
                    "We could play cave-rn clubs. Small venues. Very exclusive. Very damp.",
                    "Heavy metal? We're surrounded by it. It's in the walls.",
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
                    "Every band needs a beat. I AM the beat.",
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
            Vector3[] spots = { new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f), new Vector3(-3f, 0f, -3f), new Vector3(3f, 0f, -3f) };
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
            SetLines(data.FindProperty("openingLines"), "An audience! Places, everyone!", "Our first gig! Somebody check the mics!");
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
                Require(beetle != null && prefab.GetComponent<ScrapDropper>() != null, member.Name + " needs CaveBeetle and a ScrapDropper.");
                var data = new SerializedObject(beetle);
                Require(data.FindProperty("displayName").stringValue == member.Name, member.Name + ": wrong display name.");
                Require(data.FindProperty("shipPart").objectReferenceValue == null, member.Name + ": the beetles drop scrap, not a ship part.");
                foreach (string field in new[] { "visual", "body", "micLight" })
                    Require(data.FindProperty(field).objectReferenceValue != null, member.Name + ": " + field + " is not set.");
                Require(data.FindProperty("legs").arraySize == 6 && data.FindProperty("crystals").arraySize > 0 && data.FindProperty("hitLines").arraySize > 0,
                    member.Name + " needs its legs, crystals and lines.");
                Require(!prefab.GetComponent<NavMeshAgent>().enabled, member.Name + ": the NavMeshAgent must start disabled.");
                Require(prefab.transform.Find("Visual/Body/Mic Pack") != null && prefab.transform.Find("Visual/Body/Headset") != null, member.Name + " needs its mic pack and headset.");
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
            foreach (Member member in Members) BuildBeetle(member);
        }

        // About 2.3 m long and 1.3 m tall to the top of the shell.
        private static GameObject BuildBeetle(Member member)
        {
            Mesh blob = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveBlob", 1));
            Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
            Mesh limb = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("CaveLimb", 5, 0.6f));
            Mesh box = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("CaveBox", 4, 1f));
            var band = new Vector3[11];
            for (int i = 0; i < band.Length; i++)
            {
                float a = Mathf.PI * i / (band.Length - 1); // half hoop over the head
                band[i] = new Vector3(-0.2f * Mathf.Cos(a), 0.17f * Mathf.Sin(a), 0f);
            }
            Mesh headband = CaveEnemyKit.SaveMesh(LowPolyMesh.Tube("BeetleHeadband", band, 0.012f, 4));
            Mesh boom = CaveEnemyKit.SaveMesh(LowPolyMesh.Tube("BeetleMicBoom", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0.02f, -0.05f, 0.08f), new Vector3(0f, -0.08f, 0.17f), new Vector3(-0.07f, -0.09f, 0.22f),
            }, 0.01f, 4));

            string key = member.File;
            Material shell = CaveEnemyKit.Lit("Beetle" + key + "Shell", member.Shell, 0.3f, 0.82f);
            Material under = CaveEnemyKit.Lit("Beetle" + key + "Underside", member.Shell * 0.45f + new Color(0.1f, 0.08f, 0.07f, 0.55f), 0f, 0.35f);
            Material growth = CaveEnemyKit.Glowing("Beetle" + key + "Crystal", Color.Lerp(member.Shell, Color.white, 0.25f), 1.2f);
            Material legMat = CaveEnemyKit.Lit("BeetleLegDark", new Color(0.12f, 0.11f, 0.1f), 0.2f, 0.45f);
            Material eye = CaveEnemyKit.Lit("BeetleEyeWhite", new Color(0.95f, 0.93f, 0.86f), 0f, 0.85f);
            Material pupil = CaveEnemyKit.Lit("BeetlePupil", new Color(0.03f, 0.03f, 0.04f), 0f, 0.9f);
            Material pack = CaveEnemyKit.Lit("BeetleMicPack", new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.55f);
            Material strap = CaveEnemyKit.Lit("BeetleMicStrap", new Color(0.1f, 0.1f, 0.11f), 0f, 0.25f);
            Material foam = CaveEnemyKit.Lit("BeetleMicFoam", new Color(0.06f, 0.06f, 0.07f), 0f, 0.1f);
            Material led = CaveEnemyKit.Glowing("BeetleMicLight", new Color(0.2f, 1f, 0.35f), 2f);

            var root = new GameObject(member.Name);
            Transform visual = Empty("Visual", root.transform, Vector3.zero);
            visual.localScale = Vector3.one * Size;
            // Everything that rears up: shell, head, mic pack and headset. Legs stay on the ground.
            Transform body = Empty("Body", visual, new Vector3(0f, 0.24f, -0.1f));
            Vector3 b = -body.localPosition; // positions below are in the old beetle's model space

            Part("Carapace", body, blob, shell, b + new Vector3(0f, 0.38f, -0.04f), Quaternion.identity, new Vector3(0.44f, 0.29f, 0.5f));
            Part("Underside", body, blob, under, b + new Vector3(0f, 0.25f, 0f), Quaternion.identity, new Vector3(0.38f, 0.15f, 0.44f));
            Part("Wing Seam", body, box, legMat, b + new Vector3(0f, 0.665f, -0.05f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.012f, 0.012f, 0.42f));
            Part("Pronotum", body, blob, shell, b + new Vector3(0f, 0.36f, 0.36f), Quaternion.identity, new Vector3(0.3f, 0.2f, 0.16f));
            Transform head = Part("Head", body, blob, shell, b + new Vector3(0f, 0.32f, 0.5f), Quaternion.identity, new Vector3(0.21f, 0.16f, 0.17f));
            // Big friendly eyes, no antennae or mandibles.
            for (int s = -1; s <= 1; s += 2)
            {
                Transform socket = Part("Eye", head, blob, eye, new Vector3(0.5f * s, 0.4f, 0.6f), Quaternion.identity, Vector3.one * 0.42f);
                Part("Pupil", socket, gem, pupil, new Vector3(0f, 0.05f, 0.72f), Quaternion.identity, Vector3.one * 0.5f);
            }

            // Crystal growths on the shoulders: they belong in the caves.
            var crystals = new List<Renderer>();
            Mesh shard = CaveEnemyKit.CrystalMesh("CrystalShard"), spire = CaveEnemyKit.CrystalMesh("CrystalSpire");
            crystals.Add(Part("Crystal L", body, shard, growth, b + new Vector3(-0.3f, 0.5f, 0.24f), Quaternion.Euler(10f, 30f, 28f), Vector3.one * 0.13f).GetComponent<Renderer>());
            crystals.Add(Part("Crystal R", body, shard, growth, b + new Vector3(0.3f, 0.5f, 0.22f), Quaternion.Euler(8f, -40f, -30f), Vector3.one * 0.12f).GetComponent<Renderer>());
            crystals.Add(Part("Crystal Tip L", body, spire, growth, b + new Vector3(-0.22f, 0.56f, -0.28f), Quaternion.Euler(-20f, 0f, 24f), Vector3.one * 0.07f).GetComponent<Renderer>());
            crystals.Add(Part("Crystal Tip R", body, spire, growth, b + new Vector3(0.2f, 0.57f, -0.32f), Quaternion.Euler(-24f, 0f, -20f), Vector3.one * 0.06f).GetComponent<Renderer>());

            // The microphone pack strapped to its back, with a light and a stubby aerial.
            Transform micPack = Empty("Mic Pack", body, b + new Vector3(0f, 0.66f, -0.12f));
            Part("Pack", micPack, box, pack, Vector3.zero, Quaternion.Euler(0f, 0f, 45f), new Vector3(0.11f, 0.11f, 0.16f));
            Part("Pack Face", micPack, box, strap, new Vector3(0f, 0.035f, 0f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.08f, 0.08f, 0.13f));
            Renderer micLight = Part("Light", micPack, gem, led, new Vector3(0.04f, 0.075f, 0.07f), Quaternion.identity, Vector3.one * 0.022f).GetComponent<Renderer>();
            Segment("Aerial", micPack, limb, strap, new Vector3(-0.04f, 0.06f, -0.06f), new Vector3(-0.07f, 0.16f, -0.1f), 0.008f);
            Part("Pack Strap", micPack, box, strap, new Vector3(0f, -0.06f, 0f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.36f, 0.36f, 0.04f));

            // Headset: a band over the head and a little boom mic curving to its mouth.
            Transform headset = Empty("Headset", body, b + new Vector3(0f, 0.36f, 0.52f));
            Part("Band", headset, headband, strap, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Ear L", headset, blob, pack, new Vector3(-0.2f, 0f, 0f), Quaternion.identity, new Vector3(0.025f, 0.045f, 0.045f));
            Part("Ear R", headset, blob, pack, new Vector3(0.2f, 0f, 0f), Quaternion.identity, new Vector3(0.025f, 0.045f, 0.045f));
            Part("Boom", headset, boom, strap, new Vector3(0.2f, 0f, 0f), Quaternion.identity, Vector3.one);
            Part("Mic", headset, blob, foam, new Vector3(0.13f, -0.09f, 0.22f), Quaternion.identity, Vector3.one * 0.03f);

            // Six short, stubby legs mostly hidden under the shell.
            var legs = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                float z = 0.2f - (i % 3) * 0.2f;
                Transform hip = Empty("Leg " + (i + 1), visual, new Vector3(0.28f * side, 0.2f, z));
                Vector3 foot = new Vector3(0.13f * side, -0.2f, (1 - i % 3) * 0.04f);
                Segment("Leg", hip, limb, legMat, Vector3.zero, foot, 0.045f);
                Part("Foot", hip, blob, legMat, foot, Quaternion.identity, new Vector3(0.04f, 0.025f, 0.05f));
                legs[i] = hip;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.center = new Vector3(0f, 0.42f, 0.08f) * Size;
            collider.radius = 0.43f * Size;
            collider.height = 1.25f * Size;

            var beetle = root.AddComponent<CaveBeetle>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = 0.85f;
            agent.height = 1.3f;
            agent.speed = 3.6f;
            agent.acceleration = 16f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.3f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 220f;
            health.ApplyModifiedPropertiesWithoutUndo();
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 3, 5), (ScrapType.Wiring, 1, 2), (ScrapType.CircuitBoards, 1, 2));

            var data = new SerializedObject(beetle);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("displayName").stringValue = member.Name;
            data.FindProperty("arenaRadius").floatValue = 24f;
            data.FindProperty("patrolRadius").floatValue = 3.5f;
            data.FindProperty("patrolSpeed").floatValue = 1.6f;
            data.FindProperty("body").objectReferenceValue = body;
            data.FindProperty("micLight").objectReferenceValue = micLight;
            SetLines(data.FindProperty("hitLines"), member.Lines);
            data.FindProperty("lastWords").stringValue = member.LastWords;
            SerializedProperty crystalList = data.FindProperty("crystals");
            crystalList.arraySize = crystals.Count;
            for (int i = 0; i < crystals.Count; i++) crystalList.GetArrayElementAtIndex(i).objectReferenceValue = crystals[i];
            SetArray(data.FindProperty("legs"), legs);
            data.ApplyModifiedPropertiesWithoutUndo();
            return CaveEnemyKit.SavePrefab(root, member.PrefabPath);
        }
    }
}
