using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Combat
{
    /// <summary>
    /// Electricity at the mining laser's muzzle while a shot charges: a small flickering core plus jagged
    /// arcs that crawl back along the barrel and spark outward. Arcs redraw every few hundredths of a second
    /// so they crackle; more of them appear, and they reach further, as the charge builds.
    /// Reaching full charge is a distinct moment: a shockwave ring and core flash burst out, then the arcs
    /// turn white-hot and run the whole barrel, and a crackling ring hovers around the muzzle until firing.
    /// Sits on an object at the muzzle whose local -Z points back along the barrel.
    /// Placeholder effect built from primitives and LineRenderers until real particles are made.
    /// </summary>
    public sealed class ChargeEffect : MonoBehaviour
    {
        private const int MaxArcs = 7;
        private const int ArcPoints = 7;
        private const int RingPoints = 20;
        private const float BurstTime = 0.18f;

        private LineRenderer[] arcs;
        private LineRenderer ring;
        private Material chargingArcMaterial, fullArcMaterial;
        private float burstStart = float.NegativeInfinity, ringRedrawAt, ringSpin;
        private bool wasFull;
        private float[] redrawAt;
        private Transform core;
        private Renderer coreRenderer;
        private float barrelLength;
        private float charge;
        private bool active;

        /// <summary>Builds the core and arcs. barrelLength is how far back along -Z arcs may crawl.</summary>
        public void Build(Material arcMaterial, Material fullMaterial, Material coreMaterial, float barrelLength)
        {
            this.barrelLength = Mathf.Max(0.05f, barrelLength);
            chargingArcMaterial = arcMaterial;
            fullArcMaterial = fullMaterial;

            GameObject coreObject = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Charge Core", coreMaterial);
            core = coreObject.transform;
            core.SetParent(transform, false);
            coreRenderer = coreObject.GetComponent<Renderer>();
            coreRenderer.enabled = false;

            arcs = new LineRenderer[MaxArcs];
            redrawAt = new float[MaxArcs];
            for (int i = 0; i < MaxArcs; i++)
            {
                var arcObject = new GameObject("Charge Arc " + (i + 1));
                arcObject.transform.SetParent(transform, false);
                LineRenderer arc = arcObject.AddComponent<LineRenderer>();
                arc.sharedMaterial = arcMaterial;
                arc.useWorldSpace = false; // arcs move with the laser during recoil and pose changes
                arc.positionCount = ArcPoints;
                arc.numCapVertices = 0;
                arc.shadowCastingMode = ShadowCastingMode.Off;
                arc.receiveShadows = false;
                arc.enabled = false;
                arcs[i] = arc;
            }

            // Ring around the muzzle, shown only at full charge. It lies across the barrel (local XY plane).
            var ringObject = new GameObject("Full Charge Ring");
            ringObject.transform.SetParent(transform, false);
            ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = fullMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
        }

        /// <summary>True for a moment after full charge is first reached.</summary>
        private bool Bursting => Time.time - burstStart < BurstTime;

        /// <summary>Call every frame. charge01 runs from 0 when charging starts to 1 at full charge.</summary>
        public void Show(bool show, float charge01)
        {
            active = show;
            charge = Mathf.Clamp01(charge01);
            if (arcs == null) return;

            coreRenderer.enabled = active;
            if (!active)
            {
                foreach (LineRenderer arc in arcs) arc.enabled = false;
                ring.enabled = false;
                wasFull = false;
                return;
            }

            bool full = charge >= 1f;
            if (full && !wasFull)
            {
                // The moment full charge is reached: burst, and redraw every arc this frame.
                burstStart = Time.time;
                for (int i = 0; i < MaxArcs; i++) redrawAt[i] = 0f;
                ringRedrawAt = 0f;
            }
            wasFull = full;
            bool bursting = full && Bursting;
            float burst = bursting ? (Time.time - burstStart) / BurstTime : 1f;

            // Core: a small flickering spark while charging; at full it flashes big, then stays larger and pulses.
            float diameter;
            if (bursting) diameter = Mathf.Lerp(0.18f, 0.085f, burst);
            else if (full) diameter = (0.085f + 0.02f * Mathf.Sin(Time.time * 22f)) * Random.Range(0.9f, 1.1f);
            else diameter = Mathf.Lerp(0.02f, 0.05f, charge) * Random.Range(0.75f, 1.25f);
            core.localScale = Vector3.one * diameter;

            UpdateRing(full, bursting, burst);

            // Arcs: one at the start of a charge, all of them at full.
            int visible = Mathf.Clamp(1 + Mathf.FloorToInt(charge * (MaxArcs - 1)), 1, MaxArcs);
            for (int i = 0; i < MaxArcs; i++)
            {
                LineRenderer arc = arcs[i];
                if (i >= visible)
                {
                    arc.enabled = false;
                    continue;
                }
                if (Time.time < redrawAt[i]) continue;

                // Each arc lives for a few hundredths of a second, with gaps so they crackle rather than glow.
                bool on = bursting || Random.value < (full ? 0.9f : 0.7f);
                arc.enabled = on;
                arc.sharedMaterial = full ? fullArcMaterial : chargingArcMaterial;
                if (on) Redraw(arc, full, bursting);
                redrawAt[i] = Time.time + Random.Range(0.03f, full ? 0.05f : 0.08f);
            }
        }

        private void UpdateRing(bool full, bool bursting, float burst)
        {
            ring.enabled = full;
            if (!full) return;

            // During the burst the ring flies outward like a shockwave; afterwards it hovers and crackles.
            if (!bursting && Time.time < ringRedrawAt) return;
            ringRedrawAt = Time.time + 0.04f;
            ringSpin += Random.Range(20f, 50f);

            float radius = bursting ? Mathf.Lerp(0.03f, 0.22f, burst) : 0.055f + 0.008f * Mathf.Sin(Time.time * 18f);
            float roughness = bursting ? 0.004f : 0.008f;
            for (int p = 0; p < RingPoints; p++)
            {
                float angle = (ringSpin + p * 360f / RingPoints) * Mathf.Deg2Rad;
                float r = radius + Random.Range(-roughness, roughness);
                ring.SetPosition(p, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, Random.Range(-0.006f, 0.006f)));
            }
            float width = bursting ? Mathf.Lerp(0.02f, 0.004f, burst) : 0.007f;
            ring.startWidth = width;
            ring.endWidth = width;
        }

        private void Redraw(LineRenderer arc, bool full, bool bursting)
        {
            Vector3 start = Random.insideUnitSphere * 0.015f;
            Vector3 end;
            if (bursting)
            {
                // Burst: arcs fly straight out from the muzzle in every direction across the barrel.
                Vector2 outward = Random.insideUnitCircle.normalized * Random.Range(0.14f, 0.24f);
                end = new Vector3(outward.x, outward.y, Random.Range(-0.03f, 0.06f));
            }
            else if (Random.value < (full ? 0.7f : 0.6f))
            {
                // Crawl back along the barrel, hugging its surface. At full charge they run its whole length.
                float back = (full ? Random.Range(0.6f, 1.05f) : Random.Range(0.25f, Mathf.Lerp(0.5f, 1f, charge))) * barrelLength;
                Vector2 around = Random.insideUnitCircle.normalized * Random.Range(0.03f, 0.05f);
                end = new Vector3(around.x, around.y, -back);
            }
            else
            {
                // Spark outward from the muzzle, reaching further as the charge builds.
                end = Random.onUnitSphere * (full ? Random.Range(0.1f, 0.17f) : Mathf.Lerp(0.04f, 0.12f, charge));
                end.z = Mathf.Abs(end.z) * 0.5f; // mostly forward and sideways, not into the barrel
            }

            // Jagged path: straight line from start to end, with each inner point knocked sideways.
            Vector3 along = end - start;
            float length = along.magnitude;
            Vector3 side = Vector3.Cross(along, Random.onUnitSphere).normalized;
            Vector3 up = Vector3.Cross(along, side).normalized;
            float jitter = Mathf.Clamp(length * 0.25f, 0.008f, 0.03f);
            for (int p = 0; p < ArcPoints; p++)
            {
                float t = p / (ArcPoints - 1f);
                Vector3 point = start + along * t;
                if (p > 0 && p < ArcPoints - 1)
                    point += side * Random.Range(-jitter, jitter) + up * Random.Range(-jitter, jitter);
                arc.SetPosition(p, point);
            }

            float width = Random.Range(0.004f, 0.009f) * (full ? 2f : Mathf.Lerp(1f, 1.5f, charge));
            arc.startWidth = width;
            arc.endWidth = width * 0.4f;
        }
    }
}
