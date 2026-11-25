using System.Collections.Generic;
using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Turns the one arena scene into five rooms at runtime: recolours floor, mat, walls and light, swaps the props
    /// outside the fence, and adds each room's twist inside it (sticky jam, slippery floor, sprinkler, robot vacuum).
    /// </summary>
    public sealed class RoomDresser : MonoBehaviour
    {
        static readonly string[] SharedNames = { "WoodFloor", "PuzzleMat", "FloorCollider", "PlaypenFence", "WallBack", "WallLeft", "WallRight", "Trim" };

        readonly Dictionary<RoomKind, GameObject> dressings = new Dictionary<RoomKind, GameObject>();
        readonly List<GameObject> nurseryProps = new List<GameObject>();
        readonly Dictionary<string, Renderer> shared = new Dictionary<string, Renderer>();
        RoundManager round;
        Light sun;
        Color nurserySun;
        Color nurserySky;
        MaterialPropertyBlock block;

        public RoomKind Current { get; private set; }

        public void Configure(RoundManager manager)
        {
            round = manager;
            block = new MaterialPropertyBlock();
            GameObject nursery = GameObject.Find("Nursery");
            if (nursery != null)
            {
                foreach (Transform child in nursery.transform)
                {
                    if (System.Array.IndexOf(SharedNames, child.name) < 0)
                        nurseryProps.Add(child.gameObject);
                    else if (child.TryGetComponent(out Renderer renderer))
                        shared[child.name] = renderer;
                }
                if (shared.TryGetValue("PlaypenFence", out Renderer fence))
                    ProcMesh.UseMaterial(fence.sharedMaterial);
            }
            GameObject sunObject = GameObject.Find("Sun");
            if (sunObject != null && sunObject.TryGetComponent(out sun))
                nurserySun = sun.color;
            nurserySky = RenderSettings.ambientSkyColor;
        }

        public void Show(RoomKind room)
        {
            Current = room;
            foreach (GameObject prop in nurseryProps)
                prop.SetActive(room == RoomKind.Nursery);
            foreach (KeyValuePair<RoomKind, GameObject> pair in dressings)
                pair.Value.SetActive(pair.Key == room);
            if (room != RoomKind.Nursery && !dressings.ContainsKey(room))
                dressings[room] = Build(room);

            Palette(room, out Color floor, out Color mat, out Color wall, out Color fence, out Color light);
            bool outdoors = room == RoomKind.Backyard;
            Texture2D ground = room == RoomKind.Nursery ? null : outdoors ? Grass() : Tiles();
            Tint("WoodFloor", floor, ground);
            Tint("PuzzleMat", mat, outdoors ? ground : null);
            Texture2D wallpaper = outdoors ? Texture2D.whiteTexture : room == RoomKind.Bath ? Tiles() : null;
            Tint("WallBack", wall, wallpaper);
            Tint("WallLeft", wall, wallpaper);
            Tint("WallRight", wall, wallpaper);
            Tint("PlaypenFence", fence);
            if (sun != null)
                sun.color = nurserySun * light;
            RenderSettings.ambientSkyColor = nurserySky * light;

            float grip = room == RoomKind.Bath ? 0.2f : 1f;
            foreach (BabyController baby in round.AllBabies)
                baby.Movement.Grip = grip;
        }

        static void Palette(RoomKind room, out Color floor, out Color mat, out Color wall, out Color fence, out Color light)
        {
            floor = mat = wall = fence = light = Color.white;
            switch (room)
            {
                case RoomKind.Kitchen:
                    floor = new Color(0.95f, 0.98f, 1f) * 0.95f;
                    mat = new Color(1f, 0.86f, 0.78f);
                    wall = new Color(1f, 0.9f, 0.7f);
                    fence = new Color(1f, 0.85f, 0.75f);
                    light = new Color(1.05f, 1f, 0.92f);
                    break;
                case RoomKind.Bath:
                    floor = new Color(0.6f, 0.85f, 1f);
                    mat = new Color(0.8f, 0.95f, 1f);
                    wall = new Color(0.7f, 0.93f, 1f);
                    fence = new Color(0.85f, 1f, 1f);
                    light = new Color(0.92f, 1f, 1.05f);
                    break;
                case RoomKind.Backyard:
                    floor = new Color(0.62f, 0.86f, 0.46f);
                    mat = new Color(0.72f, 0.93f, 0.55f);
                    wall = new Color(0.62f, 0.84f, 1f);
                    fence = new Color(1f, 0.97f, 0.9f);
                    light = new Color(1.1f, 1.05f, 0.9f);
                    break;
                case RoomKind.Daycare:
                    floor = new Color(0.8f, 0.7f, 1f);
                    mat = new Color(1f, 0.93f, 1f);
                    wall = new Color(0.9f, 0.8f, 1f);
                    fence = new Color(1f, 0.9f, 0.6f);
                    light = new Color(1f, 0.95f, 1.02f);
                    break;
            }
        }

        Texture2D tiles;
        Texture2D grass;

        Texture2D Tiles()
        {
            if (tiles == null)
                tiles = Paint("RuntimeTiles", (x, y) => x % 32 < 2 || y % 32 < 2 ? 0.8f : (x / 32 + y / 32) % 2 == 0 ? 1f : 0.93f);
            return tiles;
        }

        Texture2D Grass()
        {
            if (grass == null)
                grass = Paint("RuntimeGrass", (x, y) => 0.88f + 0.12f * Mathf.PerlinNoise(x * 0.19f, y * 0.07f) - (Mathf.PerlinNoise(x * 0.9f + 40f, y * 0.3f) > 0.68f ? 0.1f : 0f));
            return grass;
        }

        static Texture2D Paint(string textureName, System.Func<int, int, float> shade)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = textureName, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte v = (byte)(Mathf.Clamp01(shade(x, y)) * 255f);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Colours a shared surface, optionally replacing its texture (null = its own).</summary>
        void Tint(string target, Color color, Texture2D texture = null)
        {
            if (!shared.TryGetValue(target, out Renderer renderer))
                return;
            block.Clear();
            block.SetColor("_BaseColor", color);
            if (texture != null)
                block.SetTexture("_BaseMap", texture);
            renderer.SetPropertyBlock(block);
        }

        GameObject Build(RoomKind room)
        {
            var root = new GameObject("Room_" + room);
            root.transform.SetParent(transform, false);
            switch (room)
            {
                case RoomKind.Kitchen: BuildKitchen(root.transform); break;
                case RoomKind.Bath: BuildBath(root.transform); break;
                case RoomKind.Backyard: BuildBackyard(root.transform); break;
                case RoomKind.Daycare: BuildDaycare(root.transform); break;
            }
            return root;
        }

        void BuildKitchen(Transform root)
        {
            Color white = new Color(0.97f, 0.97f, 0.95f), mint = new Color(0.6f, 0.88f, 0.78f), steel = new Color(0.78f, 0.82f, 0.88f), wood = new Color(0.85f, 0.64f, 0.42f);
            var counter = new ProcMesh();
            for (int i = 0; i < 6; i++)
            {
                float x = -9f + i * 2.6f;
                counter.Box(new Vector3(x, 0.75f, 8.4f), new Vector3(2.5f, 1.5f, 1.4f), i % 2 == 0 ? mint : white)
                    .Sphere(new Vector3(x + 0.9f, 1f, 7.66f), 0.09f, steel);
            }
            counter.Box(new Vector3(-2.5f, 1.56f, 8.35f), new Vector3(15.8f, 0.14f, 1.7f), wood)
                // Stove with pots.
                .Box(new Vector3(-6.4f, 1.66f, 8.3f), new Vector3(2f, 0.06f, 1.2f), new Color(0.25f, 0.25f, 0.3f))
                .Cylinder(new Vector3(-6.9f, 1.68f, 8.3f), 0.36f, 0.45f, new Color(1f, 0.45f, 0.4f)).Cylinder(new Vector3(-5.9f, 1.68f, 8.3f), 0.28f, 0.6f, steel)
                // Fruit bowl.
                .Lathe(new Vector3(-1.5f, 1.63f, 8.2f), white, new Vector2(0.15f, 0f), new Vector2(0.5f, 0.16f), new Vector2(0.62f, 0.4f), new Vector2(0.56f, 0.4f), new Vector2(0f, 0.15f))
                .Sphere(new Vector3(-1.7f, 1.98f, 8.2f), 0.2f, new Color(1f, 0.35f, 0.3f)).Sphere(new Vector3(-1.3f, 1.98f, 8.3f), 0.2f, new Color(1f, 0.65f, 0.2f))
                .Sphere(new Vector3(-1.5f, 2.1f, 8.1f), 0.18f, new Color(0.55f, 0.8f, 0.3f))
                // Fridge.
                .Box(new Vector3(8.6f, 1.9f, 8.2f), new Vector3(2.4f, 3.8f, 1.7f), white).Box(new Vector3(8.6f, 2.5f, 7.33f), new Vector3(2.3f, 0.05f, 0.05f), steel)
                .Cylinder(new Vector3(7.75f, 1.4f, 7.25f), 0.06f, 0.8f, steel).Cylinder(new Vector3(7.75f, 2.8f, 7.25f), 0.06f, 0.5f, steel)
                .Box(new Vector3(9f, 3f, 7.32f), new Vector3(0.5f, 0.4f, 0.04f), new Color(1f, 0.8f, 0.3f));
            counter.Spawn("Counters", root, Vector3.zero);
            var chair = new ProcMesh();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(11.3f * side, 0f, 1.5f);
                foreach (float lx in new[] { -0.5f, 0.5f })
                    foreach (float lz in new[] { -0.5f, 0.5f })
                        chair.Cylinder(at + new Vector3(lx, 0f, lz), 0.08f, 1.4f, wood);
                chair.Box(at + new Vector3(0f, 1.45f, 0f), new Vector3(1.3f, 0.2f, 1.2f), side < 0 ? new Color(1f, 0.6f, 0.65f) : new Color(0.5f, 0.72f, 1f))
                    .Box(at + new Vector3(0.6f * side, 2.1f, 0f), new Vector3(0.2f, 1.2f, 1.2f), white).Box(at + new Vector3(-0.6f * side, 1.9f, 0f), new Vector3(0.6f, 0.1f, 1.3f), white);
            }
            chair.Spawn("HighChairs", root, Vector3.zero);
            // Twist: sticky jam puddles.
            StickyZone.Create(root, new Vector3(-1.9f, 0f, -3f), 1.25f, new Color(0.85f, 0.15f, 0.3f, 0.85f), 0.5f);
            StickyZone.Create(root, new Vector3(1.9f, 0f, 3f), 1.25f, new Color(0.95f, 0.6f, 0.1f, 0.85f), 0.5f);
            StickyZone.Create(root, new Vector3(-5.8f, 0f, -1.8f), 0.9f, new Color(0.5f, 0.2f, 0.6f, 0.85f), 0.5f);
            StickyZone.Create(root, new Vector3(5.8f, 0f, 1.8f), 0.9f, new Color(0.45f, 0.75f, 0.2f, 0.85f), 0.5f);
        }

        void BuildBath(Transform root)
        {
            Color white = new Color(0.98f, 0.99f, 1f), gold = new Color(1f, 0.82f, 0.4f), aqua = new Color(0.5f, 0.85f, 0.95f), duck = new Color(1f, 0.85f, 0.2f);
            var tub = new ProcMesh();
            tub.Lathe(new Vector3(-3.5f, 0.2f, 8f), Quaternion.identity, new Vector3(2.4f, 1f, 1f), white, 32, new Vector2(0f, 0f), new Vector2(0.85f, 0.05f), new Vector2(1.1f, 0.7f),
                    new Vector2(1.2f, 1.5f), new Vector2(1.28f, 1.55f), new Vector2(1.2f, 1.62f), new Vector2(1.1f, 1.55f), new Vector2(1.05f, 1.3f))
                .Lathe(new Vector3(-3.5f, 1.6f, 8f), Quaternion.identity, new Vector3(2.4f, 1f, 1f), aqua, 32, new Vector2(1.08f, 0f), new Vector2(0f, 0.02f))
                .Cylinder(new Vector3(-0.4f, 1.4f, 8.6f), 0.08f, 1.1f, gold).Torus(new Vector3(-0.75f, 2.5f, 8.5f), 0.35f, 0.08f, gold, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 12; i++)
                tub.Sphere(new Vector3(-5.6f + i * 0.4f, 1.75f + (i % 3) * 0.12f, 7.8f + (i % 2) * 0.4f), 0.22f + (i % 4) * 0.06f, white);
            foreach (Vector3 foot in new[] { new Vector3(-5.2f, 0f, 7.4f), new Vector3(-1.8f, 0f, 7.4f) })
                tub.Sphere(foot + Vector3.up * 0.2f, 0.25f, gold);
            tub.Sphere(new Vector3(-4.6f, 2f, 7.4f), 0.3f, duck).Sphere(new Vector3(-4.4f, 2.4f, 7.3f), 0.2f, duck).Ellipsoid(new Vector3(-4.2f, 2.38f, 7.25f), new Vector3(0.1f, 0.05f, 0.08f), new Color(1f, 0.5f, 0.2f));
            tub.Spawn("Bathtub", root, Vector3.zero);
            var props = new ProcMesh();
            // Sink, toilet-paper tower, towels and shampoo.
            props.Cylinder(new Vector3(6f, 0f, 8.2f), 0.35f, 1.3f, white).Lathe(new Vector3(6f, 1.2f, 8.2f), white, new Vector2(0.3f, 0f), new Vector2(0.95f, 0.25f), new Vector2(1.1f, 0.6f), new Vector2(0.95f, 0.6f), new Vector2(0f, 0.3f))
                .Cylinder(new Vector3(6f, 1.7f, 8.8f), 0.08f, 0.6f, gold);
            for (int i = 0; i < 4; i++)
                props.Torus(new Vector3(10.8f, 0.3f + i * 0.52f, 4.5f), 0.33f, 0.24f, white);
            Color[] towels = { new Color(1f, 0.65f, 0.75f), aqua, new Color(1f, 0.9f, 0.6f) };
            for (int i = 0; i < 3; i++)
                props.Box(new Vector3(-11.2f, 0.25f + i * 0.45f, 3f), new Vector3(2f, 0.42f, 1.3f), towels[i]);
            for (int i = 0; i < 3; i++)
                props.Cylinder(new Vector3(-11.6f + i * 0.8f, 0f, -3.5f), 0.28f, 1.2f + i * 0.25f, towels[i]).Cylinder(new Vector3(-11.6f + i * 0.8f, 1.2f + i * 0.25f, -3.5f), 0.12f, 0.25f, white);
            props.Spawn("BathProps", root, Vector3.zero);
            // Twist: slippery everywhere (grip), shown by shiny puddles and drifting bubbles.
            Vector3[] puddles = { new Vector3(-4f, 0f, -2.6f), new Vector3(4f, 0f, 2.6f), new Vector3(0f, 0f, -3.4f), new Vector3(-6f, 0f, 2.5f), new Vector3(6f, 0f, -2.8f) };
            for (int i = 0; i < puddles.Length; i++)
                new ProcMesh().Disc(Vector3.zero, 0f, 0.8f + i % 3 * 0.25f, new Color(0.85f, 0.97f, 1f, 0.45f)).Spawn("Puddle", root, puddles[i] + Vector3.up * 0.03f, ProcMesh.Transparent);
            DriftingBubbles.Create(root, 9);
        }

        void BuildBackyard(Transform root)
        {
            Color leaf = new Color(0.36f, 0.72f, 0.32f), trunk = new Color(0.58f, 0.4f, 0.26f), white = new Color(1f, 0.98f, 0.94f), red = new Color(0.95f, 0.4f, 0.36f);
            var garden = new ProcMesh();
            Vector3[] trees = { new Vector3(-9f, 0f, 8f), new Vector3(9.5f, 0f, 8.2f), new Vector3(-12.5f, 0f, 1f), new Vector3(13f, 0f, -0.5f), new Vector3(4.2f, 0f, 8.8f) };
            foreach (Vector3 t in trees)
            {
                garden.Cylinder(t, 0.38f, 1.9f, trunk, 0.26f).Sphere(t + new Vector3(0f, 2.7f, 0f), 1.3f, leaf)
                    .Sphere(t + new Vector3(0.9f, 2.3f, -0.3f), 0.9f, leaf * 1.1f).Sphere(t + new Vector3(-0.9f, 2.2f, -0.2f), 0.85f, leaf * 0.93f);
                for (int i = 0; i < 4; i++)
                    garden.Sphere(t + new Vector3(Mathf.Cos(i * 1.7f) * 1.1f, 2.4f + i * 0.3f, -1.05f), 0.14f, red);
            }
            // Doghouse, flowers and picket fence.
            garden.Box(new Vector3(-3.5f, 0.8f, 8f), new Vector3(2.2f, 1.6f, 1.8f), red).Box(new Vector3(-4.1f, 1.9f, 8f), new Vector3(1.7f, 0.18f, 2.1f), white, Quaternion.Euler(0f, 0f, 38f))
                .Box(new Vector3(-2.9f, 1.9f, 8f), new Vector3(1.7f, 0.18f, 2.1f), white, Quaternion.Euler(0f, 0f, -38f)).Ellipsoid(new Vector3(-3.5f, 0.6f, 7.1f), new Vector3(0.55f, 0.7f, 0.05f), new Color(0.25f, 0.15f, 0.15f));
            for (int i = 0; i < 26; i++)
                garden.Box(new Vector3(-13f + i * 1.04f, 0.7f, 9.2f), new Vector3(0.7f, 1.4f, 0.12f), white).Cone(new Vector3(-13f + i * 1.04f, 1.4f, 9.2f), 0.33f, 0.4f, white);
            Color[] petals = { new Color(1f, 0.5f, 0.65f), new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.6f, 1f), white };
            for (int i = 0; i < 18; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = new Vector3(side * (9.6f + i % 4 * 0.6f), 0f, -5.5f + i * 0.62f);
                garden.Cylinder(at, 0.035f, 0.5f, leaf).Sphere(at + Vector3.up * 0.55f, 0.1f, new Color(1f, 0.8f, 0.2f));
                for (int p = 0; p < 5; p++)
                    garden.Sphere(at + new Vector3(Mathf.Cos(p * 1.257f) * 0.14f, 0.55f, Mathf.Sin(p * 1.257f) * 0.14f), 0.09f, petals[i % 4]);
            }
            garden.Spawn("Garden", root, Vector3.zero);
            // Twists: sandpits slow you down, the sprinkler pushes you around.
            StickyZone.Create(root, new Vector3(-5.8f, 0f, 2.6f), 1.5f, new Color(0.95f, 0.85f, 0.55f, 0.95f), 0.6f);
            StickyZone.Create(root, new Vector3(5.8f, 0f, -2.6f), 1.5f, new Color(0.95f, 0.85f, 0.55f, 0.95f), 0.6f);
            Sprinkler.Create(root, round, new Vector3(0f, 0.75f, 0f));
        }

        void BuildDaycare(Transform root)
        {
            Color[] c = { new Color(1f, 0.6f, 0.7f), new Color(0.5f, 0.75f, 1f), new Color(1f, 0.85f, 0.4f), new Color(0.55f, 0.88f, 0.65f), new Color(0.75f, 0.62f, 1f) };
            var props = new ProcMesh();
            for (int i = 0; i < 5; i++)
            {
                // Cubbies with name stars.
                float x = -9f + i * 2.1f;
                props.Box(new Vector3(x, 1f, 8.6f), new Vector3(1.9f, 2f, 1f), c[i]).Box(new Vector3(x, 1.2f, 8.08f), new Vector3(1.5f, 1.2f, 0.06f), Color.white * 0.96f)
                    .Sphere(new Vector3(x, 1.2f, 8.05f), 0.32f, c[(i + 2) % 5]);
            }
            // Slide.
            props.Box(new Vector3(7.6f, 1.15f, 8.2f), new Vector3(4f, 0.18f, 1.4f), c[1], Quaternion.Euler(0f, 0f, -28f)).Box(new Vector3(5.6f, 1.1f, 8.2f), new Vector3(0.6f, 2.2f, 1.4f), c[0]);
            for (int i = 0; i < 4; i++)
                props.Cylinder(new Vector3(4.9f, 0.4f + i * 0.5f, 7.7f), 0.07f, 0.06f, c[2]).Box(new Vector3(5.1f, 0.4f + i * 0.5f, 8.2f), new Vector3(0.3f, 0.12f, 1.2f), c[2]);
            // Nap mats, balls, giant crayons.
            for (int i = 0; i < 3; i++)
                props.Box(new Vector3(-11.6f, 0.1f, -3f + i * 2.4f), new Vector3(2.6f, 0.2f, 1.6f), c[i]).Ellipsoid(new Vector3(-12.4f, 0.32f, -3f + i * 2.4f), new Vector3(0.45f, 0.2f, 0.6f), Color.white);
            for (int i = 0; i < 9; i++)
                props.Sphere(new Vector3(11f + i % 3 * 0.9f, 0.4f + i / 6 * 0.55f, -3f + i / 3 * 0.9f), 0.42f, c[i % 5]);
            for (int i = 0; i < 5; i++)
                props.Cylinder(new Vector3(10.6f + i * 0.7f, 0f, 4.5f), 0.26f, 2f, c[i]).Cone(new Vector3(10.6f + i * 0.7f, 2f, 4.5f), 0.2f, 0.5f, c[i]);
            props.Spawn("DaycareProps", root, Vector3.zero);
            // Twist: the robot vacuum.
            RobotVacuum.Create(root, round);
        }
    }

    /// <summary>A puddle or sandpit that slows babies standing in it.</summary>
    public sealed class StickyZone : MonoBehaviour
    {
        float scale;

        public static void Create(Transform root, Vector3 position, float radius, Color color, float speedScale)
        {
            var go = new GameObject("StickyZone");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var shape = new ProcMesh().Disc(Vector3.zero, 0f, radius, color);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f + radius;
                shape.Disc(new Vector3(Mathf.Cos(a) * radius * 0.85f, 0.002f, Mathf.Sin(a) * radius * 0.85f), 0f, radius * (0.28f + i % 3 * 0.07f), color, 18);
            }
            shape.Spawn("Puddle", go.transform, new Vector3(0f, 0.03f, 0f), ProcMesh.Transparent);
            var zone = go.AddComponent<SphereCollider>();
            zone.isTrigger = true;
            zone.radius = radius;
            go.AddComponent<StickyZone>().scale = speedScale;
        }

        void OnTriggerStay(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body != null && body.TryGetComponent(out BabyController baby))
                BabyEffects.On(baby).StickTo(scale);
        }
    }

    /// <summary>Bath bubbles that float up around the arena (decoration).</summary>
    public sealed class DriftingBubbles : MonoBehaviour
    {
        Transform[] bubbles;
        Vector3[] origins;

        public static void Create(Transform root, int count)
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(root, false);
            var self = go.AddComponent<DriftingBubbles>();
            self.bubbles = new Transform[count];
            self.origins = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float radius = 0.08f + i % 4 * 0.035f;
                self.bubbles[i] = new ProcMesh().Sphere(Vector3.zero, radius, new Color(0.9f, 1f, 1f)).Spawn("Bubble", go.transform, Vector3.zero, ProcMesh.Transparent).transform;
                Mesh mesh = self.bubbles[i].GetComponent<MeshFilter>().sharedMesh;
                var colors = new Color[mesh.vertexCount];
                for (int v = 0; v < colors.Length; v++)
                    colors[v] = new Color(0.9f, 1f, 1f, 0.35f);
                mesh.colors = colors;
                self.origins[i] = new Vector3(Mathf.Sin(i * 12.9f) * 9f, i * 0.37f, Mathf.Sin(i * 7.7f) * 6f);
            }
        }

        void Update()
        {
            for (int i = 0; i < bubbles.Length; i++)
            {
                float y = Mathf.Repeat(origins[i].y + Time.time * (0.3f + i % 3 * 0.1f), 2.2f);
                bubbles[i].localPosition = new Vector3(origins[i].x + Mathf.Sin(Time.time + i) * 0.4f, 0.3f + y, origins[i].z);
            }
        }
    }

    /// <summary>Rotating lawn sprinkler: its jet pushes babies outward.</summary>
    public sealed class Sprinkler : MonoBehaviour
    {
        const float TurnSpeed = 55f;
        const float Range = 6f;
        const float HalfAngle = 16f;
        const float Force = 26f;

        RoundManager round;
        Transform jets;

        public static void Create(Transform root, RoundManager manager, Vector3 position)
        {
            var go = new GameObject("Sprinkler");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var self = go.AddComponent<Sprinkler>();
            self.round = manager;
            Color metal = new Color(0.4f, 0.65f, 0.95f);
            new ProcMesh().Cylinder(Vector3.zero, 0.16f, 0.35f, metal).Sphere(new Vector3(0f, 0.4f, 0f), 0.15f, new Color(1f, 0.8f, 0.3f)).Spawn("Post", go.transform, Vector3.zero);
            self.jets = new GameObject("Jets").transform;
            self.jets.SetParent(go.transform, false);
            for (int side = 0; side < 2; side++)
            {
                var jet = new ProcMesh();
                for (int i = 0; i < 9; i++)
                {
                    float u = (i + 1) / 9f;
                    jet.Disc(new Vector3(0f, Mathf.Sin(u * Mathf.PI) * 0.45f - position.y * u * 0.9f, u * Range), 0f, 0.1f + u * 0.75f, new Color(0.65f, 0.9f, 1f, 0.55f - u * 0.35f), 14);
                }
                Transform t = jet.Spawn("Jet", self.jets, new Vector3(0f, 0.4f, 0f), ProcMesh.Transparent).transform;
                t.localRotation = Quaternion.Euler(0f, side * 180f, 0f);
            }
        }

        void FixedUpdate()
        {
            jets.localRotation = Quaternion.Euler(0f, Time.time * TurnSpeed, 0f);
            if (round.Phase != RoundPhase.Playing)
                return;
            foreach (BabyController baby in round.Babies)
            {
                Vector3 offset = baby.transform.position - transform.position;
                offset.y = 0f;
                if (!baby.IsActiveInRound || offset.magnitude > Range)
                    continue;
                float angle = Vector3.Angle(offset, jets.forward);
                if (angle < HalfAngle || angle > 180f - HalfAngle)
                    baby.Movement.Push(offset.normalized * (Force * Time.fixedDeltaTime));
            }
        }
    }

    /// <summary>A robot vacuum that patrols the daycare and bumps into babies.</summary>
    public sealed class RobotVacuum : MonoBehaviour
    {
        const float Speed = 2.6f;
        const float BumpRadius = 0.95f;
        const float BumpCooldown = 1.2f;

        static readonly Vector3[] route = { new Vector3(-6.6f, 0f, 3.8f), new Vector3(6.6f, 0f, 3.8f), new Vector3(6.6f, 0f, 0.2f), new Vector3(6.6f, 0f, -3.8f), new Vector3(-6.6f, 0f, -3.8f), new Vector3(-6.6f, 0f, 0.2f) };

        RoundManager round;
        int target = 1;
        readonly float[] cooldowns = new float[MatchSetup.MaxPlayers];

        public static void Create(Transform root, RoundManager manager)
        {
            Color shell = new Color(0.95f, 0.95f, 1f), dark = new Color(0.3f, 0.3f, 0.4f);
            GameObject go = new ProcMesh().Cylinder(new Vector3(0f, 0.04f, 0f), 0.62f, 0.24f, shell).Cylinder(new Vector3(0f, 0.02f, 0f), 0.66f, 0.12f, new Color(1f, 0.5f, 0.6f))
                .Cylinder(new Vector3(0f, 0.27f, 0f), 0.3f, 0.06f, dark).Sphere(new Vector3(0.2f, 0.3f, 0.36f), 0.09f, Color.white).Sphere(new Vector3(-0.2f, 0.3f, 0.36f), 0.09f, Color.white)
                .Sphere(new Vector3(0.2f, 0.32f, 0.43f), 0.05f, dark).Sphere(new Vector3(-0.2f, 0.32f, 0.43f), 0.05f, dark).Spawn("RobotVacuum", root, route[0]);
            go.AddComponent<RobotVacuum>().round = manager;
        }

        void FixedUpdate()
        {
            Vector3 to = route[target] - transform.localPosition;
            float step = Speed * Time.fixedDeltaTime;
            if (to.magnitude <= step)
                target = (target + 1) % route.Length;
            else
                transform.localPosition += to.normalized * step;
            if (to.sqrMagnitude > 0.01f)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(to), 240f * Time.fixedDeltaTime);
            if (round.Phase != RoundPhase.Playing)
                return;
            foreach (BabyController baby in round.Babies)
            {
                int slot = baby.PlayerIndex;
                cooldowns[slot] -= Time.fixedDeltaTime;
                Vector3 offset = baby.transform.position - transform.position;
                offset.y = 0f;
                if (cooldowns[slot] > 0f || offset.magnitude > BumpRadius || !baby.IsActiveInRound || baby.IsCrying)
                    continue;
                cooldowns[slot] = BumpCooldown;
                baby.ReceiveHit(new HitRequest(4f, 8f, 1f), offset, baby.transform.position + Vector3.up * 0.3f, false);
            }
        }
    }
}
