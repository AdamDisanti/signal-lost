using System.Collections.Generic;
using System.IO;
using System.Linq;
using SignalLost.Finale;
using SignalLost.NPC;
using SignalLost.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>
    /// Puts the capybara Cavendish (Lawson's model, Prefabs/NPC/Cavendish) into every scene by rebuilding the prefab the
    /// scenes and builders already use, GrayboxProps/CapybaraAstronaut.prefab, around the new model (same GUID, so every
    /// scene, builder and the finale's pilot pick him up). CavendishPose turns the old "seated" drop into lying down.
    /// Menu: Signal Lost > NPC.
    /// </summary>
    internal static class CavendishEverywhere
    {
        public const string PrefabPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string SandboxComparison = "Cavendish (Capybara)";

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Tutorial/Tutorial Demo.unity",
            "Assets/Scenes/Large Areas/DesertDunes.unity",
            "Assets/Scenes/Large Areas/CrystalCaves.unity",
            "Assets/Scenes/Large Areas/Mudlands.unity",
            "Assets/Scenes/Large Areas/Wasteland.unity",
            "Assets/Scenes/Testing Sandbox.unity",
        };

        private static string ShotFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CavendishShots");

        // ---------------------------------------------------------------- putting him everywhere

        [MenuItem("Signal Lost/NPC/Put New Cavendish In Every Scene")]
        public static void PutEverywhere()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string original = SceneManager.GetActiveScene().path;

            // 1. Remember how far each scene lowered his old model (how he is seated), keyed by his place in the hierarchy.
            var seated = new Dictionary<string, List<(string path, Vector3 visual)>>();
            foreach (string path in Scenes.Where(Exists))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                seated[path] = Instances().Select(c => (HierarchyPath(c.transform), Visual(c) != null ? Visual(c).localPosition : Vector3.zero)).ToList();
            }

            // 2. Rebuild the prefab around the new model.
            BuildPrefab();

            // 3. Put each seat drop back on the new Visual, tidy each scene and save it.
            int count = 0;
            foreach (string path in Scenes.Where(Exists))
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject cavendish in Instances())
                {
                    string where = HierarchyPath(cavendish.transform);
                    var match = seated[path].FirstOrDefault(s => s.path == where);
                    Transform visual = Visual(cavendish);
                    if (visual != null && match.path != null && match.visual.y < -0.01f)
                        visual.localPosition = new Vector3(0f, match.visual.y, 0f);
                    // The story scenes' speech-bubble anchor: just over his helmet (the pose keeps it there in play).
                    Transform voice = cavendish.transform.Find("Cavendish Dialogue");
                    Transform anchor = cavendish.transform.Find("DialogueAnchor");
                    if (voice != null && anchor != null) voice.localPosition = anchor.localPosition;
                    count++;
                }
                // The sandbox had Lawson's model beside the old one to compare; now they're the same, so keep one.
                foreach (GameObject root in scene.GetRootGameObjects().Where(g => g.name == SandboxComparison).ToArray())
                    Object.DestroyImmediate(root);
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                            PrefabUtility.RemoveUnusedOverrides(new[] { t.gameObject }, InteractionMode.AutomatedAction);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            Debug.Log($"CAVENDISH_EVERYWHERE: {count} Cavendish(es) in {Scenes.Count(Exists)} scenes now use the capybara model.");
            Shots(); // pictures of the result, to check every scene at a glance
        }

        /// <summary>Rebuilds CapybaraAstronaut.prefab: the new model under Visual/Model, his idle animation and pose.</summary>
        public static void BuildPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CavendishCapybaraBuilder.PrefabPath);
            if (source == null)
            {
                CavendishCapybaraBuilder.Build();
                source = AssetDatabase.LoadAssetAtPath<GameObject>(CavendishCapybaraBuilder.PrefabPath);
            }
            // Readable meshes, so the desert outfit can find the top of his head.
            var importer = AssetImporter.GetAtPath("Assets/Prefabs/NPC/Cavendish/Models/CavendishCapybara.fbx") as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            Transform sourceModel = source.transform.Find("Model");
            if (sourceModel == null) throw new System.Exception("CavendishCapybara.prefab has no Model.");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform anchor = root.transform.Find("DialogueAnchor");
                if (anchor == null)
                {
                    anchor = new GameObject("DialogueAnchor").transform;
                    anchor.SetParent(root.transform, false);
                }
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.transform.GetChild(i);
                    if (child != anchor) Object.DestroyImmediate(child.gameObject);
                }
                foreach (Component old in root.GetComponents<Component>())
                    if (old is Collider || old is CavendishIdle || old is CavendishPose) Object.DestroyImmediate(old);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                GameObject model = Object.Instantiate(sourceModel.gameObject, visual, false);
                model.name = "Model";

                Transform Part(string name) =>
                    model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) ?? throw new System.Exception("The Cavendish model has no " + name + ".");
                Transform head = Part("CavendishHead");
                var idle = new SerializedObject(root.AddComponent<CavendishIdle>());
                idle.FindProperty("head").objectReferenceValue = head;
                idle.FindProperty("body").objectReferenceValue = Part("CavendishBody");
                idle.FindProperty("antennaLight").objectReferenceValue = Part("CavendishAntennaLight");
                SerializedProperty ears = idle.FindProperty("ears"), eyes = idle.FindProperty("eyes");
                ears.arraySize = eyes.arraySize = 2;
                ears.GetArrayElementAtIndex(0).objectReferenceValue = Part("CavendishEarL");
                ears.GetArrayElementAtIndex(1).objectReferenceValue = Part("CavendishEarR");
                eyes.GetArrayElementAtIndex(0).objectReferenceValue = Part("CavendishEyeL");
                eyes.GetArrayElementAtIndex(1).objectReferenceValue = Part("CavendishEyeR");
                idle.ApplyModifiedPropertiesWithoutUndo();

                Bounds all = MeshBounds(root.transform, model.transform);
                Bounds helmet = MeshBounds(root.transform, Part("CavendishHelmet"));
                var box = root.AddComponent<BoxCollider>();
                box.center = all.center;
                box.size = all.size;
                Vector3 bubble = new Vector3(0f, all.max.y + 0.3f, helmet.center.z);
                anchor.localPosition = bubble;

                var pose = new SerializedObject(root.AddComponent<CavendishPose>());
                pose.FindProperty("visual").objectReferenceValue = visual;
                pose.FindProperty("model").objectReferenceValue = model.transform;
                pose.FindProperty("reach").floatValue = all.max.z;
                pose.FindProperty("dialoguePoint").vector3Value = bubble;
                pose.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"CAVENDISH_PREFAB_REBUILT: {all.size.x:0.00} x {all.size.y:0.00} x {all.size.z:0.00} m, face {all.max.z:0.00} m ahead of his root.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- check shots

        /// <summary>
        /// Renders every Cavendish in every scene (posed as in play), him on the boat and in the finale mech's cockpit, to
        /// signal-lost/Logs/CavendishShots. Nothing is saved to the scenes.
        /// </summary>
        [MenuItem("Signal Lost/NPC/Cavendish Check Shots")]
        public static void Shots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string original = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory(ShotFolder);
            foreach (string old in Directory.GetFiles(ShotFolder, "*.png")) File.Delete(old);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            int shots = 0;
            foreach (string path in Scenes.Where(Exists))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string tag = Path.GetFileNameWithoutExtension(path).Replace(" ", "");
                int n = 0;
                foreach (GameObject cavendish in Instances())
                {
                    n++;
                    CavendishPose pose = cavendish.GetComponent<CavendishPose>();
                    if (pose != null) pose.ApplyNow();
                    Physics.SyncTransforms();
                    Transform t = cavendish.transform;
                    Bounds b = RenderBounds(cavendish);
                    shots += Shot($"{tag}_{n}_front", b.center + t.rotation * new Vector3(1.6f, 0.9f, 3.2f), b.center, 40f);
                    shots += Shot($"{tag}_{n}_side", b.center + t.rotation * new Vector3(4f, 0.8f, 0.2f), b.center, 40f);
                    shots += Shot($"{tag}_{n}_wide", b.center + t.rotation * new Vector3(-3f, 6f, 9f), b.center, 55f);
                    shots += Shot($"{tag}_{n}_back", b.center + t.rotation * new Vector3(-2.5f, 2.5f, -4f), b.center, 45f);
                }

                // On the boat: how the voyage seats him (lowered by the old seat drop, parented to the stern thwart).
                DockBoat boat = Object.FindObjectsByType<DockBoat>(FindObjectsInactive.Exclude).FirstOrDefault();
                if (boat != null && prefab != null)
                {
                    var data = new SerializedObject(boat);
                    var seat = data.FindProperty("cavendishSeat").objectReferenceValue as Transform;
                    var helm = data.FindProperty("helm").objectReferenceValue as Transform;
                    if (seat != null)
                    {
                        var rider = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        rider.transform.SetParent(seat, false);
                        IslandCamp.SetCavendishDrop(rider.transform, IslandCamp.CavendishSitDrop);
                        rider.GetComponent<CavendishPose>()?.ApplyNow();
                        if (helm != null)
                        {
                            GameObject mechanic = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                            mechanic.name = "Mechanic stand-in";
                            mechanic.transform.SetPositionAndRotation(helm.position + Vector3.up * 0.9f, helm.rotation);
                            mechanic.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                        }
                        Physics.SyncTransforms();
                        Bounds b = RenderBounds(rider);
                        Transform bt = boat.transform;
                        shots += Shot($"{tag}_boat_side", b.center + bt.rotation * new Vector3(6f, 2.5f, 0f), b.center, 45f);
                        shots += Shot($"{tag}_boat_top", b.center + bt.rotation * new Vector3(1.5f, 7f, -3f), b.center, 55f);
                    }
                }

                // The finale: the mech's cockpit with him as the pilot.
                CavendishMech mech = Object.FindObjectsByType<CavendishMech>(FindObjectsInactive.Include).FirstOrDefault();
                if (mech != null && prefab != null)
                {
                    var holder = new GameObject("Mech preview");
                    holder.transform.position = mech.transform.position + Vector3.right * 40f;
                    MechRig rig = MechModel.Build(holder.transform, prefab);
                    Physics.SyncTransforms();
                    if (rig.Cockpit != null)
                    {
                        Vector3 c = rig.Cockpit.position;
                        shots += Shot("Mech_cockpit_front", c + holder.transform.rotation * new Vector3(1.2f, 0.6f, 4.5f), c + Vector3.up * 0.1f, 40f);
                        shots += Shot("Mech_cockpit_side", c + holder.transform.rotation * new Vector3(4.5f, 0.6f, 0.8f), c + Vector3.up * 0.1f, 40f);
                        shots += Shot("Mech_whole", holder.transform.position + holder.transform.rotation * new Vector3(10f, 7f, 22f), holder.transform.position + Vector3.up * 7f, 45f);
                    }
                }
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            Debug.Log($"CAVENDISH_SHOTS: {shots} pictures in {ShotFolder}");
        }

        private static int Shot(string name, Vector3 from, Vector3 at, float fov)
        {
            var go = new GameObject("Check Camera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 600f;
                var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                File.WriteAllBytes(Path.Combine(ShotFolder, name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
                return 1;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Runs the check shots once after a compile when signal-lost/Logs/cavendish_shots.request exists (so they can be
        // asked for without a menu click), then deletes the request.
        [InitializeOnLoadMethod]
        private static void ShotsOnRequest()
        {
            string request = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "cavendish_shots.request");
            if (!File.Exists(request) || Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(request);
                if (SceneManager.GetActiveScene().isDirty)
                {
                    Debug.LogWarning("Cavendish check shots skipped: save the open scene first, then use Signal Lost > NPC > Cavendish Check Shots.");
                    return;
                }
                Shots();
            };
        }

        // ---------------------------------------------------------------- helpers

        private static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

        /// <summary>Every placed Cavendish (an instance of the prefab) in the open scene.</summary>
        private static List<GameObject> Instances()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var found = new List<GameObject>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == prefab)
                        found.Add(t.gameObject);
            return found;
        }

        private static Transform Visual(GameObject cavendish) => cavendish.transform.Find("Visual");

        private static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Insert(0, t.name + "#" + t.GetSiblingIndex());
            return string.Join("/", parts);
        }

        private static Bounds MeshBounds(Transform space, Transform root)
        {
            Bounds? total = null;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = space.InverseTransformPoint(filter.transform.TransformPoint(v));
                    if (total == null) total = new Bounds(p, Vector3.zero);
                    else { Bounds b = total.Value; b.Encapsulate(p); total = b; }
                }
            }
            return total ?? new Bounds();
        }

        private static Bounds RenderBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
