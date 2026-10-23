using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Meadow
{
    /// <summary>
    /// The ring of water that radiates out from where the Chief Toad lands: a low wall of water racing outward,
    /// throwing spray off its crest and leaving a foam ring on the ground, shrinking as it spreads. It hits the player
    /// once if it passes them while they are on the ground (jump it), knocking them outward. Built in code, so it
    /// splashes the same on dry land as in the pond.
    /// </summary>
    public sealed class SplashWave : MonoBehaviour
    {
        private const int Sides = 40;
        private static Mesh band;

        private float maxRadius, speed, damage, radius;
        private GameObject owner;
        private Transform wall;
        private LineRenderer foam;
        private Transform player;
        private CharacterController playerBody;
        private Health playerHealth;
        private bool hitPlayer;

        public float Radius => radius;

        /// <param name="material">The water look for the wall (the toad's water material); null uses a soft glow.</param>
        public static SplashWave Spawn(Material material, Vector3 centre, float maxRadius, float speed, float damage, GameObject owner)
        {
            var go = new GameObject("Splash Wave");
            go.transform.position = centre;
            var wave = go.AddComponent<SplashWave>();
            wave.maxRadius = maxRadius;
            wave.speed = speed;
            wave.damage = damage;
            wave.owner = owner;
            wave.radius = 0.6f;

            var wall = new GameObject("Wall");
            wall.transform.SetParent(go.transform, false);
            wall.AddComponent<MeshFilter>().sharedMesh = Band();
            var renderer = wall.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null ? material : EffectMaterials.AlphaBlended(EffectMaterials.Look.Glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            wave.wall = wall.transform;

            wave.foam = go.AddComponent<LineRenderer>();
            wave.foam.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Beam);
            wave.foam.useWorldSpace = false;
            wave.foam.loop = true;
            wave.foam.positionCount = Sides;
            wave.foam.shadowCastingMode = ShadowCastingMode.Off;
            wave.foam.receiveShadows = false;

            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                wave.player = controller.transform;
                wave.playerBody = controller.GetComponent<CharacterController>();
                wave.playerHealth = controller.GetComponent<Health>();
            }
            wave.Draw();
            return wave;
        }

        private void Update()
        {
            radius += speed * Time.deltaTime;
            if (radius >= maxRadius)
            {
                Destroy(gameObject);
                return;
            }
            Draw();

            // Spray off the crest, more of it while the wave is young and high.
            float k = radius / maxRadius;
            int count = Mathf.RoundToInt(Mathf.Lerp(10f, 3f, k));
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = transform.position + outward * radius + Vector3.up * Height * 0.8f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Water, at, outward + Vector3.up * 1.6f, 1, 0.16f, Mathf.Lerp(5f, 2.5f, k), 25f);
                if (i == 0) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, outward + Vector3.up, 1, 1f, 1.2f, 30f);
            }

            if (hitPlayer || player == null || playerHealth == null || playerHealth.IsDead) return;
            Vector3 offset = player.position - transform.position;
            float height = offset.y;
            offset.y = 0f;
            float distance = offset.magnitude;
            // The crest is passing the player, who is on the ground (not jumping it) and near the wave's level.
            if (Mathf.Abs(distance - radius) > 0.9f || height > 2.5f || height < -2.5f) return;
            if (playerBody != null && !playerBody.isGrounded) return;
            hitPlayer = true;
            if (EnemyAllegiance.CanHurt(owner, playerHealth)) playerHealth.TakeDamage(damage, owner);
            if (playerBody != null && playerBody.enabled && distance > 0.01f) playerBody.Move(offset / distance * 1.2f);
            CameraShake.Add(0.3f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, player.position + Vector3.up, offset + Vector3.up * 2f, 18, 0.2f, 5f, 50f);
        }

        private float Height => Mathf.Lerp(1.1f, 0.25f, radius / maxRadius);

        private void Draw()
        {
            float k = radius / maxRadius;
            wall.localScale = new Vector3(radius, Height, radius);
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                foam.SetPosition(i, new Vector3(Mathf.Cos(a) * (radius - 0.3f), 0.06f, Mathf.Sin(a) * (radius - 0.3f)));
            }
            foam.startWidth = foam.endWidth = Mathf.Lerp(0.9f, 0.3f, k);
            Color white = new Color(0.92f, 0.97f, 1f, 0.75f * (1f - k));
            foam.startColor = foam.endColor = white;
        }

        // An open ring band of unit radius and unit height, leaning in at the top, visible from both sides.
        private static Mesh Band()
        {
            if (band != null) return band;
            var vertices = new Vector3[(Sides + 1) * 4];
            var triangles = new int[Sides * 12];
            for (int i = 0; i <= Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 bottom = dir, top = dir * 0.9f + Vector3.up;
                vertices[i * 4] = bottom;
                vertices[i * 4 + 1] = top;
                vertices[i * 4 + 2] = bottom; // inside copies, so each side gets its own normal
                vertices[i * 4 + 3] = top;
            }
            for (int i = 0, t = 0; i < Sides; i++)
            {
                int a = i * 4, b = (i + 1) * 4;
                // Outside (faces outward), then inside (faces in).
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b + 1;
                triangles[t++] = a; triangles[t++] = b + 1; triangles[t++] = b;
                triangles[t++] = a + 2; triangles[t++] = b + 3; triangles[t++] = a + 3;
                triangles[t++] = a + 2; triangles[t++] = b + 2; triangles[t++] = b + 3;
            }
            band = new Mesh { name = "Splash Wave" };
            band.vertices = vertices;
            band.triangles = triangles;
            band.RecalculateNormals();
            band.RecalculateBounds();
            return band;
        }
    }
}
