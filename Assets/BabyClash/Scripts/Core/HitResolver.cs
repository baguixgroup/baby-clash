#nullable enable
using System;

namespace BabyClash.Core
{
    /// <summary>What an attack brings, before the defender's diaper state is taken into account.</summary>
    public readonly struct HitRequest
    {
        public HitRequest(float baseCry, float baseKnockback, float strength)
        {
            BaseCry = baseCry;
            BaseKnockback = baseKnockback;
            Strength = strength;
        }

        public float BaseCry { get; }

        /// <summary>Knockback speed in m/s for a full-strength hit on an unmodified defender.</summary>
        public float BaseKnockback { get; }

        /// <summary>1 = a normal full-speed throw. Slower impacts are weaker, stronger throwers exceed 1.</summary>
        public float Strength { get; }
    }

    /// <summary>How a baby takes hits in its current state (wearing a diaper or not).</summary>
    public readonly struct DefenseProfile
    {
        public DefenseProfile(float cryMultiplier, float knockbackMultiplier, float stunDuration)
        {
            CryMultiplier = cryMultiplier;
            KnockbackMultiplier = knockbackMultiplier;
            StunDuration = stunDuration;
        }

        public float CryMultiplier { get; }
        public float KnockbackMultiplier { get; }
        public float StunDuration { get; }
    }

    public readonly struct HitOutcome
    {
        public HitOutcome(float cry, float knockbackSpeed, float stunDuration)
        {
            Cry = cry;
            KnockbackSpeed = knockbackSpeed;
            StunDuration = stunDuration;
        }

        public float Cry { get; }
        public float KnockbackSpeed { get; }
        public float StunDuration { get; }
    }

    public static class HitResolver
    {
        /// <summary>Upper clamp for <see cref="HitRequest.Strength"/> so freak physics can't launch babies into orbit.</summary>
        public const float MaxStrength = 2f;

        /// <param name="knockbackResistance">0 = full knockback, 1 = immovable.</param>
        public static HitOutcome Resolve(in HitRequest hit, in DefenseProfile defense, float knockbackResistance)
        {
            float strength = Clamp(hit.Strength, 0f, MaxStrength);
            float resistance = Clamp(knockbackResistance, 0f, 1f);

            float cry = Math.Max(0f, hit.BaseCry) * Math.Max(0f, defense.CryMultiplier);
            float knockback = Math.Max(0f, hit.BaseKnockback) * strength
                              * Math.Max(0f, defense.KnockbackMultiplier) * (1f - resistance);
            return new HitOutcome(cry, knockback, Math.Max(0f, defense.StunDuration));
        }

        static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
