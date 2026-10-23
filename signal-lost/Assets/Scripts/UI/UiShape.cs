using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SignalLost.UI
{
    /// <summary>
    /// A flat-coloured polygon drawn on a canvas (points in the RectTransform's local pixels, filled as a fan from the
    /// first point, so keep it convex), or a "ridge": a skyline filled straight down to a base height, for mountains
    /// and hills with valleys. Used for the low-poly art on the title screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiShape : MaskableGraphic
    {
        private readonly List<Vector2> points = new List<Vector2>();
        private Color topTint = Color.white;
        private bool ridge;
        private float ridgeBase;

        public static UiShape Create(string name, Transform parent, Color color, params Vector2[] polygon)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var shape = go.AddComponent<UiShape>();
            shape.color = color;
            shape.raycastTarget = false;
            shape.SetPoints(polygon);
            return shape;
        }

        /// <summary>A skyline (points left to right) filled down to <paramref name="baseY"/>.</summary>
        public static UiShape Ridge(string name, Transform parent, Color color, float baseY, params Vector2[] skyline)
        {
            UiShape shape = Create(name, parent, color);
            shape.ridge = true;
            shape.ridgeBase = baseY;
            shape.SetPoints(skyline);
            return shape;
        }

        public void SetPoints(IEnumerable<Vector2> polygon)
        {
            points.Clear();
            points.AddRange(polygon);
            SetVerticesDirty();
        }

        /// <summary>Multiplies the colour toward the top of the shape, for a simple lit-from-above gradient.</summary>
        public void SetTopTint(Color tint)
        {
            topTint = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (ridge)
            {
                float top = float.MinValue;
                foreach (Vector2 p in points) top = Mathf.Max(top, p.y);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 a = points[i], b = points[i + 1];
                    int start = vh.currentVertCount;
                    vh.AddVert(new Vector2(a.x, ridgeBase), color, Vector2.zero);
                    vh.AddVert(a, color * Color.Lerp(Color.white, topTint, Mathf.InverseLerp(ridgeBase, top, a.y)), Vector2.zero);
                    vh.AddVert(b, color * Color.Lerp(Color.white, topTint, Mathf.InverseLerp(ridgeBase, top, b.y)), Vector2.zero);
                    vh.AddVert(new Vector2(b.x, ridgeBase), color, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start, start + 2, start + 3);
                }
                return;
            }
            if (points.Count < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 p in points) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            foreach (Vector2 p in points)
            {
                float t = Mathf.InverseLerp(minY, maxY, p.y);
                vh.AddVert(p, color * Color.Lerp(Color.white, topTint, t), Vector2.zero);
            }
            for (int i = 1; i < points.Count - 1; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
