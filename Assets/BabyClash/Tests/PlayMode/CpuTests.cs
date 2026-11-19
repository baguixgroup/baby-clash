using System.Collections;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyClash.Tests
{
    /// <summary>The CPU baby plays by the same rules as a human and actually gets things done.</summary>
    public sealed class CpuTests
    {
        TestArena arena;
        BabyController[] babies;
        Diaper[] diapers;
        RoundManager round;

        [SetUp]
        public void SetUp()
        {
            arena = new TestArena();
            Time.timeScale = 3f; // behaviour tests, not feel tests: run them faster
        }

        [TearDown]
        public void TearDown() => arena.Dispose();

        IEnumerator Duel(float distance) =>
            arena.StartDuel(distance, (b, d, r) =>
            {
                babies = b;
                diapers = d;
                round = r;
            });

        [UnityTest]
        public IEnumerator CpuHitsAnIdleOpponent()
        {
            yield return Duel(9f);
            arena.AddBrain(babies[0], round, CpuDifficulty.Hard);

            yield return TestArena.WaitUntil(() => babies[1].Cry.Value > 0f, 25f);
            Assert.Greater(babies[1].Cry.Value, 0f, "the CPU found a way to hit its opponent");
        }

        [UnityTest]
        public IEnumerator DiaperlessCpuGoesBackForADiaper()
        {
            yield return Duel(12f);
            babies[0].Movement.FaceInstantly(Vector3.forward);
            Assert.IsTrue(babies[0].TryThrow());
            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, 5f);
            arena.AddBrain(babies[0], round, CpuDifficulty.Normal);

            yield return TestArena.WaitUntil(() => babies[0].IsProtected, 20f);
            Assert.IsTrue(babies[0].IsProtected, "the CPU put a diaper back on");
        }

        [UnityTest]
        public IEnumerator CpuStopsWhenTheRoundIsOver()
        {
            arena.Match.roundResultDuration = 30f; // stay on the result, no auto next round
            yield return Duel(9f);
            arena.AddBrain(babies[0], round, CpuDifficulty.Hard);
            yield return null;
            babies[0].ReceiveHit(new HitRequest(1000f, 0f, 1f), Vector3.left, babies[0].transform.position, true);

            Assert.AreEqual(RoundPhase.RoundOver, round.Phase);
            yield return new WaitForSeconds(0.6f); // let any running momentum slide out
            Vector3 position = babies[0].transform.position;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector3.Distance(position, babies[0].transform.position), 0.05f, "a crying CPU doesn't wander off");
        }
    }
}
