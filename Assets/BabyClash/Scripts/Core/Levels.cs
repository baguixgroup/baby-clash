#nullable enable
using System;
using System.Collections.Generic;

namespace BabyClash.Core
{
    public enum GameMode
    {
        /// <summary>Best-of-3 knockout duel (or free-for-all with more babies): last baby not crying wins the round.</summary>
        Clash,

        /// <summary>2–4 babies, free-for-all or teams, knockout rounds.</summary>
        Brawl,

        /// <summary>Stand by the giant bottle to score; criers come back after a moment.</summary>
        KingOfTheBottle,

        /// <summary>Endless waves of CPU babies; how long can you last?</summary>
        Survival,

        /// <summary>Toddler mode: hits make babies giggle, nobody loses, no text.</summary>
        TinyTots,
    }

    public enum RoomKind
    {
        Nursery,
        Kitchen,
        Bath,
        Backyard,
        Daycare,
    }

    public enum StarGoal
    {
        None,

        /// <summary>Win the level (in Survival: reach the wave in <see cref="LevelDef.WaveGoal"/>).</summary>
        Win,

        /// <summary>Win without the human baby ever crying.</summary>
        Flawless,

        /// <summary>Win within <see cref="LevelDef.TimeGoal"/> seconds.</summary>
        Quick,

        /// <summary>Land at least <see cref="LevelDef.HitGoal"/> hits.</summary>
        Hits,

        /// <summary>Win without losing a round.</summary>
        NoRoundLost,

        /// <summary>Survival: reach wave <see cref="LevelDef.WaveGoal"/> + 2.</summary>
        ExtraWaves,
    }

    /// <summary>One story level: where, what mode, who you face, and what earns each of its three stars.</summary>
    public sealed class LevelDef
    {
        public LevelDef(string id, string name, RoomKind room, GameMode mode, int opponents, int cpuLevel,
            StarGoal second, StarGoal third, bool allyCpu = false, bool boss = false)
        {
            Id = id;
            Name = name;
            Room = room;
            Mode = mode;
            Opponents = opponents;
            CpuLevel = cpuLevel;
            Second = second;
            Third = third;
            AllyCpu = allyCpu;
            Boss = boss;
        }

        public string Id { get; }
        public string Name { get; }
        public RoomKind Room { get; }
        public GameMode Mode { get; }

        /// <summary>CPU opponents (1–3).</summary>
        public int Opponents { get; }

        /// <summary>0 = easy, 1 = normal, 2 = hard.</summary>
        public int CpuLevel { get; }

        public StarGoal Second { get; }
        public StarGoal Third { get; }

        /// <summary>A CPU teammate joins the player (2v2).</summary>
        public bool AllyCpu { get; }

        /// <summary>The (first) opponent is a big boss baby.</summary>
        public bool Boss { get; }

        public int TimeGoal { get; set; } = 60;
        public int HitGoal { get; set; } = 10;
        public int WaveGoal { get; set; } = 3;
    }

    public sealed class LevelResult
    {
        public bool won;
        public bool cried;
        public int roundsLost;
        public float seconds;
        public int hits;
        public int wave;
    }

    /// <summary>The story tour: 20 levels over 5 rooms, unlocked in order by earning at least one star.</summary>
    public static class LevelCatalog
    {
        public const int StarsPerLevel = 3;

