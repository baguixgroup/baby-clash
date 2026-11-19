using UnityEngine;

namespace BabyClash
{
    /// <summary>Match and round flow, plus toy spawning.</summary>
    [CreateAssetMenu(fileName = "MatchConfig", menuName = "Baby Clash/Match Config")]
    public sealed class MatchConfig : ScriptableObject
    {
        [Header("Match")]
        [Tooltip("Round wins needed to win the match (2 = best of 3).")]
        [Min(1)] public int roundsToWin = 2;

        [Header("Round")]
        [Tooltip("Countdown before babies can move (s). 0 = start immediately.")]
        [Min(0f)] public float countdownDuration = 2.4f;

        [Tooltip("Round time limit (s). When it runs out, the calmest baby (lowest Cry) wins. 0 = no limit.")]
        [Min(0f)] public float roundTimeLimit = 75f;

        [Tooltip("How long a round result stays on screen before the next round (s).")]
        [Min(0.5f)] public float roundResultDuration = 2.8f;

        [Tooltip("After a match ends, menu input is ignored for this long (s) so mashing doesn't skip the result.")]
        [Min(0f)] public float matchResultLockDuration = 1f;

        [Header("Toys")]
        [Tooltip("Seconds into a round before the first toy drops in.")]
        [Min(0f)] public float firstToyDelay = 3f;

        [Tooltip("Time between toy drops (s), random within this range.")]
        public Vector2 toySpawnInterval = new Vector2(3.5f, 6.5f);

        [Tooltip("Most toys lying around (or held) at once.")]
        [Range(0, 8)] public int maxToys = 3;
    }
}
