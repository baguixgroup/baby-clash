#nullable enable
using System;
using System.Collections.Generic;

namespace BabyClash.Core
{
    public enum CosmeticSlot
    {
        Hat,
        Face,
        Outfit,
    }

    public sealed class CosmeticItem
    {
        public CosmeticItem(string id, string name, CosmeticSlot slot, int price)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Price = price;
        }

        public string Id { get; }
        public string Name { get; }
        public CosmeticSlot Slot { get; }

        /// <summary>Rattles. 0 = owned from the start.</summary>
        public int Price { get; }

        public bool IsFree => Price <= 0;
    }

    /// <summary>Every outfit piece in the game. Ids are saved in profiles: never rename one.</summary>
    public static class CosmeticCatalog
    {
        public const string NoHat = "hat.none";
        public const string NoFace = "face.none";
        public const string DefaultOutfit = "outfit.team";

        static readonly CosmeticItem[] items =
        {
            new CosmeticItem(NoHat, "No hat", CosmeticSlot.Hat, 0),
            new CosmeticItem("hat.party", "Party hat", CosmeticSlot.Hat, 40),
            new CosmeticItem("hat.beanie", "Pom-pom beanie", CosmeticSlot.Hat, 80),
            new CosmeticItem("hat.flower", "Flower crown", CosmeticSlot.Hat, 100),
            new CosmeticItem("hat.bunny", "Bunny ears", CosmeticSlot.Hat, 120),
            new CosmeticItem("hat.chef", "Chef hat", CosmeticSlot.Hat, 150),
            new CosmeticItem("hat.propeller", "Propeller cap", CosmeticSlot.Hat, 200),
            new CosmeticItem("hat.pirate", "Pirate hat", CosmeticSlot.Hat, 250),
            new CosmeticItem("hat.crown", "Royal crown", CosmeticSlot.Hat, 350),
            new CosmeticItem("hat.unicorn", "Unicorn horn", CosmeticSlot.Hat, 500),

            new CosmeticItem(NoFace, "Nothing", CosmeticSlot.Face, 0),
            new CosmeticItem("face.pacifier", "Pacifier", CosmeticSlot.Face, 30),
            new CosmeticItem("face.bow", "Big bow", CosmeticSlot.Face, 60),
            new CosmeticItem("face.glasses", "Round glasses", CosmeticSlot.Face, 90),
            new CosmeticItem("face.shades", "Cool shades", CosmeticSlot.Face, 160),
            new CosmeticItem("face.moustache", "Fancy moustache", CosmeticSlot.Face, 220),

            new CosmeticItem(DefaultOutfit, "Team colour", CosmeticSlot.Outfit, 0),
            new CosmeticItem("outfit.mint", "Mint onesie", CosmeticSlot.Outfit, 50),
            new CosmeticItem("outfit.lilac", "Lilac onesie", CosmeticSlot.Outfit, 50),
            new CosmeticItem("outfit.sunny", "Sunny onesie", CosmeticSlot.Outfit, 70),
            new CosmeticItem("outfit.bubblegum", "Bubblegum onesie", CosmeticSlot.Outfit, 70),
            new CosmeticItem("outfit.midnight", "Midnight onesie", CosmeticSlot.Outfit, 120),
            new CosmeticItem("outfit.gold", "Golden onesie", CosmeticSlot.Outfit, 400),
            new CosmeticItem("outfit.rainbow", "Rainbow onesie", CosmeticSlot.Outfit, 600),
        };

        public static IReadOnlyList<CosmeticItem> All => items;

        public static CosmeticItem? Find(string id)
        {
            foreach (CosmeticItem item in items)
            {
                if (item.Id == id)
                    return item;
            }
            return null;
        }

        public static IEnumerable<CosmeticItem> InSlot(CosmeticSlot slot)
        {
            foreach (CosmeticItem item in items)
            {
                if (item.Slot == slot)
                    yield return item;
            }
        }

        public static string DefaultFor(CosmeticSlot slot) =>
            slot == CosmeticSlot.Hat ? NoHat : slot == CosmeticSlot.Face ? NoFace : DefaultOutfit;
    }

    /// <summary>What one local player slot wears.</summary>
    [Serializable]
    public sealed class BabyLook
    {
        public string hat = CosmeticCatalog.NoHat;
        public string face = CosmeticCatalog.NoFace;
        public string outfit = CosmeticCatalog.DefaultOutfit;

        public string Get(CosmeticSlot slot) => slot == CosmeticSlot.Hat ? hat : slot == CosmeticSlot.Face ? face : outfit;

        public void Set(CosmeticSlot slot, string id)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: hat = id; break;
                case CosmeticSlot.Face: face = id; break;
                default: outfit = id; break;
            }
        }

        public BabyLook Copy() => new BabyLook { hat = hat, face = face, outfit = outfit };
    }

    [Serializable]
    public sealed class LevelRecord
    {
        public string id = "";
        public int stars;
    }

    [Serializable]
    public sealed class ChallengeState
    {
        public string id = "";
        public int progress;
        public bool done;
    }

    [Serializable]
    public sealed class LifetimeStats
    {
        public int matches;
        public int wins;
        public int throws;
        public int hits;
        public int shoves;
        public int knockouts;
        public int giggles;
        public int powerUps;
    }

    /// <summary>
    /// Everything saved on this device: rattles (coins), owned outfit pieces, what each local slot wears, story stars,
    /// daily challenges and records. Plain data: the runtime saves it as JSON.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentVersion = 1;
        public const int LocalSlots = 4;

        public int version = CurrentVersion;
        public int rattles;
        public List<string> owned = new List<string>();
        public List<BabyLook> looks = new List<BabyLook>();
        public List<LevelRecord> levels = new List<LevelRecord>();
        public string challengeDay = "";
        public List<ChallengeState> challenges = new List<ChallengeState>();
        public int streak;
        public string lastStreakDay = "";
        public string lastWinBonusDay = "";
        public int bestSurvivalWave;
        public int bestSurvivalScore;
        public LifetimeStats lifetime = new LifetimeStats();
        public string nickname = "";

        /// <summary>Fills in anything missing (new install, or a save from an older version).</summary>
        public void Normalize()
        {
            owned ??= new List<string>();
            looks ??= new List<BabyLook>();
            levels ??= new List<LevelRecord>();
            challenges ??= new List<ChallengeState>();
            lifetime ??= new LifetimeStats();
            while (looks.Count < LocalSlots)
                looks.Add(new BabyLook());
            for (int index = 0; index < looks.Count; index++)
            {
                BabyLook look = looks[index] ?? new BabyLook();
                looks[index] = look;
                foreach (CosmeticSlot slot in new[] { CosmeticSlot.Hat, CosmeticSlot.Face, CosmeticSlot.Outfit })
                {
                    CosmeticItem? item = CosmeticCatalog.Find(look.Get(slot));
                    if (item == null || item.Slot != slot || !Owns(item.Id))
                        look.Set(slot, CosmeticCatalog.DefaultFor(slot));
                }
            }
            levels.RemoveAll(record => record == null);
            challenges.RemoveAll(state => state == null);
            rattles = Math.Max(0, rattles);
            version = CurrentVersion;
        }

        public bool Owns(string id)
        {
            CosmeticItem? item = CosmeticCatalog.Find(id);
            return item != null && (item.IsFree || owned.Contains(id));
        }

        public BabyLook LookFor(int slot)
        {
            Normalize();
            return looks[Math.Max(0, Math.Min(LocalSlots - 1, slot))];
        }

        public int StarsFor(string levelId)
        {
            foreach (LevelRecord record in levels)
            {
                if (record.id == levelId)
                    return record.stars;
            }
            return 0;
        }

        /// <summary>Keeps the best result. Returns how many new stars were earned.</summary>
        public int RecordStars(string levelId, int stars)
        {
            stars = Math.Max(0, Math.Min(3, stars));
            foreach (LevelRecord record in levels)
            {
                if (record.id != levelId)
                    continue;
                int gained = Math.Max(0, stars - record.stars);
                record.stars = Math.Max(record.stars, stars);
                return gained;
            }
            levels.Add(new LevelRecord { id = levelId, stars = stars });
            return stars;
        }

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (LevelRecord record in levels)
                    total += record.stars;
                return total;
            }
        }
    }

    public enum PurchaseResult
    {
        Bought,
        AlreadyOwned,
        NotEnoughRattles,
        UnknownItem,
    }

    public static class Shop
    {
        public static PurchaseResult TryBuy(PlayerProfile profile, string itemId)
        {
            CosmeticItem? item = CosmeticCatalog.Find(itemId);
            if (item == null)
                return PurchaseResult.UnknownItem;
            if (profile.Owns(itemId))
                return PurchaseResult.AlreadyOwned;
            if (profile.rattles < item.Price)
                return PurchaseResult.NotEnoughRattles;
            profile.rattles -= item.Price;
            profile.owned.Add(itemId);
            return PurchaseResult.Bought;
        }

        /// <summary>Puts an owned item on a local slot. False if it isn't owned.</summary>
        public static bool Equip(PlayerProfile profile, int slot, string itemId)
        {
            CosmeticItem? item = CosmeticCatalog.Find(itemId);
            if (item == null || !profile.Owns(itemId))
                return false;
            profile.LookFor(slot).Set(item.Slot, itemId);
            return true;
        }
    }

    /// <summary>What a local player did in one match (summed over this device's human players).</summary>
    public sealed class MatchStats
    {
        public bool won;
        public bool tinyTots;
        public int throws;
        public int hits;
        public int toyHits;
        public int diaperHits;
        public int shoves;
        public int dodges;
        public int knockouts;
        public int giggles;
        public int powerUps;
        public int survivalWave;
        public float bottleSeconds;
        public int stars;
        public bool cried;
        public string mode = "";
    }

    public static class Rewards
    {
        public const int FirstWinOfTheDayBonus = 25;

        /// <summary>Rattles for a finished match. Everyone earns some; winning and playing well earn more.</summary>
        public static int ForMatch(MatchStats stats)
        {
            if (stats.tinyTots)
                return 10 + Math.Min(20, stats.giggles * 2);
            int total = 10;
            if (stats.won)
                total += 15;
            total += Math.Min(15, stats.hits);
            total += Math.Min(10, stats.knockouts * 3);
            total += Math.Min(10, stats.survivalWave * 2);
            if (stats.won && !stats.cried)
                total += 5;
            return total;
        }

        /// <summary>
        /// Applies a match to the profile: rattles (with the first-win-of-the-day bonus) and lifetime stats.
        /// Returns the rattles earned.
        /// </summary>
        public static int Apply(PlayerProfile profile, MatchStats stats, DateTime today)
        {
            int earned = ForMatch(stats);
            string day = DayKey(today);
            if (stats.won && profile.lastWinBonusDay != day)
            {
                profile.lastWinBonusDay = day;
                earned += FirstWinOfTheDayBonus;
            }
            profile.rattles += earned;

            LifetimeStats life = profile.lifetime;
            life.matches++;
            if (stats.won)
                life.wins++;
            life.throws += stats.throws;
            life.hits += stats.hits;
            life.shoves += stats.shoves;
            life.knockouts += stats.knockouts;
            life.giggles += stats.giggles;
            life.powerUps += stats.powerUps;
            return earned;
        }

        public static string DayKey(DateTime date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}
