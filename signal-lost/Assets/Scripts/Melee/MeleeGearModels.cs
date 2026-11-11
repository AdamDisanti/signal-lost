using System.Collections.Generic;
using SignalLost.Mudlands;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// Code-built models for the melee gear (docs/melee-rage.md), in the mining laser's style: dark machined housings,
    /// copper coils and glowing gauss-blue cores.
    /// - Gaussian Brass Knuckles: a brass duster (four finger rings, a studded striking plate) with a gauss coil housing,
    ///   fitted onto the mechanic's glove bones so the fingers stay in their rings as the hand closes (first person and
    ///   the pockets figure), and as a standalone duster for the shop icon.
    /// - Moon Shoes: Back to the Future style self-lacing high-tops: silver uppers, a strap over the instep, glowing
    ///   strips round the sole and a light-up heel, on copper springs.
    /// </summary>
    public static class MeleeGearModels
    {
        public static readonly Color Brass = new Color(0.86f, 0.68f, 0.3f);
        public static readonly Color BrassDark = new Color(0.62f, 0.46f, 0.18f);
        public static readonly Color Gunmetal = new Color(0.12f, 0.14f, 0.16f);
        public static readonly Color Copper = new Color(0.8f, 0.46f, 0.24f);
        public static readonly Color Silver = new Color(0.74f, 0.76f, 0.8f);
        public static readonly Color SoleWhite = new Color(0.95f, 0.94f, 0.9f);
        public static readonly Color Strap = new Color(0.24f, 0.25f, 0.29f);
        private const float GlowStrength = 2.4f;

        // Finger knuckle joints on MechanicModel's hand, index to pinky: x = -(1.5 - i) * 0.0205 * side.
        private static float FingerX(int i, float side) => -(1.5f - i) * 0.0205f * side;

        // ---------- Gaussian Brass Knuckles ----------

        /// <summary>
        /// Fits the knuckles onto a MechanicModel hand (its root, with the "Finger 1".."Finger 4" bones): a ring round each
        /// finger's first segment, the striking plate across them and the coil housing on the back of the hand. Returns
        /// every part (to show and hide them) and the glowing ones (to pulse them).
        /// </summary>
        public static (List<GameObject> parts, List<Renderer> glowing) FitKnuckles(Transform hand, float side, bool shadows = false)
        {
            var parts = new List<GameObject>();
            var glowing = new List<Renderer>();
            for (int i = 0; i < 4; i++)
            {
                Transform finger = hand.Find("Finger " + (i + 1));
                if (finger == null) continue;
                parts.Add(MudArt.Part(finger, "Knuckle Ring", Vector3.zero, Quaternion.identity, "MeleeKnuckleRing", FingerRing, shadows).gameObject);
                if (i != 1) continue;
                // The plate rides on the second finger, reaching across all four (they close together in a fist).
                Transform plate = MudArt.Part(finger, "Striking Plate", new Vector3(-FingerX(1, side), 0f, 0f), Quaternion.identity,
                    side > 0f ? "MeleeKnucklePlateR" : "MeleeKnucklePlateL", kit => StrikingPlate(kit, side), shadows);
                parts.Add(plate.gameObject);
                glowing.Add(plate.GetComponent<Renderer>());
            }
            Transform coil = MudArt.Part(hand, "Gauss Coil", new Vector3(0.002f * side, 0f, 0f), Quaternion.identity, "MeleeKnuckleCoil", CoilHousing, shadows);
            parts.Add(coil.gameObject);
            glowing.Add(coil.GetComponent<Renderer>());
            return (parts, glowing);
        }

        // A brass ring round a finger's first segment (bone space: the finger runs along +Z, its back faces +Y).
        private static void FingerRing(MeshKit kit)
        {
            const float radius = 0.0175f, z = 0.012f;
            var path = new Vector3[13];
            for (int k = 0; k < path.Length; k++)
            {
                float a = k / (float)(path.Length - 1) * Mathf.PI * 2f;
                path[k] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z);
            }
            kit.Tube(path, 0.005f, 6, Brass);
        }

        // The striking plate over the backs of the four rings, studded at each finger, with a glowing seam (bone space of
        // the second finger, shifted so x = 0 is the middle of the hand). It reaches back past the knuckles so its top
        // edge shows over a closed fist.
        private static void StrikingPlate(MeshKit kit, float side)
        {
            kit.Box(new Vector3(0f, 0.0225f, 0.006f), new Vector3(0.1f, 0.008f, 0.038f), BrassDark);
            kit.Box(new Vector3(0f, 0.0272f, 0.006f), new Vector3(0.092f, 0.002f, 0.007f), GaussFx.Energy, GlowStrength);
            for (int i = 0; i < 4; i++)
            {
                float x = FingerX(i, side);
                kit.Prism(new Vector3(x, 0.026f, 0.016f), Quaternion.identity, 0.0075f, 0.004f, 0.009f, 6, Brass);
            }
        }

        // The gauss coil housing on the back of the hand (hand space: back of the hand +Y, fingers +Z): a low dark cradle
        // with a glowing cyan core across it between copper windings.
        private static void CoilHousing(MeshKit kit)
        {
            kit.Box(new Vector3(0f, 0.03f, -0.014f), new Vector3(0.062f, 0.007f, 0.04f), Gunmetal);
            kit.Rod(new Vector3(-0.033f, 0.04f, -0.014f), new Vector3(0.033f, 0.04f, -0.014f), 0.0095f, 8, GaussFx.Energy, GlowStrength);
            for (int k = 0; k < 4; k++)
            {
                float x = -0.021f + k * 0.014f;
                kit.Rod(new Vector3(x - 0.0022f, 0.04f, -0.014f), new Vector3(x + 0.0022f, 0.04f, -0.014f), 0.0115f, 8, Copper);
            }
            kit.Box(new Vector3(-0.036f, 0.037f, -0.014f), new Vector3(0.006f, 0.016f, 0.026f), Gunmetal);
            kit.Box(new Vector3(0.036f, 0.037f, -0.014f), new Vector3(0.006f, 0.016f, 0.026f), Gunmetal);
            // Leads down to the plate.
            kit.Rod(new Vector3(-0.02f, 0.032f, 0.006f), new Vector3(-0.02f, 0.026f, 0.028f), 0.002f, 4, Copper);
            kit.Rod(new Vector3(0.02f, 0.032f, 0.006f), new Vector3(0.02f, 0.026f, 0.028f), 0.002f, 4, Copper);
        }

        /// <summary>The knuckles on their own (shop and pockets icon): four rings, the studded plate, a grip bar and the coil.</summary>
        public static void KnuckleDuster(MeshKit kit)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -0.042f + i * 0.028f;
                var path = new Vector3[13];
                for (int k = 0; k < path.Length; k++)
                {
                    float a = k / (float)(path.Length - 1) * Mathf.PI * 2f;
                    path[k] = new Vector3(x + Mathf.Cos(a) * 0.0125f, Mathf.Sin(a) * 0.0125f, 0f);
                }
                kit.Tube(path, 0.0045f, 6, Brass);
                kit.Prism(new Vector3(x, 0.021f, 0f), Quaternion.identity, 0.0065f, 0.0035f, 0.008f, 6, Brass);
            }
            kit.Box(new Vector3(0f, 0.017f, 0f), new Vector3(0.118f, 0.007f, 0.022f), BrassDark);
            kit.Box(new Vector3(0f, 0.0215f, 0.0112f), new Vector3(0.11f, 0.003f, 0.002f), GaussFx.Energy, GlowStrength);
            // The palm grip underneath, bowed toward the palm.
            kit.Tube(new[] { new Vector3(-0.054f, -0.006f, 0f), new Vector3(-0.03f, -0.026f, 0f), new Vector3(0.03f, -0.026f, 0f), new Vector3(0.054f, -0.006f, 0f) },
                0.006f, 6, BrassDark);
            // The coil housing riding on top of the plate.
            kit.Box(new Vector3(0f, 0.029f, -0.004f), new Vector3(0.08f, 0.012f, 0.02f), Gunmetal);
            kit.Rod(new Vector3(-0.036f, 0.04f, -0.004f), new Vector3(0.036f, 0.04f, -0.004f), 0.0068f, 8, GaussFx.Energy, GlowStrength);
            for (int k = 0; k < 5; k++)
            {
                float x = -0.026f + k * 0.013f;
                kit.Rod(new Vector3(x - 0.002f, 0.04f, -0.004f), new Vector3(x + 0.002f, 0.04f, -0.004f), 0.009f, 8, Copper);
            }
        }

        // ---------- Moon Shoes ----------

        /// <summary>
        /// One Moon Shoe (<paramref name="side"/> 1 right, -1 left), its origin under the middle of the sole, toe +Z:
        /// about 0.33 m long, 0.2 m wide and 0.27 m tall.
        /// </summary>
        public static void MoonShoe(MeshKit kit, float side, Vector3 offset = default, float yaw = 0f, float tilt = 0f)
        {
            Quaternion turn = Quaternion.Euler(tilt, yaw, 0f);
            Vector3 P(float x, float y, float z) => offset + turn * new Vector3(x * side, y, z);

            // Copper springs under the heel and the ball of the foot (the moon bounce).
            foreach (float z in new[] { -0.09f, 0.08f })
            {
                var coil = new Vector3[25];
                for (int k = 0; k < coil.Length; k++)
                {
                    float t = k / (float)(coil.Length - 1);
                    float a = t * Mathf.PI * 2f * 3f;
                    coil[k] = P(Mathf.Cos(a) * 0.06f, 0.004f + t * 0.032f, z + Mathf.Sin(a) * 0.05f);
                }
                kit.Tube(coil, 0.006f, 5, Copper);
            }
            // The thick white sole, with a glowing strip round it and a rounded toe (chunky, to go over the mechanic's boots).
            kit.Box(P(0f, 0.058f, -0.005f), new Vector3(0.2f, 0.042f, 0.33f), turn, SoleWhite);
            kit.Ball(P(0f, 0.058f, 0.15f), new Vector3(0.1f, 0.021f, 0.045f), turn, SoleWhite, 1);
            kit.Box(P(0.102f, 0.06f, -0.01f), new Vector3(0.006f, 0.012f, 0.3f), turn, GaussFx.Energy, GlowStrength);
            kit.Box(P(-0.102f, 0.06f, -0.01f), new Vector3(0.006f, 0.012f, 0.3f), turn, GaussFx.Energy, GlowStrength);
            // The silver upper and the toe box.
            kit.Box(P(0f, 0.115f, -0.02f), new Vector3(0.19f, 0.075f, 0.27f), turn, Silver);
            kit.Ball(P(0f, 0.105f, 0.105f), new Vector3(0.095f, 0.05f, 0.075f), turn, Silver, 1);
            // The high-top collar round the ankle, padded at the rim.
            kit.Prism(P(0f, 0.13f, -0.07f), turn, 0.1f, 0.094f, 0.125f, 10, Silver);
            kit.Prism(P(0f, 0.25f, -0.07f), turn, 0.1f, 0.096f, 0.02f, 10, Strap);
            // The strap over the instep, and the self-tightening laces as glowing bars beneath it.
            kit.Box(P(0f, 0.165f, 0.035f), new Vector3(0.205f, 0.022f, 0.06f), turn * Quaternion.Euler(-28f, 0f, 0f), Strap);
            for (int k = 0; k < 3; k++)
                kit.Box(P(0f, 0.155f - k * 0.012f, 0.075f + k * 0.022f), new Vector3(0.12f, 0.006f, 0.008f), turn * Quaternion.Euler(-28f, 0f, 0f), GaussFx.Energy, GlowStrength);
            // The light-up heel.
            kit.Box(P(0f, 0.125f, -0.157f), new Vector3(0.1f, 0.05f, 0.008f), turn, GaussFx.Energy, GlowStrength);
            kit.Box(P(0f, 0.16f, -0.158f), new Vector3(0.14f, 0.012f, 0.01f), turn, Strap);
        }

        /// <summary>A pair, side by side and slightly splayed, tipped toward the viewer, for the shop and pockets icon.</summary>
        public static void MoonShoePair(MeshKit kit)
        {
            MoonShoe(kit, -1f, new Vector3(-0.13f, 0f, -0.03f), -12f, 40f);
            MoonShoe(kit, 1f, new Vector3(0.13f, 0f, 0.03f), 12f, 40f);
        }
    }
}
