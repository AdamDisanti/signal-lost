using UnityEngine;

namespace SignalLost.Player
{
    // Save and load (Christian, 10-27): the save-only part of PlayerFlask. The rest is in Player/PlayerFlask.cs.
    public sealed partial class PlayerFlask
    {
        /// <summary>Seconds until the current drink wears off; 0 when sober.</summary>
        public float StackSecondsLeft => DrunkStacks > 0 ? Mathf.Max(0f, nextSober - Time.time) : 0f;

        /// <summary>
        /// Loading a save: this many drinks in the system, the current one wearing off in
        /// <paramref name="secondsLeft"/>. Apply upgrades first, since they change how many drinks fit.
        /// </summary>
        public void RestoreStacks(int stacks, float secondsLeft)
        {
            DrunkStacks = Mathf.Clamp(stacks, 0, BlackoutLimit);
            IsDrinking = false;
            nextDrink = Time.time;
            nextSober = Time.time + Mathf.Clamp(secondsLeft, 0.1f, secondsPerStack);
        }
    }
}
