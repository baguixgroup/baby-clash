using UnityEngine;

namespace BabyClash
{
    public enum HitSource
    {
        Thrown,
        Shove,
    }

    /// <summary>What happened when a baby got hit. Payload for feedback (camera shake, particles, sound, animation).</summary>
    public readonly struct BabyHitReport
    {
        public BabyHitReport(Vector3 point, Vector3 direction, float cryAdded, float knockbackSpeed,
            bool wasProtected, bool wasDirectHit, HitSource source)
        {
            Point = point;
            Direction = direction;
            CryAdded = cryAdded;
            KnockbackSpeed = knockbackSpeed;
            WasProtected = wasProtected;
            WasDirectHit = wasDirectHit;
            Source = source;
        }

        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public float CryAdded { get; }
        public float KnockbackSpeed { get; }
        public bool WasProtected { get; }
        public bool WasDirectHit { get; }
        public HitSource Source { get; }
    }
}
