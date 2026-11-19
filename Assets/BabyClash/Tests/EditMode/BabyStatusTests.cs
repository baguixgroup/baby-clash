using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class BabyStatusTests
    {
        [Test]
        public void StartsReadyAndReportsDiaperState()
        {
            var status = new BabyStatus();
            Assert.IsTrue(status.CanAct);
            Assert.IsTrue(status.CanMove);
            Assert.IsTrue(status.CanDash);
            Assert.AreEqual(BabyState.Protected, status.ToBabyState(true));
            Assert.AreEqual(BabyState.Diaperless, status.ToBabyState(false));
        }

        [Test]
        public void ThrowEntersThrowingThenRecovers()
        {
            var status = new BabyStatus();
            Assert.IsTrue(status.TryBeginThrow(0.2f, 0.3f));
            Assert.AreEqual(BabyActivity.Throwing, status.Activity);
            Assert.IsTrue(status.CanMove);
            Assert.IsFalse(status.CanAct);

            status.Tick(0.25f);
            Assert.AreEqual(BabyActivity.Ready, status.Activity);
            Assert.IsFalse(status.CanAct, "still cooling down");

            status.Tick(0.1f);
            Assert.IsTrue(status.CanAct);
        }

        [Test]
        public void DashHasADodgeWindowThenACooldown()
        {
            var status = new BabyStatus();
            Assert.IsTrue(status.TryBeginDash(0.3f, 1f));
            Assert.AreEqual(BabyState.Dashing, status.ToBabyState(true));
            Assert.IsFalse(status.CanMove, "a dash can't be steered");
            Assert.IsFalse(status.CanAct);
            Assert.IsTrue(status.IsDodging(0.2f));

            status.Tick(0.25f);
            Assert.IsFalse(status.IsDodging(0.2f), "dodge window over");
            Assert.IsTrue(status.IsDashing);

            status.Tick(0.1f);
            Assert.AreEqual(BabyActivity.Ready, status.Activity);
            Assert.IsFalse(status.CanDash, "cooling down");
            Assert.IsFalse(status.TryBeginDash(0.3f, 1f));

            status.Tick(1f);
            Assert.IsTrue(status.CanDash);
        }

        [Test]
        public void StunInterruptsADash()
        {
            var status = new BabyStatus();
            status.TryBeginDash(0.3f, 1f);
            status.ApplyStun(0.4f);

            Assert.AreEqual(BabyActivity.Stunned, status.Activity);
            Assert.AreEqual(0f, status.DashElapsed);
        }

        [Test]
        public void StunBlocksMovingActingAndDashing()
        {
            var status = new BabyStatus();
            status.ApplyStun(0.4f);

            Assert.AreEqual(BabyState.Stunned, status.ToBabyState(true));
            Assert.IsFalse(status.CanMove);
            Assert.IsFalse(status.CanAct);
            Assert.IsFalse(status.CanDash);

            status.Tick(0.5f);
            Assert.IsTrue(status.CanMove);
        }

        [Test]
        public void OverlappingStunsKeepTheLongerOne()
        {
            var status = new BabyStatus();
            status.ApplyStun(0.5f);
            status.Tick(0.1f);
            status.ApplyStun(0.1f);

            status.Tick(0.2f);
            Assert.AreEqual(BabyActivity.Stunned, status.Activity);
        }

        [Test]
        public void CryingIsTerminalUntilReset()
        {
            var status = new BabyStatus();
            status.StartCrying();

            Assert.IsFalse(status.CanAct);
            Assert.IsFalse(status.CanMove);
            Assert.IsFalse(status.CanDash);
            Assert.IsFalse(status.TryBeginThrow(0.2f, 0.3f));
            Assert.IsFalse(status.ApplyStun(1f));
            status.Tick(10f);
            Assert.AreEqual(BabyState.Crying, status.ToBabyState(true));

            status.Reset();
            Assert.AreEqual(BabyActivity.Ready, status.Activity);
            Assert.IsTrue(status.CanAct);
        }
    }
}
