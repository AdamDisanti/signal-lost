using SignalLost.NPC;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    // Scene files changed outside an already-open editor do not update its in-memory scene automatically.
    // Repair only the tutorial's NPC bindings, preserving existing unsaved scene objects and edits.
    [InitializeOnLoad]
    public static class TutorialNpcUiRepair
    {
        private const string PrefabPath = "Assets/Prefabs/UI/TutorialNpcWindows.prefab";
        static TutorialNpcUiRepair()
        {
            EditorApplication.delayCall += Repair;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Repair;
            };
        }

        [MenuItem("Signal Lost/Tutorial/Repair NPC UI Bindings")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Tutorial.unity") return;
            TutorialQuestFlow quest = Find<TutorialQuestFlow>(scene);
            TutorialPlayer player = Find<TutorialPlayer>(scene);
            if (quest == null || player == null) return;
            TutorialNpcUi ui = Find<TutorialNpcUi>(scene);
            bool changed = false;
            if (ui == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) return; // Wait for the prefab to finish importing; the menu can retry.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Restore tutorial NPC windows");
                ui = instance.GetComponent<TutorialNpcUi>();
                changed = true;
            }
            changed |= Bind(ui, "player", player.transform);
            changed |= Bind(player, "npcUi", ui);
            changed |= Bind(quest, "ui", ui);
            changed |= Bind(quest, "player", player);
            MerchantCrab merchant = Find<MerchantCrab>(scene);
            if (merchant != null) changed |= Bind(quest, "merchant", merchant);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var actor in root.GetComponentsInChildren<NpcActor>(true))
                    if (actor.IsQuestGiver)
                    {
                        NpcDialogue speech = actor.GetComponentInChildren<NpcDialogue>(true);
                        if (speech != null) changed |= Bind(quest, "cavendish", speech);
                        changed |= Bind(actor, "quest", quest);
                    }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var shop in root.GetComponentsInChildren<MerchantShop>(true)) changed |= Bind(shop, "ui", ui);
            if (!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Repaired tutorial NPC UI bindings in the open scene. Existing edits were preserved; save the scene when ready.");
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                T match = root.GetComponentInChildren<T>(true);
                if (match != null) return match;
            }
            return null;
        }
        private static bool Bind(Object component, string field, Object value)
        {
            var data = new SerializedObject(component);
            var property = data.FindProperty(field);
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            data.ApplyModifiedProperties();
            return true;
        }
    }
}
