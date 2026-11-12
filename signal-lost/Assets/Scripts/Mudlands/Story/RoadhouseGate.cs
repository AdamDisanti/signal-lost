using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The roadhouse gate before the stage boss: closed (solid) until the story opens it, then it swings aside.
    /// </summary>
    public sealed class RoadhouseGate : MonoBehaviour
    {
        [SerializeField] private Transform leftLeaf, rightLeaf;
        private float openedAt = -1f;

        public void Configure(Transform left, Transform right)
        {
            leftLeaf = left;
            rightLeaf = right;
        }

        public void Open()
        {
            if (openedAt >= 0f) return;
            openedAt = Time.time;
            // Only the two leaves stop being solid; the posts stay.
            foreach (Transform leaf in new[] { leftLeaf, rightLeaf })
                if (leaf != null) foreach (Collider part in leaf.GetComponentsInChildren<Collider>()) part.enabled = false;
            Sfx.PlayAt("crab_block", transform.position, 1f, 0.5f, 50f);
        }

        private void Update()
        {
            if (openedAt < 0f) return;
            float k = Mathf.SmoothStep(0f, 1f, (Time.time - openedAt) / 1.5f);
            if (leftLeaf != null) leftLeaf.localRotation = Quaternion.Euler(0f, -100f * k, 0f);
            if (rightLeaf != null) rightLeaf.localRotation = Quaternion.Euler(0f, 100f * k, 0f);
        }
    }
}
