using System.Collections;
using System.Collections.Generic;
using SignalLost.Effects;
using SignalLost.NPC;
using UnityEngine;

namespace SignalLost.World
{
    /// <summary>
    /// Cavendish's camp on an island the boat sails to (11-08): the same base camp (fire, tent, two logs, supplies,
    /// trophy stands), themed for that island, with Cavendish and Eugene living there. When the player arrives by boat,
    /// the camp starts packed away; the <see cref="IslandVoyage"/> brings the two of them ashore and they set it up,
    /// piece by piece. Loaded any other way (a fresh start, a respawn, the editor) it is simply there, with them
    /// sitting by the fire.
    /// </summary>
    public sealed class IslandCamp : MonoBehaviour
    {
        /// <summary>How far Cavendish's (unrigged) model sinks to look seated on a log.</summary>
        public const float CavendishSitDrop = 0.55f;

        [SerializeField] private NpcActor cavendish;
        [SerializeField] private NpcActor merchant;
        [Tooltip("Log tops they sit on.")]
        [SerializeField] private Transform cavendishSeat;
        [SerializeField] private Transform merchantSeat;
        [Tooltip("Where they stand to pitch camp, beside the fire.")]
        [SerializeField] private Transform cavendishStand;
        [SerializeField] private Transform merchantStand;
        [Tooltip("The camp's pieces in the order they go up.")]
        [SerializeField] private Transform[] pieces = new Transform[0];

        private static readonly List<IslandCamp> All = new List<IslandCamp>();
        private Vector3[] scales;

        public static IslandCamp Current => All.Count > 0 ? All[0] : null;

        public NpcActor Cavendish => cavendish;
        public NpcActor Merchant => merchant;
        public Transform CavendishSeat => cavendishSeat;
        public Transform MerchantSeat => merchantSeat;
        public Transform CavendishStand => cavendishStand != null ? cavendishStand : cavendishSeat;
        public Transform MerchantStand => merchantStand != null ? merchantStand : merchantSeat;
        public bool IsPacked { get; private set; }
        /// <summary>The middle of the camp (the fire).</summary>
        public Vector3 Centre => transform.position;

        private void Awake()
        {
            scales = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) if (pieces[i] != null) scales[i] = pieces[i].localScale;
            // Arriving by boat: everything is still in the boat.
            if (IslandVoyage.InProgress) Pack();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void Pack()
        {
            foreach (Transform piece in pieces) if (piece != null) piece.gameObject.SetActive(false);
            IsPacked = true;
        }

        /// <summary>Puts the camp up piece by piece over about <paramref name="seconds"/>: each one pops up out of a puff of sand.</summary>
        public IEnumerator SetUp(float seconds)
        {
            float gap = pieces.Length > 1 ? Mathf.Max(0.05f, (seconds - 0.45f) / (pieces.Length - 1)) : 0f;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null || pieces[i].gameObject.activeSelf) continue;
                StartCoroutine(PopUp(i));
                yield return new WaitForSeconds(gap);
            }
            yield return new WaitForSeconds(0.45f);
            IsPacked = false;
        }

        /// <summary>Everything up at once (skipping the cutscene).</summary>
        public void SetUpNow()
        {
            StopAllCoroutines();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].gameObject.SetActive(true);
                pieces[i].localScale = scales[i];
            }
            IsPacked = false;
        }

        private IEnumerator PopUp(int index)
        {
            Transform piece = pieces[index];
            Vector3 full = scales[index];
            piece.localScale = full * 0.05f;
            piece.gameObject.SetActive(true);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, piece.position + Vector3.up * 0.2f, Vector3.up, 14, 0.45f, 2.2f, 70f);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float u = t / 0.4f;
                // Up past full size and back: a little bounce as it lands.
                float k = 1f + Mathf.Sin(u * Mathf.PI) * 0.12f;
                piece.localScale = full * (Mathf.SmoothStep(0.05f, 1f, Mathf.Min(1f, u * 1.6f)) * k);
                yield return null;
            }
            piece.localScale = full;
        }

        /// <summary>Sits Cavendish and Eugene on their logs.</summary>
        public void SeatResidents()
        {
            if (cavendish != null && cavendishSeat != null) SitCavendish(cavendish.transform, cavendishSeat);
            if (merchant != null && merchantSeat != null) merchant.transform.SetPositionAndRotation(merchantSeat.position, merchantSeat.rotation);
        }

        /// <summary>Cavendish's root on the seat, his model sunk onto it (standing: drop 0).</summary>
        public static void SitCavendish(Transform cavendish, Transform seat)
        {
            cavendish.SetPositionAndRotation(seat.position, seat.rotation);
            SetCavendishDrop(cavendish, CavendishSitDrop);
        }

        public static void SetCavendishDrop(Transform cavendish, float drop)
        {
            Transform visual = cavendish.Find("Visual");
            if (visual != null) visual.localPosition = new Vector3(visual.localPosition.x, -drop, visual.localPosition.z);
        }
    }
}
