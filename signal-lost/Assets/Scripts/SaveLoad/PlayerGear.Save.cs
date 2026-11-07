namespace SignalLost.Player
{
    // Save and load (Christian, 10-27): the save-only part of PlayerGear. The rest is in Player/PlayerGear.cs.
    public sealed partial class PlayerGear
    {
        /// <summary>
        /// Brings the hands up to date with the pockets after items were put there directly (loading a save), without
        /// the "new gear" cards.
        /// </summary>
        public void Sync() => Refresh();
    }
}