        static readonly LevelDef[] levels =
        {
            new LevelDef("nursery.1", "First Fling", RoomKind.Nursery, GameMode.Clash, 1, 0, StarGoal.Hits, StarGoal.Flawless) { HitGoal = 6 },
            new LevelDef("nursery.2", "Toy Box Tussle", RoomKind.Nursery, GameMode.Clash, 1, 0, StarGoal.NoRoundLost, StarGoal.Quick) { TimeGoal = 70 },
            new LevelDef("nursery.3", "Bottle Bash", RoomKind.Nursery, GameMode.KingOfTheBottle, 1, 0, StarGoal.Hits, StarGoal.Flawless) { HitGoal = 8 },
            new LevelDef("nursery.4", "Big Brother", RoomKind.Nursery, GameMode.Clash, 1, 1, StarGoal.NoRoundLost, StarGoal.Flawless, boss: true),

            new LevelDef("kitchen.1", "Food Fight", RoomKind.Kitchen, GameMode.Clash, 1, 1, StarGoal.Hits, StarGoal.Quick) { HitGoal = 10, TimeGoal = 75 },
            new LevelDef("kitchen.2", "High Chair Havoc", RoomKind.Kitchen, GameMode.Brawl, 2, 0, StarGoal.Hits, StarGoal.NoRoundLost) { HitGoal = 12 },
            new LevelDef("kitchen.3", "Sippy Showdown", RoomKind.Kitchen, GameMode.KingOfTheBottle, 2, 1, StarGoal.Hits, StarGoal.Flawless) { HitGoal = 10 },
            new LevelDef("kitchen.4", "Snack Attack", RoomKind.Kitchen, GameMode.Survival, 0, 0, StarGoal.Hits, StarGoal.ExtraWaves) { WaveGoal = 3, HitGoal = 15 },

            new LevelDef("bath.1", "Slippery Start", RoomKind.Bath, GameMode.Clash, 1, 1, StarGoal.NoRoundLost, StarGoal.Flawless),
            new LevelDef("bath.2", "Bubble Trouble", RoomKind.Bath, GameMode.Brawl, 3, 0, StarGoal.Hits, StarGoal.NoRoundLost) { HitGoal = 14 },
            new LevelDef("bath.3", "Rubber Duck Royale", RoomKind.Bath, GameMode.KingOfTheBottle, 3, 1, StarGoal.Hits, StarGoal.Flawless) { HitGoal = 12 },
            new LevelDef("bath.4", "Splash Zone", RoomKind.Bath, GameMode.Survival, 0, 1, StarGoal.Hits, StarGoal.ExtraWaves) { WaveGoal = 4, HitGoal = 20 },

            new LevelDef("backyard.1", "Sandbox Scuffle", RoomKind.Backyard, GameMode.Clash, 1, 2, StarGoal.Hits, StarGoal.NoRoundLost) { HitGoal = 12 },
            new LevelDef("backyard.2", "Buddy System", RoomKind.Backyard, GameMode.Brawl, 2, 1, StarGoal.Hits, StarGoal.Flawless, allyCpu: true) { HitGoal = 12 },
            new LevelDef("backyard.3", "Sprinkler Summit", RoomKind.Backyard, GameMode.KingOfTheBottle, 2, 2, StarGoal.Hits, StarGoal.Flawless) { HitGoal = 14 },
            new LevelDef("backyard.4", "Garden Gauntlet", RoomKind.Backyard, GameMode.Survival, 0, 1, StarGoal.Hits, StarGoal.ExtraWaves) { WaveGoal = 5, HitGoal = 25 },

            new LevelDef("daycare.1", "Nap Time Riot", RoomKind.Daycare, GameMode.Brawl, 3, 2, StarGoal.Hits, StarGoal.NoRoundLost) { HitGoal = 16 },
            new LevelDef("daycare.2", "Tag Team Tantrum", RoomKind.Daycare, GameMode.Brawl, 2, 2, StarGoal.NoRoundLost, StarGoal.Flawless, allyCpu: true),
            new LevelDef("daycare.3", "Endless Recess", RoomKind.Daycare, GameMode.Survival, 0, 2, StarGoal.Hits, StarGoal.ExtraWaves) { WaveGoal = 6, HitGoal = 30 },
            new LevelDef("daycare.4", "Boss Baby", RoomKind.Daycare, GameMode.Clash, 1, 2, StarGoal.NoRoundLost, StarGoal.Flawless, boss: true),
        };

        public static IReadOnlyList<LevelDef> All => levels;

        public static LevelDef? Find(string id)
        {
            foreach (LevelDef level in levels)
            {
                if (level.Id == id)
                    return level;
            }
            return null;
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i].Id == id)
                    return i;
            }
            return -1;
        }

        /// <summary>The first level is always open; each next one opens once the previous has a star.</summary>
        public static bool IsUnlocked(PlayerProfile profile, int index)
        {
            if (index <= 0)
                return index == 0;
            if (index >= levels.Length)
                return false;
            return profile.StarsFor(levels[index - 1].Id) > 0;
        }

        /// <summary>Stars earned (0–3). The first star is always "win" (or reach the wave in Survival).</summary>
        public static int Evaluate(LevelDef level, LevelResult result)
        {
            if (!Meets(level, StarGoal.Win, result))
                return 0;
            int stars = 1;
            if (Meets(level, level.Second, result))
                stars++;
            if (Meets(level, level.Third, result))
                stars++;
            return stars;
        }

        public static bool Meets(LevelDef level, StarGoal goal, LevelResult result)
        {
            bool won = level.Mode == GameMode.Survival ? result.wave >= level.WaveGoal : result.won;
            switch (goal)
            {
                case StarGoal.None: return false;
                case StarGoal.Win: return won;
                case StarGoal.Flawless: return won && !result.cried && level.Mode != GameMode.Survival;
                case StarGoal.Quick: return won && result.seconds <= level.TimeGoal;
                case StarGoal.Hits: return won && result.hits >= level.HitGoal;
                case StarGoal.NoRoundLost: return won && result.roundsLost == 0;
                case StarGoal.ExtraWaves: return result.wave >= level.WaveGoal + 2;
                default: return false;
            }
        }

        public static string Describe(LevelDef level, StarGoal goal)
        {
            switch (goal)
            {
                case StarGoal.Win:
                    return level.Mode == GameMode.Survival ? $"Reach wave {level.WaveGoal}" : "Win";
                case StarGoal.Flawless: return "Win without crying";
                case StarGoal.Quick: return $"Win in under {level.TimeGoal} s";
                case StarGoal.Hits: return $"Land {level.HitGoal} hits";
                case StarGoal.NoRoundLost: return "Don't lose a round";
                case StarGoal.ExtraWaves: return $"Reach wave {level.WaveGoal + 2}";
                default: return "";
            }
        }
    }
}
