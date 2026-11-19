#nullable enable
using System;

namespace BabyClash.Core
{
    public enum RoundPhase
    {
        /// <summary>The world is being put back to its initial state.</summary>
        Restarting,

        /// <summary>Countdown; babies can't act yet.</summary>
        Starting,

        Playing,

        /// <summary>Someone cried (or time ran out). The result is shown for a moment.</summary>
        RoundOver,
    }

    /// <summary>
    /// One round: Restarting -> Starting (countdown) -> Playing -> RoundOver. Works for any number of players split
    /// into sides (free-for-all: one side per player; 2v2: two sides). The round ends when only one side still has a
    /// baby that isn't crying, or when the time limit runs out. Winner and loser are side indices.
    /// </summary>
    public sealed class RoundStateMachine
    {
        public const int NoPlayer = -1;

        readonly bool[] crying;
        readonly int[] sides;
        readonly float countdownDuration;
        readonly float timeLimit;
        float timer;

        /// <param name="timeLimit">Seconds of play before the round is decided on points. 0 = no limit.</param>
        /// <param name="sideOfPlayer">Side index per player (0-based, contiguous). Null = free-for-all.</param>
        public RoundStateMachine(int playerCount, float countdownDuration, float timeLimit, int[]? sideOfPlayer = null)
        {
            if (playerCount < 1)
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Need at least one player.");
            if (sideOfPlayer != null && sideOfPlayer.Length != playerCount)
                throw new ArgumentException("Need one side per player.", nameof(sideOfPlayer));

            crying = new bool[playerCount];
            sides = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                sides[i] = sideOfPlayer != null ? sideOfPlayer[i] : i;
                if (sides[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(sideOfPlayer), sides[i], "Sides must be 0 or more.");
                SideCount = Math.Max(SideCount, sides[i] + 1);
            }
            this.countdownDuration = Math.Max(0f, countdownDuration);
            this.timeLimit = Math.Max(0f, timeLimit);
        }

        public int PlayerCount => crying.Length;
        public int SideCount { get; }
        public RoundPhase Phase { get; private set; } = RoundPhase.Restarting;
        public int RoundNumber { get; private set; }
        /// <summary>Winning side (the player index in free-for-all), or <see cref="NoPlayer"/> for a draw.</summary>
        public int WinnerIndex { get; private set; } = NoPlayer;

        /// <summary>The player whose tears ended the round (the last one to cry), if any.</summary>
        public int LoserIndex { get; private set; } = NoPlayer;

        /// <summary>True when the round was decided by the clock rather than by tears.</summary>
        public bool EndedOnTime { get; private set; }

        public bool HasTimeLimit => timeLimit > 0f;
        public float CountdownDuration => countdownDuration;
        public float CountdownRemaining => Phase == RoundPhase.Starting ? timer : 0f;
        public float TimeRemaining => Phase == RoundPhase.Playing ? timer : Phase == RoundPhase.RoundOver ? 0f : timeLimit;

        /// <summary>Seconds since the round ended.</summary>
        public float ResultElapsed => Phase == RoundPhase.RoundOver ? timer : 0f;

        public event Action<RoundPhase>? PhaseChanged;

        /// <summary>Raised once when the clock runs out. The listener decides the winner with <see cref="EndRound"/>.</summary>
        public event Action? TimeExpired;

        public bool IsCrying(int playerIndex) => IsValid(playerIndex) && crying[playerIndex];

        public int SideOf(int playerIndex) => IsValid(playerIndex) ? sides[playerIndex] : NoPlayer;

        /// <summary>Round numbering starts over (a new match).</summary>
        public void ResetRoundNumber() => RoundNumber = 0;

        /// <summary>Resets per-round state and starts the countdown. Valid from any phase.</summary>
        public void StartNewRound()
        {
            Array.Clear(crying, 0, crying.Length);
            WinnerIndex = NoPlayer;
            LoserIndex = NoPlayer;
            EndedOnTime = false;
            RoundNumber++;

            SetPhase(RoundPhase.Restarting);
            timer = countdownDuration;
            SetPhase(RoundPhase.Starting);
            if (timer <= 0f)
                BeginPlay();
        }

        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;

            switch (Phase)
            {
                case RoundPhase.Starting:
                    timer -= deltaTime;
                    if (timer <= 0f)
                        BeginPlay();
                    break;
                case RoundPhase.Playing when timeLimit > 0f:
                    timer -= deltaTime;
                    if (timer <= 0f)
                    {
                        timer = 0f;
                        TimeExpired?.Invoke();
                        // A listener that didn't end the round gets a draw, so the clock can never stall.
                        if (Phase == RoundPhase.Playing)
                            EndRound(NoPlayer, true);
                    }
                    break;
                case RoundPhase.RoundOver:
                    timer += deltaTime;
                    break;
            }
        }

        /// <summary>
        /// A baby's cry meter filled up. Only counts while playing; ends the round when one side is left.
        /// Returns true if the report was accepted.
        /// </summary>
        public bool ReportCrying(int playerIndex)
        {
            if (Phase != RoundPhase.Playing || !IsValid(playerIndex) || crying[playerIndex])
                return false;

            crying[playerIndex] = true;
            LoserIndex = playerIndex;

            int lastSide = NoPlayer;
            bool severalSides = false;
            for (int i = 0; i < crying.Length; i++)
            {
                if (crying[i])
                    continue;
                if (lastSide == NoPlayer)
                    lastSide = sides[i];
                else if (sides[i] != lastSide)
                    severalSides = true;
            }

            if (!severalSides)
                EndRound(lastSide, false);
            return true;
        }

        /// <summary>Ends the round now with the given winning side (<see cref="NoPlayer"/> = draw).</summary>
        public void EndRound(int winnerSide, bool onTime)
        {
            if (Phase != RoundPhase.Playing)
                return;
            WinnerIndex = winnerSide >= 0 && winnerSide < SideCount ? winnerSide : NoPlayer;
            EndedOnTime = onTime;
            timer = 0f;
            SetPhase(RoundPhase.RoundOver);
        }

        void BeginPlay()
        {
            timer = timeLimit;
            SetPhase(RoundPhase.Playing);
        }

        bool IsValid(int playerIndex) => playerIndex >= 0 && playerIndex < crying.Length;

        // No same-phase dedupe on purpose: the very first StartNewRound goes Restarting -> Restarting, and listeners
        // rely on that event to set up the world.
        void SetPhase(RoundPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
