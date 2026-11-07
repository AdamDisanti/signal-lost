namespace SignalLost.Story
{
    // Save and load (Christian, 10-27): the save-only part of ChiefToadEncounter. The rest is in Story/ChiefToadEncounter.cs.
    public sealed partial class ChiefToadEncounter
    {
        /// <summary>Loading a save from after the fight: he is already beaten and stays gone.</summary>
        public void RestoreDefeated()
        {
            StopAllCoroutines();
            Current = State.Defeated;
            Signal(false);
            if (toad != null) toad.gameObject.SetActive(false);
        }
    }
}
