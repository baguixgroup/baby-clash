using System.Collections.Generic;
using BabyClash.Core;

namespace BabyClash
{
    /// <summary>Who controls a player slot.</summary>
    public enum SlotDriver
    {
        Off,
        Human,
        CpuEasy,
        CpuNormal,
        CpuHard,

        /// <summary>A person playing on another device (online). Never cycled to in local menus.</summary>
        Online,
    }

    /// <summary>Everything that defines one match: mode, room, players, teams and rules.</summary>
    public sealed class MatchSetup
    {
        public const int MaxPlayers = 4;

        public GameMode Mode = GameMode.Clash;
        public RoomKind Room = RoomKind.Nursery;
        public bool PowerUps = true;
        public bool Teams;
        public SlotDriver[] Slots = { SlotDriver.Human, SlotDriver.CpuNormal, SlotDriver.Off, SlotDriver.Off };

        /// <summary>Story level being played, or null.</summary>
        public LevelDef Level;

        /// <summary>Slot of the boss baby, or -1.</summary>
        public int BossSlot = -1;

        /// <summary>Attract mode: the match never ends.</summary>
        public bool Endless;

        public bool Eliminates => Mode == GameMode.Clash || Mode == GameMode.Brawl;
        public bool UsesScore => !Eliminates;
        public bool IsToddler => Mode == GameMode.TinyTots;

        /// <summary>Score that ends the match. 0 = none.</summary>
        public float ScoreTarget => Mode == GameMode.KingOfTheBottle ? 20f : 0f;

        /// <summary>Round clock (s); -1 = the balance default, 0 = none.</summary>
        public float TimeLimit => Mode == GameMode.TinyTots ? 60f : Mode == GameMode.KingOfTheBottle ? 90f : Mode == GameMode.Survival ? 0f : -1f;

        /// <summary>Round wins for the match; 0 = the balance default.</summary>
        public int RoundsToWin => Eliminates ? 0 : 1;

        public bool IsPlaying(int slot) => slot >= 0 && slot < Slots.Length && Slots[slot] != SlotDriver.Off;
        public bool IsHuman(int slot) => slot >= 0 && slot < Slots.Length && Slots[slot] == SlotDriver.Human;

        /// <summary>A baby played by someone on another device.</summary>
        public bool IsOnline(int slot) => slot >= 0 && slot < Slots.Length && Slots[slot] == SlotDriver.Online;

        /// <summary>A real person, here or online.</summary>
        public bool IsPerson(int slot) => IsHuman(slot) || IsOnline(slot);

        public bool IsCpu(int slot) => IsPlaying(slot) && !IsPerson(slot);

        public int Count(bool humansOnly)
        {
            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (humansOnly ? IsHuman(i) : IsPlaying(i))
                    count++;
            }
            return count;
        }

        /// <summary>Side per participating slot (in slot order), contiguous from 0.</summary>
        public int[] BuildSides()
        {
            var sides = new List<int>();
            int next = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!IsPlaying(i))
                    continue;
                if (Mode == GameMode.Survival)
                    sides.Add(IsPerson(i) ? 0 : 1);
                else
                    sides.Add(Teams ? next % 2 : next);
                next++;
            }
            // Survival with only humans still needs an (empty) enemy side.
            return sides.ToArray();
        }

        public MatchSetup Copy()
        {
            var copy = (MatchSetup)MemberwiseClone();
            copy.Slots = (SlotDriver[])Slots.Clone();
            return copy;
        }

        public static CpuDifficulty DifficultyOf(SlotDriver driver) =>
            driver == SlotDriver.CpuEasy ? CpuDifficulty.Easy : driver == SlotDriver.CpuHard ? CpuDifficulty.Hard : CpuDifficulty.Normal;

        public static SlotDriver CpuOf(int level) =>
            level <= 0 ? SlotDriver.CpuEasy : level == 1 ? SlotDriver.CpuNormal : SlotDriver.CpuHard;

        public static MatchSetup Duel(bool solo, CpuDifficulty difficulty) => new MatchSetup
        {
            PowerUps = false,
            Slots = new[] { SlotDriver.Human, solo ? CpuOf((int)difficulty) : SlotDriver.Human, SlotDriver.Off, SlotDriver.Off },
        };

        public static MatchSetup Tots(int humans) => new MatchSetup
        {
            Mode = GameMode.TinyTots,
            Slots = new[] { SlotDriver.Human, humans > 1 ? SlotDriver.Human : SlotDriver.CpuEasy, SlotDriver.Off, SlotDriver.Off },
        };

        public static MatchSetup Demo(CpuDifficulty difficulty) => new MatchSetup
        {
            Endless = true,
            Slots = new[] { CpuOf((int)difficulty), CpuOf((int)difficulty), SlotDriver.Off, SlotDriver.Off },
        };

        public static MatchSetup ForLevel(LevelDef level)
        {
            var setup = new MatchSetup { Mode = level.Mode, Room = level.Room, Level = level, Teams = level.AllyCpu };
            SlotDriver cpu = CpuOf(level.CpuLevel);
            if (level.Mode == GameMode.Survival)
            {
                setup.Slots = new[] { SlotDriver.Human, SlotDriver.CpuEasy, SlotDriver.CpuEasy, SlotDriver.CpuEasy };
            }
            else if (level.AllyCpu)
            {
                // Sides alternate by slot: player and ally are 0 and 2.
                setup.Slots = new[] { SlotDriver.Human, cpu, SlotDriver.CpuNormal, cpu };
            }
            else
            {
                for (int i = 1; i < MaxPlayers; i++)
                    setup.Slots[i] = i <= level.Opponents ? cpu : SlotDriver.Off;
            }
            if (level.Boss)
                setup.BossSlot = 1;
            return setup;
        }
    }
}
