using System.Collections.Generic;
using SignalLost.Player;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Bakes the mechanic's seamless meshes (the gloves, the suit and the arms, see <see cref="MechanicModel"/> and
    /// <see cref="SoftShape"/>) into a folder under Resources, one asset per mesh, so the game loads them instead of
    /// building them when a scene starts. Run it again whenever those shapes change, after bumping
    /// <see cref="MechanicModel.MeshVersion"/>. (One file per mesh: Unity renames a file's main object after the file.)
    /// </summary>
    public static class MechanicMeshBaker
    {
        private const string Folder = "Assets/Resources/" + MechanicModel.BakedPath;

        [MenuItem("Signal Lost/Models/Bake Mechanic Meshes")]
        public static void Bake()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, Mesh> meshes = MechanicModel.BuildMeshesForBaking();
            EnsureFolder(Folder);
            // Clear out earlier bakes (any version), including the single-file layout of the first bake.
            AssetDatabase.DeleteAsset(Folder + ".asset");
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Folder }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            int vertices = 0;
            foreach (KeyValuePair<string, Mesh> pair in meshes)
            {
                Mesh copy = Object.Instantiate(pair.Value);
                copy.name = pair.Key + " v" + MechanicModel.MeshVersion;
                copy.hideFlags = HideFlags.None;
                vertices += copy.vertexCount;
                AssetDatabase.CreateAsset(copy, Folder + "/" + copy.name + ".asset");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"MECHANIC_MESHES_BAKED {meshes.Count} meshes, {vertices} vertices, version {MechanicModel.MeshVersion}, " +
                      $"{watch.ElapsedMilliseconds} ms -> {Folder}");
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod SignalLost.Editor.MechanicMeshBaker.BakeBatch -quit</summary>
        public static void BakeBatch() => Bake();

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
