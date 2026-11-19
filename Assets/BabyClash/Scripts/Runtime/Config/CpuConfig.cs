using System;
using UnityEngine;

namespace BabyClash
{
    public enum CpuDifficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>How the computer-controlled baby plays, per difficulty.</summary>
    [CreateAssetMenu(fileName = "CpuConfig", menuName = "Baby Clash/CPU Config")]
    public sealed class CpuConfig : ScriptableObject
    {
        public CpuSettings easy = new CpuSettings(0.55f, 22f, 0.15f, 0.35f, 0.25f, 0.8f);
        public CpuSettings normal = new CpuSettings(0.32f, 12f, 0.4f, 0.55f, 0.5f, 0.95f);
        public CpuSettings hard = new CpuSettings(0.18f, 5f, 0.7f, 0.75f, 0.8f, 1f);

        public CpuSettings Get(CpuDifficulty difficulty) =>
            difficulty == CpuDifficulty.Easy ? easy : difficulty == CpuDifficulty.Hard ? hard : normal;
    }

    [Serializable]
    public sealed class CpuSettings
    {
        [Tooltip("Seconds between decisions; also how late it reacts to incoming throws.")]
        [Min(0.05f)] public float reactionTime = 0.3f;

        [Tooltip("Random throw error (degrees).")]
        [Min(0f)] public float aimError = 10f;

        [Tooltip("Chance to dash away from an incoming throw.")]
        [Range(0f, 1f)] public float dodgeChance = 0.4f;

        [Tooltip("Willingness to throw its own diaper (0 = never, 1 = whenever it has a shot).")]
        [Range(0f, 1f)] public float diaperThrowWillingness = 0.5f;

        [Tooltip("Chance to dash-shove when close to the opponent.")]
        [Range(0f, 1f)] public float shoveChance = 0.5f;

        [Tooltip("Top speed multiplier (the CPU walks a bit slower on easier settings).")]
        [Range(0.3f, 1f)] public float speedMultiplier = 1f;

        public CpuSettings() { }

        public CpuSettings(float reactionTime, float aimError, float dodgeChance, float diaperThrowWillingness,
            float shoveChance, float speedMultiplier)
        {
            this.reactionTime = reactionTime;
            this.aimError = aimError;
            this.dodgeChance = dodgeChance;
            this.diaperThrowWillingness = diaperThrowWillingness;
            this.shoveChance = shoveChance;
            this.speedMultiplier = speedMultiplier;
        }
    }
}
