using System;
using System.Collections.Generic;
using SignalLost.Enemies;
using SignalLost.Inventory;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the merchant crab's ten cousins, the Bucket Brothers (meeting notes 10-22): one Hermit Crab prefab
    /// variant each, with its own name, junk shell, shell and body colours, size and lines (<see cref="CrabCousin"/>).
    /// They keep the basic crab's health and moves; only the looks and the voice differ. Each drops a wire on top of
    /// the usual scrap, so beating the family pays for a first upgrade.
    /// Shells follow the bucket's convention (origin at the opening, +Z out of it, top of the shell toward -Z before
    /// it is laid down). Rebuilding replaces the assets in place (same GUIDs).
    /// Menu: Signal Lost > Enemies > Build Crab Cousins.
    /// </summary>
    internal static class CrabCousinBuilder
    {
        public const string Folder = EnemyTestBuilder.EnemyFolder + "/Cousins";
        private const string ShellFolder = Folder + "/Shells";
        private const string MeshFolder = Folder + "/Meshes";
        private const string MaterialFolder = Folder + "/Materials";

        private enum Paint { Main, Trim, Dark, Bright }

        private sealed class Cousin
        {
            public string Name, ShellName;
            public Color Body, Main, Trim, Dark, Bright;
            public float Size = 1f, ShellScale = 1f, Depth = 0.6f; // depth: how far the mouth is from the closed end
            public string[] Barks;
            public string LastWords;
            public Action<FlatMeshKit<Paint>> Shell;
        }

        /// <summary>Cousin prefab paths, in the order they stand around the merchant.</summary>
        public static IEnumerable<string> PrefabPaths
        {
            get { foreach (Cousin cousin in Family) yield return PrefabPath(cousin.Name); }
        }

        public static string PrefabPath(string name) => Folder + "/" + name + ".prefab";

        private static readonly Cousin[] Family =
        {
            new Cousin
            {
                Name = "Moe", ShellName = "PaintCan", Body = new Color(0.78f, 0.22f, 0.16f),
                Main = new Color(0.9f, 0.88f, 0.82f), Trim = new Color(0.82f, 0.13f, 0.1f), Dark = new Color(0.3f, 0.3f, 0.32f), Bright = new Color(0.82f, 0.13f, 0.1f),
                Barks = new[] { "Why, you little...!", "Spread out, ya knuckleheads!" }, LastWords = "Oh, a wise guy, eh...",
                Shell = PaintCan
            },
            new Cousin
            {
                Name = "Larry", ShellName = "TinCan", Body = new Color(0.9f, 0.5f, 0.25f),
                Main = new Color(0.72f, 0.74f, 0.77f), Trim = new Color(0.24f, 0.55f, 0.3f), Dark = new Color(0.4f, 0.42f, 0.45f), Bright = new Color(0.95f, 0.85f, 0.3f),
                Barks = new[] { "Hey Moe! He's hitting me!", "Ow! What'd I do?" }, LastWords = "I'm not even supposed to be here today.",
                Shell = TinCan
            },
            new Cousin
            {
                Name = "Curly", ShellName = "TrafficCone", Body = new Color(0.95f, 0.8f, 0.45f), ShellScale = 0.95f, Depth = 0.62f,
                Main = new Color(1f, 0.45f, 0.1f), Trim = new Color(0.96f, 0.96f, 0.94f), Dark = new Color(0.15f, 0.15f, 0.16f), Bright = new Color(1f, 0.45f, 0.1f),
                Barks = new[] { "Nyuk nyuk nyuk!", "Woob woob woob!" }, LastWords = "Soitenly... not...",
                Shell = TrafficCone
            },
            new Cousin
            {
                Name = "Shemp", ShellName = "Teapot", Body = new Color(0.62f, 0.38f, 0.66f),
                Main = new Color(0.95f, 0.95f, 0.97f), Trim = new Color(0.25f, 0.4f, 0.82f), Dark = new Color(0.25f, 0.4f, 0.82f), Bright = new Color(0.95f, 0.8f, 0.3f),
                Barks = new[] { "Eeeb eeb eeb!", "I'm a little teapot, and I'm ANGRY!" }, LastWords = "I'll be in the teapot.",
                Shell = Teapot
            },
            new Cousin
            {
                Name = "Clawdius", ShellName = "CenturionHelmet", Body = new Color(0.55f, 0.16f, 0.2f),
                Main = new Color(0.86f, 0.66f, 0.26f), Trim = new Color(0.8f, 0.1f, 0.1f), Dark = new Color(0.45f, 0.3f, 0.12f), Bright = new Color(0.8f, 0.1f, 0.1f),
                Barks = new[] { "Veni, vidi, pinchi!", "For the glory of the bucket!" }, LastWords = "Et tu, mechanic?",
                Shell = CenturionHelmet
            },
            new Cousin
            {
                Name = "Sir Pinchington", ShellName = "TopHat", Body = new Color(0.45f, 0.56f, 0.78f), ShellScale = 1.05f,
                Main = new Color(0.1f, 0.1f, 0.12f), Trim = new Color(0.6f, 0.1f, 0.18f), Dark = new Color(0.1f, 0.1f, 0.12f), Bright = new Color(0.9f, 0.78f, 0.3f),
                Barks = new[] { "I say! Have at thee!", "En garde, you ruffian!" }, LastWords = "Most unsporting. Most unsporting.",
                Shell = TopHat
            },
            new Cousin
            {
                Name = "Barnacle Bob", ShellName = "DivingHelmet", Body = new Color(0.28f, 0.6f, 0.55f),
                Main = new Color(0.76f, 0.46f, 0.25f), Trim = new Color(0.5f, 0.85f, 0.85f), Dark = new Color(0.45f, 0.28f, 0.15f), Bright = new Color(0.9f, 0.86f, 0.74f),
                Barks = new[] { "Arr! Ye'll walk the plank!", "Batten down yer hatches!" }, LastWords = "Tell me mum I loved her bucket...",
                Shell = DivingHelmet
            },
            new Cousin
            {
                Name = "Crabigail", ShellName = "FlowerPot", Body = new Color(0.95f, 0.55f, 0.66f),
                Main = new Color(0.8f, 0.42f, 0.26f), Trim = new Color(0.98f, 0.98f, 0.95f), Dark = new Color(0.3f, 0.55f, 0.22f), Bright = new Color(1f, 0.82f, 0.2f),
                Barks = new[] { "Don't you dare touch my petunias!", "Hands off the flowerpot!" }, LastWords = "My petunias...",
                Shell = FlowerPot
            },
            new Cousin
            {
                Name = "Little Steve", ShellName = "CoffeeMug", Body = new Color(0.5f, 0.76f, 0.3f), Size = 0.72f,
                Main = new Color(0.95f, 0.93f, 0.88f), Trim = new Color(0.2f, 0.45f, 0.8f), Dark = new Color(0.35f, 0.22f, 0.12f), Bright = new Color(0.9f, 0.15f, 0.2f),
                Barks = new[] { "I'm small but I'm SCRAPPY!", "Big Steve! BIG STEVE!" }, LastWords = "Tell Big Steve... he's still big.",
                Shell = CoffeeMug
            },
            new Cousin
            {
                Name = "Big Steve", ShellName = "OilDrum", Body = new Color(0.45f, 0.28f, 0.18f), Size = 1.32f, Depth = 0.62f,
                Main = new Color(0.15f, 0.3f, 0.66f), Trim = new Color(0.98f, 0.8f, 0.15f), Dark = new Color(0.1f, 0.1f, 0.12f), Bright = new Color(0.98f, 0.8f, 0.15f),
                Barks = new[] { "Who touched Little Steve?!", "Big Steve SMASH." }, LastWords = "Little Steve... I'm coming...",
                Shell = OilDrum
            }
        };

        [MenuItem("Signal Lost/Enemies/Build Crab Cousins")]
        public static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder(ShellFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            var crab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTestBuilder.CrabPrefabPath);
            if (crab == null) throw new InvalidOperationException("Missing " + EnemyTestBuilder.CrabPrefabPath);
            foreach (Cousin cousin in Family) BuildCousin(cousin, crab, BuildShell(cousin));
            AssetDatabase.SaveAssets();
            Debug.Log("CRAB_COUSINS_BUILT");
        }

        // ---------- Cousin prefab variants ----------

        private static void BuildCousin(Cousin cousin, GameObject crab, GameObject shell)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(crab);
            instance.name = cousin.Name;
            instance.transform.localScale = Vector3.one * cousin.Size;

            var rig = new SerializedObject(instance.GetComponent<HermitCrabRig>());
            rig.FindProperty("shellPrefab").objectReferenceValue = shell;
            rig.FindProperty("shellScale").floatValue = cousin.ShellScale;
            rig.ApplyModifiedPropertiesWithoutUndo();

            // Recolour the crab: body, legs and claws in the cousin's shade (eyes stay as they are).
            Material body = Lit(cousin.Name + " Body", cousin.Body, 0f, 0.25f);
            Material limb = Lit(cousin.Name + " Limb", cousin.Body * 0.8f, 0f, 0.25f);
            Material claw = Lit(cousin.Name + " Claw", Color.Lerp(cousin.Body, Color.white, 0.18f), 0f, 0.3f);
            foreach (MeshRenderer part in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                string material = part.sharedMaterial != null ? part.sharedMaterial.name : "";
                if (material == "CrabBody") part.sharedMaterial = body;
                else if (material == "CrabLimb") part.sharedMaterial = limb;
                else if (material == "CrabClaw") part.sharedMaterial = claw;
            }

            var drops = new SerializedObject(instance.GetComponent<ScrapDropper>());
            SetDrops(drops.FindProperty("drops"), (ScrapType.ScrapMetal, 2, 3), (ScrapType.Wiring, 1, 1), (ScrapType.CircuitBoards, 0, 1));
            drops.ApplyModifiedPropertiesWithoutUndo();

            CrabCousin voice = instance.GetComponent<CrabCousin>();
            if (voice == null) voice = instance.AddComponent<CrabCousin>();
            var lines = new SerializedObject(voice);
            SerializedProperty barks = lines.FindProperty("barks");
            barks.arraySize = cousin.Barks.Length;
            for (int i = 0; i < cousin.Barks.Length; i++) barks.GetArrayElementAtIndex(i).stringValue = cousin.Barks[i];
            lines.FindProperty("lastWords").stringValue = cousin.LastWords;
            lines.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath(cousin.Name));
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static void SetDrops(SerializedProperty list, params (ScrapType type, int min, int max)[] drops)
        {
            list.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("type").enumValueIndex = (int)drops[i].type;
                entry.FindPropertyRelative("min").intValue = drops[i].min;
                entry.FindPropertyRelative("max").intValue = drops[i].max;
            }
        }

        // ---------- Shells ----------

        private static GameObject BuildShell(Cousin cousin)
        {
            var kit = new FlatMeshKit<Paint>();
            cousin.Shell(kit);
            Mesh mesh = SaveMesh(kit.Build(cousin.ShellName));

            var root = new GameObject(cousin.ShellName);
            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);
            // Laid on its side like the bucket: mouth forward, tilted 15 degrees up, top of the shell upward.
            Quaternion lieDown = Quaternion.Euler(75f, 0f, 0f);
            shell.transform.localRotation = lieDown;
            shell.transform.localPosition = lieDown * new Vector3(0f, -cousin.Depth, 0f);
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                Paint paint = FlatMeshKit<Paint>.MaterialOf(mesh, i);
                Color color = paint == Paint.Main ? cousin.Main : paint == Paint.Trim ? cousin.Trim : paint == Paint.Dark ? cousin.Dark : cousin.Bright;
                bool metal = cousin.ShellName == "TinCan" || cousin.ShellName == "OilDrum" || cousin.ShellName == "DivingHelmet" || cousin.ShellName == "CenturionHelmet";
                materials[i] = Lit(cousin.ShellName + " " + paint, color, metal && paint != Paint.Trim ? 0.7f : 0f, metal ? 0.55f : 0.35f);
            }
            shell.AddComponent<MeshRenderer>().sharedMaterials = materials;
            // For use as a prop; HermitCrabRig removes it when the shell is worn.
            var box = shell.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ShellFolder + "/" + cousin.ShellName + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // The shells are built upright: closed end at y = 0, the opening at the top (y = Depth).
        // A container spun around Y: the outside profile from the base up to the rim, hollowed by the wall thickness.
        private static Mesh Vessel(string name, Vector2[] outside, float wall, int sides = 12)
        {
            var profile = new List<Vector2> { new Vector2(0f, 0f) };
            profile.AddRange(outside);
            for (int i = outside.Length - 1; i >= 0; i--)
                profile.Add(new Vector2(Mathf.Max(0.01f, outside[i].x - wall), Mathf.Max(wall, outside[i].y)));
            profile.Add(new Vector2(0f, wall));
            return LowPolyMesh.Lathe(name, profile.ToArray(), sides);
        }

        // A band (a short, thin ring) around the outside, e.g. a label or a stripe.
        private static Mesh Band(float bottomRadius, float topRadius, float bottom, float top, int sides = 12) =>
            LowPolyMesh.Lathe("Band", new[]
            {
                new Vector2(bottomRadius - 0.01f, bottom), new Vector2(bottomRadius + 0.012f, bottom),
                new Vector2(topRadius + 0.012f, top), new Vector2(topRadius - 0.01f, top)
            }, sides);

        private static Mesh Arc(Vector3 centre, float radius, float from, float to, Vector3 axisA, Vector3 axisB, float thickness)
        {
            var path = new Vector3[9];
            for (int i = 0; i < path.Length; i++)
            {
                float t = Mathf.Lerp(from, to, i / (path.Length - 1f)) * Mathf.Deg2Rad;
                path[i] = centre + (axisA * Mathf.Cos(t) + axisB * Mathf.Sin(t)) * radius;
            }
            return LowPolyMesh.Tube("Arc", path, thickness, 5);
        }

        private static void Gem(FlatMeshKit<Paint> kit, Vector3 at, float size, Paint paint) =>
            kit.Add(LowPolyMesh.Icosphere("Gem", 0), Matrix4x4.TRS(at, Quaternion.identity, Vector3.one * size), paint);

        private static void PaintCan(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Can", new[] { new Vector2(0.29f, 0f), new Vector2(0.3f, 0.03f), new Vector2(0.3f, 0.55f), new Vector2(0.315f, 0.57f), new Vector2(0.315f, 0.6f) }, 0.025f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.3f, 0.3f, 0.14f, 0.42f), Matrix4x4.identity, Paint.Trim);
            // Paint drips running back from the rim on the top side (-Z).
            float[] drips = { -40f, -15f, 5f, 30f, 55f };
            for (int i = 0; i < drips.Length; i++)
            {
                float a = (drips[i] - 90f) * Mathf.Deg2Rad, length = 0.08f + 0.05f * (i % 3);
                var at = new Vector3(Mathf.Cos(a) * 0.318f, 0.6f - length * 0.5f, Mathf.Sin(a) * 0.318f);
                kit.Box(at, new Vector3(0.035f, length, 0.02f), Quaternion.LookRotation(new Vector3(at.x, 0f, at.z)), Paint.Bright);
            }
            kit.Add(Arc(new Vector3(0f, 0.5f, 0f), 0.33f, 180f, 360f, Vector3.right, Vector3.back * 1.15f + Vector3.up * 0.15f, 0.012f), Matrix4x4.identity, Paint.Dark);
        }

        private static void TinCan(FlatMeshKit<Paint> kit)
        {
            var outside = new List<Vector2> { new Vector2(0.27f, 0f) };
            for (float y = 0.04f; y < 0.56f; y += 0.06f) { outside.Add(new Vector2(0.28f, y)); outside.Add(new Vector2(0.27f, y + 0.03f)); }
            outside.Add(new Vector2(0.285f, 0.58f));
            outside.Add(new Vector2(0.285f, 0.6f));
            kit.Add(Vessel("Tin", outside.ToArray(), 0.02f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.282f, 0.282f, 0.12f, 0.46f), Matrix4x4.identity, Paint.Trim);
            kit.Add(Band(0.29f, 0.29f, 0.26f, 0.32f), Matrix4x4.identity, Paint.Bright);
            // The ring-pull lid still hanging on at the mouth.
            kit.Box(new Vector3(0f, 0.62f, -0.2f), new Vector3(0.36f, 0.015f, 0.3f), Quaternion.Euler(-55f, 0f, 0f), Paint.Dark);
        }

        private static void TrafficCone(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Cone", new[] { new Vector2(0.06f, 0f), new Vector2(0.07f, 0.03f), new Vector2(0.32f, 0.6f), new Vector2(0.32f, 0.62f) }, 0.02f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.155f, 0.19f, 0.2f, 0.28f), Matrix4x4.identity, Paint.Trim);
            kit.Add(Band(0.215f, 0.25f, 0.34f, 0.42f), Matrix4x4.identity, Paint.Trim);
            // Square rubber foot around the mouth, built as a frame so the crab still fits through.
            for (int side = 0; side < 4; side++)
            {
                Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
                kit.Box(turn * new Vector3(0f, 0.61f, 0.38f), new Vector3(0.84f, 0.04f, 0.1f), turn, Paint.Dark);
            }
        }

        private static void Teapot(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Pot", new[] { new Vector2(0.16f, 0f), new Vector2(0.24f, 0.05f), new Vector2(0.33f, 0.18f), new Vector2(0.35f, 0.32f), new Vector2(0.31f, 0.48f), new Vector2(0.27f, 0.58f), new Vector2(0.28f, 0.6f) }, 0.025f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.345f, 0.335f, 0.24f, 0.34f), Matrix4x4.identity, Paint.Trim);
            kit.Add(LowPolyMesh.Tube("Spout", new[] { new Vector3(0.3f, 0.25f, 0f), new Vector3(0.43f, 0.3f, 0f), new Vector3(0.52f, 0.42f, 0f), new Vector3(0.56f, 0.5f, 0f) }, 0.04f, 6), Matrix4x4.identity, Paint.Main);
            kit.Add(Arc(new Vector3(-0.33f, 0.32f, 0f), 0.13f, -80f, 80f, Vector3.left, Vector3.up, 0.025f), Matrix4x4.identity, Paint.Main);
            Gem(kit, new Vector3(0f, -0.02f, 0f), 0.06f, Paint.Bright); // the lid knob, now on the back end
        }

        private static void CenturionHelmet(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Helmet", new[] { new Vector2(0.06f, 0f), new Vector2(0.18f, 0.03f), new Vector2(0.28f, 0.13f), new Vector2(0.33f, 0.3f), new Vector2(0.34f, 0.56f), new Vector2(0.36f, 0.6f) }, 0.025f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.34f, 0.355f, 0.52f, 0.58f), Matrix4x4.identity, Paint.Dark);
            // A red horsehair crest along the top (-Z), front to back.
            for (int i = 0; i < 7; i++)
            {
                float y = 0.06f + i * 0.08f;
                float r = Mathf.Lerp(0.2f, 0.36f, Mathf.Clamp01(y / 0.3f));
                kit.Box(new Vector3(0f, y, -r - 0.07f), new Vector3(0.07f, 0.09f, 0.16f + 0.02f * (i % 2)), Quaternion.identity, Paint.Trim);
            }
        }

        private static void TopHat(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Crown", new[] { new Vector2(0.25f, 0f), new Vector2(0.26f, 0.02f), new Vector2(0.24f, 0.3f), new Vector2(0.26f, 0.6f) }, 0.02f), Matrix4x4.identity, Paint.Main);
            kit.Add(LowPolyMesh.Lathe("Brim", new[] { new Vector2(0.25f, 0.6f), new Vector2(0.44f, 0.59f), new Vector2(0.46f, 0.615f), new Vector2(0.25f, 0.625f) }, 12), Matrix4x4.identity, Paint.Dark);
            kit.Add(Band(0.245f, 0.255f, 0.44f, 0.54f), Matrix4x4.identity, Paint.Trim);
            Gem(kit, new Vector3(0.21f, 0.5f, -0.14f), 0.05f, Paint.Bright); // a gold pin in the band
        }

        private static void DivingHelmet(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Dome", new[] { new Vector2(0.08f, 0f), new Vector2(0.22f, 0.04f), new Vector2(0.33f, 0.16f), new Vector2(0.37f, 0.32f), new Vector2(0.34f, 0.5f), new Vector2(0.3f, 0.57f), new Vector2(0.33f, 0.6f) }, 0.025f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.31f, 0.33f, 0.53f, 0.6f), Matrix4x4.identity, Paint.Dark);
            // Portholes on the top and both sides, then barnacles.
            foreach (Vector3 direction in new[] { Vector3.back, Vector3.left, Vector3.right })
            {
                Gem(kit, new Vector3(0f, 0.3f, 0f) + direction * 0.36f, 0.11f, Paint.Dark);
                Gem(kit, new Vector3(0f, 0.3f, 0f) + direction * 0.4f, 0.085f, Paint.Trim);
            }
            var random = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f, y = 0.08f + (float)random.NextDouble() * 0.4f;
                float r = Mathf.Lerp(0.25f, 0.37f, Mathf.Clamp01(y / 0.3f));
                Gem(kit, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), 0.04f + 0.02f * (i % 2), Paint.Bright);
            }
        }

        private static void FlowerPot(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Pot", new[] { new Vector2(0.21f, 0f), new Vector2(0.22f, 0.02f), new Vector2(0.3f, 0.48f), new Vector2(0.345f, 0.49f), new Vector2(0.345f, 0.6f) }, 0.025f), Matrix4x4.identity, Paint.Main);
            // Two daisies sprouting from the top (-Z) of the pot, as if the crab carries its garden around.
            for (int f = 0; f < 2; f++)
            {
                var stemBase = new Vector3(f == 0 ? -0.06f : 0.08f, 0.22f + f * 0.12f, -0.26f);
                Vector3 head = stemBase + new Vector3(f == 0 ? -0.06f : 0.05f, 0.02f, -0.26f + f * 0.06f);
                kit.Add(LowPolyMesh.Tube("Stem", new[] { stemBase, Vector3.Lerp(stemBase, head, 0.5f) + Vector3.up * 0.03f, head }, 0.012f, 4), Matrix4x4.identity, Paint.Dark);
                for (int p = 0; p < 6; p++)
                {
                    float a = p * Mathf.PI / 3f;
                    Gem(kit, head + new Vector3(Mathf.Cos(a) * 0.055f, Mathf.Sin(a) * 0.055f, 0f), 0.04f, Paint.Trim);
                }
                Gem(kit, head + Vector3.back * 0.01f, 0.04f, Paint.Bright);
            }
        }

        private static void CoffeeMug(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Mug", new[] { new Vector2(0.26f, 0f), new Vector2(0.27f, 0.02f), new Vector2(0.28f, 0.6f) }, 0.03f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.272f, 0.278f, 0.08f, 0.14f), Matrix4x4.identity, Paint.Trim);
            kit.Add(Band(0.278f, 0.281f, 0.5f, 0.55f), Matrix4x4.identity, Paint.Trim);
            kit.Add(Arc(new Vector3(0.28f, 0.32f, 0f), 0.14f, -90f, 90f, Vector3.down, Vector3.right, 0.03f), Matrix4x4.identity, Paint.Main);
            // A little heart on the side.
            Gem(kit, new Vector3(-0.27f, 0.36f, -0.02f), 0.05f, Paint.Bright);
            Gem(kit, new Vector3(-0.27f, 0.36f, 0.04f), 0.05f, Paint.Bright);
            Gem(kit, new Vector3(-0.28f, 0.31f, 0.01f), 0.05f, Paint.Bright);
        }

        private static void OilDrum(FlatMeshKit<Paint> kit)
        {
            kit.Add(Vessel("Drum", new[] { new Vector2(0.29f, 0f), new Vector2(0.3f, 0.02f), new Vector2(0.3f, 0.6f), new Vector2(0.31f, 0.61f), new Vector2(0.31f, 0.62f) }, 0.02f), Matrix4x4.identity, Paint.Main);
            kit.Add(Band(0.305f, 0.305f, 0.19f, 0.23f), Matrix4x4.identity, Paint.Dark);
            kit.Add(Band(0.305f, 0.305f, 0.4f, 0.44f), Matrix4x4.identity, Paint.Dark);
            kit.Add(Band(0.302f, 0.302f, 0.27f, 0.36f), Matrix4x4.identity, Paint.Trim);
            kit.Add(LowPolyMesh.Lathe("Bung", new[] { new Vector2(0.01f, -0.03f), new Vector2(0.05f, -0.03f), new Vector2(0.05f, 0.01f), new Vector2(0.01f, 0.01f) }, 6),
                Matrix4x4.Translate(new Vector3(0.15f, 0f, -0.1f)), Paint.Dark);
        }

        // ---------- Assets ----------

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string order = FlatMeshKit<Paint>.Order(mesh);
            EditorUtility.CopySerialized(mesh, existing);
            FlatMeshKit<Paint>.Remember(existing, order);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            color.a = 1f;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
