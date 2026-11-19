using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    /// <summary>The item ownership loop: held -> thrown -> hits -> lands -> picked up by the other baby -> thrown back.</summary>
    public sealed class ItemStatusTests
    {
        const int BabyA = 0;
        const int BabyB = 1;

        static ItemStatus HeldBy(int babyId)
        {
            var item = new ItemStatus();
            item.ForceHold(babyId);
            return item;
        }

        [Test]
        public void ThrowingTransfersFromHolderToFlight()
        {
            ItemStatus item = HeldBy(BabyA);

            Assert.IsTrue(item.TryThrow(BabyA));
            Assert.AreEqual(ItemState.Flying, item.State);
            Assert.AreEqual(ItemStatus.Nobody, item.HolderId);
            Assert.AreEqual(BabyA, item.ThrowerId);
        }

        [Test]
        public void OnlyTheHolderCanThrowIt()
        {
            ItemStatus item = HeldBy(BabyA);
            Assert.IsFalse(item.TryThrow(BabyB));
            Assert.AreEqual(ItemState.Held, item.State);
        }

        [Test]
        public void ThrowerIsImmuneToTheirOwnThrow()
        {
            ItemStatus item = HeldBy(BabyA);
            item.TryThrow(BabyA);

            Assert.IsFalse(item.TryRegisterHit(BabyA, out _));
            Assert.IsTrue(item.IsDangerous);
        }

        [Test]
        public void OneThrowHitsAtMostOnce()
        {
            ItemStatus item = HeldBy(BabyA);
            item.TryThrow(BabyA);

            Assert.IsTrue(item.TryRegisterHit(BabyB, out bool direct));
            Assert.IsTrue(direct);
            Assert.IsFalse(item.TryRegisterHit(BabyB, out _));
            Assert.IsFalse(item.IsDangerous);
        }

        [Test]
        public void HitAfterBounceIsNotDirect()
        {
            ItemStatus item = HeldBy(BabyA);
            item.TryThrow(BabyA);
            item.RegisterBounce();

            Assert.IsTrue(item.TryRegisterHit(BabyB, out bool direct));
            Assert.IsFalse(direct);
        }

        [Test]
        public void FlyingItemCannotBePickedUp()
        {
            ItemStatus item = HeldBy(BabyA);
            item.TryThrow(BabyA);

            Assert.IsFalse(item.CanBePickedUp);
            Assert.IsFalse(item.TryClaim(BabyB));
        }

        [Test]
        public void FullLoop_OpponentPicksUpTheSameItemAndThrowsItBack()
        {
            ItemStatus item = HeldBy(BabyA);

            item.TryThrow(BabyA);
            item.TryRegisterHit(BabyB, out _);
            Assert.IsTrue(item.TrySettle(), "the item lands instead of disappearing");
            Assert.AreEqual(ItemState.Loose, item.State);

            Assert.IsTrue(item.TryClaim(BabyB));
            Assert.AreEqual(ItemState.Held, item.State);
            Assert.AreEqual(BabyB, item.HolderId);

            Assert.IsTrue(item.TryThrow(BabyB));
            Assert.IsTrue(item.CanHit(BabyA), "a fresh throw can hit the original owner");
            Assert.IsFalse(item.CanHit(BabyB));
        }

        [Test]
        public void TwoBabiesCannotClaimTheSameItem()
        {
            var item = new ItemStatus();

            Assert.IsTrue(item.TryClaim(BabyA));
            Assert.IsFalse(item.TryClaim(BabyB));
            Assert.AreEqual(BabyA, item.HolderId);
        }

        [Test]
        public void MakeLooseClearsOwnership()
        {
            ItemStatus item = HeldBy(BabyA);
            item.TryThrow(BabyA);
            item.MakeLoose();

            Assert.AreEqual(ItemState.Loose, item.State);
            Assert.AreEqual(ItemStatus.Nobody, item.ThrowerId);
            Assert.IsFalse(item.IsDangerous);
            Assert.IsTrue(item.CanBePickedUp);
        }

        [Test]
        public void SettleOnlyAppliesToFlyingItems()
        {
            ItemStatus item = HeldBy(BabyA);
            Assert.IsFalse(item.TrySettle());
            Assert.AreEqual(ItemState.Held, item.State);
        }
    }
}
