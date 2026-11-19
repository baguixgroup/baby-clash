using System.Collections;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyClash.Tests
{
    /// <summary>Toys (extra ammo that doesn't cost your diaper), dash dodges and dash shoves.</summary>
    public sealed class ToyAndMoveTests
    {
        TestArena arena;
        BabyController[] babies;
        Diaper[] diapers;
        RoundManager round;
        ToySpawner spawner;
        Toy toy;

        [SetUp]
        public void SetUp() => arena = new TestArena();

        [TearDown]
        public void TearDown() => arena.Dispose();

        IEnumerator Duel(float distance)
        {
            toy = arena.CreateToy("TestToy");
            spawner = arena.CreateSpawner(new[] { toy });
            yield return arena.StartDuel(distance, (b, d, r) =>
            {
                babies = b;
                diapers = d;
                round = r;
            }, spawner);
        }

        IEnumerator DropToyAt(Vector3 groundPosition)
        {
            spawner.SpawnAt(toy, groundPosition);
            yield return TestArena.WaitUntil(() => toy.Body.linearVelocity.sqrMagnitude < 0.01f && toy.transform.position.y < 0.4f, 3f);
        }

        [UnityTest]
        public IEnumerator ToyIsGrabbedWithoutLosingTheDiaper_AndThrownToHit()
        {
            yield return Duel(5f);
            yield return DropToyAt(babies[0].transform.position + Vector3.right * 0.6f);

            babies[0].PressAction();
            Assert.AreSame(toy, babies[0].Hands.Held, "grabbed the toy");
            Assert.AreEqual(ItemState.Held, toy.State);
            Assert.IsTrue(babies[0].IsProtected, "still wearing the diaper");

            babies[0].PressAction();
            Assert.AreEqual(ItemState.Flying, toy.State, "second press throws the toy");
            Assert.IsFalse(babies[0].Hands.IsHolding);
            Assert.IsTrue(babies[0].IsProtected, "throwing a toy never costs the diaper");

            yield return TestArena.WaitUntil(() => babies[1].Cry.Value > 0f, 2f);
            Assert.AreEqual(arena.Block.directHitCry * arena.Baby.protectedDefense.cryMultiplier, babies[1].Cry.Value, 0.01f);

            bool expired = false;
            toy.Expired += _ => expired = true;
            yield return TestArena.WaitUntil(() => expired, arena.Block.despawnAfterLanding + 5f);
            Assert.IsTrue(expired, "thrown toys vanish after landing");
            Assert.IsFalse(toy.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator DashingThroughAThrowDodgesIt()
        {
            // Stand still inside the throw's path, but dashing: the dodge window lets it pass through.
            arena.Baby.dashSpeed = 0f;
            arena.Baby.dashDuration = 1f;
            arena.Baby.dashDodgeWindow = 1f;
            yield return Duel(5f);

            Assert.IsTrue(babies[1].PressDash());
            Assert.IsTrue(babies[0].TryThrow());
            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, 4f);

            Assert.AreEqual(0f, babies[1].Cry.Value, "the dodge worked");
            Assert.IsFalse(diapers[0].Status.HasHit);
        }

        [UnityTest]
        public IEnumerator DashingIntoTheOpponentShovesThem_AndKnocksTheirToyLoose()
        {
            yield return Duel(2.2f);
            yield return DropToyAt(babies[1].transform.position + Vector3.left * 0.4f);
            babies[1].PressAction();
            Assert.IsTrue(babies[1].Hands.IsHolding);

            BabyController shoved = null;
            BabyState stateWhenShoved = BabyState.Protected;
            babies[0].Shoved += (shover, victim) =>
            {
                shoved = victim;
                stateWhenShoved = victim.State;
            };
            babies[0].SetMoveInput(Vector2.right);
            Assert.IsTrue(babies[0].PressDash());
            yield return TestArena.WaitUntil(() => shoved != null, 2f);

            Assert.AreSame(babies[1], shoved);
            float expected = arena.Baby.shoveCry * arena.Baby.protectedDefense.cryMultiplier;
            Assert.AreEqual(expected, babies[1].Cry.Value, 0.01f);
            Assert.IsFalse(babies[1].Hands.IsHolding, "the toy got knocked loose");
            Assert.AreEqual(ItemState.Loose, toy.State);
            Assert.AreEqual(BabyState.Stunned, stateWhenShoved);
        }

        [UnityTest]
        public IEnumerator DiaperlessBabyGrabsTheDiaperBeforeATossedToy()
        {
            yield return Duel(20f);
            babies[0].TryThrow();
            yield return TestArena.WaitUntil(() => diapers[0].State == ItemState.Loose, 5f);

            // Put the loose diaper and a toy right next to baby 2's teammate spot: baby 1 goes to both.
            Vector3 spot = diapers[0].transform.position;
            spot.y = 0f;
            yield return DropToyAt(spot + Vector3.forward * 0.3f);
            babies[0].Movement.Teleport(spot, Quaternion.identity);
            yield return new WaitForFixedUpdate();

            babies[0].PressAction();
            Assert.IsTrue(babies[0].IsProtected, "armour first");
            Assert.IsFalse(babies[0].Hands.IsHolding);
            babies[0].PressAction();
            Assert.IsTrue(babies[0].Hands.IsHolding, "then the toy");
        }
    }
}
