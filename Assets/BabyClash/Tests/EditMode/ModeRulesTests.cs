using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class ModeRulesTests
    {
        static RoundStateMachine Playing(int players, int[] sides)
        {
            var round = new RoundStateMachine(players, 0f, 60f, sides);
            round.StartNewRound();
            return round;
        }

        [Test]
        public void FreeForAllGoesToTheLastCalmBaby()
        {
            RoundStateMachine round = Playing(4, null);
            round.ReportCrying(0);
            round.ReportCrying(3);
            Assert.AreEqual(RoundPhase.Playing, round.Phase);
            round.ReportCrying(1);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(2, round.WinnerIndex);
        }

        [Test]
        public void TeamRoundEndsOnlyWhenAWholeTeamIsCrying()
        {
            RoundStateMachine round = Playing(4, new[] { 0, 1, 0, 1 });
            round.ReportCrying(0);
            round.ReportCrying(1);
            Assert.AreEqual(RoundPhase.Playing, round.Phase, "one baby of each team is still calm");
            round.ReportCrying(3);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreEqual(0, round.WinnerIndex, "the winner is the team");
        }

        [Test]
        public void WinningSideMustExist()
        {
            RoundStateMachine round = Playing(4, new[] { 0, 1, 0, 1 });
            round.EndRound(3, false);
            Assert.AreEqual(RoundStateMachine.NoPlayer, round.WinnerIndex);
        }

        [Test]
        public void TimeUpComparesTeamAverages()
        {
            var sides = new[] { 0, 1, 0, 1 };
            Assert.AreEqual(1, RoundRules.CalmestSide(new[] { 10f, 30f, 80f, 40f }, sides, 2, 0.5f));
            Assert.AreEqual(MatchStateMachine.NoPlayer, RoundRules.CalmestSide(new[] { 10f, 20f, 30f, 20f }, sides, 2, 0.5f));
        }

        [Test]
        public void ScoreBoardStopsAtItsTargetOnce()
        {
            var board = new ScoreBoard(2, 20f);
            int reached = 0;
            board.TargetReached += side => reached++;
            board.Add(0, 12f);
            board.Add(1, 5f);
            board.Add(0, 12f);
            board.Add(0, 12f);
            Assert.AreEqual(1, reached);
            Assert.AreEqual(20f, board.Get(0));
            Assert.AreEqual(0, board.Leader());
        }

        [Test]
        public void ScoreBoardTieHasNoLeader()
        {
            var board = new ScoreBoard(3);
            Assert.AreEqual(ScoreBoard.NoSide, board.Leader());
            board.Add(2, 1f);
            board.Add(1, 1f);
            Assert.AreEqual(ScoreBoard.NoSide, board.Leader());
            board.Add(1, 1f);
            Assert.AreEqual(1, board.Leader());
        }

        [Test]
        public void CuddlesCalmButNeverUndoTears()
        {
            var meter = new CryMeter(100f);
            meter.Add(60f);
            meter.Reduce(35f);
            Assert.AreEqual(25f, meter.Value, 0.001f);
            meter.Reduce(100f);
            Assert.AreEqual(0f, meter.Value);
            meter.Add(100f);
            meter.Reduce(50f);
            Assert.IsTrue(meter.IsFull);
        }

        [Test]
        public void EveryLevelIsWinnableForThreeStars()
        {
            foreach (LevelDef level in LevelCatalog.All)
            {
                var perfect = new LevelResult { won = true, seconds = 1f, hits = 999, wave = 99 };
                Assert.AreEqual(3, LevelCatalog.Evaluate(level, perfect), level.Id);
                Assert.AreEqual(0, LevelCatalog.Evaluate(level, new LevelResult { seconds = 1f, hits = 999 }), level.Id);
                Assert.IsNotEmpty(LevelCatalog.Describe(level, level.Second), level.Id);
                Assert.IsNotEmpty(LevelCatalog.Describe(level, level.Third), level.Id);
            }
        }

        [Test]
        public void SurvivalStarsDependOnTheWaveNotOnWinning()
        {
            LevelDef level = LevelCatalog.Find("kitchen.4");
            Assert.AreEqual(0, LevelCatalog.Evaluate(level, new LevelResult { won = true, wave = level.WaveGoal - 1 }));
            Assert.AreEqual(1, LevelCatalog.Evaluate(level, new LevelResult { wave = level.WaveGoal }));
            Assert.AreEqual(2, LevelCatalog.Evaluate(level, new LevelResult { wave = level.WaveGoal + 2 }));
        }
    }
}
