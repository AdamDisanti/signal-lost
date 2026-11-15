using System;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Anything thrown in an arc at a spot on the ground: the possum's moonshine jugs, the otter's mud balls, the
    /// witch's potion globs. It flies from A to B in a set time along a parabola, spinning, then calls its landing
    /// action. A telegraph ring marks the landing spot. If made shootable, a bolt or swing pops it in mid-air
    /// (<see cref="Popped"/>), which counts as landing where it is.
    /// </summary>
    public sealed class Lob : MonoBehaviour
    {
        private Vector3 from, to, spin;
        private float duration, height, born;
        private Action<Vector3> landed;
        private GroundRing ring;
        private bool done;

        public bool Popped { get; private set; }

        public static Lob Throw(Transform model, Vector3 start, Vector3 target, float seconds, float arcHeight, Action<Vector3> onLand,
            float ringRadius = 0f, Color? ringColor = null, float shootableHp = 0f)
        {
            var go = new GameObject("Lob");
            go.transform.position = start;
            var lob = go.AddComponent<Lob>();
            model.SetParent(go.transform, false);
            model.localPosition = Vector3.zero;
            lob.from = start;
            lob.to = target;
            lob.duration = Mathf.Max(0.1f, seconds);
            lob.height = arcHeight;
            lob.landed = onLand;
            lob.born = Time.time;
            lob.spin = UnityEngine.Random.onUnitSphere * 360f;
            if (ringRadius > 0f) lob.ring = GroundRing.Spawn(target, ringRadius, ringColor ?? new Color(1f, 0.3f, 0.2f, 0.55f));
            if (shootableHp > 0f)
            {
                go.AddComponent<SphereCollider>().radius = 0.45f;
                var health = go.AddComponent<Health>();
                health.SetMax(shootableHp);
                health.Restore();
                health.Died += _ => { lob.Popped = true; lob.Land(go.transform.position); };
            }
            return lob;
        }

        private void Update()
        {
            if (done) return;
            float t = (Time.time - born) / duration;
            if (ring != null) ring.Progress = Mathf.Clamp01(t);
            if (t >= 1f) { Land(to); return; }
            Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * (4f * height * t * (1f - t));
            transform.position = p;
            transform.Rotate(spin * Time.deltaTime, Space.World);
        }

        private void Land(Vector3 at)
        {
            if (done) return;
            done = true;
            if (ring != null) Destroy(ring.gameObject);
            landed?.Invoke(at);
            Destroy(gameObject);
        }
    }

    /// <summary>A telegraph ring on the ground that fills from the middle as the attack comes (red unless told otherwise).</summary>
    public sealed class GroundRing : MonoBehaviour
    {
        private Transform fill;
        private float radius;

        /// <summary>0 to 1: how close the attack is.</summary>
        public float Progress { get; set; }

        public static GroundRing Spawn(Vector3 point, float radius, Color color)
        {
            var go = new GameObject("Ground Ring");
            if (GroundProbe.Find(point, 3f, 5f, out Vector3 ground)) point = ground;
            go.transform.position = point + Vector3.up * 0.06f;
            var ring = go.AddComponent<GroundRing>();
            ring.radius = radius;
            Material edge = MudArt.Clear(new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.3f)), 1.5f);
            Material inner = MudArt.Clear(new Color(color.r, color.g, color.b, color.a * 0.55f), 1f);
            Transform outline = Disc(go.transform, "Edge", edge, 1f, 0.9f);
            outline.localScale = Vector3.one * radius;
            ring.fill = Disc(go.transform, "Fill", inner, 1f, 0f);
            ring.fill.localScale = Vector3.zero;
            return ring;
        }

        private static Transform Disc(Transform parent, string name, Material material, float outer, float innerFraction)
        {
            var part = new GameObject(name).transform;
            part.SetParent(parent, false);
            var kit = new MeshKit();
            const int sides = 24;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * outer;
                var o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * outer;
                if (innerFraction <= 0f) kit.Tri(Vector3.zero, o1, o0, Color.white);
                else kit.Quad(o1 * innerFraction, o1, o0, o0 * innerFraction, Color.white);
            }
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = kit.Build(name);
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return part;
        }

        public void MoveTo(Vector3 point) => transform.position = point + Vector3.up * 0.06f;

        private void Update()
        {
            fill.localScale = Vector3.one * radius * Mathf.Clamp01(Progress);
            float pulse = 1f + Mathf.Sin(Time.time * (8f + Progress * 14f)) * 0.03f;
            transform.localScale = Vector3.one * pulse;
        }

        /// <summary>Removes it after a short flash.</summary>
        public void Finish(float delay = 0.05f) => Destroy(gameObject, delay);
    }
}
