using SignalLost.Combat;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// On-screen help for the Mudlands creatures (11-20), drawn with IMGUI like the desert's fodder overlay:
    /// <list type="bullet">
    /// <item>a marker over every Jug Possum that is after you (within 40 m): a small orange chevron, or a big pulsing
    /// "!" while it winds up a throw; when it is off-screen or behind you, an arrow on the edge of the screen points
    /// at it (always while it winds up);</item>
    /// <item>"MUDPUPPY ON YOU! Swing or jump to shake it off" while pups are clinging to the visor.</item>
    /// </list>
    /// Created the first time a possum or a clinging pup needs it; one per scene.
    /// </summary>
    public sealed class CritterOverlay : MonoBehaviour
    {
        private const float PossumRange = 40f;
        private static readonly Color Orange = new Color(1f, 0.55f, 0.15f), Hot = new Color(1f, 0.3f, 0.1f);

        private static CritterOverlay instance;
        private static Texture2D arrow;

        private GUIStyle banner, mark;
        private Health playerHealth;
        private Transform player;

        public static void Ensure()
        {
            if (instance != null) return;
            instance = FindAnyObjectByType<CritterOverlay>();
            if (instance == null) instance = new GameObject("Critter Overlay").AddComponent<CritterOverlay>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (player == null)
            {
                var body = FindAnyObjectByType<FirstPersonController>();
                if (body == null) return;
                player = body.transform;
                playerHealth = body.GetComponent<Health>();
            }
            if ((playerHealth != null && playerHealth.IsDead) || UiMenus.AnyOpen) return;
            float scale = Screen.height / 1080f;
            if (banner == null)
            {
                banner = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                mark = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            banner.fontSize = Mathf.RoundToInt(26f * scale);
            mark.fontSize = Mathf.RoundToInt(40f * scale);

            DrawPossums(scale);

            int pups = Mudpuppy.ClingingCount;
            if (pups > 0)
            {
                string text = pups == 1 ? "MUDPUPPY ON YOU!" : $"{pups} MUDPUPPIES ON YOU!";
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
                Label(new Rect(0f, Screen.height * 0.66f, Screen.width, 34f * scale), text, banner, new Color(1f, 0.55f * pulse + 0.2f, 0.4f));
                Label(new Rect(0f, Screen.height * 0.66f + 32f * scale, Screen.width, 30f * scale), "Swing or jump to shake them off", banner,
                    new Color(1f, 0.9f, 0.75f));
            }
        }

        private void DrawPossums(float scale)
        {
            Camera view = Camera.main;
            if (view == null) return;
            float margin = 70f * scale;
            // Edge arrows stay inside this box: clear of the quest panel at the top and the status bar at the bottom.
            var box = Rect.MinMaxRect(margin, 150f * scale, Screen.width - margin, Screen.height - 190f * scale);
            foreach (JugPossum possum in JugPossum.All)
            {
                if (possum == null || !possum.Hunting) continue;
                Vector3 at = possum.MarkerPoint;
                float distance = Vector3.Distance(player.position, at);
                if (distance > PossumRange) continue;
                bool winding = possum.WindingUp;
                float pulse = winding ? 0.7f + 0.3f * Mathf.Sin(Time.time * 18f) : 1f;

                Vector3 screen = view.WorldToScreenPoint(at);
                bool behind = screen.z < 0f;
                var gui = new Vector2(screen.x, Screen.height - screen.y);
                bool onScreen = !behind && gui.x > margin && gui.x < Screen.width - margin && gui.y > margin && gui.y < Screen.height - margin;

                if (onScreen)
                {
                    // Over its head: a chevron pointing down at it, and a big "!" while it winds up.
                    float size = (winding ? 34f : 22f) * scale * pulse;
                    Color colour = winding ? Hot : new Color(Orange.r, Orange.g, Orange.b, 0.85f);
                    Arrow(new Vector2(gui.x, gui.y - size * 0.4f), 180f, size, colour);
                    if (winding) Label(new Rect(gui.x - 40f * scale, gui.y - size - 46f * scale, 80f * scale, 44f * scale), "!", mark, Hot);
                    continue;
                }

                // Off-screen or behind: an arrow on the screen edge pointing the way (dimmer unless it's winding up).
                // Worked out in the camera's own space, so something behind you points left or right, not at the top.
                Vector3 local = view.transform.InverseTransformPoint(at);
                var direction = new Vector2(local.x, -local.y); // GUI y runs down
                if (behind)
                {
                    direction.y *= 0.25f; // behind: mostly sideways, toward the shorter turn
                    if (Mathf.Abs(direction.x) < 0.05f) direction.x = 1f;
                }
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
                direction.Normalize();
                Vector2 centre = box.center;
                float hx = box.width * 0.5f, hy = box.height * 0.5f;
                float reach = Mathf.Min(hx / Mathf.Max(0.001f, Mathf.Abs(direction.x)), hy / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
                Vector2 edge = centre + direction * reach;
                float angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg; // the arrow texture points up
                float arrowSize = (winding ? 46f : 30f) * scale * pulse;
                Arrow(edge, angle, arrowSize, winding ? Hot : new Color(Orange.r, Orange.g, Orange.b, 0.7f));
                if (winding) Label(new Rect(edge.x - direction.x * 44f * scale - 30f * scale, edge.y - direction.y * 44f * scale - 22f * scale,
                    60f * scale, 44f * scale), "!", mark, Hot);
            }
        }

        private static void Arrow(Vector2 centre, float degrees, float size, Color colour)
        {
            if (arrow == null) arrow = MakeArrow();
            Matrix4x4 before = GUI.matrix;
            Color previous = GUI.color;
            GUIUtility.RotateAroundPivot(degrees, centre);
            var rect = new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);
            GUI.color = new Color(0f, 0f, 0f, colour.a * 0.6f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), arrow);
            GUI.color = colour;
            GUI.DrawTexture(rect, arrow);
            GUI.color = previous;
            GUI.matrix = before;
        }

        // A soft-edged triangle pointing up.
        private static Texture2D MakeArrow()
        {
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Critter Overlay Arrow" };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Texture rows go bottom-up: the tip is at the top (y = n - 1).
                    float v = (y + 0.5f) / n, u = (x + 0.5f) / n;
                    float half = (1f - v) * 0.5f; // the triangle's half-width at this height
                    float inside = Mathf.Min(half - Mathf.Abs(u - 0.5f), v - 0.05f, 0.95f - v);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(inside * n / 2f));
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color colour)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = colour;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }
    }
}
