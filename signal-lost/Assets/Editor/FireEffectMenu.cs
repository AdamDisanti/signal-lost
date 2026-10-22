using SignalLost.Effects;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// GameObject > Signal Lost > Fire...: drops one of the fire prefabs from Prefabs/Effects into the open scene (or
    /// under the selected object), so level designers can place fires quickly. Each is a plain FireEffect: change its
    /// size, tint, smoke and light in the Inspector.
    /// </summary>
    public static class FireEffectMenu
    {
        private const string Folder = "Assets/Prefabs/Effects/";

        [MenuItem("GameObject/Signal Lost/Fire", false, 10)]
        private static void Fire(MenuCommand command) => Place("Fire", command);

        [MenuItem("GameObject/Signal Lost/Fire (Large)", false, 11)]
        private static void FireLarge(MenuCommand command) => Place("Fire Large", command);

        [MenuItem("GameObject/Signal Lost/Fire (Small)", false, 12)]
        private static void FireSmall(MenuCommand command) => Place("Fire Small", command);

        [MenuItem("GameObject/Signal Lost/Fire Jet", false, 13)]
        private static void FireJet(MenuCommand command) => Place("Fire Jet", command);

        private static void Place(string name, MenuCommand command)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            GameObject fire;
            if (prefab != null) fire = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
            {
                fire = new GameObject(name);
                fire.AddComponent<FireEffect>();
            }
            GameObjectUtility.SetParentAndAlign(fire, command.context as GameObject);
            if (command.context == null && SceneView.lastActiveSceneView != null)
                fire.transform.position = SceneView.lastActiveSceneView.pivot;
            Undo.RegisterCreatedObjectUndo(fire, "Create " + name);
            Selection.activeObject = fire;
        }
    }
}
