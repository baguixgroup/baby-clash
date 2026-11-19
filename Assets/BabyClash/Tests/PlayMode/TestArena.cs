using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BabyClash.Tests
{
    /// <summary>
    /// Builds minimal, art-free babies, diapers, toys and a floor in code, mirroring the generated prefabs' physics
    /// setup. Objects are created inactive, configured, then activated, so Awake sees fully wired components.
    /// </summary>
    sealed class TestArena : IDisposable
    {
        readonly List<Object> created = new List<Object>();

        public TestArena()
        {
            Baby = ScriptableObject.CreateInstance<BabyConfig>();
            Baby.aimAssistAngle = 0f; // deterministic throws in tests
            Baby.actionCooldown = 0f;
            Baby.throwDuration = 0f;
            Diaper = ScriptableObject.CreateInstance<DiaperConfig>();
            Block = ScriptableObject.CreateInstance<ToyConfig>();
            Block.name = "TestBlock";
            Block.directHitCry = 12f;
            Match = ScriptableObject.CreateInstance<MatchConfig>();
            Match.countdownDuration = 0f;
            Match.roundTimeLimit = 0f;
            Match.roundResultDuration = 0.5f;
            Match.roundsToWin = 2;
            Match.maxToys = 0;
            Cpu = ScriptableObject.CreateInstance<CpuConfig>();
            created.Add(Baby);
            created.Add(Diaper);
            created.Add(Block);
            created.Add(Match);
            created.Add(Cpu);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            created.Add(floor);
        }

        public BabyConfig Baby { get; }
        public DiaperConfig Diaper { get; }
        public ToyConfig Block { get; }
        public MatchConfig Match { get; }
        public CpuConfig Cpu { get; }

        public BabyController CreateBaby(int index, Vector3 position, Vector3 facing)
        {
            var go = new GameObject($"TestBaby_{index + 1}");
            go.SetActive(false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.7f, 0f);
            capsule.radius = 0.36f;
            capsule.height = 1.4f;
            go.AddComponent<Rigidbody>();
            go.AddComponent<BabyMovement>();

            var socket = new GameObject("DiaperSocket").transform;
            socket.SetParent(go.transform, false);
            socket.localPosition = new Vector3(0f, 0.33f, 0f);
            go.AddComponent<BabyDiaperSlot>().Configure(socket);

            var hold = new GameObject("HoldSocket").transform;
            hold.SetParent(go.transform, false);
            hold.localPosition = new Vector3(0f, 1.52f, 0f);
            go.AddComponent<BabyHands>().Configure(hold);

            var baby = go.AddComponent<BabyController>();
            baby.Configure(Baby, index, $"BABY {index + 1}", Color.white);

            go.SetActive(true);
            created.Add(go);
            return baby;
        }

        public Diaper CreateDiaper(string name, Vector3 position)
        {
            GameObject go = ItemObject(name, position, out BoxCollider solid, out SphereCollider hitZone);
            go.AddComponent<Diaper>().Configure(Diaper, solid, hitZone, null);
            go.SetActive(true);
            return go.GetComponent<Diaper>();
        }

        /// <summary>A toy, inactive (in the pool) until spawned.</summary>
        public Toy CreateToy(string name)
        {
            GameObject go = ItemObject(name, new Vector3(0f, -5f, 0f), out BoxCollider solid, out SphereCollider hitZone);
            go.AddComponent<Toy>().Configure(Block, solid, hitZone, null, new Renderer[0]);
            return go.GetComponent<Toy>();
        }

        GameObject ItemObject(string name, Vector3 position, out BoxCollider solid, out SphereCollider hitZone)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.position = position;
            go.AddComponent<Rigidbody>();
            solid = go.AddComponent<BoxCollider>();
            solid.size = new Vector3(0.5f, 0.25f, 0.45f);
            hitZone = go.AddComponent<SphereCollider>();
            hitZone.isTrigger = true;
            hitZone.radius = 0.4f;
            created.Add(go);
            return go;
        }

        /// <summary>Round manager over the given babies, spawning them where they currently stand. Starts on its own.</summary>
        public RoundManager CreateRound(BabyController[] babies, Diaper[] diapers, ArenaBounds arena = null, ToySpawner toys = null)
        {
            var spawns = new Transform[babies.Length];
            for (int i = 0; i < babies.Length; i++)
            {
                spawns[i] = new GameObject($"Spawn_{i + 1}").transform;
                spawns[i].SetPositionAndRotation(babies[i].transform.position, babies[i].transform.rotation);
                created.Add(spawns[i].gameObject);
            }

            var go = new GameObject("TestRound");
            go.SetActive(false);
            var round = go.AddComponent<RoundManager>();
            round.Configure(Match, babies, spawns, diapers, arena, toys, true);
            go.SetActive(true);
            created.Add(go);
            return round;
        }

        public ToySpawner CreateSpawner(Toy[] toys)
        {
            var go = new GameObject("TestToySpawner");
            go.SetActive(false);
            var spawner = go.AddComponent<ToySpawner>();
            spawner.Configure(Match, null, toys);
            go.SetActive(true);
            created.Add(go);
            return spawner;
        }

        public ArenaBounds CreateBounds(Vector2 halfExtents, BabyController[] babies, Throwable[] items)
        {
            var go = new GameObject("TestBounds");
            go.SetActive(false);
            var bounds = go.AddComponent<ArenaBounds>();
            bounds.Configure(halfExtents, null, babies, items);
            go.SetActive(true);
            created.Add(go);
            return bounds;
        }

        public BabyBrain AddBrain(BabyController baby, RoundManager round, CpuDifficulty difficulty)
        {
            baby.gameObject.SetActive(false);
            var brain = baby.gameObject.AddComponent<BabyBrain>();
            brain.Configure(Cpu, round, difficulty);
            baby.gameObject.SetActive(true);
            return brain;
        }

        /// <summary>Two babies facing each other on the X axis, a match wired up, and the first round running.</summary>
        public IEnumerator StartDuel(float distance, Action<BabyController[], Diaper[], RoundManager> ready, ToySpawner toys = null)
        {
            var babies = new[]
            {
                CreateBaby(0, new Vector3(-distance * 0.5f, 0f, 0f), Vector3.right),
                CreateBaby(1, new Vector3(distance * 0.5f, 0f, 0f), Vector3.left),
            };
            var diapers = new[]
            {
                CreateDiaper("Diaper_01", new Vector3(0f, 5f, -5f)),
                CreateDiaper("Diaper_02", new Vector3(0f, 5f, 5f)),
            };
            RoundManager round = CreateRound(babies, diapers, null, toys);
            yield return null; // RoundManager.Start: hands out diapers; zero countdown -> Playing
            ready(babies, diapers, round);
        }

        public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline)
                yield return null;
        }

        public void Dispose()
        {
            Time.timeScale = 1f;
            foreach (Object obj in created)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            created.Clear();
        }
    }
}
