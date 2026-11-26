using UnityEngine;

namespace BabyClash
{
    /// <summary>What this device is in an online game.</summary>
    public enum NetMode
    {
        Offline,

        /// <summary>Runs the real match and sends it to everyone else.</summary>
        Host,

        /// <summary>Shows the host's match and sends its own player's controls.</summary>
        Guest,
    }

    public static class NetRole
    {
        public static NetMode Mode { get; internal set; } = NetMode.Offline;
        public static bool IsOnline => Mode != NetMode.Offline;
        public static bool IsHost => Mode == NetMode.Host;
        public static bool IsGuest => Mode == NetMode.Guest;
    }

    /// <summary>Game time that is the same on every screen in an online match (the host's).</summary>
    public static class NetClock
    {
        static float hostGameTime;
        static float receivedAt;
        static bool synced;

        public static float GameTime => NetRole.IsGuest && synced ? hostGameTime + (Time.unscaledTime - receivedAt) : Time.time;

        internal static void Sync(float gameTime)
        {
            hostGameTime = gameTime;
            receivedAt = Time.unscaledTime;
            synced = true;
        }

        internal static void Reset() => synced = false;
    }
}
