using UnityEngine;

namespace SignalLost.Story
{
    /// <summary>
    /// Save and load (Christian, 10-27): added to the tutorial story's object at Start by
    /// <c>TutorialStory.LoadSaveInstead</c>, so autosaving can listen for events and run every frame without touching
    /// TutorialStory's own OnEnable, OnDisable and Update. Nothing to set up in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")] // added from code only
    public sealed class TutorialSaveWatcher : MonoBehaviour
    {
        private TutorialStory story;

        private void Awake() => story = GetComponent<TutorialStory>();

        private void OnEnable()
        {
            if (story != null) story.SaveHooks(true);
        }

        private void OnDisable()
        {
            if (story != null) story.SaveHooks(false);
        }

        private void Update()
        {
            if (story != null) story.SaveTick();
        }
    }
}
