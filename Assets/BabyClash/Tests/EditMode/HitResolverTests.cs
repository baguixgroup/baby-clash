using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class HitResolverTests
    {
        // Same numbers as the default BabyConfig / DiaperConfig assets.
        static readonly DefenseProfile Protected = new DefenseProfile(0.5f, 0.6f, 0.2f);
        static readonly DefenseProfile Diaperless = new DefenseProfile(1.5f, 1.4f, 0.45f);
        static readonly HitRequest NormalImpact = new HitRequest(10f, 5f, 1f);
        static readonly HitRequest DirectHit = new HitRequest(16f, 8f, 1f);

        [Test]
        public void ProtectedBabyTakesReducedCry()
        {
            HitOutcome outcome = HitResolver.Resolve(NormalImpact, Protected, 0f);
            Assert.AreEqual(5f, outcome.Cry, 0.0001f);
        }

        [Test]
        public void DiaperlessBabyTakesIncreasedCry()
        {
            HitOutcome outcome = HitResolver.Resolve(NormalImpact, Diaperless, 0f);
            Assert.AreEqual(15f, outcome.Cry, 0.0001f);
        }

        [Test]
        public void DiaperlessBabyIsKnockedBackFurtherAndStunnedLonger()
        {
            HitOutcome protectedOutcome = HitResolver.Resolve(DirectHit, Protected, 0f);
            HitOutcome diaperlessOutcome = HitResolver.Resolve(DirectHit, Diaperless, 0f);

            Assert.Greater(diaperlessOutcome.KnockbackSpeed, protectedOutcome.KnockbackSpeed);
            Assert.Greater(diaperlessOutcome.StunDuration, protectedOutcome.StunDuration);
        }

        [Test]
        public void DirectHitHurtsMoreThanNormalImpact()
        {
            Assert.Greater(HitResolver.Resolve(DirectHit, Protected, 0f).Cry, HitResolver.Resolve(NormalImpact, Protected, 0f).Cry);
            Assert.Greater(HitResolver.Resolve(DirectHit, Diaperless, 0f).Cry, HitResolver.Resolve(NormalImpact, Diaperless, 0f).Cry);
        }

        [Test]
        public void KnockbackScalesWithThrowStrengthButNotCry()
        {
            HitOutcome weak = HitResolver.Resolve(new HitRequest(10f, 5f, 0.5f), Diaperless, 0f);
            HitOutcome strong = HitResolver.Resolve(new HitRequest(10f, 5f, 1f), Diaperless, 0f);

            Assert.AreEqual(strong.KnockbackSpeed * 0.5f, weak.KnockbackSpeed, 0.0001f);
            Assert.AreEqual(strong.Cry, weak.Cry, 0.0001f);
        }

        [Test]
        public void StrengthIsClamped()
        {
            HitOutcome outcome = HitResolver.Resolve(new HitRequest(10f, 5f, 100f), new DefenseProfile(1f, 1f, 0f), 0f);
            Assert.AreEqual(5f * HitResolver.MaxStrength, outcome.KnockbackSpeed, 0.0001f);
        }

        [Test]
        public void ResistanceReducesKnockback()
        {
            HitOutcome none = HitResolver.Resolve(DirectHit, Diaperless, 0f);
            HitOutcome half = HitResolver.Resolve(DirectHit, Diaperless, 0.5f);
            HitOutcome full = HitResolver.Resolve(DirectHit, Diaperless, 1f);

            Assert.AreEqual(none.KnockbackSpeed * 0.5f, half.KnockbackSpeed, 0.0001f);
            Assert.AreEqual(0f, full.KnockbackSpeed, 0.0001f);
        }

        [Test]
        public void NegativeInputsNeverHeal()
        {
            HitOutcome outcome = HitResolver.Resolve(new HitRequest(-10f, -5f, -1f), new DefenseProfile(-1f, -1f, -1f), -3f);
            Assert.AreEqual(0f, outcome.Cry);
            Assert.AreEqual(0f, outcome.KnockbackSpeed);
            Assert.AreEqual(0f, outcome.StunDuration);
        }
    }
}
