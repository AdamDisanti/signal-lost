using System;
using SignalLost.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.NPC
{
    // Screen-space NPC windows live on a scene canvas, independently of moving NPC models.
    public sealed class TutorialNpcUi : MonoBehaviour
    {
        [SerializeField] private GameObject conversationPanel, shopPanel, trackerPanel;
        [SerializeField] private TextMeshProUGUI conversationTitle, conversationBody, conversationHint, trackerText;
        [SerializeField] private Transform player;
        private FirstPersonController cursorController;
        private bool ownsCursor;
        private int windowChangedFrame = -1;
        public bool WindowChangedThisFrame => windowChangedFrame == Time.frameCount;
        private string[] lines;
        private int index;
        private Action completed;
        public Transform Owner { get; private set; }
        public bool ConversationActive => conversationPanel != null && conversationPanel.activeSelf;
        public bool ShopIsOpen => shopPanel != null && shopPanel.activeSelf;
        public bool HasOpenWindow => ConversationActive || ShopIsOpen;
        public string CurrentLine => ConversationActive && lines != null ? lines[index] : "";

        public void BindPlayer(Transform target) => player = target;

        public void BeginConversation(Transform owner, string title, string[] dialogue, Action onCompleted = null)
        {
            CloseAll();
            if (dialogue == null || dialogue.Length == 0) return;
            Owner = owner; lines = dialogue; index = 0; completed = onCompleted;
            conversationTitle.text = title;
            windowChangedFrame = Time.frameCount;
            conversationPanel.SetActive(true);
            ReleaseCursor();
            Refresh();
        }

        public bool Advance()
        {
            if (!HasOpenWindow) return false;
            if (!InRange()) { CloseAll(); return false; }
            if (ShopIsOpen) { CloseAll(); return true; }
            index++;
            if (index >= lines.Length)
            {
                Action action = completed;
                CloseAll();
                action?.Invoke();
            }
            else Refresh();
            return true;
        }

        public void OpenShop(Transform owner)
        {
            CloseAll(); Owner = owner;
            windowChangedFrame = Time.frameCount;
            shopPanel.SetActive(true);
            ReleaseCursor();
        }

        public void TrackQuest(bool visible, bool finished, string objective)
        {
            trackerPanel.SetActive(visible);
            trackerText.text = (finished ? "<color=#8FE0AE>QUEST COMPLETE</color>" : "<color=#F4C66A>ACTIVE QUEST</color>") +
                "\n<b>Recover Your Flask</b>\n" + objective;
        }

        public void CloseAll()
        {
            if (HasOpenWindow) windowChangedFrame = Time.frameCount;
            conversationPanel?.SetActive(false); shopPanel?.SetActive(false);
            lines = null; completed = null; Owner = null;
            if (ownsCursor)
            {
                if (cursorController != null) cursorController.SetMenuOpen(false);
                ownsCursor = false;
            }
        }
        public void CloseFor(Transform owner) { if (Owner == owner) CloseAll(); }
        private void ReleaseCursor()
        {
            cursorController = player != null ? player.GetComponent<FirstPersonController>() : null;
            if (cursorController == null) return;
            ownsCursor = true;
            cursorController.SetMenuOpen(true);
        }
        private bool InRange() => Owner != null && player != null && Vector3.Distance(player.position, Owner.position) <= 6f;
        private void Refresh()
        {
            conversationBody.text = lines[index];
            conversationHint.text = index == lines.Length - 1 ? "E  Continue / finish    •    Esc  Close" : "E  Next    •    Esc  Close";
        }
        private void Update()
        {
            if (!HasOpenWindow) return;
            if (!InRange() || Keyboard.current?.escapeKey.wasPressedThisFrame == true) CloseAll();
            else if (!WindowChangedThisFrame && Keyboard.current?.eKey.wasPressedThisFrame == true) Advance();
        }
        private void OnDisable() => CloseAll();
    }
}
