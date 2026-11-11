using SignalLost.Audio;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// The Moon Shoes' double jump, seen and heard (Moon Shoes III): a gauss-blue shock ring and a burst of sparks
    /// under the feet, and a springy boing. Added to the player at runtime by <see cref="PlayerGear"/>.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class MoonShoesFx : MonoBehaviour
    {
        private FirstPersonController movement;

        private void Awake() => movement = GetComponent<FirstPersonController>();
        private void OnEnable() => movement.DoubleJumped += OnDoubleJump;
        private void OnDisable() => movement.DoubleJumped -= OnDoubleJump;

        private void OnDoubleJump(Vector3 feet)
        {
            GaussFx.Ring(feet, 1.6f, 0.3f, 0.12f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, feet, Vector3.down, 18, 0.05f, 5f, 70f, Color.Lerp(GaussFx.Energy, Color.white, 0.3f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, feet, Vector3.zero, 3, 0.35f, 0.4f, 90f, new Color(GaussFx.Energy.r, GaussFx.Energy.g, GaussFx.Energy.b, 0.5f));
            Sfx.PlayAt("player_jump", feet, 0.8f, 1.45f, 15f);
            Sfx.PlayAt("hex_zap", feet, 0.35f, 1.8f, 15f);
        }
    }
}
