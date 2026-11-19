#nullable enable
using System;
using System.Collections.Generic;

namespace BabyClash.Core
{
    public enum ChallengeStat
    {
        Matches,
        Wins,
        Hits,
        ToyHits,
        DiaperHits,
        Shoves,
        Dodges,
        Knockouts,
        PowerUps,
        Giggles,
        SurvivalWave,
        BottleSeconds,
        FlawlessWins,
    }

    public sealed class ChallengeDef
    {
        public ChallengeDef(string id, string text, ChallengeStat stat, int goal, int reward, bool inOneMatch = false)
        {
            Id = id;
            Text = text;
            Stat = stat;
            Goal = goal;
            Reward = reward;
            InOneMatch = inOneMatch;
        }

        public string Id { get; }
        public string Text { get; }
        public ChallengeStat Stat { get; }
        public int Goal { get; }
        public int Reward { get; }

        /// <summary>Must be reached within a single match (otherwise progress adds up over the day).</summary>
        public bool InOneMatch { get; }
    }

    /// <summary>
    /// Three daily challenges, the same for everyone on a given date, with a streak for finishing all three on
    /// consecutive days. Pure logic over <see cref="PlayerProfile"/>.
    /// </summary>
    public static class ChallengeBook
    {
        public const int DailyCount = 3;
        public const int StreakBonusPerDay = 10;
        public const int MaxStreakBonus = 70;

        static readonly ChallengeDef[] pool =
        {
            new ChallengeDef("play3", "Play 3 matches", ChallengeStat.Matches, 3, 20),
            new ChallengeDef("win2", "Win 2 matches", ChallengeStat.Wins, 2, 30),
            new ChallengeDef("hits25", "Land 25 hits", ChallengeStat.Hits, 25, 25),
            new ChallengeDef("toys10", "Hit 10 times with toys", ChallengeStat.ToyHits, 10, 25),
            new ChallengeDef("diapers8", "Hit 8 times with a diaper", ChallengeStat.DiaperHits, 8, 25),
            new ChallengeDef("shove5", "Shove 5 babies with a dash", ChallengeStat.Shoves, 5, 25),
            new ChallengeDef("dodge5", "Dodge 5 throws with a dash", ChallengeStat.Dodges, 5, 30),
            new ChallengeDef("ko6", "Make 6 babies cry", ChallengeStat.Knockouts, 6, 30),
            new ChallengeDef("power6", "Grab 6 power-ups", ChallengeStat.PowerUps, 6, 25),
            new ChallengeDef("giggle10", "Make babies giggle 10 times in Tiny Tots", ChallengeStat.Giggles, 10, 20),
            new ChallengeDef("wave5", "Reach wave 5 in Survival", ChallengeStat.SurvivalWave, 5, 35, true),
            new ChallengeDef("bottle40", "Hold the bottle for 40 seconds", ChallengeStat.BottleSeconds, 40, 30),
            new ChallengeDef("flawless", "Win a match without crying", ChallengeStat.FlawlessWins, 1, 35, true),
            new ChallengeDef("shove3match", "Shove 3 times in one match", ChallengeStat.Shoves, 3, 25, true),
            new ChallengeDef("hits12match", "Land 12 hits in one match", ChallengeStat.Hits, 12, 30, true),
        };

        public static IReadOnlyList<ChallengeDef> Pool => pool;

        public static ChallengeDef? Find(string id)
        {
            foreach (ChallengeDef def in pool)
            {
                if (def.Id == id)
                    return def;
            }
            return null;
        }

        /// <summary>The day's challenges: deterministic per date, no repeats within a day.</summary>
        public static ChallengeDef[] ForDate(DateTime date, Predicate<ChallengeDef>? available = null)
        {
            // Stable across platforms and runtimes (string.GetHashCode is randomized in .NET Core).
            uint seed = (uint)(date.Year * 10000 + date.Month * 100 + date.Day);
            var indices = new List<int>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
                if (available == null || available(pool[i]))
                    indices.Add(i);
            if (indices.Count < DailyCount)
                throw new ArgumentException("Need at least three available challenges.", nameof(available));
            var picked = new ChallengeDef[DailyCount];
            for (int i = 0; i < DailyCount; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                int at = (int)((seed >> 8) % (uint)indices.Count);
                picked[i] = pool[indices[at]];
                indices.RemoveAt(at);
            }
            return picked;
        }

