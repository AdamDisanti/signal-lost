using System;

namespace SignalLost.Core
{
    /// <summary>
    /// A testing cheat (Lawson, 11-08), toggled with End (the "cheat" key binding). While it is on:
    /// <list type="bullet">
    /// <item>every hit the player lands (laser bolts, the laser combo, slams, anything sourced from the player) does
    /// <see cref="Damage"/>, applied in <c>Health.TakeDamage</c>;</item>
    /// <item>the player flies and passes through everything (<c>FirstPersonController</c>): move where you look,
    /// Jump to rise, Crouch to sink, Sprint to go fast. The deep-water rescue is off.</item>
    /// </list>
    /// It lasts for the play session (across scene loads) and is never saved. Health, enemies and quests are
    /// otherwise untouched; it is not god mode.
    /// </summary>
    public static class CheatMode
    {
        public const float Damage = 9999f;

        public static bool Enabled { get; private set; }

        /// <summary>Raised whenever the cheat is switched on or off.</summary>
        public static event Action<bool> Changed;

        public static void Set(bool on)
        {
            if (on == Enabled) return;
            Enabled = on;
            Changed?.Invoke(on);
        }

        public static void Toggle() => Set(!Enabled);
    }
}
