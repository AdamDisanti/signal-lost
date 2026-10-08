using System.Collections;
using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// Test target for combat scenes. Reddens as it loses health, flashes when hit,
    /// disappears at zero health, and comes back at full health after a delay.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color healthyColor = new Color(0.85f, 0.85f, 0.8f);
        [SerializeField] private Color damagedColor = new Color(0.8f, 0.15f, 0.1f);
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Min(0f)] private float flashSeconds = 0.06f;
        [SerializeField, Min(0f)] private float respawnSeconds = 3f;

        private Health health;
        private Renderer[] renderers;
        private Collider[] colliders;
        private MaterialPropertyBlock block;
        private float flashUntil;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            ApplyColor(CurrentColor());
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update() => ApplyColor(Time.time < flashUntil ? hitFlashColor : CurrentColor());

        private Color CurrentColor() => Color.Lerp(damagedColor, healthyColor, health.Normalized);

        // A continuous beam damages every frame, so the flash stays on while the beam is held.
        private void OnDamaged(Health _, float amount, GameObject source) => flashUntil = Time.time + flashSeconds;

        private void OnDied(Health _) => StartCoroutine(Respawn());

        private IEnumerator Respawn()
        {
            SetVisible(false);
            yield return new WaitForSeconds(respawnSeconds);
            health.Restore();
            flashUntil = 0f;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer item in renderers) item.enabled = visible;
            foreach (Collider item in colliders) item.enabled = visible;
        }

        private void ApplyColor(Color color)
        {
            foreach (Renderer item in renderers)
            {
                item.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                item.SetPropertyBlock(block);
            }
        }
    }
}
