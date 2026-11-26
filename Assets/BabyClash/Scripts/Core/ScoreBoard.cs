#nullable enable
using System;

namespace BabyClash.Core
{
    /// <summary>
    /// Points per side for score-based modes (King of the Bottle, Tiny Tots, Survival). Optionally ends at a target.
    /// </summary>
    public sealed class ScoreBoard
    {
        public const int NoSide = -1;

        readonly float[] scores;

        /// <param name="target">Score that wins immediately. 0 = no target (decided when time runs out).</param>
        public ScoreBoard(int sideCount, float target = 0f)
        {
            if (sideCount < 1)
                throw new ArgumentOutOfRangeException(nameof(sideCount), sideCount, "Need at least one side.");
            scores = new float[sideCount];
            Target = Math.Max(0f, target);
        }

        public int SideCount => scores.Length;
        public float Target { get; }
        public bool HasTarget => Target > 0f;

        /// <summary>Raised when a side reaches the target (once).</summary>
        public event Action<int>? TargetReached;

        public bool IsTargetReached { get; private set; }

        public float Get(int side) => side >= 0 && side < scores.Length ? scores[side] : 0f;

        public void Add(int side, float amount)
        {
            if (side < 0 || side >= scores.Length || !(amount > 0f) || IsTargetReached)
                return;
            scores[side] += amount;
            if (HasTarget && !IsTargetReached && scores[side] >= Target)
            {
                scores[side] = Target;
                IsTargetReached = true;
                TargetReached?.Invoke(side);
            }
        }

        public void Reset()
        {
            Array.Clear(scores, 0, scores.Length);
            IsTargetReached = false;
        }

        /// <summary>The side with the most points, or <see cref="NoSide"/> if the top is tied within the tolerance.</summary>
        public int Leader(float tolerance = 0.001f)
        {
            int best = NoSide;
            float bestScore = float.MinValue;
            bool tied = false;
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] > bestScore + tolerance)
                {
                    best = i;
                    bestScore = scores[i];
                    tied = false;
                }
                else if (Math.Abs(scores[i] - bestScore) <= tolerance)
                {
                    tied = true;
                }
            }
            return tied ? NoSide : best;
        }
    }
}
