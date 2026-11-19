#nullable enable
using System;

namespace BabyClash.Core
{
    /// <summary>What the baby is busy doing. Diaper protection is tracked separately (see <see cref="BabyState"/>).</summary>
    public enum BabyActivity
    {
        Ready,
        Throwing,
        Dashing,
        Stunned,
        Crying,
    }

    /// <summary>Combined, human-readable state used by the HUD, animation and debug overlay.</summary>
    public enum BabyState
    {
        Protected,
        Diaperless,
        Throwing,
        Dashing,
        Stunned,
        Crying,
    }

    /// <summary>
    /// Timed action state for one baby: throw recovery, dash, hit stun, cooldowns and the terminal crying state.
    /// Crying is sticky until <see cref="Reset"/>, which is what stops a losing baby from fighting on.
    /// </summary>
    public sealed class BabyStatus
    {
        float activityTimer;
        float actionCooldown;
        float dashCooldown;

        public BabyActivity Activity { get; private set; } = BabyActivity.Ready;
        public bool IsCrying => Activity == BabyActivity.Crying;
        public bool IsDashing => Activity == BabyActivity.Dashing;

        /// <summary>Seconds since the current dash started (0 when not dashing).</summary>
        public float DashElapsed { get; private set; }

        public bool CanMove => Activity == BabyActivity.Ready || Activity == BabyActivity.Throwing;
        public bool CanAct => Activity == BabyActivity.Ready && actionCooldown <= 0f;
        public bool CanDash => (Activity == BabyActivity.Ready || Activity == BabyActivity.Throwing) && dashCooldown <= 0f;
        public float CooldownRemaining => actionCooldown;
        public float DashCooldownRemaining => dashCooldown;

        public event Action<BabyActivity>? ActivityChanged;

        public BabyState ToBabyState(bool isProtected)
        {
            switch (Activity)
            {
                case BabyActivity.Crying: return BabyState.Crying;
                case BabyActivity.Stunned: return BabyState.Stunned;
                case BabyActivity.Dashing: return BabyState.Dashing;
                case BabyActivity.Throwing: return BabyState.Throwing;
                default: return isProtected ? BabyState.Protected : BabyState.Diaperless;
            }
        }

        /// <summary>True during the first <paramref name="window"/> seconds of a dash: thrown items pass through.</summary>
        public bool IsDodging(float window) => IsDashing && DashElapsed < window;

        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;

            if (actionCooldown > 0f)
                actionCooldown = Math.Max(0f, actionCooldown - deltaTime);
            if (dashCooldown > 0f)
                dashCooldown = Math.Max(0f, dashCooldown - deltaTime);
            if (IsDashing)
                DashElapsed += deltaTime;

            if (Activity == BabyActivity.Throwing || Activity == BabyActivity.Stunned || Activity == BabyActivity.Dashing)
            {
                activityTimer -= deltaTime;
                if (activityTimer <= 0f)
                {
                    activityTimer = 0f;
                    SetActivity(BabyActivity.Ready);
                }
            }
        }

        /// <summary>Starts a throw if the baby is free to act. The cooldown blocks the next throw/pickup.</summary>
        public bool TryBeginThrow(float throwDuration, float cooldown)
        {
            if (!CanAct)
                return false;

            actionCooldown = Math.Max(0f, cooldown);
            if (throwDuration > 0f)
            {
                activityTimer = throwDuration;
                SetActivity(BabyActivity.Throwing);
            }
            return true;
        }

        public bool TryBeginDash(float duration, float cooldown)
        {
            if (!CanDash || !(duration > 0f))
                return false;

            dashCooldown = Math.Max(0f, cooldown);
            DashElapsed = 0f;
            activityTimer = duration;
            SetActivity(BabyActivity.Dashing);
            return true;
        }

        /// <summary>Ends a dash early (e.g. after shoving someone).</summary>
        public void EndDash()
        {
            if (!IsDashing)
                return;
            activityTimer = 0f;
            SetActivity(BabyActivity.Ready);
        }

        public void StartCooldown(float cooldown)
        {
            if (!IsCrying)
                actionCooldown = Math.Max(actionCooldown, cooldown);
        }

        /// <summary>Stuns the baby (interrupting a dash). Overlapping stuns keep the longer remaining time.</summary>
        public bool ApplyStun(float duration)
        {
            if (IsCrying || !(duration > 0f))
                return false;

            activityTimer = Activity == BabyActivity.Stunned ? Math.Max(activityTimer, duration) : duration;
            SetActivity(BabyActivity.Stunned);
            return true;
        }

        public void StartCrying()
        {
            activityTimer = 0f;
            actionCooldown = 0f;
            SetActivity(BabyActivity.Crying);
        }

        public void Reset()
        {
            activityTimer = 0f;
            actionCooldown = 0f;
            dashCooldown = 0f;
            DashElapsed = 0f;
            SetActivity(BabyActivity.Ready);
        }

        void SetActivity(BabyActivity activity)
        {
            if (activity != BabyActivity.Dashing)
                DashElapsed = 0f;
            if (Activity == activity)
                return;
            Activity = activity;
            ActivityChanged?.Invoke(activity);
        }
    }
}
