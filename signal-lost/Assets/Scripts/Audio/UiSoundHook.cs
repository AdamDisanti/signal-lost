using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalLost.Audio
{
    /// <summary>
    /// Hover tick and click sound for a UI control. <see cref="SoundDirector"/> adds it to every button, slider and
    /// toggle it finds (menus are built in code, so there's nothing to set up by hand). Plays during pause too.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSoundHook : MonoBehaviour, IPointerEnterHandler
    {
        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (selectable is Button button) button.onClick.AddListener(OnClick);
            else if (selectable is Toggle toggle) toggle.onValueChanged.AddListener(_ => OnClick());
        }

        private void OnClick() => Sfx.Play("ui_click", 0.5f, Random.Range(0.97f, 1.03f), SoundBus.Interface);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (selectable != null && !selectable.interactable) return;
            Sfx.Play("ui_hover", 0.3f, Random.Range(0.96f, 1.04f), SoundBus.Interface);
        }
    }
}
