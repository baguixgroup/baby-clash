using System;
using UnityEngine;

namespace BabyClash
{
    /// <summary>Player options, persisted in PlayerPrefs.</summary>
    public static class GameSettings
    {
        const string MusicKey = "BabyClash.MusicVolume";
        const string SfxKey = "BabyClash.SfxVolume";
        const float DefaultMusic = 0.55f;
        const float DefaultSfx = 0.8f;

        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
            set
            {
                PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
                Changed?.Invoke();
            }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
            set
            {
                PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
                Changed?.Invoke();
            }
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreen = value;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
