using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Is anything fighting the player right now? Used to block saving in combat (10-24). An enemy counts while it is
    /// alive and the player is inside its fight radius (<see cref="Territory"/>), and a boss counts while engaged.
    /// Enemies switched off (the friendly merchant, cousins waiting for their quest) never count.
    /// It looks through the scene, so call it now and then (a save, a check every half second), not every frame.
    /// </summary>
    public static class CombatCheck
    {
        public static bool EnemyEngaging(Vector3 player)
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>())
            {
                var territorial = behaviour as ITerritorial;
                if (territorial == null || !behaviour.isActiveAndEnabled) continue;
                if (behaviour.TryGetComponent(out Health health) && health.IsDead) continue;
                if (behaviour is DesertMiniBoss boss && boss.Engaged) return true;
                if (territorial.Territory != null && territorial.Territory.Contains(player)) return true;
            }
            return false;
        }
    }
}
