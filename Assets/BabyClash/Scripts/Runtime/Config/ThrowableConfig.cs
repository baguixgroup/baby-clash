using UnityEngine;

namespace BabyClash
{
    public enum SpinStyle
    {
        /// <summary>Flat frisbee spin around the vertical axis (diapers).</summary>
        Frisbee,

        /// <summary>End-over-end tumble (blocks, bottles, ducks).</summary>
        Tumble,
    }

    /// <summary>Projectile and impact balance shared by everything a baby can throw. Plain data.</summary>
    public abstract class ThrowableConfig : ScriptableObject
    {
        [Header("Throw")]
        [Tooltip("Projectile speed: horizontal launch speed (m/s), multiplied by the thrower's Throw Strength.")]
        [Min(0.1f)] public float throwSpeed = 15f;

        [Tooltip("Arc: upward launch speed (m/s). Higher = loftier, slower-landing throws.")]
        [Min(0f)] public float throwArc = 3.5f;

        [Tooltip("Gravity multiplier while flying. Above 1 gives snappier arcs.")]
        [Min(0f)] public float gravityMultiplier = 1.6f;

        [Tooltip("Spin while flying (degrees/s). Purely cosmetic.")]
        [Min(0f)] public float spinSpeed = 900f;

        public SpinStyle spinStyle = SpinStyle.Frisbee;

        [Header("Impact")]
        [Tooltip("Base Cry for a direct hit (hit before touching anything). Multiplied by the victim's defense.")]
        [Min(0f)] public float directHitCry = 16f;

        [Tooltip("Base Cry for a normal impact (hit after bouncing). Multiplied by the victim's defense.")]
        [Min(0f)] public float bounceHitCry = 10f;

        [Tooltip("Base knockback speed of a direct hit (m/s).")]
        [Min(0f)] public float directHitKnockback = 8f;

        [Tooltip("Base knockback speed of a normal impact (m/s).")]
        [Min(0f)] public float bounceHitKnockback = 5f;

        [Tooltip("Slow throws still push with at least this fraction of full knockback.")]
        [Range(0f, 1f)] public float minImpactStrength = 0.4f;

        [Header("After impact")]
        [Tooltip("Fraction of horizontal speed kept (reversed) after hitting a baby.")]
        [Range(0f, 1f)] public float reboundHorizontalFactor = 0.2f;

        [Tooltip("Upward pop after hitting a baby (m/s), so it drops near the victim.")]
        [Min(0f)] public float reboundUpSpeed = 3f;

        [Header("Landing")]
        [Tooltip("Fraction of horizontal speed kept each time a flying item hits the floor. Low = soft thud, " +
                 "high = long skids into the walls.")]
        [Range(0f, 1f)] public float landingSpeedRetention = 0.35f;

        [Tooltip("Once it has bounced, an item slower than this (m/s) has landed and can be picked up.")]
        [Min(0.01f)] public float settleSpeed = 1.2f;

        [Tooltip("Safety net: an item counts as landed after flying this long (s).")]
        [Min(0.1f)] public float maxFlightTime = 3f;
    }
}
