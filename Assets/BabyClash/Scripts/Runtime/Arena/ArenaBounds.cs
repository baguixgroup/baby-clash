using System;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Safety net for the playable area. Items that escape (or get stranded somewhere unreachable) are put back
    /// at a neutral point; babies that escape are put back just inside the edge. Assumes an axis-aligned arena
    /// centred on this transform.
    /// </summary>
    public sealed class ArenaBounds : MonoBehaviour
    {
        [Tooltip("Half size of the playable floor on X and Z (m), around this transform.")]
        [SerializeField] Vector2 halfExtents = new Vector2(8f, 5f);

        [Tooltip("How far past the edge something may go before it counts as out of bounds (m).")]
        [SerializeField] float margin = 0.75f;

        [Tooltip("Anything below this world height is out of bounds.")]
        [SerializeField] float killHeight = -3f;

        [Tooltip("A loose item resting higher than this above the floor (e.g. on top of a tall block) can't be " +
                 "reached, so it is recovered too (m).")]
        [SerializeField] float maxLooseItemHeight = 1.2f;

        [Tooltip("How long an item must rest up there before it counts as stranded (s).")]
        [SerializeField] float strandedDelay = 1f;

        [Tooltip("Neutral spot where escaped items are dropped back in. Defaults to the arena centre.")]
        [SerializeField] Transform diaperRecoveryPoint;

        [Tooltip("Recovered items are dropped from this height above the recovery point (m).")]
        [SerializeField] float recoveryDropHeight = 2f;

        [Tooltip("Escaped babies are placed this far inside the edge (m).")]
        [SerializeField] float babyReturnInset = 1f;

        [SerializeField] BabyController[] babies = Array.Empty<BabyController>();
        [Tooltip("Diapers and toys to watch.")]
        [SerializeField] Throwable[] items = Array.Empty<Throwable>();

        const float RestingSpeed = 0.1f;
        float[] strandedTime = Array.Empty<float>();

        public Vector2 HalfExtents => halfExtents;

        public Vector3 DiaperRecoveryPosition =>
            (diaperRecoveryPoint != null ? diaperRecoveryPoint.position : transform.position)
            + Vector3.up * recoveryDropHeight;

        /// <summary>Scene wiring (editor tooling and tests). Call before the object is activated.</summary>
        public void Configure(Vector2 playableHalfExtents, Transform recoveryPoint, BabyController[] trackedBabies,
            Throwable[] trackedItems)
        {
            halfExtents = playableHalfExtents;
            diaperRecoveryPoint = recoveryPoint;
            babies = trackedBabies;
            items = trackedItems;
        }

        public bool IsInside(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - transform.position;
            return worldPosition.y >= killHeight
                   && Mathf.Abs(local.x) <= halfExtents.x + margin
                   && Mathf.Abs(local.z) <= halfExtents.y + margin;
        }

        public Vector3 ClampInside(Vector3 worldPosition, float inset)
        {
            Vector3 center = transform.position;
            float maxX = Mathf.Max(0f, halfExtents.x - inset);
            float maxZ = Mathf.Max(0f, halfExtents.y - inset);
            return new Vector3(
                center.x + Mathf.Clamp(worldPosition.x - center.x, -maxX, maxX),
                center.y,
                center.z + Mathf.Clamp(worldPosition.z - center.z, -maxZ, maxZ));
        }

        void FixedUpdate()
        {
            if (strandedTime.Length != items.Length)
                strandedTime = new float[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                Throwable item = items[i];
                if (item == null || !item.isActiveAndEnabled || item.State == ItemState.Held)
                {
                    strandedTime[i] = 0f;
                    continue;
                }

                Vector3 position = item.transform.position;
                // Resting (not merely passing through) up high: an item dropped from the recovery point is fine.
                bool restingHigh = item.State == ItemState.Loose
                                   && position.y - transform.position.y > maxLooseItemHeight
                                   && item.Body.linearVelocity.sqrMagnitude < RestingSpeed * RestingSpeed;
                strandedTime[i] = restingHigh ? strandedTime[i] + Time.fixedDeltaTime : 0f;

                if (!IsInside(position) || strandedTime[i] >= strandedDelay)
                {
                    strandedTime[i] = 0f;
                    item.RecoverTo(DiaperRecoveryPosition);
                }
            }

            foreach (BabyController baby in babies)
            {
                if (baby == null)
                    continue;

                Vector3 position = baby.transform.position;
                if (!IsInside(position))
                    baby.Movement.Teleport(ClampInside(position, babyReturnInset), baby.transform.rotation);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.8f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(halfExtents.x * 2f, 0.05f, halfExtents.y * 2f));
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position,
                new Vector3((halfExtents.x + margin) * 2f, 0.05f, (halfExtents.y + margin) * 2f));
        }
    }
}
