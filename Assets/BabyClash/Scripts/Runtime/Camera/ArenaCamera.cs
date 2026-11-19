using System;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Shared camera: looks at the babies' midpoint from a fixed angle, pulls back as they separate,
    /// and supports trauma-based shake for strong hits.
    /// </summary>
    public sealed class ArenaCamera : MonoBehaviour
    {
        [SerializeField] Transform[] targets = Array.Empty<Transform>();

        [Header("Framing")]
        [Tooltip("Where the camera looks when it has no targets; focus never drifts further than Max Focus Offset from it.")]
        [SerializeField] Vector3 arenaCenter = Vector3.zero;

        [Tooltip("How far the focus point may follow the babies away from the arena centre (m).")]
        [SerializeField] float maxFocusOffset = 2f;

        [Tooltip("Looking-down angle (degrees).")]
        [Range(20f, 89f)] [SerializeField] float pitch = 52f;

        [SerializeField] float yaw;

        [Tooltip("Camera distance when the babies are close together (m).")]
        [SerializeField] float minDistance = 13.5f;

        [Tooltip("Camera distance when the babies are far apart (m).")]
        [SerializeField] float maxDistance = 19.5f;

        [Tooltip("Separation (m) at which the camera reaches Max Distance.")]
        [SerializeField] float separationForMaxDistance = 13f;

        [Tooltip("Aim this far behind the babies' midpoint (m) so the action sits a bit lower, clear of the HUD.")]
        [SerializeField] float hudClearance = 1.2f;

        [Tooltip("Depth separation counts extra, because the screen is wider than it is tall.")]
        [SerializeField] float depthSeparationWeight = 1.5f;

        [Tooltip("Follow smoothing (s). Lower = snappier.")]
        [SerializeField] float smoothTime = 0.3f;

        [Header("Shake")]
        [SerializeField] float maxShakeOffset = 0.35f;
        [SerializeField] float maxShakeRoll = 2f;
        [SerializeField] float shakeFrequency = 22f;

        [Tooltip("Trauma lost per second.")]
        [SerializeField] float shakeDecay = 1.8f;

        Vector3 focus;
        Vector3 focusVelocity;
        float distance;
        float distanceVelocity;
        float trauma;
        float noiseSeed;

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(Transform[] followTargets, Vector3 center)
        {
            targets = followTargets;
            arenaCenter = center;
        }

        /// <summary>Adds shake trauma (0..1). Shake strength grows with trauma squared, so small hits stay subtle.</summary>
        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));

        void Start()
        {
            noiseSeed = UnityEngine.Random.value * 100f;
            ComputeGoal(out focus, out distance);
            Apply(focus, distance, 0f);
        }

        void LateUpdate()
        {
            ComputeGoal(out Vector3 goalFocus, out float goalDistance);
            focus = Vector3.SmoothDamp(focus, goalFocus, ref focusVelocity, smoothTime);
            distance = Mathf.SmoothDamp(distance, goalDistance, ref distanceVelocity, smoothTime);

            float shake = trauma * trauma;
            Apply(focus, distance, shake);
            trauma = Mathf.Max(0f, trauma - shakeDecay * Time.unscaledDeltaTime);
        }

        void Apply(Vector3 lookAt, float dist, float shake)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = lookAt - rotation * Vector3.forward * dist;

            if (shake > 0f)
            {
                float t = Time.unscaledTime * shakeFrequency;
                var offset = new Vector3(Noise(noiseSeed, t), Noise(noiseSeed + 10f, t), 0f);
                position += rotation * offset * (maxShakeOffset * shake);
                rotation *= Quaternion.Euler(0f, 0f, Noise(noiseSeed + 20f, t) * maxShakeRoll * shake);
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        void ComputeGoal(out Vector3 goalFocus, out float goalDistance)
        {
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            int count = 0;
            foreach (Transform target in targets)
            {
                if (target == null)
                    continue;
                min = Vector3.Min(min, target.position);
                max = Vector3.Max(max, target.position);
                count++;
            }

            if (count == 0)
            {
                goalFocus = arenaCenter;
                goalDistance = minDistance;
                return;
            }

            Vector3 mid = (min + max) * 0.5f;
            Vector3 offset = Vector3.ClampMagnitude(new Vector3(mid.x - arenaCenter.x, 0f, mid.z - arenaCenter.z), maxFocusOffset);
            goalFocus = arenaCenter + offset + Vector3.forward * hudClearance;

            float separation = new Vector2(max.x - min.x, (max.z - min.z) * depthSeparationWeight).magnitude;
            goalDistance = Mathf.Lerp(minDistance, maxDistance, separation / Mathf.Max(0.01f, separationForMaxDistance));
        }

        static float Noise(float seed, float t) => Mathf.PerlinNoise(seed, t) * 2f - 1f;
    }
}
