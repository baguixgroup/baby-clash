using System;
using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class ProgressionTests
    {
        [Test]
        public void IncompleteSavedProfileRecoversAndPreservesBalance()
        {
            var profile = new PlayerProfile { rattles = 80, owned = null, challenges = null, levels = null, lifetime = null };
            profile.looks.Add(null);
            profile.Normalize();
            Assert.AreEqual(80, profile.rattles);
            Assert.AreEqual(4, profile.looks.Count);
            Assert.AreEqual(CosmeticCatalog.NoHat, profile.looks[0].hat);
        }

        [Test]
        public void PurchaseSpendsOnceAndEquipsOnlyOwnedItems()
        {
            var profile = new PlayerProfile { rattles = 100 };
            profile.Normalize();
            Assert.IsFalse(Shop.Equip(profile, 0, "hat.party"));
            Assert.AreEqual(PurchaseResult.Bought, Shop.TryBuy(profile, "hat.party"));
            Assert.AreEqual(60, profile.rattles);
            Assert.AreEqual(PurchaseResult.AlreadyOwned, Shop.TryBuy(profile, "hat.party"));
            Assert.IsTrue(Shop.Equip(profile, 0, "hat.party"));
            Assert.AreEqual("hat.party", profile.LookFor(0).hat);
            Assert.AreEqual(60, profile.rattles);
        }

        [Test]
        public void FirstWinBonusPaysOncePerDay()
        {
            var profile = new PlayerProfile();
            profile.Normalize();
            var stats = new MatchStats { won = true, hits = 10 };
            var today = new DateTime(2026, 11, 18);
            int first = Rewards.Apply(profile, stats, today);
            int second = Rewards.Apply(profile, stats, today);
            Assert.AreEqual(Rewards.FirstWinOfTheDayBonus, first - second);
            Assert.AreEqual(first, Rewards.Apply(profile, stats, today.AddDays(1)));
        }

        [Test]
        public void ChallengesPayOnceAndAdvanceStreakOnConsecutiveDays()
        {
            var profile = new PlayerProfile();
            profile.Normalize();
            var stats = new MatchStats { won = true, hits = 100, toyHits = 100, diaperHits = 100,
                shoves = 100, dodges = 100, knockouts = 100, powerUps = 100, giggles = 100,
                survivalWave = 100, bottleSeconds = 100 };
            var day = new DateTime(2026, 11, 18);
            for (int i = 0; i < 3; i++)
                ChallengeBook.Record(profile, stats, day, out _);
            Assert.IsTrue(ChallengeBook.AllDone(profile));
            int paid = profile.rattles;
            ChallengeBook.Record(profile, stats, day, out int duplicate);
            Assert.AreEqual(0, duplicate);
            Assert.AreEqual(paid, profile.rattles);
            for (int i = 0; i < 3; i++)
                ChallengeBook.Record(profile, stats, day.AddDays(1), out _);
            Assert.AreEqual(2, profile.streak);
        }

        [Test]
        public void StoryUnlocksFollowWinsAndStarsNeverDecrease()
        {
            var profile = new PlayerProfile();
            profile.Normalize();
            Assert.IsTrue(LevelCatalog.IsUnlocked(profile, 0));
            Assert.IsFalse(LevelCatalog.IsUnlocked(profile, 1));
            var level = LevelCatalog.All[0];
            int stars = LevelCatalog.Evaluate(level, new LevelResult { won = true, hits = 10 });
            Assert.AreEqual(3, stars);
            profile.RecordStars(level.Id, stars);
            profile.RecordStars(level.Id, 1);
            Assert.AreEqual(3, profile.StarsFor(level.Id));
            Assert.IsTrue(LevelCatalog.IsUnlocked(profile, 1));
        }

        [Test]
        public void AvailableChallengesStayDeterministicAndKeepCurrentProgress()
        {
            var day = new DateTime(2026, 11, 18);
            Predicate<ChallengeDef> available = definition => definition.Stat == ChallengeStat.Matches ||
                definition.Stat == ChallengeStat.Wins || definition.Stat == ChallengeStat.Hits;
            var profile = new PlayerProfile();
            profile.Normalize();
            Assert.IsTrue(ChallengeBook.Refresh(profile, day, available));
            profile.challenges[0].progress = 1;
            Assert.IsFalse(ChallengeBook.Refresh(profile, day, available));
            Assert.AreEqual(1, profile.challenges[0].progress);
            var selected = ChallengeBook.ForDate(day, available);
            for (int index = 0; index < selected.Length; index++)
            {
                Assert.IsTrue(available(selected[index]));
                Assert.AreEqual(selected[index].Id, profile.challenges[index].id);
            }
        }
    }
}
