using SignalLost.Enemies;
using UnityEngine;

namespace SignalLost.NPC
{
    // Save and load (Christian, 10-27): the save-only part of MerchantCrab. The rest is in NPC/MerchantCrab.cs.
    public sealed partial class MerchantCrab
    {
        /// <summary>
        /// Loading a save from after the fight: the crab is already home on its log by the fire, friendly, with the
        /// flask handed over and its cousins gone. <paramref name="greeted"/>: it has introduced its shop already.
        /// </summary>
        public void RestoreFriendly(bool greeted)
        {
            gameObject.SetActive(true); // it waits switched off until the quest; Awake runs here if it has not yet
            StopDamageDialogue();
            questStarted = true;
            IsDefeated = IsHome = true;
            returning = waitingForPlayer = false;
            encounterGreetingShown = true;
            friendlyGreetingShown = greeted;
            nextHealthDialogue = HealthDialogueThresholds.Length;
            brain.enabled = false;
            health.Invulnerable = true;
            health.Restore();
            if (carriedFlask != null) carriedFlask.SetActive(false);
            if (flaskDrop != null) flaskDrop.gameObject.SetActive(false);
            foreach (CrabCousin cousin in cousins) if (cousin != null) cousin.Despawn();
            agent.enabled = false;
            Transform spot = seat != null ? seat : returnPoint;
            if (spot != null) transform.SetPositionAndRotation(spot.position, spot.rotation);
            rig.ResetPose();
            rig.MoveSpeed = 0f;
        }

        /// <summary>
        /// Loading a save from the big-crab fight after the crab gave up but before its cousins were all down: it is
        /// back in the hollow, beaten, and leads the player home once the brawl is over. The flask lies beside it
        /// unless the player had already picked it up.
        /// </summary>
        public void RestoreSurrendered(bool flaskTaken)
        {
            gameObject.SetActive(true);
            BeginQuest();
            if (IsDefeated) return;
            IsDefeated = true;
            StopDamageDialogue();
            nextHealthDialogue = HealthDialogueThresholds.Length;
            encounterGreetingShown = true;
            health.Invulnerable = true;
            brain.enabled = false;
            if (carriedFlask != null) carriedFlask.SetActive(false);
            if (!flaskTaken) DropFlaskAfterLoad();
            StartCoroutine(Recover());
        }

        // The flask lands on the ground beside the crab, floating and turning. Same placement as in Defeated
        // (MerchantCrab.cs); if that changes, change this too.
        private void DropFlaskAfterLoad()
        {
            if (flaskDrop == null) return;
            Vector3 drop = transform.position + transform.right * 2f;
            if (Physics.Raycast(drop + Vector3.up * 8, Vector3.down, out RaycastHit hit, 20, ~0, QueryTriggerInteraction.Ignore))
                drop = hit.point + Vector3.up * 0.03f;
            flaskDrop.transform.position = drop;
            flaskDrop.gameObject.SetActive(true);
            Transform bottle = flaskDrop.transform.Find("Visual");
            if (bottle != null && flaskDrop.GetComponent<SignalLost.Progression.PickupSpin>() == null)
            {
                bottle.localPosition += Vector3.up * 0.45f;
                flaskDrop.gameObject.AddComponent<SignalLost.Progression.PickupSpin>().Model = bottle;
            }
        }
    }
}
