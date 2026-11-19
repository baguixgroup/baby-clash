using System;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>Balance data for a baby: movement, actions and how it takes hits. Plain data, tweak freely in the Inspector.</summary>
    [CreateAssetMenu(fileName = "BabyConfig", menuName = "Baby Clash/Baby Config")]
    public sealed class BabyConfig : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Top running speed (m/s).")]
        [Min(0f)] public float moveSpeed = 5.5f;

        [Tooltip("How fast the baby reaches top speed (m/s²).")]
        [Min(0f)] public float acceleration = 45f;

        [Tooltip("How fast the baby stops when the stick is released (m/s²).")]
        [Min(0f)] public float deceleration = 35f;

        [Tooltip("Turning speed (degrees/s).")]
        [Min(0f)] public float turnSpeed = 900f;

        [Tooltip("Speed multiplier while in the short Throwing state.")]
        [Range(0f, 1f)] public float throwingMoveMultiplier = 0.4f;

        [Header("Actions")]
        [Tooltip("Throw force multiplier. Scales the diaper's launch speed and therefore its knockback.")]
        [Min(0.1f)] public float throwStrength = 1f;

        [Tooltip("How long the baby stays in the Throwing state after releasing a diaper (s).")]
        [Min(0f)] public float throwDuration = 0.2f;

        [Tooltip("Cooldown after a throw or pickup before the next action (s).")]
        [Min(0f)] public float actionCooldown = 0.3f;

        [Tooltip("How close a loose diaper or toy must be to be picked up (m, measured from the hips).")]
        [Min(0.1f)] public float pickupRadius = 1.3f;

        [Tooltip("Diapers a baby can wear at once. 1 = a baby wearing a diaper cannot pick up another. " +
                 "2 = experiment: a second diaper is carried as spare ammo and thrown first.")]
        [Range(1, 2)] public int maxDiapers = 1;

        [Tooltip("Throws snap toward an opponent within this angle of the facing direction (degrees). 0 = off.")]
        [Range(0f, 90f)] public float aimAssistAngle = 25f;

        [Tooltip("Aim assist only considers opponents within this distance (m).")]
        [Min(0f)] public float aimAssistRange = 12f;

        [Header("Dash & shove")]
        [Tooltip("Belly-slide dash speed (m/s).")]
        [Min(0f)] public float dashSpeed = 11f;

        [Tooltip("How long a dash lasts (s).")]
        [Min(0.05f)] public float dashDuration = 0.3f;

        [Tooltip("Time between dashes (s).")]
        [Min(0f)] public float dashCooldown = 0.9f;

        [Tooltip("Start of the dash during which thrown items pass straight through (s).")]
        [Min(0f)] public float dashDodgeWindow = 0.2f;

        [Tooltip("How quickly a dash slows down while sliding (m/s²).")]
        [Min(0f)] public float dashDeceleration = 14f;

        [Tooltip("Base Cry when a dashing baby bumps into you. Multiplied by your defense.")]
        [Min(0f)] public float shoveCry = 6f;

        [Tooltip("Base knockback of a shove (m/s). Multiplied by your defense.")]
        [Min(0f)] public float shoveKnockback = 8f;

        [Tooltip("Speed a shoved baby's held toy is knocked loose with (m/s).")]
        [Min(0f)] public float shoveDropSpeed = 3f;

        [Header("Taking hits")]
        [Tooltip("Size of the Cry Meter. At this value the baby cries and loses.")]
        [Min(1f)] public float maxCry = 100f;

        [Tooltip("Knockback reduction for this baby: 0 = full knockback, 0.9 = barely moves.")]
        [Range(0f, 0.9f)] public float knockbackResistance = 0f;

        [Tooltip("Upward hop on knockback, as a fraction of the knockback speed.")]
        [Min(0f)] public float knockbackLift = 0.3f;

        [Tooltip("How quickly a knocked-back / stunned baby slides to a stop (m/s²).")]
        [Min(0.1f)] public float knockbackDeceleration = 16f;

        [Tooltip("Hits taken while wearing a diaper.")]
        public DefenseSettings protectedDefense = new DefenseSettings(0.5f, 0.6f, 0.2f);

        [Tooltip("Hits taken while diaperless.")]
        public DefenseSettings diaperlessDefense = new DefenseSettings(1.5f, 1.4f, 0.45f);

        public DefenseProfile GetDefense(bool isProtected) =>
            (isProtected ? protectedDefense : diaperlessDefense).ToProfile();
    }

    [Serializable]
    public sealed class DefenseSettings
    {
        [Tooltip("Multiplies the attack's base Cry.")]
        [Min(0f)] public float cryMultiplier = 1f;

        [Tooltip("Multiplies the attack's knockback.")]
        [Min(0f)] public float knockbackMultiplier = 1f;

        [Tooltip("Hit stun: no moving or acting for this long (s).")]
        [Min(0f)] public float stunDuration = 0.3f;

        public DefenseSettings() { }

        public DefenseSettings(float cryMultiplier, float knockbackMultiplier, float stunDuration)
        {
            this.cryMultiplier = cryMultiplier;
            this.knockbackMultiplier = knockbackMultiplier;
            this.stunDuration = stunDuration;
        }

        public DefenseProfile ToProfile() => new DefenseProfile(cryMultiplier, knockbackMultiplier, stunDuration);
    }
}
