using System;
using SignalLost.Combat;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Inventory
{
    /// <summary>
    /// Drops scrap when this enemy's <see cref="Health"/> reaches zero (meeting notes 2026-10-20). Each entry
    /// drops a random amount of one scrap type; the chunks pop out around the body for the player to collect.
    /// Placeholder amounts.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class ScrapDropper : MonoBehaviour
    {
        [Serializable]
        public struct Drop
        {
            public ScrapType type;
            [Min(0)] public int min;
            [Min(0)] public int max;
        }

        [SerializeField] private Drop[] drops = { new Drop { type = ScrapType.ScrapMetal, min = 1, max = 2 } };
        [Tooltip("Height above the enemy's origin the scrap pops out from.")]
        [SerializeField, Min(0f)] private float popHeight = 0.6f;

        private Health health;

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(Health _)
        {
            Vector3 origin = transform.position + Vector3.up * popHeight;
            foreach (Drop drop in drops)
            {
                int count = Random.Range(drop.min, Mathf.Max(drop.min, drop.max) + 1);
                for (int i = 0; i < count; i++)
                {
                    Vector2 spread = Random.insideUnitCircle * 2.2f;
                    ScrapPickup.Spawn(drop.type, origin, new Vector3(spread.x, Random.Range(4f, 6f), spread.y));
                }
            }
        }
    }
}
