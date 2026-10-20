using TMPro;
using UnityEngine;

namespace SignalLost.NPC
{
    // World-space conversation: no cursor changes, input lock, or time-scale changes.
    public sealed class NpcDialogue : MonoBehaviour
    {
        [SerializeField] private TextMeshPro text;
        [SerializeField] private string speaker;
        [SerializeField, Min(1)] private float secondsPerLine = 6f;
        [SerializeField] private float visibleDistance = 14f;
        private string[] lines;
        private int index;
        private float nextLine;
        private Camera viewer;
        private bool manual;
        public bool IsSpeaking => lines != null && index < lines.Length;
        public bool IsManual => manual && lines != null;
        public bool Suppressed { get; set; }
        public string CurrentLine => lines != null && index < lines.Length ? lines[index] : "";

        public void Speak(params string[] dialogue)
        {
            manual = false;
            lines = dialogue;
            index = 0;
            nextLine = Time.time + secondsPerLine;
            Refresh();
        }

        public void SpeakManual(params string[] dialogue)
        {
            Speak(dialogue);
            manual = true;
        }

        // Returns true even on the final line, so the same E press cannot restart the conversation.
        public bool AdvanceManual()
        {
            if (!IsManual) return false;
            index++;
            if (index >= lines.Length) { lines = null; manual = false; }
            Refresh();
            return true;
        }

        private void LateUpdate()
        {
            if (viewer == null) viewer = Camera.main;
            if (!manual && lines != null && Time.time >= nextLine)
            {
                index++;
                nextLine = Time.time + secondsPerLine;
                if (index >= lines.Length) lines = null;
            }
            Refresh();
            if (viewer == null || text == null) return;
            text.enabled = !Suppressed && Vector3.Distance(viewer.transform.position, transform.position) <= visibleDistance;
            Vector3 facing = transform.position - viewer.transform.position;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
        }

        private void Refresh()
        {
            if (text != null) text.text = "<b>" + speaker + "</b>\n" + CurrentLine + (IsManual ? "\n<size=75%>[E] Continue</size>" : "");
        }
    }
}
