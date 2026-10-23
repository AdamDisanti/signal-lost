namespace SignalLost.Player
{
    /// <summary>
    /// Anything the player can use with E while looking at it within reach: pickups, NPCs, switches.
    /// <see cref="PlayerInteractor"/> finds the nearest one under the crosshair and shows its prompt.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Prompt shown under the crosshair, e.g. "Talk to Cavendish". Empty hides it.</summary>
        string Prompt { get; }

        bool CanInteract(PlayerInteractor player);

        /// <summary>Uses it. Returns true if something happened.</summary>
        bool Interact(PlayerInteractor player);
    }
}
