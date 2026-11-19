using System;
using System.Collections.Generic;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Drops toys from a fixed pool into free spots of the playroom during play. No runtime instantiation:
    /// every toy that can ever appear already exists (inactive) in the scene.
    /// </summary>
    public sealed class ToySpawner : MonoBehaviour
    {
        const int ProbeBufferSize = 8;

        [SerializeField] MatchConfig config;
        [SerializeField] ArenaBounds arena;
        [SerializeField] Toy[] pool = Array.Empty<Toy>();

        [Tooltip("Toys fall in from this height (m).")]
        [SerializeField] float dropHeight = 3.5f;

        [Tooltip("A spawn spot must be this far from walls, obstacles and babies (m).")]
        [SerializeField] float clearance = 0.9f;

        [Tooltip("Spots are kept this far inside the arena edge (m).")]
        [SerializeField] float edgeMargin = 1.2f;

        [SerializeField] int placementAttempts = 16;

        readonly Collider[] probe = new Collider[ProbeBufferSize];
        float timer;
        bool running;

        public IReadOnlyList<Toy> Pool => pool;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (Toy toy in pool)
                {
                    if (toy != null && toy.gameObject.activeSelf)
                        count++;
                }
                return count;
            }
        }

        public event Action<Toy> Spawned;

        /// <summary>Scene wiring (editor tooling and tests). Call before the object is activated.</summary>
        public void Configure(MatchConfig matchConfig, ArenaBounds arenaBounds, Toy[] toys)
        {
            config = matchConfig;
            arena = arenaBounds;
            pool = toys;
        }

        /// <summary>Clears every toy and restarts the drop timer. Spawning waits for <see cref="SetRunning"/>.</summary>
        public void ResetForRound()
        {
            foreach (Toy toy in pool)
            {
                if (toy != null)
                    toy.Despawn();
            }
            timer = config != null ? config.firstToyDelay : 0f;
            running = false;
        }

        public void SetRunning(bool value) => running = value;

        /// <summary>Drops a specific toy at a spot right away (tests, tutorials).</summary>
        public void SpawnAt(Toy toy, Vector3 groundPosition)
        {
            toy.Spawn(groundPosition + Vector3.up * dropHeight);
            Spawned?.Invoke(toy);
        }

        void Update()
        {
            if (!running || config == null || config.maxToys <= 0)
                return;

            timer -= Time.deltaTime;
            if (timer > 0f)
                return;
            timer = UnityEngine.Random.Range(config.toySpawnInterval.x, Mathf.Max(config.toySpawnInterval.x, config.toySpawnInterval.y));

            if (ActiveCount >= config.maxToys)
                return;
            Toy toy = PickInactiveToy();
            if (toy != null && TryFindSpot(out Vector3 spot))
                SpawnAt(toy, spot);
        }

        Toy PickInactiveToy()
        {
            float total = 0f;
            foreach (Toy toy in pool)
            {
                if (toy != null && !toy.gameObject.activeSelf)
                    total += toy.Config != null ? toy.Config.spawnWeight : 1f;
            }
            if (total <= 0f)
                return null;

            float roll = UnityEngine.Random.value * total;
            foreach (Toy toy in pool)
            {
                if (toy == null || toy.gameObject.activeSelf)
                    continue;
                roll -= toy.Config != null ? toy.Config.spawnWeight : 1f;
                if (roll <= 0f)
                    return toy;
            }
            return null;
        }

        bool TryFindSpot(out Vector3 spot)
        {
            Vector3 center = arena != null ? arena.transform.position : transform.position;
            Vector2 half = arena != null ? arena.HalfExtents : new Vector2(5f, 5f);
            float maxX = Mathf.Max(0f, half.x - edgeMargin);
            float maxZ = Mathf.Max(0f, half.y - edgeMargin);

            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                spot = center + new Vector3(UnityEngine.Random.Range(-maxX, maxX), 0f, UnityEngine.Random.Range(-maxZ, maxZ));
                // Probe above the floor so the floor itself never counts as blocking.
                Vector3 probeCenter = spot + Vector3.up * (clearance + 0.1f);
                int hits = Physics.OverlapSphereNonAlloc(probeCenter, clearance, probe, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                    probe[i] = null;
                if (hits == 0)
                    return true;
            }
            spot = default;
            return false;
        }
    }
}
