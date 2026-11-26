using System;
using BabyClash.Core;
using Unity.Netcode;
using UnityEngine;

namespace BabyClash
{
    /// <summary>Message names and shared formats for online play. Bump <see cref="Version"/> on any format change.</summary>
    public static class NetWire
    {
        public const byte Version = 1;

        /// <summary>Guest to host, once connected: protocol version and outfit.</summary>
        public const string Hello = "bc.hello";

        /// <summary>Host to guests: the room's players and settings.</summary>
        public const string Lobby = "bc.lobby";

        /// <summary>Host to guests: a match is starting.</summary>
        public const string Start = "bc.start";

        /// <summary>Host to guests, many times a second (unreliable): where everything is.</summary>
        public const string Snapshot = "bc.snap";

        /// <summary>Host to guests: things that happened (hits, throws...), with the host time they happened at.</summary>
        public const string Events = "bc.events";

        /// <summary>Guest to host, many times a second (unreliable): stick and button presses.</summary>
        public const string Input = "bc.input";

        /// <summary>Guest to host (a request), host to guests (to show it).</summary>
        public const string Emote = "bc.emote";

        /// <summary>Host to guests: back to the room screen.</summary>
        public const string Menu = "bc.menu";

        /// <summary>Kid-safe messages instead of chat. Nothing typed is ever sent.</summary>
        public static readonly string[] Emotes = { "HI!", "HEE HEE!", "WAAAH!", "GOOD GAME!" };

        public static readonly Color[] EmoteColors =
        {
            new Color(0.45f, 0.7f, 1f), new Color(1f, 0.78f, 0.3f), new Color(0.6f, 0.75f, 1f), new Color(0.45f, 0.85f, 0.62f),
        };

        public static void WriteLook(ref FastBufferWriter writer, BabyLook look)
        {
            look ??= new BabyLook();
            writer.WriteValueSafe(look.hat ?? "", true);
            writer.WriteValueSafe(look.face ?? "", true);
            writer.WriteValueSafe(look.outfit ?? "", true);
        }

        /// <summary>Reads an outfit, keeping only items that really exist (anything else falls back to the default).</summary>
        public static BabyLook ReadLook(ref FastBufferReader reader)
        {
            var look = new BabyLook();
            foreach (CosmeticSlot slot in new[] { CosmeticSlot.Hat, CosmeticSlot.Face, CosmeticSlot.Outfit })
            {
                reader.ReadValueSafe(out string id, true);
                CosmeticItem item = id != null && id.Length <= 40 ? CosmeticCatalog.Find(id) : null;
                if (item != null && item.Slot == slot)
                    look.Set(slot, id);
            }
            return look;
        }

        public static ushort PackYaw(Quaternion rotation) => (ushort)Mathf.RoundToInt(Mathf.Repeat(rotation.eulerAngles.y, 360f) / 360f * 65535f);

        public static Quaternion UnpackYaw(ushort yaw) => Quaternion.Euler(0f, yaw / 65535f * 360f, 0f);

        public static short PackSpeed(float metersPerSecond) => (short)Mathf.Clamp(Mathf.RoundToInt(metersPerSecond * 100f), short.MinValue, short.MaxValue);

        public static float UnpackSpeed(short value) => value / 100f;
    }

    /// <summary>The online room: who sits in which baby slot, and the match settings the host picked.</summary>
    public sealed class LobbyState
    {
        public GameMode Mode = GameMode.Clash;
        public RoomKind Room = RoomKind.Nursery;
        public bool PowerUps = true;
        public bool Teams;

        /// <summary>Host's view: Human = the host, Online = a guest, CPU levels, or Off.</summary>
        public readonly SlotDriver[] Slots = { SlotDriver.Human, SlotDriver.CpuNormal, SlotDriver.Off, SlotDriver.Off };

        /// <summary>Network id of the guest in each Online slot.</summary>
        public readonly ulong[] Owners = new ulong[MatchSetup.MaxPlayers];

        public bool InMatch;
        public string Code = "";

        /// <summary>Seconds until a public room starts by itself, or -1.</summary>
        public float AutoStart = -1f;

        public int People
        {
            get
            {
                int count = 0;
                foreach (SlotDriver driver in Slots)
                {
                    if (driver == SlotDriver.Human || driver == SlotDriver.Online)
                        count++;
                }
                return count;
            }
        }

        public int SlotOf(ulong clientId)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == SlotDriver.Online && Owners[i] == clientId)
                    return i;
            }
            return -1;
        }

        public MatchSetup ToSetup()
        {
            var setup = new MatchSetup { Mode = Mode, Room = Room, PowerUps = PowerUps, Teams = Teams && CanTeam };
            setup.Slots = (SlotDriver[])Slots.Clone();
            if (Mode == GameMode.Survival)
            {
                // The horde fills every free slot.
                for (int i = 0; i < setup.Slots.Length; i++)
                {
                    if (!setup.IsPerson(i))
                        setup.Slots[i] = SlotDriver.CpuEasy;
                }
            }
            return setup;
        }

        public bool CanTeam
        {
            get
            {
                if (Mode == GameMode.Survival || Mode == GameMode.TinyTots)
                    return false;
                foreach (SlotDriver driver in Slots)
                {
                    if (driver == SlotDriver.Off)
                        return false;
                }
                return true;
            }
        }

        public void Write(ref FastBufferWriter writer)
        {
            writer.WriteValueSafe((byte)Mode);
            writer.WriteValueSafe((byte)Room);
            writer.WriteValueSafe(PowerUps);
            writer.WriteValueSafe(Teams);
            for (int i = 0; i < Slots.Length; i++)
            {
                writer.WriteValueSafe((byte)Slots[i]);
                writer.WriteValueSafe(Owners[i]);
            }
            writer.WriteValueSafe(InMatch);
            writer.WriteValueSafe(Code ?? "", true);
            writer.WriteValueSafe(AutoStart);
        }

        public void Read(ref FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte mode);
            reader.ReadValueSafe(out byte room);
            Mode = Enum.IsDefined(typeof(GameMode), (int)mode) ? (GameMode)mode : GameMode.Clash;
            Room = Enum.IsDefined(typeof(RoomKind), (int)room) ? (RoomKind)room : RoomKind.Nursery;
            reader.ReadValueSafe(out PowerUps);
            reader.ReadValueSafe(out Teams);
            for (int i = 0; i < Slots.Length; i++)
            {
                reader.ReadValueSafe(out byte driver);
                Slots[i] = Enum.IsDefined(typeof(SlotDriver), (int)driver) ? (SlotDriver)driver : SlotDriver.Off;
                reader.ReadValueSafe(out Owners[i]);
            }
            reader.ReadValueSafe(out InMatch);
            reader.ReadValueSafe(out string code, true);
            Code = code ?? "";
            reader.ReadValueSafe(out AutoStart);
        }

        public void CopyFrom(LobbyState other)
        {
            Mode = other.Mode;
            Room = other.Room;
            PowerUps = other.PowerUps;
            Teams = other.Teams;
            Array.Copy(other.Slots, Slots, Slots.Length);
            Array.Copy(other.Owners, Owners, Owners.Length);
            InMatch = other.InMatch;
            Code = other.Code;
            AutoStart = other.AutoStart;
        }
    }
}
