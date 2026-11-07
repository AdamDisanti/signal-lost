using UnityEngine;

namespace SignalLost.Combat
{
    // Save and load (Christian, 10-27): the save-only part of Health. The rest of the class is in Combat/Health.cs.
    public sealed partial class Health
    {
        /// <summary>
        /// Sets health directly, between 1 and the maximum (loading a save). Raises <see cref="Changed"/> but not
        /// <see cref="Damaged"/>, so nothing reacts as if it were a hit.
        /// </summary>
        public void SetCurrent(float value)
        {
            current = Mathf.Clamp(value, 1f, maxHealth);
            Changed?.Invoke(this);
        }
    }
}
