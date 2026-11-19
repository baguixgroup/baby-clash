using System.Collections.Generic;
using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class RoundStateMachineTests
    {
        static RoundStateMachine StartedRound(float countdown = 1f, float timeLimit = 60f)
        {
            var round = new RoundStateMachine(2, countdown, timeLimit);
            round.StartNewRound();
            return round;
        }

        [Test]
        public void NewRoundResetsThenCountsDownThenPlays()
        {
            var round = new RoundStateMachine(2, 1f, 60f);
            var phases = new List<RoundPhase>();
            round.PhaseChanged += phases.Add;

            round.StartNewRound();
            Assert.AreEqual(RoundPhase.Starting, round.Phase);
            round.Tick(1.1f);

            CollectionAssert.AreEqual(new[] { RoundPhase.Restarting, RoundPhase.Starting, RoundPhase.Playing }, phases,
                "the first round must announce Restarting too, so listeners set up the world");
            Assert.AreEqual(60f, round.TimeRemaining, 0.001f);
        }

        [Test]
        public void ZeroCountdownStartsPlayingImmediately()
        {
            RoundStateMachine round = StartedRound(countdown: 0f);
            Assert.AreEqual(RoundPhase.Playing, round.Phase);
        }

        [Test]
        public void CryingBeforePlayIsIgnored()
        {
            RoundStateMachine round = StartedRound();
            Assert.IsFalse(round.ReportCrying(0));
            Assert.AreEqual(RoundPhase.Starting, round.Phase);
        }

        [Test]
        public void FirstBabyToCryLosesAndTheOtherWins()
        {
            RoundStateMachine round = StartedRound();
            round.Tick(2f);

            Assert.IsTrue(round.ReportCrying(1));
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(0, round.WinnerIndex);
            Assert.AreEqual(1, round.LoserIndex);
            Assert.IsFalse(round.EndedOnTime);
        }

        [Test]
        public void LateCryAfterRoundOverIsIgnored()
        {
            RoundStateMachine round = StartedRound();
            round.Tick(2f);
            round.ReportCrying(1);

            Assert.IsFalse(round.ReportCrying(0));
            Assert.AreEqual(0, round.WinnerIndex);
        }

        [Test]
        public void ClockRunningOutAsksForADecision()
        {
            RoundStateMachine round = StartedRound(countdown: 0f, timeLimit: 10f);
            round.TimeExpired += () => round.EndRound(1, true);

            round.Tick(9f);
            Assert.AreEqual(RoundPhase.Playing, round.Phase);
            Assert.AreEqual(1f, round.TimeRemaining, 0.001f);

            round.Tick(2f);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(1, round.WinnerIndex);
            Assert.IsTrue(round.EndedOnTime);
        }

        [Test]
        public void UnansweredTimeUpIsADraw()
        {
            RoundStateMachine round = StartedRound(countdown: 0f, timeLimit: 5f);
            round.Tick(6f);

            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(RoundStateMachine.NoPlayer, round.WinnerIndex);
        }

        [Test]
        public void NoTimeLimitMeansNoClock()
        {
            RoundStateMachine round = StartedRound(countdown: 0f, timeLimit: 0f);
            round.Tick(10000f);
            Assert.AreEqual(RoundPhase.Playing, round.Phase);
        }

        [Test]
        public void ResultTimerCountsAfterTheRoundEnds()
        {
            RoundStateMachine round = StartedRound(countdown: 0f);
            round.ReportCrying(0);
            round.Tick(1.5f);
            Assert.AreEqual(1.5f, round.ResultElapsed, 0.001f);

            round.StartNewRound();
            Assert.AreEqual(2, round.RoundNumber);
            Assert.AreEqual(RoundStateMachine.NoPlayer, round.WinnerIndex);
        }

        [Test]
        public void WithMorePlayersTheLastOneStandingWins()
        {
            var round = new RoundStateMachine(3, 0f, 0f);
            round.StartNewRound();

            round.ReportCrying(0);
            Assert.AreEqual(RoundPhase.Playing, round.Phase);
            round.ReportCrying(2);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(1, round.WinnerIndex);
        }
    }
}
