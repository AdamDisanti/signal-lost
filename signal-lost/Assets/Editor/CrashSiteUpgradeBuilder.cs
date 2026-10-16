using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>Saved-scene art upgrades. Adds no weapon, pickup, quest or exit logic.</summary>
    public static partial class CrashSiteUpgradeBuilder
    {
        public const string Root = TutorialCrashSiteBuilder.AssetRoot + "/Upgrades";
        [Serializable] private class Palette { public Entry[] materials; }
        [Serializable] private class Entry { public string name, hex; public float metallic, smoothness; }

        [MenuItem("Signal Lost/Tutorial Crash Site/Add Ship Upgrades And Laser")]
        public static void AddShipAndLaser()
        {
            Scene scene = OpenSavedScene();
            GameObject environment = GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName);
            if (environment == null) throw new InvalidOperationException("Impact-site root missing.");
            if (environment.transform.Find("Enhanced Crashed Rocket") != null)
                throw new InvalidOperationException("Ship upgrades already exist; edit the saved prefab instead.");
            Transform ship = environment.transform.Find("Crashed Ship");
            if (ship == null) throw new InvalidOperationException("Original crashed ship missing.");
            Terrain terrain = environment.GetComponentInChildren<Terrain>();
            GameObject basin = GameObject.Find("BurntRockBasin");
            if (basin == null || terrain == null) throw new InvalidOperationException("Basin or terrain missing.");
            Backup("TutorialBeforeShipUpgrades.unity");
            EnsureFolder(Root + "/Materials"); EnsureFolder(Root + "/Meshes");
            var steel = Mat("EngineSteel", new Color(0.36f, 0.43f, 0.46f), 0.7f, 0.4f);
            var dark = Mat("EngineGraphite", new Color(0.095f, 0.13f, 0.16f), 0.5f, 0.35f);
            var glass = Mat("RocketWindowGlass", new Color(0.045f, 0.21f, 0.29f), 0.25f, 0.92f);
            var lens = Mat("EngineLens", new Color(0.12f, 0.44f, 0.49f), 0.2f, 0.55f);
            CreateWindow(steel, dark, glass);
            CreateEngine(steel, dark, lens);
            CreateContactPrototypes();
            CreateLaserPrefab();
            Vector3 oldScale = ship.lossyScale;
            GameObject assembly = new GameObject("Enhanced Crashed Rocket");
            assembly.transform.SetParent(environment.transform, false);
            ship.SetParent(assembly.transform, true);
            ship.position += Vector3.down * 0.35f; // Deeper contact, not a larger rocket or a changed crater heightmap.
            MeshFilter cylinder = FindCylinder(ship);
            Bounds body = RelativeBounds(ship, cylinder);
            GameObject window = Instantiate("RocketWindow", ship);
            window.transform.localPosition = new Vector3(body.min.x - 0.02f, body.center.y + 0.12f, body.center.z);
            window.transform.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.left);
            window.transform.localScale = Vector3.one / oldScale.x;
            PrefabUtility.RecordPrefabInstancePropertyModifications(window.transform);
            var engines = new GameObject("Base Engines"); engines.transform.SetParent(ship, false);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                GameObject engine = Instantiate("RocketEngine", engines.transform);
                engine.name = "Engine " + (i + 1);
                engine.transform.localPosition = new Vector3(body.center.x + Mathf.Cos(a) * body.extents.x * 0.50f,
                    body.min.y - 0.015f, body.center.z + Mathf.Sin(a) * body.extents.z * 0.50f);
                engine.transform.localScale = Vector3.one / oldScale.x;
                PrefabUtility.RecordPrefabInstancePropertyModifications(engine.transform);
            }
            AddContactDebris(assembly.transform, ship, terrain);
            PrefabUtility.SaveAsPrefabAssetAndConnect(assembly, Root + "/EnhancedCrashedRocket.prefab", InteractionMode.AutomatedAction);
            PlaceLaser(basin.transform);
            if (Vector3.Distance(ship.lossyScale, oldScale) > 0.001f) throw new InvalidOperationException("Ship scale changed.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save ship upgrades.");
            Debug.Log("SHIP_AND_MINING_LASER_CREATED");
        }

        private static Scene OpenSavedScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene conversion canceled.");
            return EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath, OpenSceneMode.Single);
        }

        private static void Backup(string filename)
        { Directory.CreateDirectory("Logs"); File.Copy(TutorialCrashSiteBuilder.ScenePath, "Logs/" + filename, true); }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path); return mat;
        }

        private static Material ExistingMaterial(string name)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TutorialCrashSiteBuilder.AssetRoot + "/Materials/" + name + ".mat");
            if (mat == null) throw new InvalidOperationException("Missing material: " + name);
            return mat;
        }

        private static GameObject Primitive(string name, PrimitiveType kind, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var obj = GameObject.CreatePrimitive(kind); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = pos; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static GameObject MeshPart(string name, Transform parent, Mesh mesh, Material mat, bool collision = true)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collision) obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            return obj;
        }

        private static Mesh Lathe(string name, Vector2[] profile, int count = 24)
        {
            string path = Root + "/Meshes/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            for (int ring = 0; ring < profile.Length; ring++)
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count, other = (ring + 1) % profile.Length;
                    float a = i * Mathf.PI * 2 / count, b = next * Mathf.PI * 2 / count;
                    Vector3 p0 = new Vector3(Mathf.Cos(a) * profile[ring].x, profile[ring].y, Mathf.Sin(a) * profile[ring].x);
                    Vector3 p1 = new Vector3(Mathf.Cos(b) * profile[ring].x, profile[ring].y, Mathf.Sin(b) * profile[ring].x);
                    Vector3 p2 = new Vector3(Mathf.Cos(b) * profile[other].x, profile[other].y, Mathf.Sin(b) * profile[other].x);
                    Vector3 p3 = new Vector3(Mathf.Cos(a) * profile[other].x, profile[other].y, Mathf.Sin(a) * profile[other].x);
                    Triangle(vertices, uv, indices, p0, p2, p1); Triangle(vertices, uv, indices, p0, p3, p2);
                }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void Triangle(List<Vector3> vertices, List<Vector2> uv, List<int> indices, Vector3 a, Vector3 b, Vector3 c)
        {
            foreach (Vector3 p in new[] { a, b, c }) { indices.Add(vertices.Count); vertices.Add(p); uv.Add(new Vector2(p.x, p.y)); }
        }

        private static void SavePrototype(string name, Action<GameObject> build)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + ".prefab") != null) return;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var obj = new GameObject(name); SceneManager.MoveGameObjectToScene(obj, preview);
                build(obj); PrefabUtility.SaveAsPrefabAsset(obj, Root + "/" + name + ".prefab");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void CreateWindow(Material steel, Material dark, Material glass)
        {
            Mesh rim = Lathe("PortholeRim", new[] {new Vector2(0.86f,0), new Vector2(0.86f,0.15f),
                new Vector2(0.65f,0.15f), new Vector2(0.65f,0)});
            SavePrototype("RocketWindow", root =>
            {
                MeshPart("Metal porthole frame", root.transform, rim, steel);
                Primitive("Dark backing", PrimitiveType.Cylinder, root.transform, new Vector3(0,0.05f,0), new Vector3(1.34f,0.025f,1.34f), dark);
                Primitive("Blue window glass", PrimitiveType.Cylinder, root.transform, new Vector3(0,0.112f,0), new Vector3(1.29f,0.03f,1.29f), glass);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    Primitive("Rim bolt", PrimitiveType.Cylinder, root.transform,
                        new Vector3(Mathf.Cos(a)*0.755f,0.16f,Mathf.Sin(a)*0.755f), new Vector3(0.09f,0.018f,0.09f), dark);
                }
            });
        }

        private static void CreateEngine(Material steel, Material dark, Material lens)
        {
            Mesh bell = Lathe("EngineBell", new[] {new Vector2(0.40f,-0.16f),new Vector2(0.25f,-0.60f),
                new Vector2(0.73f,-1.20f),new Vector2(0.86f,-1.20f),new Vector2(0.31f,-0.58f),new Vector2(0.52f,-0.16f)});
            SavePrototype("RocketEngine", root =>
            {
                Primitive("Engine mount", PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(1.20f,0.07f,1.20f), steel);
                Primitive("Engine chamber", PrimitiveType.Cylinder, root.transform, new Vector3(0,-0.24f,0), new Vector3(0.85f,0.22f,0.85f), dark);
                MeshPart("Exhaust bell", root.transform, bell, steel);
                Primitive("Nozzle throat", PrimitiveType.Cylinder, root.transform, new Vector3(0,-0.62f,0), new Vector3(0.49f,0.025f,0.49f), lens);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2;
                    var rib = Primitive("Cooling rib", PrimitiveType.Cube, root.transform,
                        new Vector3(Mathf.Cos(a)*0.43f,-0.30f,Mathf.Sin(a)*0.43f), new Vector3(0.12f,0.42f,0.15f), steel);
                    rib.transform.localRotation = Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
                }
            });
        }

        private static void CreateContactPrototypes()
        {
            Mesh rock = AssetDatabase.LoadAssetAtPath<Mesh>(TutorialCrashSiteBuilder.AssetRoot + "/Meshes/FacetedBasalt.asset");
            Mesh panel = AssetDatabase.LoadAssetAtPath<Mesh>(TutorialCrashSiteBuilder.AssetRoot + "/Meshes/BentShipPanel.asset");
            if (rock == null || panel == null) throw new InvalidOperationException("Original rock/debris meshes missing.");
            SavePrototype("ContactRock", root => MeshPart("Basalt contact rock", root.transform, rock, ExistingMaterial("Basalt")));
            SavePrototype("ContactHullShard", root => MeshPart("Torn buried hull shard", root.transform, panel, ExistingMaterial("BurntMetal"), false));
        }

        private static GameObject Instantiate(string name, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing prefab " + name);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            obj.transform.SetParent(parent, false); return obj;
        }

        private static MeshFilter FindCylinder(Transform ship)
        {
            foreach (MeshFilter f in ship.GetComponentsInChildren<MeshFilter>())
                if (f.name.IndexOf("Cylinder", StringComparison.OrdinalIgnoreCase) >= 0 && f.sharedMesh != null && f.sharedMesh.vertexCount > 0) return f;
            throw new InvalidOperationException("Rocket's cylinder body missing.");
        }

        private static Bounds RelativeBounds(Transform reference, MeshFilter filter)
        {
            Bounds b = filter.sharedMesh.bounds;
            Bounds output = new Bounds(reference.InverseTransformPoint(filter.transform.TransformPoint(b.center)), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                output.Encapsulate(reference.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents,new Vector3(x,y,z)))));
            return output;
        }

        private static float Ground(Terrain terrain, Vector3 p) { return terrain.SampleHeight(p) + terrain.transform.position.y; }

        private static void AddContactDebris(Transform assembly, Transform ship, Terrain terrain)
        {
            var group = new GameObject("Ground Contact Debris"); group.transform.SetParent(assembly, false);
            Physics.SyncTransforms(); MeshCollider[] colliders = ship.GetComponentsInChildren<MeshCollider>();
            var rng = new System.Random(1822); int rocks = 0, panels = 0;
            Vector3 origin = assembly.position;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40;
                Vector3 direction = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 from = origin + direction * 15; from.y = Ground(terrain, from) + 0.20f;
                float nearest = float.PositiveInfinity; Vector3 contact = default;
                foreach (MeshCollider collider in colliders)
                    if (collider.Raycast(new Ray(from,-direction),out RaycastHit hit,30) && hit.distance < nearest)
                    { nearest = hit.distance; contact = hit.point; }
                if (float.IsPositiveInfinity(nearest)) continue;
                var rock = Instantiate("ContactRock", group.transform); rock.name = "Wedged Basalt " + (++rocks).ToString("00");
                float size = 0.45f + (float)rng.NextDouble() * 0.42f;
                Vector3 point = contact + direction * 0.18f;
                point.y = Ground(terrain,point) + size * 0.10f;
                rock.transform.position = point;
                rock.transform.rotation = Quaternion.Euler(8,a*Mathf.Rad2Deg,18);
                rock.transform.localScale = new Vector3(size*1.1f,size*0.62f,size);
                // Small contact chips are decorative: do not obstruct the existing learning routes.
                foreach (Collider c in rock.GetComponentsInChildren<Collider>()) c.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rock.transform);
                foreach (Collider c in rock.GetComponentsInChildren<Collider>()) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                var shard = Instantiate("ContactHullShard", group.transform); shard.name = "Buried Hull Fragment " + (++panels).ToString("00");
                point = contact + direction * (0.35f + (float)rng.NextDouble()*0.45f);
                point.y = Ground(terrain,point) - 0.04f;
                shard.transform.position = point;
                shard.transform.rotation = Quaternion.Euler(12+(float)rng.NextDouble()*24,-a*Mathf.Rad2Deg,14);
                shard.transform.localScale = new Vector3(0.24f+(float)rng.NextDouble()*0.30f,0.65f,0.32f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(shard.transform);
            }
            if (rocks < 8) throw new InvalidOperationException("Could not locate sufficient ship-ground contact edges.");
        }

        private static void CreateLaserPrefab()
        {
            TextAsset paletteFile = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Models/MiningLaserPalette.json");
            if (paletteFile == null) throw new InvalidOperationException("Export the mining laser model and palette first.");
            var mats = new Dictionary<string,Material>();
            foreach (Entry e in JsonUtility.FromJson<Palette>(paletteFile.text).materials)
            {
                if (!ColorUtility.TryParseHtmlString("#"+e.hex,out Color color)) throw new InvalidOperationException("Bad laser palette.");
                mats[e.name] = Mat(e.name,color,e.metallic,e.smoothness);
            }
            string modelPath = Root + "/Models/MiningLaser.fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Mining laser FBX missing.");
            importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
            importer.globalScale=1; importer.useFileScale=true; importer.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            SavePrototype("MiningLaser", root =>
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model,root.scene); visual.name="Visual";
                visual.transform.SetParent(root.transform,false);
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                {
                    Material[] mapped = r.sharedMaterials;
                    for (int i=0;i<mapped.Length;i++)
                    {
                        if (mapped[i]==null || !mats.TryGetValue(mapped[i].name,out Material material)) throw new InvalidOperationException("Unknown laser material.");
                        mapped[i]=material;
                    }
                    r.sharedMaterials=mapped; PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                Bounds b = LocalRendererBounds(root.transform);
                var collider = root.AddComponent<BoxCollider>(); collider.center=b.center; collider.size=b.size;
                var muzzle = new GameObject("MuzzleAnchor"); muzzle.transform.SetParent(root.transform,false);
                muzzle.transform.localPosition=new Vector3(0,0.32f,b.max.z+0.015f);
                var grip = new GameObject("GripAnchor"); grip.transform.SetParent(root.transform,false); grip.transform.localPosition=new Vector3(0,0.11f,-0.11f);
            });
        }

        private static Bounds LocalRendererBounds(Transform root)
        {
            bool first=true; Bounds result=default;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh==null || filter.sharedMesh.vertexCount==0) continue;
                Bounds b=RelativeBounds(root,filter);
                if (first) {result=b;first=false;} else result.Encapsulate(b);
            }
            if (first) throw new InvalidOperationException("No model geometry."); return result;
        }

        private static void PlaceLaser(Transform basin)
        {
            Transform anchor=basin.Find("MiningLaserAnchor"); if(anchor==null) throw new InvalidOperationException("Laser anchor missing.");
            if(anchor.childCount>0) throw new InvalidOperationException("Mining laser anchor is already occupied; preserve its authored contents.");
            var laser=Instantiate("MiningLaser",anchor); laser.name="Mining Laser";
            laser.transform.localRotation=Quaternion.Euler(0,42,90);
            laser.transform.localScale=Vector3.one*0.86f;
            laser.transform.localPosition=Vector3.zero;
            // Compute just the weapon bounds in basin coordinates, not the rock's bounds.
            bool first=true; Bounds weapon=default;
            foreach(MeshFilter f in laser.GetComponentsInChildren<MeshFilter>())
            {
                Bounds part=RelativeBounds(basin,f);
                if(first){weapon=part;first=false;}else weapon.Encapsulate(part);
            }
            Vector3 offset=new Vector3(-weapon.center.x,0.65f-weapon.min.y,-weapon.center.z);
            laser.transform.position+=basin.TransformVector(offset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(laser.transform);
        }
    }
}
