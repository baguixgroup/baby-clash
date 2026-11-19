using System.Collections;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyClash.Tests
{
    /// <summary>The core mechanic with real physics: wear, throw, hit, land, pick up, throw back.</summary>
    public sealed class DiaperLoopTests
    {
        const float LandTimeout = 5f;
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

        [UnityTest]
        public IEnumerator EachBabyStartsTheRoundProtectedByItsOwnDiaper()
        {
            yield return Duel(6f);

            Assert.AreEqual(RoundPhase.Playing, round.Phase);
            for (int i = 0; i < babies.Length; i++)
            {
                Assert.IsTrue(babies[i].IsProtected);
                Assert.AreEqual(BabyState.Protected, babies[i].State);
                Assert.AreSame(babies[i], diapers[i].Holder);
                Assert.AreEqual(ItemState.Held, diapers[i].State);
            }
        }

        [UnityTest]
        public IEnumerator ThrowingRemovesProtection_AndTheDiaperLandsInsteadOfDisappearing()
        {
            yield return Duel(30f); // too far apart to hit

            Assert.IsTrue(babies[0].TryThrow());
            Assert.IsFalse(babies[0].IsProtected);
            Assert.AreEqual(BabyState.Diaperless, babies[0].State);
            Assert.AreEqual(ItemState.Flying, diapers[0].State);
            Assert.IsNull(diapers[0].Holder);

            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, LandTimeout);

            Assert.AreEqual(ItemState.Loose, diapers[0].State);
            Assert.IsTrue(diapers[0] != null && diapers[0].gameObject.activeInHierarchy, "the same diaper is still in play");
            Assert.Less(diapers[0].transform.position.y, 0.6f, "it is lying on the floor");
            Assert.IsFalse(babies[0].IsProtected, "the thrower does not get a free replacement");
        }

        [UnityTest]
        public IEnumerator ThrownDiaperHitsTheOpponent_ThenLandsNearThem()
        {
            yield return Duel(5f);

            Assert.IsTrue(babies[0].TryThrow());
            yield return TestArena.WaitUntil(() => babies[1].Cry.Value > 0f, 2f);

            float expectedCry = arena.Diaper.directHitCry * arena.Baby.protectedDefense.cryMultiplier;
            Assert.AreEqual(expectedCry, babies[1].Cry.Value, 0.01f, "one direct hit on a protected baby");
            Assert.IsTrue(diapers[0].Status.HasHit);

            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, LandTimeout);
            Assert.AreEqual(ItemState.Loose, diapers[0].State);
            Assert.AreEqual(expectedCry, babies[1].Cry.Value, 0.01f, "one throw never hits twice");

            Vector3 offset = diapers[0].transform.position - babies[1].transform.position;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 5f, "dropped near the victim");
        }

        [UnityTest]
        public IEnumerator DiaperlessBabyPicksUpTheThrownDiaper_AndThrowsItBack()
        {
            yield return Duel(5f);

            // Baby 2 tosses its own diaper sideways first, so it's diaperless and allowed to pick one up.
            babies[1].Movement.FaceInstantly(Vector3.forward);
            Assert.IsTrue(babies[1].TryThrow());
            yield return TestArena.WaitUntil(() => diapers[1].State == ItemState.Loose, LandTimeout);

            Assert.IsTrue(babies[0].TryThrow());
            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, LandTimeout);
            Assert.Greater(babies[1].Cry.Value, 0f, "baby 2 got hit");

            // Put baby 2 on the landed diaper (keeps the test independent of movement tuning).
            Vector3 spot = diapers[0].transform.position;
            spot.y = 0f;
            babies[1].Movement.Teleport(spot, Quaternion.LookRotation(Vector3.left));
            yield return new WaitForFixedUpdate();
            yield return TestArena.WaitUntil(() => babies[1].Status.CanAct, 1f); // hit stun wears off

            Assert.IsTrue(babies[1].TryPickUp());
            Assert.AreSame(babies[1], diapers[0].Holder, "the same diaper changed hands");
            Assert.IsTrue(babies[1].IsProtected);
            Assert.IsFalse(babies[0].IsProtected);

            float cryBefore = babies[0].Cry.Value;
            Assert.IsTrue(babies[1].TryThrow());
            yield return TestArena.WaitUntil(() => babies[0].Cry.Value > cryBefore, 2f);

            float expected = cryBefore + arena.Diaper.directHitCry * arena.Baby.diaperlessDefense.cryMultiplier;
            Assert.AreEqual(expected, babies[0].Cry.Value, 0.01f, "direct hit on the now-diaperless original owner");
        }

        [UnityTest]
        public IEnumerator WearingBabyCannotPickUpAnotherDiaper_ActionThrowsInstead()
        {
            yield return Duel(5f);

            babies[0].Movement.FaceInstantly(Vector3.forward);
            babies[0].TryThrow();
            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, LandTimeout);

            Vector3 spot = diapers[0].transform.position;
            spot.y = 0f;
            babies[1].Movement.Teleport(spot, Quaternion.LookRotation(Vector3.back));
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(babies[1].TryPickUp());
            Assert.AreEqual(ItemState.Loose, diapers[0].State);
            Assert.AreEqual(1, babies[1].Diapers.Count);

            babies[1].PressAction();
            Assert.AreEqual(ItemState.Flying, diapers[1].State, "wearing + action = throw");
            Assert.AreEqual(ItemState.Loose, diapers[0].State);
        }

        [UnityTest]
        public IEnumerator ProtectedBabyTakesLessCryAndKnockbackThanDiaperlessBaby()
        {
            yield return Duel(20f);

            babies[1].Movement.FaceInstantly(Vector3.forward);
            Assert.IsTrue(babies[1].TryThrow());

            var hit = new HitRequest(10f, 5f, 1f);
            babies[0].ReceiveHit(hit, Vector3.left, babies[0].transform.position, false);
            babies[1].ReceiveHit(hit, Vector3.right, babies[1].transform.position, false);

            Assert.AreEqual(5f, babies[0].Cry.Value, 0.01f, "protected");
            Assert.AreEqual(15f, babies[1].Cry.Value, 0.01f, "diaperless");
            Assert.Greater(Horizontal(babies[1].Movement.Velocity), Horizontal(babies[0].Movement.Velocity));
            Assert.AreEqual(BabyState.Stunned, babies[0].State);
        }

        static float Horizontal(Vector3 velocity) => new Vector2(velocity.x, velocity.z).magnitude;
    }
}
