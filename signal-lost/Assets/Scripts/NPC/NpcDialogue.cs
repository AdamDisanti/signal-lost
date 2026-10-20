using SignalLost.UI;
using TMPro;
using UnityEngine;

namespace SignalLost.NPC
{
    // Overhead chatter for one NPC: each line shows in a speech bubble above its head and, when the player is
    // close enough to hear it, as a bottom-middle subtitle. No cursor changes, input lock or time-scale changes.
    // Quest information belongs in DialogueUi popups instead.
    public sealed class NpcDialogue : MonoBehaviour
    {
        [Tooltip("Legacy world-space text from the first NPC pass. Hidden at runtime; the speech bubble replaces it.")]
        [SerializeField] private TextMeshPro text;
        [SerializeField] private string speaker;
        [SerializeField, Min(1)] private float secondsPerLine = 6f;
        [SerializeField] private float visibleDistance = 14f;
        [Tooltip("Lines also appear as subtitles while the player is this close.")]
        [SerializeField, Min(0f)] private float subtitleDistance = 20f;
        private string[] lines;
        private int index;
        private float nextLine, lineSeconds;
        private Camera viewer;
        private bool manual, warning;
        private SpeechBubble bubble;
        private string subtitled;
        public bool IsSpeaking => lines != null && index < lines.Length;
        public bool IsManual => manual && lines != null;
        public bool Suppressed { get; set; }
        public string Speaker => speaker;
        public string CurrentLine => lines != null && index < lines.Length ? lines[index] : "";

        private void Awake()
        {
            if (text != null) text.enabled = false;
        }

        private void OnDisable() => bubble?.Hide();

        private void OnDestroy()
        {
            if (bubble != null) Destroy(bubble.gameObject);
        }

        public void Speak(params string[] dialogue) => Play(dialogue, secondsPerLine, false, false);

        /// <summary>Auto-advancing lines with their own pacing, e.g. a quick intro.</summary>
        public void Speak(float seconds, params string[] dialogue) => Play(dialogue, seconds, false, false);

        /// <summary>Red, shaking bubble plus a red subtitle. Never blocks the player.</summary>
        public void Warn(params string[] dialogue) => Play(dialogue, Mathf.Min(secondsPerLine, 3.5f), false, true);

        public void SpeakManual(params string[] dialogue) => Play(dialogue, secondsPerLine, true, false);

        // Returns true even on the final line, so the same E press cannot restart the conversation.
        public bool AdvanceManual()
        {
            if (!IsManual) return false;
            index++;
            if (index >= lines.Length) { lines = null; manual = false; }
            Refresh();
            return true;
        }

        private void Play(string[] dialogue, float seconds, bool isManual, bool isWarning)
        {
            manual = isManual;
            warning = isWarning;
            lines = dialogue != null && dialogue.Length > 0 ? dialogue : null;
            index = 0;
            lineSeconds = Mathf.Max(1f, seconds);
            nextLine = Time.time + lineSeconds;
            subtitled = null;
            Refresh();
        }

        private void LateUpdate()
        {
            if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
            if (!manual && lines != null && Time.time >= nextLine)
            {
                index++;
                nextLine = Time.time + lineSeconds;
                if (index >= lines.Length) lines = null;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (!Application.isPlaying) return;
            if (bubble == null)
            {
                if (DialogueUi.Instance == null) return;
                bubble = DialogueUi.Instance.CreateBubble(transform, speaker);
            }
            bubble.VisibleDistance = visibleDistance;
            // While this NPC's own quest popup is open, the popup does the talking.
            DialogueUi ui = DialogueUi.Instance;
            bool popupOwned = ui.PopupOpen && ui.Owner != null && transform.IsChildOf(ui.Owner);
            string line = Suppressed || popupOwned ? null : CurrentLine;
            bubble.Show(line, warning, IsManual ? "[E] Continue" : null);

            // One subtitle per line, only while the player is close enough to hear it.
            if (string.IsNullOrEmpty(line) || line == subtitled || viewer == null) return;
            if (Vector3.Distance(viewer.transform.position, transform.position) > subtitleDistance) return;
            subtitled = line;
            ui.Subtitle(speaker, line, IsManual ? 6f : lineSeconds, warning);
        }
    }
}
