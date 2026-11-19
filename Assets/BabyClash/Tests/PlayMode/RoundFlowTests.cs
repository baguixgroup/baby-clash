using System.Collections;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyClash.Tests
{
    public sealed class RoundFlowTests
    {
        TestArena arena;
        BabyController[] babies;
        Diaper[] diapers;
        RoundManager round;

        [SetUp]
        public void SetUp() => arena = new TestArena();

        [TearDown]
        public void TearDown() => arena.Dispose();

        IEnumerator Duel(float distance) =>
            arena.StartDuel(distance, (b, d, r) =>
            {
                babies = b;
                diapers = d;
                round = r;
            });

        static readonly HitRequest KnockOut = new HitRequest(1000f, 3f, 1f);

        [UnityTest]
        public IEnumerator FullCryEndsTheRound_AndTheNextRoundRestoresEverything()
        {
            yield return Duel(20f);
            Vector3[] spawns = { babies[0].transform.position, babies[1].transform.position };

            // Mess up the world: baby 1 throws its diaper away, baby 2 gets hit hard enough to cry.
            Assert.IsTrue(babies[0].TryThrow());
            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, 5f);
            babies[1].ReceiveHit(KnockOut, Vector3.right, babies[1].transform.position, true);

            Assert.AreEqual(100f, babies[1].Cry.Value, "the meter clamps at its max");
            Assert.IsTrue(babies[1].IsCrying);
            Assert.AreEqual(BabyState.Crying, babies[1].State);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.AreSame(babies[0], round.RoundWinner);
            Assert.AreSame(babies[1], round.RoundLoser);
            Assert.AreEqual(1, round.GetWins(babies[0]));

            // Nobody can act once the round is over.
            Assert.IsFalse(babies[1].TryThrow());
            Assert.IsFalse(babies[0].TryPickUp());
            Assert.IsFalse(babies[0].ReceiveHit(new HitRequest(10f, 1f, 1f), Vector3.left, Vector3.zero, true));

            // The next round starts by itself after the result is shown.
            yield return TestArena.WaitUntil(() => round.RoundNumber == 2 && round.Phase == RoundPhase.Playing, 3f);
            Assert.AreEqual(2, round.RoundNumber);
            for (int i = 0; i < babies.Length; i++)
            {
                Assert.AreEqual(0f, babies[i].Cry.Value);
                Assert.IsFalse(babies[i].IsCrying);
                Assert.AreEqual(BabyState.Protected, babies[i].State);
                Assert.AreSame(babies[i], diapers[i].Holder, "every baby gets its own diaper back");
                Assert.Less(Vector3.Distance(spawns[i], babies[i].transform.position), 0.1f, "back at spawn");
            }
            Assert.AreEqual(1, round.GetWins(babies[0]), "score carries over");
        }

        [UnityTest]
        public IEnumerator FirstToTwoRoundWinsTakesTheMatch()
        {
            yield return Duel(20f);
            BabyController matchWinner = null;
            round.MatchEnded += winner => matchWinner = winner;

            for (int roundIndex = 0; roundIndex < 2; roundIndex++)
            {
                yield return TestArena.WaitUntil(() => round.Phase == RoundPhase.Playing, 3f);
                babies[1].ReceiveHit(KnockOut, Vector3.right, babies[1].transform.position, true);
                Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            }

            Assert.AreSame(babies[0], matchWinner);
            Assert.IsTrue(round.IsMatchOver);
            Assert.AreSame(babies[0], round.MatchWinner);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(RoundPhase.RoundOver, round.Phase, "a finished match waits for the menu");

            round.StartMatch(false);
            yield return null;
            Assert.AreEqual(0, round.GetWins(babies[0]), "a rematch starts from zero");
            Assert.IsFalse(round.IsMatchOver);
        }

        [UnityTest]
        public IEnumerator TimeUpGivesTheRoundToTheCalmestBaby()
        {
            arena.Match.roundTimeLimit = 1f;
            yield return Duel(20f);

            babies[1].ReceiveHit(new HitRequest(10f, 1f, 1f), Vector3.right, babies[1].transform.position, true);
            yield return TestArena.WaitUntil(() => round.Phase == RoundPhase.RoundOver, 3f);

            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            Assert.IsTrue(round.RoundEndedOnTime);
            Assert.AreSame(babies[0], round.RoundWinner);
            Assert.IsTrue(babies[1].IsCrying, "losing on points still ends in tears");
            Assert.AreEqual(1, round.GetWins(babies[0]));
        }
    }
}
