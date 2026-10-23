using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Core
{
    /// <summary>
    /// Player settings shared by the title screen and the pause menu, saved in PlayerPrefs on this machine.
    /// A proof of concept: master volume, mouse sensitivity, fullscreen and quality really apply; music and effects
    /// volume are stored for when the game has separate audio mixers.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "SignalLost.Settings.";

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "Master", 0.8f);
            set { PlayerPrefs.SetFloat(Prefix + "Master", Mathf.Clamp01(value)); Apply(); }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "Music", 0.7f);
            set => PlayerPrefs.SetFloat(Prefix + "Music", Mathf.Clamp01(value));
        }

        public static float EffectsVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "Effects", 0.8f);
            set => PlayerPrefs.SetFloat(Prefix + "Effects", Mathf.Clamp01(value));
        }

        /// <summary>Multiplier on mouse and stick look speed (0.25 to 3).</summary>
        public static float LookSensitivity
        {
            get => PlayerPrefs.GetFloat(Prefix + "Look", 1f);
            set { PlayerPrefs.SetFloat(Prefix + "Look", Mathf.Clamp(value, 0.25f, 3f)); Apply(); }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) == 1;
            set { PlayerPrefs.SetInt(Prefix + "Fullscreen", value ? 1 : 0); Apply(); }
        }

        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set { PlayerPrefs.SetInt(Prefix + "Quality", Mathf.Clamp(value, 0, QualitySettings.names.Length - 1)); Apply(); }
        }

        /// <summary>Pushes the stored settings into the engine. Called on scene start and whenever one changes.</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            FirstPersonController.SensitivityScale = LookSensitivity;
            if (!Application.isEditor) Screen.fullScreen = Fullscreen;
            if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
        }
    }
}
