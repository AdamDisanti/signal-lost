using System;
using System.Collections.Generic;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' on-screen extras, drawn with IMGUI like the mini-boss bar (<see cref="SignalLost.Enemies.Desert.MiniBossHud"/>)
    /// so the shared HUD prefab is untouched:
    /// <list type="bullet">
    /// <item>the Drunk Driving pip above the status bar (ready / cooldown), the chug bar, and while driving the bottle,
    /// the car's health and the controls;</item>
    /// <item>short toasts ("Spilled it!", "Too drunk to drive"), big shouts ("MY GO-KART!!!");</item>
    /// <item>cinematic letterbox bars and boss name cards for the entrances;</item>
    /// <item>the boss chase banner and timer, little bars over creatures (Kart Kong's banana), and the driving-test quiz.</item>
    /// </list>
    /// Created on demand; one per scene. A placeholder look until the UI pass, like the mini-boss bar.
    /// </summary>
    public sealed class MudHud : MonoBehaviour
    {
        private static MudHud instance;
        private static readonly List<(string text, Color color, float until)> Toasts = new List<(string, Color, float)>();
        private static readonly List<(Transform anchor, float fraction, Color color, float height, int frame)> Bars =
            new List<(Transform, float, Color, float, int)>();
        private static string shout, titleName, titleLine, chaseText;
        private static Color shoutColor;
        private static float shoutStart, shoutUntil, titleStart = float.NegativeInfinity, titleUntil, chaseFraction;
        private static float letterbox, letterboxTarget;
        private static int chaseFrame = -10;
        private static Quiz quiz;

        private GUIStyle toastStyle, shoutStyle, titleStyle, lineStyle, small, button, question;

        private sealed class Quiz
        {
            public string Title, Question;
            public string[] Answers;
            public Action<int> Done;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Toasts.Clear();
            Bars.Clear();
            shoutUntil = titleUntil = 0f;
            letterbox = letterboxTarget = 0f;
            quiz = null;
        }

        /// <summary>Makes sure the HUD exists (it draws the car pip, so the driver creates it up front).</summary>
        public static void Ensure()
        {
            if (instance != null) return;
            instance = FindAnyObjectByType<MudHud>();
            if (instance == null) instance = new GameObject("Mudlands HUD").AddComponent<MudHud>();
        }

        // ---------- API ----------

        public static void Toast(string text, Color color, float seconds = 2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            Ensure();
            Toasts.Add((text, color, Time.time + seconds));
            if (Toasts.Count > 3) Toasts.RemoveAt(0);
        }

        /// <summary>A huge shaking line in the middle of the screen ("MY GO-KART!!!").</summary>
        public static void Shout(string text, Color color, float seconds = 2.2f)
        {
            Ensure();
            shout = text;
            shoutColor = color;
            shoutStart = Time.time;
            shoutUntil = Time.time + seconds;
        }

        /// <summary>Black bars top and bottom for a cutscene.</summary>
        public static void Letterbox(bool on)
        {
            Ensure();
            letterboxTarget = on ? 1f : 0f;
        }

        /// <summary>A boss's name card under the letterbox.</summary>
        public static void Title(string name, string line, float seconds = 3f)
        {
            Ensure();
            titleName = name;
            titleLine = line;
            titleStart = Time.time;
            titleUntil = Time.time + seconds;
        }

        /// <summary>The boss chase banner: call every frame while it runs (fraction = time left, 0-1).</summary>
        public static void Chase(string text, float fraction)
        {
            Ensure();
            chaseText = text;
            chaseFraction = fraction;
            chaseFrame = Time.frameCount;
        }

        /// <summary>A small bar over something in the world: call every frame while it shows.</summary>
        public static void WorldBar(Transform anchor, float fraction, Color color, float height = 2.5f)
        {
            Ensure();
            Bars.Add((anchor, fraction, color, height, Time.frameCount));
        }

        /// <summary>A multiple-choice popup (the driving test). Frees the cursor until answered.</summary>
        public static void Ask(string title, string questionText, string[] answers, Action<int> done)
        {
            Ensure();
            quiz = new Quiz { Title = title, Question = questionText, Answers = answers, Done = done };
            UiMenus.Open(instance);
        }

        public static bool Asking => quiz != null;

        // ---------- Drawing ----------

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            letterbox = Mathf.MoveTowards(letterbox, letterboxTarget, Time.unscaledDeltaTime * 2.5f);
            Toasts.RemoveAll(t => Time.time > t.until);
            Bars.RemoveAll(b => b.frame < Time.frameCount - 1 || b.anchor == null);
        }

        private void Styles(float scale)
        {
            if (toastStyle != null && Mathf.Approximately(toastStyle.fontSize, Mathf.RoundToInt(26 * scale))) return;
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            shoutStyle = new GUIStyle(toastStyle) { fontSize = Mathf.RoundToInt(78 * scale) };
            titleStyle = new GUIStyle(toastStyle) { fontSize = Mathf.RoundToInt(54 * scale), alignment = TextAnchor.MiddleLeft };
            lineStyle = new GUIStyle(toastStyle) { fontSize = Mathf.RoundToInt(26 * scale), fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleLeft };
            small = new GUIStyle(toastStyle) { fontSize = Mathf.RoundToInt(18 * scale), alignment = TextAnchor.MiddleLeft };
            question = new GUIStyle(toastStyle) { fontSize = Mathf.RoundToInt(28 * scale), wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * scale), wordWrap = true };
        }

        private void OnGUI()
        {
            float scale = Screen.height / 1080f;
            Styles(scale);
            float w = Screen.width, h = Screen.height;

            if (letterbox > 0f)
            {
                float bar = h * 0.11f * Mathf.SmoothStep(0f, 1f, letterbox);
                Fill(new Rect(0, 0, w, bar), Color.black);
                Fill(new Rect(0, h - bar, w, bar), Color.black);
            }

            if (Time.time < titleUntil && titleName != null)
            {
                float t = Time.time - titleStart;
                float alpha = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((titleUntil - Time.time) / 0.5f);
                float slide = Mathf.Lerp(-60f, 0f, Mathf.SmoothStep(0f, 1f, t / 0.4f)) * scale;
                Fill(new Rect(80 * scale + slide, h * 0.66f, 10 * scale, 110 * scale), new Color(1f, 0.75f, 0.3f, alpha));
                Label(new Rect(110 * scale + slide, h * 0.66f, w, 70 * scale), titleName.ToUpperInvariant(), titleStyle, new Color(1f, 1f, 1f, alpha));
                Label(new Rect(112 * scale + slide, h * 0.66f + 64 * scale, w, 40 * scale), titleLine, lineStyle, new Color(1f, 0.85f, 0.6f, alpha));
            }

            foreach (var bar in Bars) WorldBarGui(bar.anchor, bar.fraction, bar.color, bar.height, scale);

            DrivingGui(scale, w, h);

            if (chaseFrame >= Time.frameCount - 1 && chaseText != null)
            {
                bool flash = Mathf.Sin(Time.time * 10f) > 0f;
                Label(new Rect(0, h * 0.14f, w, 60 * scale), chaseText, new GUIStyle(toastStyle) { fontSize = Mathf.RoundToInt(40 * scale) },
                    flash ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.85f, 0.4f));
                var bar = new Rect(w * 0.5f - 260 * scale, h * 0.14f + 62 * scale, 520 * scale, 14 * scale);
                Fill(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0f, 0f, 0f, 0.7f));
                Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(chaseFraction), bar.height), new Color(1f, 0.4f, 0.2f));
            }

            float y = h * 0.62f;
            foreach (var toast in Toasts)
            {
                float alpha = Mathf.Clamp01((toast.until - Time.time) / 0.4f);
                Label(new Rect(0, y, w, 40 * scale), toast.text, toastStyle, new Color(toast.color.r, toast.color.g, toast.color.b, alpha));
                y += 38 * scale;
            }

            if (Time.time < shoutUntil && shout != null)
            {
                float t = Time.time - shoutStart;
                float pop = t < 0.15f ? Mathf.Lerp(2.2f, 1f, t / 0.15f) : 1f;
                float shake = Mathf.Clamp01(1.2f - t) * 10f * scale;
                var rect = new Rect(Mathf.Sin(t * 70f) * shake, h * 0.3f + Mathf.Cos(t * 53f) * shake, w, 120 * scale);
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.ScaleAroundPivot(Vector2.one * pop, new Vector2(w * 0.5f, h * 0.3f + 60 * scale));
                float alpha = Mathf.Clamp01((shoutUntil - Time.time) / 0.4f);
                Label(rect, shout, shoutStyle, new Color(shoutColor.r, shoutColor.g, shoutColor.b, alpha));
                GUI.matrix = previous;
            }

            if (quiz != null) QuizGui(scale, w, h);
        }

        private void DrivingGui(float scale, float w, float h)
        {
            DrunkDriving driving = DrunkDriving.Instance;
            if (driving == null || !(driving.Unlocked || driving.InCar || driving.Free) || UiMenus.FullScreenOpen) return;
            float statusTop = h - 156f * scale; // GameHud.BottomReserved at 1080p
            if (driving.InCar)
            {
                DrunkCar car = driving.Car;
                var panel = new Rect(w * 0.5f - 230 * scale, statusTop - 96 * scale, 460 * scale, 86 * scale);
                Fill(panel, new Color(0.05f, 0.07f, 0.08f, 0.72f));
                Meter(new Rect(panel.x + 14 * scale, panel.y + 12 * scale, 300 * scale, 14 * scale), car.EndlessBottle ? 1f : car.Bottle01,
                    new Color(0.85f, 0.55f, 0.18f), car.EndlessBottle ? "FLASK  (bottomless)" : "FLASK", scale);
                Meter(new Rect(panel.x + 14 * scale, panel.y + 42 * scale, 300 * scale, 14 * scale), car.Health01, new Color(0.45f, 0.85f, 0.8f), "CAR", scale);
                Label(new Rect(panel.x + 330 * scale, panel.y + 6 * scale, 120 * scale, 50 * scale), $"{car.Speed * 3.6f:0}",
                    new GUIStyle(titleStyle) { fontSize = Mathf.RoundToInt(40 * scale), alignment = TextAnchor.MiddleRight }, Color.white);
                Label(new Rect(panel.x + 330 * scale, panel.y + 48 * scale, 120 * scale, 24 * scale), "km/h", new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, new Color(1f, 1f, 1f, 0.7f));
                Label(new Rect(0, panel.y - 28 * scale, w, 24 * scale), "WASD drive    SPACE handbrake    CLICK horn    F bail out", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter },
                    new Color(1f, 1f, 1f, 0.75f));
                return;
            }
            if (driving.ChugActive)
            {
                var bar = new Rect(w * 0.5f - 160 * scale, h * 0.58f, 320 * scale, 16 * scale);
                Meter(bar, driving.ChugProgress, new Color(0.9f, 0.6f, 0.2f), "CHUG!", scale);
                return;
            }
            // The pip: ready, or the cooldown draining.
            var pip = new Rect(24 * scale, statusTop - 50 * scale, 230 * scale, 40 * scale);
            Fill(pip, new Color(0.05f, 0.07f, 0.08f, 0.65f));
            float cooling = driving.Free ? 0f : driving.CooldownRemaining;
            if (cooling > 0f)
            {
                Fill(new Rect(pip.x, pip.y, pip.width * (1f - cooling / driving.CooldownLength), pip.height), new Color(0.3f, 0.45f, 0.45f, 0.6f));
                Label(new Rect(pip.x + 10 * scale, pip.y, pip.width, pip.height), $"CAR  {cooling:0} s", small, new Color(1f, 1f, 1f, 0.75f));
            }
            else
            {
                Fill(new Rect(pip.x, pip.y, 6 * scale, pip.height), new Color(1f, 0.7f, 0.25f));
                Label(new Rect(pip.x + 14 * scale, pip.y, pip.width, pip.height), "CAR READY  hold F", small, new Color(1f, 0.85f, 0.55f));
            }
        }

        private void Meter(Rect rect, float fraction, Color color, string label, float scale)
        {
            Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(0f, 0f, 0f, 0.7f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color);
            Label(new Rect(rect.xMax + 8 * scale, rect.y - 6 * scale, 200 * scale, rect.height + 12 * scale), label, small, Color.white);
        }

        private void WorldBarGui(Transform anchor, float fraction, Color color, float height, float scale)
        {
            Camera view = Camera.main;
            if (view == null || anchor == null) return;
            Vector3 point = view.WorldToScreenPoint(anchor.position + Vector3.up * height);
            if (point.z <= 0f) return;
            var rect = new Rect(point.x - 60 * scale, Screen.height - point.y, 120 * scale, 10 * scale);
            Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), new Color(0f, 0f, 0f, 0.7f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color);
        }

        private void QuizGui(float scale, float w, float h)
        {
            var panel = new Rect(w * 0.5f - 420 * scale, h * 0.2f, 840 * scale, 520 * scale);
            Fill(panel, new Color(0.08f, 0.1f, 0.1f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6 * scale), new Color(1f, 0.75f, 0.3f));
            Label(new Rect(panel.x, panel.y + 18 * scale, panel.width, 40 * scale), quiz.Title, toastStyle, new Color(1f, 0.8f, 0.45f));
            Label(new Rect(panel.x + 40 * scale, panel.y + 70 * scale, panel.width - 80 * scale, 110 * scale), quiz.Question, question, Color.white);
            for (int i = 0; i < quiz.Answers.Length; i++)
            {
                var rect = new Rect(panel.x + 60 * scale, panel.y + (200 + i * 92) * scale, panel.width - 120 * scale, 78 * scale);
                bool picked = GUI.Button(rect, $"{(char)('A' + i)})  {quiz.Answers[i]}", button);
                if (!picked && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Alpha1 + i) picked = true;
                if (!picked) continue;
                Quiz answered = quiz;
                quiz = null;
                UiMenus.Close(this);
                answered.Done?.Invoke(i);
                break;
            }
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f * color.a);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
