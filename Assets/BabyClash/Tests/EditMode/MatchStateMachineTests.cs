using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class MatchStateMachineTests
    {
        [Test]
        public void FirstToTwoRoundWinsTakesTheMatch()
        {
            var match = new MatchStateMachine(2, 2);

            Assert.IsFalse(match.RecordRound(0));
            Assert.IsFalse(match.RecordRound(1));
            Assert.IsTrue(match.RecordRound(1));
            Assert.AreEqual(1, match.WinnerIndex);
            Assert.AreEqual(1, match.GetWins(0));
            Assert.AreEqual(2, match.GetWins(1));
            Assert.AreEqual(3, match.RoundsPlayed);
        }

        [Test]
        public void DrawsCountAsPlayedButScoreNothing()
        {
            var match = new MatchStateMachine(2, 2);
            Assert.IsFalse(match.RecordRound(MatchStateMachine.NoPlayer));
            Assert.AreEqual(1, match.RoundsPlayed);
            Assert.AreEqual(0, match.GetWins(0));
        }

        [Test]
        public void EndlessMatchesNeverEnd()
        {
            var match = new MatchStateMachine(2, 0);
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(match.RecordRound(0));
            Assert.AreEqual(20, match.GetWins(0));
        }

        [Test]
        public void ResetStartsAFreshMatch()
        {
            var match = new MatchStateMachine(2, 1);
            match.RecordRound(0);
            match.Reset();

            Assert.IsFalse(match.IsOver);
            Assert.AreEqual(MatchStateMachine.NoPlayer, match.WinnerIndex);
            Assert.AreEqual(0, match.GetWins(0));
            Assert.AreEqual(0, match.RoundsPlayed);
        }

        [Test]
        public void CalmestBabyWinsOnTime()
        {
            Assert.AreEqual(1, RoundRules.CalmestBaby(new[] { 40f, 25f }, 0.5f));
            Assert.AreEqual(0, RoundRules.CalmestBaby(new[] { 10f, 90f, 30f }, 0.5f));
        }

        [Test]
        public void EqualCryOnTimeIsADraw()
        {
            Assert.AreEqual(MatchStateMachine.NoPlayer, RoundRules.CalmestBaby(new[] { 30f, 30.2f }, 0.5f));
            Assert.AreEqual(2, RoundRules.CalmestBaby(new[] { 30f, 30f, 5f }, 0.5f), "a tie for last place doesn't matter");
        }
    }
}
