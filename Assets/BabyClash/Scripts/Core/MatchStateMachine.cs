#nullable enable
using System;
using System.Collections.Generic;

namespace BabyClash.Core
{
    /// <summary>A match is a series of rounds: the first baby to win <see cref="RoundsToWin"/> rounds wins it.</summary>
    public sealed class MatchStateMachine
    {
        public const int NoPlayer = -1;

        readonly int[] wins;

        /// <param name="roundsToWin">Round wins needed for the match. 0 = endless (attract mode, free play).</param>
        public MatchStateMachine(int playerCount, int roundsToWin)
        {
            if (playerCount < 1)
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Need at least one player.");
            wins = new int[playerCount];
            RoundsToWin = Math.Max(0, roundsToWin);
        }

        public int RoundsToWin { get; }
        public bool IsEndless => RoundsToWin == 0;
        public int RoundsPlayed { get; private set; }
        public bool IsOver { get; private set; }
        public int WinnerIndex { get; private set; } = NoPlayer;

        public int GetWins(int playerIndex) => playerIndex >= 0 && playerIndex < wins.Length ? wins[playerIndex] : 0;

        public void Reset()
        {
            Array.Clear(wins, 0, wins.Length);
            RoundsPlayed = 0;
            IsOver = false;
            WinnerIndex = NoPlayer;
        }

        /// <summary>Records a finished round (<see cref="NoPlayer"/> = draw). Returns true if the match is now over.</summary>
        public bool RecordRound(int roundWinner)
        {
            if (IsOver)
                return true;

            RoundsPlayed++;
            if (roundWinner < 0 || roundWinner >= wins.Length)
                return false;

            wins[roundWinner]++;
            if (!IsEndless && wins[roundWinner] >= RoundsToWin)
            {
                IsOver = true;
                WinnerIndex = roundWinner;
            }
            return IsOver;
        }
    }

    public static class RoundRules
    {
        /// <summary>Time-up decision: the calmest baby (lowest Cry) wins; a tie within <paramref name="tolerance"/> is a draw.</summary>
        public static int CalmestBaby(IReadOnlyList<float> cryValues, float tolerance)
        {
            int best = MatchStateMachine.NoPlayer;
            float bestValue = float.MaxValue;
            bool tied = false;
            for (int i = 0; i < cryValues.Count; i++)
            {
                float value = cryValues[i];
                if (value < bestValue - tolerance)
                {
                    best = i;
                    bestValue = value;
                    tied = false;
                }
                else if (Math.Abs(value - bestValue) <= tolerance)
                {
                    tied = true;
                }
            }
            return tied ? MatchStateMachine.NoPlayer : best;
        }

        /// <summary>
        /// Time-up decision for teams: the side with the lowest average Cry wins; a tie within
        /// <paramref name="tolerance"/> is a draw.
        /// </summary>
        public static int CalmestSide(IReadOnlyList<float> cryValues, IReadOnlyList<int> sideOfPlayer, int sideCount, float tolerance)
        {
            var averages = new float[sideCount];
            var counts = new int[sideCount];
            for (int i = 0; i < cryValues.Count; i++)
            {
                int side = sideOfPlayer[i];
                averages[side] += cryValues[i];
                counts[side]++;
            }
            var present = new List<float>(sideCount);
            var index = new List<int>(sideCount);
            for (int side = 0; side < sideCount; side++)
            {
                if (counts[side] == 0)
                    continue;
                present.Add(averages[side] / counts[side]);
                index.Add(side);
            }
            int best = CalmestBaby(present, tolerance);
            return best == MatchStateMachine.NoPlayer ? best : index[best];
        }
    }
}
