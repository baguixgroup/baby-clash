#nullable enable
using System;

namespace BabyClash.Core
{
    /// <summary>
    /// A baby's "health": starts empty and fills up when the baby gets hit.
    /// A full meter means the baby bursts into tears and loses the round.
    /// </summary>
    public sealed class CryMeter
    {
        // Absorbs float drift (relative to Max) so fractional hits that add up to Max really count as full,
        // e.g. 1000 hits of 0.1 end ~0.001 short in float math. Far below anything the HUD can show.
        const float RelativeFullTolerance = 0.0001f;

        public CryMeter(float max)
        {
            if (!(max > 0f))
                throw new ArgumentOutOfRangeException(nameof(max), max, "Cry meter max must be positive.");
            Max = max;
        }

        public float Max { get; }
        public float Value { get; private set; }
        public float Normalized => Value / Max;
        public bool IsFull => Value >= Max;

        /// <summary>Raised with the new value whenever the meter changes, including resets.</summary>
        public event Action<float>? Changed;

        /// <summary>Raised once, when the meter reaches its maximum.</summary>
        public event Action? Filled;

        /// <summary>
        /// Adds cry and returns how much was actually applied (the meter clamps at <see cref="Max"/>).
        /// Non-positive or NaN amounts are ignored, and a full meter ignores everything.
        /// </summary>
        public float Add(float amount)
        {
            if (IsFull || !(amount > 0f))
                return 0f;

            float previous = Value;
            Value = Math.Min(Max, Value + amount);
            if (Max - Value <= Max * RelativeFullTolerance)
                Value = Max;

            Changed?.Invoke(Value);
            if (IsFull)
                Filled?.Invoke();
            return Value - previous;
        }

        public void Reset()
        {
            Value = 0f;
            Changed?.Invoke(Value);
        }
    }
}
