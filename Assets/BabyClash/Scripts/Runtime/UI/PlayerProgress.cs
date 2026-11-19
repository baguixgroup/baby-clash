using System;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    public static class PlayerProgress
    {
        const string SaveKey = "BabyClash.Profile";
        static PlayerProfile profile;

        public static PlayerProfile Profile
        {
            get
            {
                if (profile != null)
                    return profile;
                string json = PlayerPrefs.GetString(SaveKey, "");
                try
                {
                    profile = string.IsNullOrEmpty(json) ? new PlayerProfile() : JsonUtility.FromJson<PlayerProfile>(json);
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning("Baby Clash profile could not be read: " + exception.Message);
                }
                profile = profile ?? new PlayerProfile();
                profile.Normalize();
                RefreshChallenges(profile);
                return profile;
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Profile));
            PlayerPrefs.Save();
        }

        public static string CompleteMatch(MatchStats stats)
        {
            var current = Profile;
            RefreshChallenges(current);
            int earned = Rewards.Apply(current, stats, DateTime.Today);
            var completed = ChallengeBook.Record(current, stats, DateTime.Today, out int bonus);
            Save();
            string message = $"+{earned + bonus} rattles · {current.rattles} total";
            foreach (var challenge in completed)
                message += "\nCompleted: " + challenge.Text;
            return message;
        }

        public static string ChallengeSummary()
        {
            var current = Profile;
            RefreshChallenges(current);
            string summary = $"RATTLES  {current.rattles}\nTODAY'S CHALLENGES";
            foreach (var state in current.challenges)
            {
                var definition = ChallengeBook.Find(state.id);
                if (definition != null)
                    summary += $"\n{definition.Text}  {state.progress}/{definition.Goal}";
            }
            return summary;
        }

        static void RefreshChallenges(PlayerProfile current)
        {
            ChallengeBook.Refresh(current, DateTime.Today, definition =>
                definition.Stat != ChallengeStat.Dodges &&
                definition.Stat != ChallengeStat.PowerUps &&
                definition.Stat != ChallengeStat.Giggles &&
                definition.Stat != ChallengeStat.SurvivalWave &&
                definition.Stat != ChallengeStat.BottleSeconds);
        }
    }
}