        /// <summary>Starts a new day's challenges if the date changed. Returns true if they were replaced.</summary>
        public static bool Refresh(PlayerProfile profile, DateTime today, Predicate<ChallengeDef>? available = null)
        {
            string day = Rewards.DayKey(today);
            bool valid = profile.challenges.Count == DailyCount;
            foreach (ChallengeState state in profile.challenges)
            {
                ChallengeDef? definition = Find(state.id);
                if (definition == null || (available != null && !available(definition)))
                    valid = false;
            }
            if (profile.challengeDay == day && valid)
                return false;
            profile.challengeDay = day;
            profile.challenges.Clear();
            foreach (ChallengeDef def in ForDate(today, available))
                profile.challenges.Add(new ChallengeState { id = def.Id });
            return true;
        }

        /// <summary>
        /// Adds a match's stats to today's challenges and pays out the ones just completed (plus the streak bonus
        /// when all three are done). Returns the completed challenges.
        /// </summary>
        public static List<ChallengeDef> Record(PlayerProfile profile, MatchStats stats, DateTime today, out int rattlesEarned)
        {
            Refresh(profile, today);
            rattlesEarned = 0;
            var completed = new List<ChallengeDef>();
            bool allDoneBefore = AllDone(profile);
            foreach (ChallengeState state in profile.challenges)
            {
                ChallengeDef? def = Find(state.id);
                if (def == null || state.done)
                    continue;
                int value = Value(def.Stat, stats);
                state.progress = def.InOneMatch ? Math.Max(state.progress, value) : state.progress + value;
                if (state.progress >= def.Goal)
                {
                    state.progress = def.Goal;
                    state.done = true;
                    rattlesEarned += def.Reward;
                    completed.Add(def);
                }
            }

            if (!allDoneBefore && AllDone(profile))
            {
                string day = Rewards.DayKey(today);
                string yesterday = Rewards.DayKey(today.AddDays(-1));
                profile.streak = profile.lastStreakDay == yesterday ? profile.streak + 1 : 1;
                profile.lastStreakDay = day;
                rattlesEarned += StreakBonus(profile.streak);
            }
            profile.rattles += rattlesEarned;
            return completed;
        }

        /// <summary>The streak counts as broken if yesterday's set wasn't finished (shown as 0 until today's is).</summary>
        public static int CurrentStreak(PlayerProfile profile, DateTime today)
        {
            string day = Rewards.DayKey(today);
            string yesterday = Rewards.DayKey(today.AddDays(-1));
            return profile.lastStreakDay == day || profile.lastStreakDay == yesterday ? profile.streak : 0;
        }

        public static int StreakBonus(int streak) => Math.Min(MaxStreakBonus, Math.Max(0, streak) * StreakBonusPerDay);

        public static bool AllDone(PlayerProfile profile)
        {
            if (profile.challenges.Count == 0)
                return false;
            foreach (ChallengeState state in profile.challenges)
            {
                if (!state.done)
                    return false;
            }
            return true;
        }

        static int Value(ChallengeStat stat, MatchStats stats)
        {
            switch (stat)
            {
                case ChallengeStat.Matches: return 1;
                case ChallengeStat.Wins: return stats.won ? 1 : 0;
                case ChallengeStat.Hits: return stats.hits;
                case ChallengeStat.ToyHits: return stats.toyHits;
                case ChallengeStat.DiaperHits: return stats.diaperHits;
                case ChallengeStat.Shoves: return stats.shoves;
                case ChallengeStat.Dodges: return stats.dodges;
                case ChallengeStat.Knockouts: return stats.knockouts;
                case ChallengeStat.PowerUps: return stats.powerUps;
                case ChallengeStat.Giggles: return stats.giggles;
                case ChallengeStat.SurvivalWave: return stats.survivalWave;
                case ChallengeStat.BottleSeconds: return (int)stats.bottleSeconds;
                case ChallengeStat.FlawlessWins: return stats.won && !stats.cried && !stats.tinyTots ? 1 : 0;
                default: return 0;
            }
        }
    }
}
