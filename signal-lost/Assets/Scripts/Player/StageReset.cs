using SignalLost.Combat;
using SignalLost.Enemies;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Puts a stage back the way it started whenever the player respawns: the <see cref="Group"/> (usually the
    /// enemies) is rebuilt from a copy taken at scene start, so dead or wandered-off enemies return at full health,
    /// and leftover projectiles (laser bolts, goo) are cleared. Put one in each scene that needs it.
    /// </summary>
    public sealed class StageReset : MonoBehaviour
    {
        [Tooltip("Everything under this object is restored on respawn. Usually the scene's enemies.")]
        [SerializeField] private GameObject group;

        private GameObject snapshot;
        private PlayerLife player;

        public GameObject Group => group;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerLife>();
            if (player != null) player.Respawned += ResetStage;
            if (group == null) return;
            // An inactive stage-start copy also restores enemies that were destroyed.
            snapshot = new GameObject("Stage-start snapshot (" + group.name + ")");
            snapshot.SetActive(false);
            Instantiate(group, snapshot.transform, true).name = group.name;
        }

        private void OnDestroy()
        {
            if (player != null) player.Respawned -= ResetStage;
            if (snapshot != null) Destroy(snapshot);
        }

        public void ResetStage()
        {
            foreach (LaserBolt bolt in FindObjectsByType<LaserBolt>()) Destroy(bolt.gameObject);
            foreach (GooBlob blob in FindObjectsByType<GooBlob>()) Destroy(blob.gameObject);
            foreach (GooPuddle puddle in FindObjectsByType<GooPuddle>()) Destroy(puddle.gameObject);
            if (group == null || snapshot == null) return;
            Transform parent = group.transform.parent;
            string name = group.name;
            group.SetActive(false);
            Destroy(group);
            group = Instantiate(snapshot.transform.GetChild(0).gameObject, parent, true);
            group.name = name;
            group.SetActive(true);
        }
    }
}
