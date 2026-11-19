using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// The single owner of Time.timeScale: pause beats hit-stop beats slow motion. Keeps juice effects and the
    /// pause menu from fighting over the clock.
    /// </summary>
    public sealed class TimeController : MonoBehaviour
    {
        const float HitStopScale = 0.05f;

        bool paused;
        float hitStopTimer;
        float slowMotionTimer;
        float slowMotionScale = 1f;

        public bool IsPaused => paused;

        public void SetPaused(bool value)
        {
            paused = value;
            Apply();
        }

        /// <summary>Near-freeze for a few frames on a big hit (real-time seconds).</summary>
        public void HitStop(float seconds) => hitStopTimer = Mathf.Max(hitStopTimer, seconds);

        /// <summary>Slow motion for a dramatic moment (real-time seconds).</summary>
        public void SlowMotion(float seconds, float scale)
        {
            slowMotionTimer = Mathf.Max(slowMotionTimer, seconds);
            slowMotionScale = Mathf.Clamp(scale, 0.05f, 1f);
        }

        /// <summary>Cancels hit-stop and slow motion (e.g. when leaving a match).</summary>
        public void ClearEffects()
        {
            hitStopTimer = 0f;
            slowMotionTimer = 0f;
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!paused)
            {
                hitStopTimer = Mathf.Max(0f, hitStopTimer - dt);
                slowMotionTimer = Mathf.Max(0f, slowMotionTimer - dt);
            }
            Apply();
        }

        void Apply()
        {
            if (paused)
                Time.timeScale = 0f;
            else if (hitStopTimer > 0f)
                Time.timeScale = HitStopScale;
            else if (slowMotionTimer > 0f)
                Time.timeScale = slowMotionScale;
            else
                Time.timeScale = 1f;
        }

        void OnDisable() => Time.timeScale = 1f;
    }
}
