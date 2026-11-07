using UnityEngine;

namespace SignalLost.Enemies
{
    // Save and load (Christian, 10-27): the save-only part of FrogWaves. The rest is in Enemies/FrogWaves.cs.
    public sealed partial class FrogWaves
    {
        /// <summary>
        /// Loading a save: carries on as if <paramref name="defeated"/> frogs were already beaten, in the wave they
        /// reached, with only that wave's remaining frogs hopping in (once, instead of <see cref="Begin"/>).
        /// </summary>
        public void Resume(int defeated)
        {
            if (Started || frogPrefab == null) return;
            defeated = Mathf.Clamp(defeated, 0, Total);
            int before = 0, index = 0;
            while (index < waves.Length && before + waves[index] <= defeated) before += waves[index++];
            if (index >= waves.Length)
            {
                Defeated = defeated;
                wave = waves.Length - 1;
                Finished = true;
                Cleared?.Invoke();
                return;
            }
            // Spawn the whole wave the usual way, then take back the frogs already beaten in it. They are removed
            // before their first frame, so they are never seen and drop nothing.
            Defeated = before;
            Spawn(index);
            for (int beaten = defeated - before; beaten > 0 && alive.Count > 0; beaten--)
            {
                MortarFrog extra = alive[alive.Count - 1];
                alive.RemoveAt(alive.Count - 1);
                if (extra == null) continue;
                extra.gameObject.SetActive(false);
                Destroy(extra.gameObject);
            }
            Defeated = defeated;
        }
    }
}
