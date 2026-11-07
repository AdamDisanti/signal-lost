using SignalLost.Audio;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Combat
{
    /// <summary>
    /// The one impact effect and sound for every melee strike, whatever it hits, so no enemy can be missing them:
    /// a hit that does damage throws a burst of energy sparks with a glow and a meaty thump; a hit that does no damage
    /// (blocked, armoured, invulnerable) rings off with white sparks; a swing into a wall or the ground kicks up dust
    /// and a dull knock. Called by <see cref="MiningLaser"/>. Enemies may still add their own reactions on top
    /// (a crab's bucket clang), but nothing relies on them.
    /// </summary>
    public static class HitFeedback
    {
        public enum Result { Damaged, NoDamage, Surface }

        private static float lastSoundTime;

        /// <param name="normal">Which way the sparks fly: back toward the attacker.</param>
        /// <param name="heavy">The combo finisher: a bigger burst, lower thump and a little screen shake.</param>
        public static void Melee(Vector3 point, Vector3 normal, Result result, bool heavy, Color energy)
        {
            float scale = heavy ? 1.6f : 1f;
            switch (result)
            {
                case Result.Damaged:
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point, normal, Mathf.RoundToInt(14 * scale), 0.07f, 5.5f * scale, 55f,
                        Color.Lerp(energy, Color.white, 0.35f));
                    // A soft glow pop (additive, so it never reads as a solid ball).
                    ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, point, Vector3.zero, heavy ? 6 : 4, 0.3f * scale, 0.5f, 90f,
                        new Color(energy.r, energy.g, energy.b, 0.6f));
                    Sound("bolt_impact", point, heavy ? 0.95f : 0.75f, Random.Range(0.72f, 0.82f) / (heavy ? 1.12f : 1f));
                    if (heavy) CameraShake.Add(0.18f);
                    break;
                case Result.NoDamage:
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point, normal, Mathf.RoundToInt(10 * scale), 0.05f, 6f, 45f,
                        new Color(1f, 0.92f, 0.75f));
                    ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, point, Vector3.zero, 2, 0.18f * scale, 0.3f, 90f, new Color(1f, 0.95f, 0.85f, 0.5f));
                    Sound("crab_block", point, 0.5f, Random.Range(1.05f, 1.2f));
                    break;
                default:
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, point, normal, 6, 0.25f, 1.8f, 50f, new Color(0.55f, 0.52f, 0.48f, 0.6f));
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point, normal, 4, 0.04f, 3.5f, 50f, Color.Lerp(energy, Color.white, 0.5f));
                    Sound("bolt_impact", point, 0.35f, Random.Range(0.55f, 0.62f));
                    break;
            }
        }

        // Several targets hit by one swing share one sound, so a wide bash doesn't stack into a roar.
        private static void Sound(string clip, Vector3 point, float volume, float pitch)
        {
            if (Time.time - lastSoundTime < 0.04f) return;
            lastSoundTime = Time.time;
            Sfx.PlayAt(clip, point, volume, pitch, 25f);
        }
    }
}
