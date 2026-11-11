using System;
using System.Collections.Generic;
using SignalLost.Inventory;
using SignalLost.Melee;
using SignalLost.Mudlands;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Shop and pockets icons for the melee gear (docs/melee-rage.md): the Gaussian Brass Knuckles and the Moon Shoes.
    /// The models come from <see cref="MeleeGearModels"/> (the same code that fits them on the first-person hands and the
    /// pockets figure), are saved as prefabs and rendered to icons by <see cref="ItemIconRenderer"/>, the way
    /// <see cref="MudlandsUpgradeIcons"/> does. <see cref="UpgradeAssetBuilder"/> adds these to the ItemCatalog; the menu
    /// item merges them into the existing catalog without rebuilding the others.
    /// </summary>
    internal static class MeleeGearIcons
    {
        private const string Folder = UpgradeAssetBuilder.Folder + "/Melee";

        private static readonly Dictionary<string, Action<MeshKit>> Models = new Dictionary<string, Action<MeshKit>>
        {
            ["gaussian_knuckles"] = MeleeGearModels.KnuckleDuster,
            ["moon_shoes"] = MeleeGearModels.MoonShoePair
        };

        /// <summary>Builds every model and icon; returns the catalog entries.</summary>
        public static List<ItemCatalog.Entry> Build()
        {
            EnsureFolder(Folder + "/Materials");
            var entries = new List<ItemCatalog.Entry>();
            foreach (KeyValuePair<string, Action<MeshKit>> model in Models)
            {
                if (UpgradeCatalog.Find(model.Key) == null) continue;
                var kit = new MeshKit();
                model.Value(kit);
                Mesh mesh = kit.Build("Upgrade_" + model.Key);
                string meshPath = Folder + "/" + model.Key + "_mesh.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                var materials = new Material[kit.SubmeshCount];
                for (int i = 0; i < materials.Length; i++)
                {
                    (Color color, float glow) = kit.SubmeshLook(i);
                    materials[i] = SavedMaterial(color, glow);
                }
                var root = new GameObject(model.Key);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + model.Key + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                Sprite icon = ItemIconRenderer.Render(prefab, Folder + "/" + model.Key + ".png", new Vector3(-25f, 35f, 0f), 256, 1.25f);
                entries.Add(new ItemCatalog.Entry { id = model.Key, model = prefab, icon = icon });
            }
            return entries;
        }

        [MenuItem("Signal Lost/Items/Build Melee Gear Icons")]
        public static void MergeIntoCatalog()
        {
            List<ItemCatalog.Entry> built = Build();
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(UpgradeAssetBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("No ItemCatalog yet: run Signal Lost > Items > Build Upgrade Assets (it includes the melee icons).");
                return;
            }
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty("entries");
            foreach (ItemCatalog.Entry entry in built)
            {
                int index = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == entry.id) { index = i; break; }
                if (index < 0) { index = list.arraySize; list.arraySize++; }
                SerializedProperty row = list.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("id").stringValue = entry.id;
                row.FindPropertyRelative("model").objectReferenceValue = entry.model;
                row.FindPropertyRelative("icon").objectReferenceValue = entry.icon;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("MELEE_GEAR_ICONS_BUILT");
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod SignalLost.Editor.MeleeGearIcons.BuildBatch</summary>
        public static void BuildBatch()
        {
            try
            {
                MergeIntoCatalog();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static Material SavedMaterial(Color color, float glow)
        {
            string path = Folder + "/Materials/" + ColorUtility.ToHtmlStringRGB(color) + (glow > 0f ? "_glow" : "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.4f);
            material.SetFloat("_Metallic", glow > 0f ? 0f : 0.35f);
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * Mathf.Min(glow, 0.9f)); // brighter clips to white in the icon
            }
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
