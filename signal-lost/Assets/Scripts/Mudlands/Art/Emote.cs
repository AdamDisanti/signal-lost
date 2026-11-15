using TMPro;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// A little world-space symbol that pops up over a creature's head ("!", "?!", "♪", "zzz", "#@!") and fades:
    /// the cheap, readable way the Mudlands creatures show surprise, panic, cheering and grumbling. Always faces the
    /// camera. One per owner at a time (a new one replaces the old).
    /// </summary>
    public sealed class Emote : MonoBehaviour
    {
        private TextMeshPro text;
        private float born, life;
        private Transform anchor;
        private Vector3 offset;
        private Color color;

        public static Emote Show(Transform anchor, string symbol, Color color, float height = 1.6f, float seconds = 1.1f, float size = 4f)
        {
            if (anchor == null) return null;
            Transform existing = anchor.Find("Emote");
            if (existing != null) Destroy(existing.gameObject);
            var go = new GameObject("Emote");
            go.transform.SetParent(anchor, false);
            var emote = go.AddComponent<Emote>();
            emote.text = go.AddComponent<TextMeshPro>();
            emote.text.text = symbol;
            emote.text.fontSize = size;
            emote.text.fontStyle = FontStyles.Bold;
            emote.text.alignment = TextAlignmentOptions.Center;
            emote.text.overflowMode = TextOverflowModes.Overflow;
            emote.text.outlineWidth = 0.25f;
            emote.text.outlineColor = new Color32(20, 16, 12, 255);
            emote.text.color = color;
            emote.text.rectTransform.sizeDelta = new Vector2(6f, 2f);
            emote.anchor = anchor;
            emote.offset = Vector3.up * height;
            emote.color = color;
            emote.born = Time.time;
            emote.life = seconds;
            go.transform.localPosition = emote.offset;
            return emote;
        }

        private void LateUpdate()
        {
            float t = (Time.time - born) / life;
            if (t >= 1f || anchor == null) { Destroy(gameObject); return; }
            Camera view = Camera.main;
            if (view != null) transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position);
            // Pops in with an overshoot, rises a little, fades at the end.
            float pop = t < 0.15f ? Mathf.Sin(t / 0.15f * Mathf.PI * 0.5f) * 1.25f : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
            Vector3 parentScale = anchor.lossyScale;
            float inverse = 1f / Mathf.Max(0.05f, (parentScale.x + parentScale.y + parentScale.z) / 3f);
            transform.localScale = Vector3.one * pop * inverse;
            transform.position = anchor.position + offset + Vector3.up * t * 0.4f;
            text.color = new Color(color.r, color.g, color.b, 1f - Mathf.Clamp01((t - 0.75f) / 0.25f));
        }
    }
}
