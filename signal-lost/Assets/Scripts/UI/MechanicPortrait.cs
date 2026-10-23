using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// The mechanic's face in the status bar, Doom-style: it glances around and blinks, winces when hurt, sweats and
    /// gets a bandage when low, goes rosy-cheeked and cross-eyed when drunk, and shows X eyes when down.
    /// Built from rounded UI shapes (no art assets). Drive it with <see cref="Set"/> and <see cref="Hurt"/>.
    /// </summary>
    public sealed class MechanicPortrait : MonoBehaviour
    {
        private static readonly Color Skin = new Color(0.93f, 0.74f, 0.58f);
        private static readonly Color Cap = new Color(0.93f, 0.48f, 0.15f);
        private static readonly Color Ink = new Color(0.12f, 0.09f, 0.08f);

        private RectTransform face, leftEye, rightEye, leftPupil, rightPupil, leftBrow, rightBrow, mouth;
        private Image leftCheek, rightCheek, sweat, bandage, mouthImage;
        private GameObject leftX, rightX;
        private float health01 = 1f, drunk01, hurtUntil, nextBlink, blinkUntil, lookChange;
        private Vector2 look;
        private bool down;

        public static MechanicPortrait Create(Transform parent, float size)
        {
            RectTransform root = UiTheme.Rect("Mechanic Portrait", parent);
            root.sizeDelta = new Vector2(size, size);
            var portrait = root.gameObject.AddComponent<MechanicPortrait>();
            portrait.Build(root, size / 120f);
            return portrait;
        }

        /// <param name="health">0 to 1.</param>
        /// <param name="drunkenness">0 (sober) to 1 (about to black out).</param>
        public void Set(float health, float drunkenness, bool isDown)
        {
            health01 = Mathf.Clamp01(health);
            drunk01 = Mathf.Clamp01(drunkenness);
            down = isDown;
        }

        /// <summary>A wince for a moment after taking damage.</summary>
        public void Hurt() => hurtUntil = Time.unscaledTime + 0.45f;

        private void Update()
        {
            float now = Time.unscaledTime;
            bool hurt = now < hurtUntil;
            if (now >= nextBlink) { blinkUntil = now + 0.12f; nextBlink = now + Random.Range(2.2f, 4.5f); }
            if (now >= lookChange) { look = new Vector2(Random.Range(-1f, 1f), Random.Range(-0.4f, 0.5f)); lookChange = now + Random.Range(0.8f, 2.4f); }
            bool blink = now < blinkUntil;

            // Head: idle bob, a jolt when hurt, a lazy sway when drunk.
            float sway = Mathf.Sin(now * 1.6f) * drunk01 * 9f;
            face.localRotation = Quaternion.Euler(0f, 0f, sway + (hurt ? Mathf.Sin(now * 60f) * 4f : 0f));
            face.anchoredPosition = new Vector2(0f, Mathf.Sin(now * 2f) * 1.5f);

            // Eyes: squint when hurt or low, half-closed when drunk, shut when blinking, X when down.
            float open = blink ? 0.12f : hurt ? 0.35f : Mathf.Lerp(1f, 0.55f, Mathf.Max(drunk01, 1f - health01) * 0.8f);
            leftEye.localScale = rightEye.localScale = new Vector3(1f, down ? 0f : open, 1f);
            if (leftX.activeSelf != down) { leftX.SetActive(down); rightX.SetActive(down); }
            Vector2 cross = new Vector2(drunk01 * 3f, 0f); // drunk: pupils drift toward the nose
            Vector2 wobble = new Vector2(Mathf.Sin(now * 5f), Mathf.Cos(now * 4f)) * drunk01 * 1.5f;
            leftPupil.anchoredPosition = look * 3f + cross + wobble;
            rightPupil.anchoredPosition = look * 3f - cross - wobble;

            // Brows: angry slant when hurt, worried when low.
            float worry = hurt ? -18f : (1f - health01) * 14f;
            leftBrow.localRotation = Quaternion.Euler(0f, 0f, -worry);
            rightBrow.localRotation = Quaternion.Euler(0f, 0f, worry);

            // Mouth: a grimace when hurt, a wobbly grin when drunk, flat otherwise.
            mouth.sizeDelta = new Vector2(hurt ? 24f : 26f + drunk01 * 6f, hurt || down ? 14f : 6f);
            mouth.localRotation = Quaternion.Euler(0f, 0f, drunk01 * Mathf.Sin(now * 2.3f) * 12f);
            mouthImage.color = hurt || down ? new Color(0.35f, 0.05f, 0.05f) : Ink;

            Color cheek = new Color(1f, 0.35f, 0.38f, drunk01 * 0.75f);
            leftCheek.color = rightCheek.color = cheek;
            sweat.enabled = health01 < 0.4f && !down;
            sweat.rectTransform.anchoredPosition = new Vector2(22f, 10f - Mathf.Repeat(now * 14f, 18f));
            bandage.enabled = health01 < 0.25f || down;
        }

        private void Build(RectTransform root, float s)
        {
            UiTheme.Stretch(UiTheme.Panel("Frame", root, new Color(0.08f, 0.1f, 0.18f, 0.95f), UiTheme.Edge, 0.6f).rectTransform);
            face = Shape("Face", root, Skin, new Vector2(0f, 0f), new Vector2(74f, 82f) * s, 1.2f);
            Shape("Ear L", face, Skin, new Vector2(-38f, 2f) * s, new Vector2(12f, 18f) * s, 0.3f);
            Shape("Ear R", face, Skin, new Vector2(38f, 2f) * s, new Vector2(12f, 18f) * s, 0.3f);
            RectTransform cap = Shape("Cap", face, Cap, new Vector2(0f, 34f) * s, new Vector2(80f, 30f) * s, 0.8f);
            Shape("Cap Peak", cap, Cap * 0.85f, new Vector2(10f, -12f) * s, new Vector2(70f, 9f) * s, 0.3f);
            Shape("Cap Badge", cap, UiTheme.Text, new Vector2(0f, 3f) * s, new Vector2(12f, 12f) * s, 0.3f);
            for (int i = 0; i < 9; i++) // stubble
                Shape("Stubble", face, new Color(0.35f, 0.25f, 0.2f, 0.45f), new Vector2(-18f + (i % 5) * 9f, -26f - (i / 5) * 6f) * s, new Vector2(3f, 3f) * s, 0.2f);
            leftEye = Shape("Eye L", face, Color.white, new Vector2(-14f, 4f) * s, new Vector2(18f, 16f) * s, 0.5f);
            rightEye = Shape("Eye R", face, Color.white, new Vector2(14f, 4f) * s, new Vector2(18f, 16f) * s, 0.5f);
            leftPupil = Shape("Pupil", leftEye, Ink, Vector2.zero, new Vector2(7f, 7f) * s, 0.3f);
            rightPupil = Shape("Pupil", rightEye, Ink, Vector2.zero, new Vector2(7f, 7f) * s, 0.3f);
            leftX = Cross("X L", face, new Vector2(-14f, 4f) * s, s);
            rightX = Cross("X R", face, new Vector2(14f, 4f) * s, s);
            leftBrow = Shape("Brow L", face, new Color(0.35f, 0.22f, 0.15f), new Vector2(-14f, 17f) * s, new Vector2(18f, 4f) * s, 0.2f);
            rightBrow = Shape("Brow R", face, new Color(0.35f, 0.22f, 0.15f), new Vector2(14f, 17f) * s, new Vector2(18f, 4f) * s, 0.2f);
            Shape("Nose", face, new Color(0.88f, 0.6f, 0.47f), new Vector2(0f, -6f) * s, new Vector2(10f, 12f) * s, 0.4f);
            leftCheek = Shape("Cheek L", face, Color.clear, new Vector2(-22f, -8f) * s, new Vector2(14f, 9f) * s, 0.4f).GetComponent<Image>();
            rightCheek = Shape("Cheek R", face, Color.clear, new Vector2(22f, -8f) * s, new Vector2(14f, 9f) * s, 0.4f).GetComponent<Image>();
            mouth = Shape("Mouth", face, Ink, new Vector2(0f, -20f) * s, new Vector2(26f, 6f) * s, 0.3f);
            mouthImage = mouth.GetComponent<Image>();
            sweat = Shape("Sweat", face, new Color(0.55f, 0.85f, 1f, 0.9f), new Vector2(22f, 10f) * s, new Vector2(6f, 9f) * s, 0.3f).GetComponent<Image>();
            bandage = Shape("Bandage", face, new Color(0.97f, 0.95f, 0.88f), new Vector2(-18f, 22f) * s, new Vector2(26f, 8f) * s, 0.2f).GetComponent<Image>();
            bandage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 30f);
        }

        private static RectTransform Shape(string name, Transform parent, Color color, Vector2 position, Vector2 size, float corner)
        {
            Image image = UiTheme.Panel(name, parent, color, null, corner);
            return UiTheme.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }

        private static GameObject Cross(string name, Transform parent, Vector2 position, float s)
        {
            RectTransform root = UiTheme.Place(UiTheme.Rect(name, parent), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(18f, 18f) * s);
            Image first = UiTheme.Block("Stroke", root, Ink);
            UiTheme.Place(first.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 4f) * s);
            first.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image second = UiTheme.Block("Stroke", first.rectTransform, Ink);
            UiTheme.Place(second.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 4f) * s);
            second.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }
    }
}
