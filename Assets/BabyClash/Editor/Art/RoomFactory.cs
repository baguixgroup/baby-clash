using System.Collections.Generic;
using BabyClash.EditorTools.Meshing;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabyClash.EditorTools.Art
{
    /// <summary>
    /// Builds the nursery around the arena: wood floor, foam puzzle mat, playpen fence, wallpapered walls with a
    /// window, and furniture/toys for set dressing. Gameplay colliders are simple boxes; decoration has none.
    /// The arena is centred on the origin; +Z points away from the camera.
    /// </summary>
    public static class RoomFactory
    {
        const float FenceInset = 0.3f;
        const float FenceHeight = 1f;
        const float BackWallZ = 9.5f;
        const float SideWallX = 17f;
        const float WallHeight = 4.5f;

        static readonly Color Cream = new Color(0.98f, 0.95f, 0.88f);
        static readonly Color Pink = new Color(1f, 0.72f, 0.78f);
        static readonly Color Sky = new Color(0.62f, 0.8f, 1f);
        static readonly Color Mint = new Color(0.66f, 0.9f, 0.76f);
        static readonly Color Butter = new Color(1f, 0.88f, 0.5f);
        static readonly Color Lavender = new Color(0.78f, 0.7f, 0.98f);
        static readonly Color WoodTone = new Color(0.86f, 0.66f, 0.46f);

        public static Transform Build(ArtKit kit, Vector2 arenaHalf, Transform parent)
        {
            Transform room = GeneratedAssets.Child("Nursery", parent, Vector3.zero);
            BuildFloor(kit, arenaHalf, room);
            BuildFence(kit, arenaHalf, room);
            BuildWalls(kit, room);
            BuildWindow(kit, room, new Vector3(-4.5f, 0f, BackWallZ));
            BuildBookshelf(kit, room, new Vector3(5.2f, 0f, BackWallZ - 0.35f));
            BuildCrib(kit, room, new Vector3(-12.3f, 0f, 6.3f));
            BuildToyChest(kit, room, new Vector3(11.8f, 0f, 7f));
            BuildTeddy(kit, room, new Vector3(9.4f, 0f, 7.6f), -20f);
            BuildStackingRings(kit, room, new Vector3(-10.6f, 0f, -5.6f));
            BuildBeachBall(kit, room, new Vector3(11.2f, 0f, -5.4f));
            BuildBlockPile(kit, room, new Vector3(-10.6f, 0f, 2.2f));
            BuildLamp(kit, room, new Vector3(14.6f, 0f, 8.4f));
            BuildWallDecor(kit, room);
            return room;
        }

        /// <summary>Gameplay obstacles inside the arena (with colliders): two giant foam blocks and a pouf.</summary>
        public static void BuildObstacles(ArtKit kit, Transform parent)
        {
            Mesh red = ItemFactory.BlockMesh("FoamBlockRed", new Color(1f, 0.5f, 0.5f), new Color(1f, 0.86f, 0.74f));
            Mesh blue = ItemFactory.BlockMesh("FoamBlockBlue", new Color(0.5f, 0.68f, 1f), new Color(0.84f, 0.92f, 1f));
            const float scale = 3.7f; // 0.38 m toy block -> 1.4 m foam block
            // Point-symmetric so neither spawn has an advantage.
            FoamBlock(kit, "FoamBlock_Red", red, new Vector3(-3.2f, 0.7f, 2f), 18f, scale, parent);
            FoamBlock(kit, "FoamBlock_Blue", blue, new Vector3(3.2f, 0.7f, -2f), 18f, scale, parent);

            var pouf = new SdfModel { BaseColor = Lavender };
            pouf.Add(new SdfEllipsoid(new Vector3(0f, 0.3f, 0f), new Vector3(0.95f, 0.32f, 0.95f)));
            pouf.Add(new SdfCustom(p => 0.02f - p.y), SdfOp.Intersect, 0.05f); // keep what's above the floor
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                pouf.Add(new SdfSphere(new Vector3(Mathf.Cos(a) * 0.62f, 0.4f, Mathf.Sin(a) * 0.62f), 0.3f), SdfOp.Union, 0.12f);
            }
            pouf.Add(new SdfSphere(new Vector3(0f, 0.62f, 0f), 0.08f), SdfOp.Subtract, 0.06f);
            pouf.Add(new SdfSphere(new Vector3(0f, 0.6f, 0f), 0.05f), SdfOp.Union, 0.02f, new Color(0.6f, 0.5f, 0.9f));
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                pouf.Add(new SdfCustom(p => Mathf.Abs(Vector3.Dot(new Vector3(p.x, 0f, p.z), new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)))) - 0.02f
                    + Mathf.Max(0f, -Vector3.Dot(new Vector3(p.x, 0f, p.z), new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))))),
                    SdfOp.Paint, 0.02f, new Color(0.7f, 0.62f, 0.95f));
            }
            var poufBounds = new Bounds(new Vector3(0f, 0.35f, 0f), new Vector3(2.1f, 0.8f, 2.1f));
            Mesh poufMesh = Save(pouf.Polygonize(poufBounds, 0.03f), "Pouf");
            GameObject poufObject = GeneratedAssets.MeshObject("Pouf", parent, poufMesh, kit.Fabric, Vector3.zero);
            // A low-poly convex hull is plenty for bumping into, and far cheaper than the 24k-triangle render mesh.
            var collider = poufObject.AddComponent<MeshCollider>();
            collider.sharedMesh = GeneratedAssets.SaveMesh(PoufHull(), "PoufCollider");
            collider.convex = true;
        }

        /// <summary>Stacked rings following the pouf's silhouette (radius by height), well under PhysX's 256-face hull limit.</summary>
        static Mesh PoufHull()
        {
            Vector2[] rings = { new Vector2(0.9f, 0f), new Vector2(0.97f, 0.3f), new Vector2(0.82f, 0.55f), new Vector2(0.45f, 0.7f) };
            const int segments = 16;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector2 ring in rings)
            {
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * ring.x, ring.y, Mathf.Sin(a) * ring.x));
                }
            }
            for (int r = 0; r < rings.Length - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * segments + s, b = r * segments + (s + 1) % segments;
                    triangles.AddRange(new[] { a, a + segments, b, b, a + segments, b + segments });
                }
            }
            var mesh = new Mesh { name = "PoufCollider" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void FoamBlock(ArtKit kit, string name, Mesh mesh, Vector3 position, float yaw, float scale, Transform parent)
        {
            GameObject block = GeneratedAssets.MeshObject(name, parent, mesh, kit.Plastic, position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            var box = block.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.38f;
        }

        static void BuildFloor(ArtKit kit, Vector2 arenaHalf, Transform room)
        {
            Mesh floor = Save(Shapes.Plane(new Vector2(44f, 32f), 2f, Color.white, 4), "WoodFloor");
            GameObject floorObject = GeneratedAssets.MeshObject("WoodFloor", room, floor, kit.Floor, new Vector3(0f, 0f, 0f));
            floorObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var collider = new GameObject("FloorCollider").AddComponent<BoxCollider>();
            collider.transform.SetParent(room, false);
            collider.center = new Vector3(0f, -0.5f, 0f);
            collider.size = new Vector3(60f, 1f, 50f);

            Vector2 matSize = (arenaHalf + Vector2.one * FenceInset) * 2f;
            Mesh mat = Save(Shapes.Plane(matSize, 2f, Color.white, 2), "PuzzleMat");
            GameObject matObject = GeneratedAssets.MeshObject("PuzzleMat", room, mat, kit.Mat, new Vector3(0f, 0.012f, 0f));
            matObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildFence(ArtKit kit, Vector2 arenaHalf, Transform room)
        {
            var fence = new MeshBuilder();
            float hx = arenaHalf.x + FenceInset;
            float hz = arenaHalf.y + FenceInset;
            Color[] railColors = { Pink, Sky, Butter, Mint };
            var corners = new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz) };
            MeshBuilder bar = Shapes.SoftCylinder(0.035f, FenceHeight - 0.1f, 0.03f, Cream, 10);
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side];
                Vector3 b = corners[(side + 1) % 4];
                Vector3 along = b - a;
                float length = along.magnitude;
                Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, along.normalized));
                Vector3 mid = (a + b) * 0.5f;
                Color rail = railColors[side];
                fence.Append(Shapes.RoundedBox(new Vector3(length, 0.14f, 0.16f), 0.07f, rail, 4), mid + Vector3.up * FenceHeight, rotation, Vector3.one);
                fence.Append(Shapes.RoundedBox(new Vector3(length, 0.1f, 0.12f), 0.05f, rail, 4), mid + Vector3.up * 0.08f, rotation, Vector3.one);
                int bars = Mathf.RoundToInt(length / 0.32f);
                for (int i = 1; i < bars; i++)
                    fence.Append(bar, Vector3.Lerp(a, b, i / (float)bars) + Vector3.up * 0.08f, Quaternion.identity, Vector3.one);
            }
            MeshBuilder post = Shapes.SoftCylinder(0.09f, FenceHeight + 0.05f, 0.04f, Cream, 16);
            for (int i = 0; i < 4; i++)
            {
                fence.Append(post, corners[i], Quaternion.identity, Vector3.one);
                fence.Append(Shapes.Sphere(0.13f, railColors[i], 16), corners[i] + Vector3.up * (FenceHeight + 0.14f), Quaternion.identity, Vector3.one);
            }
            Mesh mesh = Save(fence, "PlaypenFence");
            GeneratedAssets.MeshObject("PlaypenFence", room, mesh, kit.Painted, Vector3.zero);
        }

        static void BuildWalls(ArtKit kit, Transform room)
        {
            // Back wall faces the camera (-Z); side walls face inward.
            Mesh back = Save(Shapes.Plane(new Vector2(2f * SideWallX, WallHeight), 1f, Color.white, 2), "WallBack");
            GeneratedAssets.MeshObject("WallBack", room, back, kit.Wallpaper, new Vector3(0f, WallHeight * 0.5f, BackWallZ), Quaternion.Euler(-90f, 0f, 0f));
            Mesh side = Save(Shapes.Plane(new Vector2(BackWallZ + 14f, WallHeight), 1f, Color.white, 2), "WallSide");
            float sideCenterZ = (BackWallZ - 14f) * 0.5f;
            GeneratedAssets.MeshObject("WallLeft", room, side, kit.Wallpaper, new Vector3(-SideWallX, WallHeight * 0.5f, sideCenterZ), Quaternion.Euler(-90f, -90f, 0f));
            GeneratedAssets.MeshObject("WallRight", room, side, kit.Wallpaper, new Vector3(SideWallX, WallHeight * 0.5f, sideCenterZ), Quaternion.Euler(-90f, 90f, 0f));

            var trim = new MeshBuilder();
            trim.Append(Shapes.RoundedBox(new Vector3(2f * SideWallX, 0.28f, 0.1f), 0.03f, Color.white, 3), new Vector3(0f, 0.14f, BackWallZ - 0.05f), Quaternion.identity, Vector3.one);
            trim.Append(Shapes.RoundedBox(new Vector3(0.1f, 0.28f, BackWallZ + 14f), 0.03f, Color.white, 3), new Vector3(-SideWallX + 0.05f, 0.14f, sideCenterZ), Quaternion.identity, Vector3.one);
            trim.Append(Shapes.RoundedBox(new Vector3(0.1f, 0.28f, BackWallZ + 14f), 0.03f, Color.white, 3), new Vector3(SideWallX - 0.05f, 0.14f, sideCenterZ), Quaternion.identity, Vector3.one);
            trim.Append(Shapes.RoundedBox(new Vector3(2f * SideWallX, 0.1f, 0.08f), 0.03f, Color.white, 3), new Vector3(0f, 1.05f, BackWallZ - 0.04f), Quaternion.identity, Vector3.one);
            GeneratedAssets.MeshObject("Trim", room, Save(trim, "WallTrim"), kit.Trim, Vector3.zero);
        }

        static void BuildWindow(ArtKit kit, Transform room, Vector3 wallPoint)
        {
            const float width = 3f, height = 2f, bottom = 1.25f;
            Vector3 center = wallPoint + new Vector3(0f, bottom + height * 0.5f, -0.02f);
            Mesh glass = Save(Shapes.Quad(new Vector2(width, height), Color.white), "WindowGlass");
            GeneratedAssets.MeshObject("WindowSky", room, glass, kit.Glass, center, Quaternion.Euler(-90f, 0f, 0f), null, false);

            var frame = new MeshBuilder();
            Color white = Color.white;
            frame.Append(Shapes.RoundedBox(new Vector3(width + 0.3f, 0.16f, 0.16f), 0.05f, white, 3), center + new Vector3(0f, height * 0.5f + 0.06f, -0.04f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(width + 0.5f, 0.12f, 0.36f), 0.05f, white, 3), center + new Vector3(0f, -height * 0.5f - 0.05f, -0.14f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(0.16f, height + 0.2f, 0.16f), 0.05f, white, 3), center + new Vector3(-width * 0.5f - 0.06f, 0f, -0.04f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(0.16f, height + 0.2f, 0.16f), 0.05f, white, 3), center + new Vector3(width * 0.5f + 0.06f, 0f, -0.04f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(0.08f, height, 0.08f), 0.03f, white, 3), center + new Vector3(0f, 0f, -0.03f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(width, 0.08f, 0.08f), 0.03f, white, 3), center + new Vector3(0f, 0.1f, -0.03f), Quaternion.identity, Vector3.one);
            GeneratedAssets.MeshObject("WindowFrame", room, Save(frame, "WindowFrame"), kit.Painted, Vector3.zero);

            // Pleated curtains on both sides.
            var curtain = new SdfModel { BaseColor = Butter };
            curtain.Add(new SdfCustom(p =>
            {
                float wave = Mathf.Abs(p.z - Mathf.Sin(p.x * 22f) * 0.05f) - 0.03f;
                float box = Mathf.Max(Mathf.Abs(p.x) - 0.4f, Mathf.Abs(p.y) - 1.25f);
                return Mathf.Max(wave, box);
            }));
            curtain.Add(new SdfCustom(p => Mathf.Abs(p.y + 0.95f) - 0.06f), SdfOp.Paint, 0.01f, new Color(1f, 0.7f, 0.5f));
            Mesh curtainMesh = Save(curtain.Polygonize(new Bounds(Vector3.zero, new Vector3(0.9f, 2.6f, 0.2f)), 0.02f), "Curtain");
            for (int side = -1; side <= 1; side += 2)
                GeneratedAssets.MeshObject("Curtain", room, curtainMesh, kit.Fabric, center + new Vector3((width * 0.5f + 0.45f) * side, 0.05f, -0.18f));
        }

        static void BuildBookshelf(ArtKit kit, Transform room, Vector3 position)
        {
            var shelf = new MeshBuilder();
            const float width = 2.6f, height = 2f, depth = 0.5f;
            Color frame = new Color(1f, 0.97f, 0.93f);
            shelf.Append(Shapes.RoundedBox(new Vector3(0.1f, height, depth), 0.03f, frame, 3), new Vector3(-width * 0.5f, height * 0.5f, 0f), Quaternion.identity, Vector3.one);
            shelf.Append(Shapes.RoundedBox(new Vector3(0.1f, height, depth), 0.03f, frame, 3), new Vector3(width * 0.5f, height * 0.5f, 0f), Quaternion.identity, Vector3.one);
            float[] levels = { 0.06f, 0.7f, 1.35f, height - 0.05f };
            foreach (float level in levels)
                shelf.Append(Shapes.RoundedBox(new Vector3(width + 0.1f, 0.08f, depth), 0.03f, frame, 3), new Vector3(0f, level, 0f), Quaternion.identity, Vector3.one);
            shelf.Append(Shapes.RoundedBox(new Vector3(width, height, 0.04f), 0.01f, new Color(0.94f, 0.88f, 0.98f), 2), new Vector3(0f, height * 0.5f, depth * 0.5f - 0.02f), Quaternion.identity, Vector3.one);

            // Books with varied colours and heights, a few leaning.
            Color[] bookColors = { Pink, Sky, Butter, Mint, Lavender, new Color(1f, 0.6f, 0.45f) };
            var random = new System.Random(4);
            for (int row = 0; row < 3; row++)
            {
                float x = -width * 0.5f + 0.12f;
                float baseY = levels[row] + 0.04f;
                while (x < width * 0.5f - 0.2f)
                {
                    float w = 0.07f + (float)random.NextDouble() * 0.06f;
                    float h = 0.34f + (float)random.NextDouble() * 0.18f;
                    Color color = bookColors[random.Next(bookColors.Length)];
                    float lean = random.NextDouble() < 0.12 ? 12f : 0f;
                    shelf.Append(Shapes.RoundedBox(new Vector3(w, h, 0.32f), 0.015f, color, 2),
                        new Vector3(x + w * 0.5f, baseY + h * 0.5f, -0.02f), Quaternion.Euler(0f, 0f, -lean), Vector3.one);
                    x += w + (lean > 0f ? 0.05f : 0.008f);
                    if (random.NextDouble() < 0.15)
                        x += 0.25f;
                }
            }
            GeneratedAssets.MeshObject("Bookshelf", room, Save(shelf, "Bookshelf"), kit.Painted, position);

            // A duck and a block keep the books company.
            GeneratedAssets.MeshObject("ShelfDuck", room, ItemFactory.DuckMesh(), kit.Plastic, position + new Vector3(0.9f, 1.5f, -0.05f), Quaternion.Euler(0f, 200f, 0f), Vector3.one * 0.8f);
        }

        static void BuildCrib(ArtKit kit, Transform room, Vector3 position)
        {
            var crib = new MeshBuilder();
            const float length = 3f, width = 1.5f, height = 1.15f;
            MeshBuilder bar = Shapes.SoftCylinder(0.03f, height - 0.35f, 0.025f, Cream, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                crib.Append(Shapes.RoundedBox(new Vector3(length, 0.1f, 0.12f), 0.05f, Sky, 3), new Vector3(0f, height, width * 0.5f * side), Quaternion.identity, Vector3.one);
                crib.Append(Shapes.RoundedBox(new Vector3(length, 0.1f, 0.1f), 0.04f, Sky, 3), new Vector3(0f, 0.35f, width * 0.5f * side), Quaternion.identity, Vector3.one);
                crib.Append(Shapes.RoundedBox(new Vector3(0.12f, 0.1f, width), 0.05f, Sky, 3), new Vector3(length * 0.5f * side, height, 0f), Quaternion.identity, Vector3.one);
                for (int i = 1; i < 12; i++)
                    crib.Append(bar, new Vector3(Mathf.Lerp(-length * 0.5f, length * 0.5f, i / 12f), 0.35f, width * 0.5f * side), Quaternion.identity, Vector3.one);
                for (int i = 1; i < 5; i++)
                    crib.Append(bar, new Vector3(length * 0.5f * side, 0.35f, Mathf.Lerp(-width * 0.5f, width * 0.5f, i / 5f)), Quaternion.identity, Vector3.one);
            }
            MeshBuilder post = Shapes.SoftCylinder(0.07f, height + 0.15f, 0.04f, Cream, 12);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    crib.Append(post, new Vector3(length * 0.5f * sx, 0f, width * 0.5f * sz), Quaternion.identity, Vector3.one);
                    crib.Append(Shapes.Sphere(0.1f, Butter, 12), new Vector3(length * 0.5f * sx, height + 0.2f, width * 0.5f * sz), Quaternion.identity, Vector3.one);
                }
            }
            crib.Append(Shapes.RoundedBox(new Vector3(length - 0.1f, 0.22f, width - 0.1f), 0.08f, Color.white, 4), new Vector3(0f, 0.5f, 0f), Quaternion.identity, Vector3.one);
            crib.Append(Shapes.RoundedBox(new Vector3(0.7f, 0.14f, 0.5f), 0.07f, Pink, 4), new Vector3(-1f, 0.66f, 0f), Quaternion.identity, Vector3.one);
            // Mobile: arm, ring and hanging stars.
            crib.Append(Shapes.SoftCylinder(0.03f, 1.1f, 0.02f, Cream, 8), new Vector3(length * 0.5f, height, 0f), Quaternion.identity, Vector3.one);
            crib.Append(Shapes.RoundedBox(new Vector3(1f, 0.05f, 0.05f), 0.02f, Cream, 2), new Vector3(length * 0.5f - 0.5f, height + 1.1f, 0f), Quaternion.identity, Vector3.one);
            Vector3 ringCenter = new Vector3(length * 0.5f - 1f, height + 1.05f, 0f);
            crib.Append(Shapes.Torus(0.35f, 0.025f, Pink, 24, 6), ringCenter, Quaternion.identity, Vector3.one);
            Color[] charms = { Butter, Sky, Mint, Lavender, Pink };
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Vector3 anchor = ringCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.35f;
                crib.Append(Shapes.Tube(new List<Vector3> { anchor, anchor + Vector3.down * 0.3f }, 0.006f, 0.006f, Color.white, 4), Matrix4x4.identity);
                crib.Append(Shapes.Star(0.09f, 0.04f, 0.04f, charms[i]), anchor + Vector3.down * 0.38f, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), Vector3.one);
            }
            GeneratedAssets.MeshObject("Crib", room, Save(crib, "Crib"), kit.Painted, position, Quaternion.Euler(0f, 90f, 0f));
        }

        static void BuildToyChest(ArtKit kit, Transform room, Vector3 position)
        {
            var chest = new MeshBuilder();
            chest.Append(Shapes.RoundedBox(new Vector3(1.8f, 0.8f, 1f), 0.1f, Pink, 4), new Vector3(0f, 0.4f, 0f), Quaternion.identity, Vector3.one);
            chest.Append(Shapes.RoundedBox(new Vector3(1.9f, 0.18f, 1.08f), 0.08f, new Color(1f, 0.85f, 0.88f), 4), new Vector3(0f, 0.86f, 0f), Quaternion.identity, Vector3.one);
            chest.Append(Shapes.Star(0.22f, 0.1f, 0.06f, Butter), new Vector3(0f, 0.42f, -0.52f), Quaternion.identity, Vector3.one);
            GeneratedAssets.MeshObject("ToyChest", room, Save(chest, "ToyChest"), kit.Painted, position, Quaternion.Euler(0f, -12f, 0f));
        }

        static void BuildTeddy(ArtKit kit, Transform room, Vector3 position, float yaw)
        {
            var brown = new Color(0.74f, 0.52f, 0.34f);
            var light = new Color(0.94f, 0.8f, 0.62f);
            var dark = new Color(0.2f, 0.12f, 0.1f);
            var teddy = new SdfModel { BaseColor = brown };
            teddy.Add(new SdfEllipsoid(new Vector3(0f, 0.34f, 0f), new Vector3(0.3f, 0.34f, 0.26f)));
            teddy.Add(new SdfSphere(new Vector3(0f, 0.82f, 0.02f), 0.24f), SdfOp.Union, 0.08f);
            for (int side = -1; side <= 1; side += 2)
            {
                teddy.Add(new SdfSphere(new Vector3(0.18f * side, 1.02f, 0f), 0.09f), SdfOp.Union, 0.04f);
                teddy.Add(new SdfSphere(new Vector3(0.18f * side, 1.02f, 0.05f), 0.05f), SdfOp.Paint, 0.02f, light);
                teddy.Add(new SdfTaperedCapsule(new Vector3(0.24f * side, 0.52f, 0.04f), new Vector3(0.36f * side, 0.3f, 0.2f), 0.09f, 0.08f), SdfOp.Union, 0.05f);
                teddy.Add(new SdfTaperedCapsule(new Vector3(0.15f * side, 0.14f, 0.1f), new Vector3(0.2f * side, 0.1f, 0.42f), 0.12f, 0.11f), SdfOp.Union, 0.05f);
                teddy.Add(new SdfSphere(new Vector3(0.2f * side, 0.1f, 0.5f), 0.07f), SdfOp.Paint, 0.02f, light);
                teddy.Add(new SdfSphere(new Vector3(0.09f * side, 0.88f, 0.21f), 0.028f), SdfOp.Paint, 0.006f, dark);
            }
            teddy.Add(new SdfEllipsoid(new Vector3(0f, 0.76f, 0.2f), new Vector3(0.11f, 0.08f, 0.08f)), SdfOp.Union, 0.04f, light);
            teddy.Add(new SdfSphere(new Vector3(0f, 0.79f, 0.29f), 0.035f), SdfOp.Union, 0.01f, dark);
            teddy.Add(new SdfEllipsoid(new Vector3(0f, 0.34f, 0.2f), new Vector3(0.17f, 0.2f, 0.1f)), SdfOp.Paint, 0.03f, light);
            Mesh mesh = Save(teddy.Polygonize(new Bounds(new Vector3(0f, 0.55f, 0.1f), new Vector3(0.9f, 1.15f, 0.9f)), 0.016f), "Teddy");
            GeneratedAssets.MeshObject("Teddy", room, mesh, kit.Fabric, position, Quaternion.Euler(0f, 180f + yaw, 0f), Vector3.one * 1.1f);
        }

        static void BuildStackingRings(ArtKit kit, Transform room, Vector3 position)
        {
            var toy = new MeshBuilder();
            toy.Append(Shapes.SoftCylinder(0.34f, 0.12f, 0.05f, new Color(0.96f, 0.4f, 0.45f), 24), Vector3.zero, Quaternion.identity, Vector3.one);
            toy.Append(Shapes.SoftCylinder(0.05f, 0.9f, 0.03f, Cream, 10), Vector3.zero, Quaternion.identity, Vector3.one);
            Color[] rings = { new Color(1f, 0.55f, 0.35f), Butter, Mint, Sky, Lavender };
            for (int i = 0; i < rings.Length; i++)
            {
                float radius = Mathf.Lerp(0.26f, 0.12f, i / (float)(rings.Length - 1));
                toy.Append(Shapes.Torus(radius, 0.07f, rings[i], 28, 12), new Vector3(0f, 0.19f + i * 0.14f, 0f), Quaternion.identity, Vector3.one);
            }
            toy.Append(Shapes.Sphere(0.1f, Pink, 16), new Vector3(0f, 0.92f, 0f), Quaternion.identity, Vector3.one);
            GeneratedAssets.MeshObject("StackingRings", room, Save(toy, "StackingRings"), kit.Plastic, position, null, Vector3.one * 1.3f);
        }

        static void BuildBeachBall(ArtKit kit, Transform room, Vector3 position)
        {
            MeshBuilder ball = Shapes.Sphere(0.4f, Color.white, 32);
            Color[] stripes = { new Color(1f, 0.45f, 0.45f), Color.white, Sky, Color.white, Butter, Color.white };
            for (int i = 0; i < ball.VertexCount; i++)
            {
                Vector3 p = ball.Vertices[i];
                float longitude = Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f) + 0.5f;
                ball.Colors[i] = Mathf.Abs(p.y) > 0.36f ? Color.white : stripes[Mathf.Clamp((int)(longitude * stripes.Length), 0, stripes.Length - 1)];
            }
            GeneratedAssets.MeshObject("BeachBall", room, Save(ball, "BeachBall"), kit.Plastic, position + Vector3.up * 0.4f, Quaternion.Euler(20f, 30f, 10f));
        }

        static void BuildBlockPile(ArtKit kit, Transform room, Vector3 position)
        {
            Mesh yellow = ItemFactory.BlockMesh("DecoBlockYellow", Butter, Color.white);
            Mesh green = ItemFactory.BlockMesh("DecoBlockGreen", Mint, Color.white);
            Mesh pink = ItemFactory.BlockMesh("DecoBlockPink", Pink, Color.white);
            const float s = 1.6f;
            GeneratedAssets.MeshObject("Block", room, yellow, kit.Plastic, position + new Vector3(0f, 0.3f, 0f), Quaternion.Euler(0f, 10f, 0f), Vector3.one * s);
            GeneratedAssets.MeshObject("Block", room, green, kit.Plastic, position + new Vector3(0.66f, 0.3f, 0.1f), Quaternion.Euler(0f, -14f, 0f), Vector3.one * s);
            GeneratedAssets.MeshObject("Block", room, pink, kit.Plastic, position + new Vector3(0.3f, 0.9f, 0.05f), Quaternion.Euler(0f, 30f, 0f), Vector3.one * s);
            GeneratedAssets.MeshObject("Block", room, green, kit.Plastic, position + new Vector3(-0.4f, 0.3f, -0.8f), Quaternion.Euler(0f, 44f, 0f), Vector3.one * s);
        }

        static void BuildLamp(ArtKit kit, Transform room, Vector3 position)
        {
            var lamp = new MeshBuilder();
            lamp.Append(Shapes.SoftCylinder(0.3f, 0.06f, 0.03f, Cream, 20), Vector3.zero, Quaternion.identity, Vector3.one);
            lamp.Append(Shapes.SoftCylinder(0.035f, 1.9f, 0.02f, Cream, 8), Vector3.zero, Quaternion.identity, Vector3.one);
            GeneratedAssets.MeshObject("LampStand", room, Save(lamp, "LampStand"), kit.Painted, position);
            var shade = new List<Vector2> { new Vector2(0.45f, 0f), new Vector2(0.28f, 0.45f), new Vector2(0f, 0.45f) };
            MeshBuilder shadeMesh = Shapes.Lathe(shade, 24, Color.white);
            GeneratedAssets.MeshObject("LampShade", room, Save(shadeMesh, "LampShade"), kit.LampShade, position + Vector3.up * 1.75f, null, null, false);

            var light = new GameObject("LampLight").AddComponent<Light>();
            light.transform.SetParent(room, false);
            light.transform.localPosition = position + Vector3.up * 1.8f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.58f);
            light.range = 6f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
        }

        static void BuildWallDecor(ArtKit kit, Transform room)
        {
            var stickers = new MeshBuilder();
            var starPositions = new[]
            {
                new Vector3(-8.5f, 2.3f, 0f), new Vector3(-7.2f, 1.7f, 0f), new Vector3(1.5f, 2.4f, 0f),
                new Vector3(-1.2f, 1.6f, 0f), new Vector3(9.2f, 2.2f, 0f), new Vector3(12.5f, 1.8f, 0f),
            };
            Color[] colors = { Butter, Sky, Pink, Mint, Butter, Lavender };
            for (int i = 0; i < starPositions.Length; i++)
            {
                float size = 0.22f + (i % 3) * 0.06f;
                stickers.Append(Shapes.Star(size, size * 0.45f, 0.05f, colors[i]), starPositions[i] + new Vector3(0f, 0f, BackWallZ - 0.04f),
                    Quaternion.Euler(0f, 0f, i * 17f), Vector3.one);
            }

            var cloud = new SdfModel { BaseColor = Color.white };
            cloud.Add(new SdfSphere(new Vector3(0f, 0f, 0f), 0.3f));
            cloud.Add(new SdfSphere(new Vector3(0.32f, -0.06f, 0f), 0.22f), SdfOp.Union, 0.08f);
            cloud.Add(new SdfSphere(new Vector3(-0.32f, -0.07f, 0f), 0.2f), SdfOp.Union, 0.08f);
            cloud.Add(new SdfCustom(p => Mathf.Abs(p.z) - 0.04f), SdfOp.Intersect, 0.03f);
            Mesh cloudMesh = Save(cloud.Polygonize(new Bounds(Vector3.zero, new Vector3(1.2f, 0.7f, 0.2f)), 0.02f), "WallCloud");
            GeneratedAssets.MeshObject("Cloud", room, cloudMesh, kit.Painted, new Vector3(-0.8f, 2.6f, BackWallZ - 0.04f), null, Vector3.one * 1.2f);
            GeneratedAssets.MeshObject("Cloud", room, cloudMesh, kit.Painted, new Vector3(10.5f, 2.9f, BackWallZ - 0.04f));
            GeneratedAssets.MeshObject("WallStars", room, Save(stickers, "WallStars"), kit.Painted, Vector3.zero);

            // Framed pictures.
            Picture(kit, room, kit.PictureSun, new Vector3(-12.2f, 2.3f, BackWallZ), 1.1f);
            Picture(kit, room, kit.PictureMoon, new Vector3(-10.5f, 2.5f, BackWallZ), 0.9f);
            Picture(kit, room, kit.PictureRainbow, new Vector3(5.2f, 2.65f, BackWallZ), 1f);
        }

        static void Picture(ArtKit kit, Transform room, Material art, Vector3 wallPoint, float size)
        {
            Mesh canvas = Save(Shapes.Quad(Vector2.one, Color.white), "PictureCanvas");
            Vector3 center = wallPoint + new Vector3(0f, 0f, -0.05f);
            GeneratedAssets.MeshObject("Picture", room, canvas, art, center, Quaternion.Euler(-90f, 0f, 0f), new Vector3(size, 1f, size), false);
            var frame = new MeshBuilder();
            float half = size * 0.5f + 0.05f;
            frame.Append(Shapes.RoundedBox(new Vector3(size + 0.2f, 0.1f, 0.06f), 0.03f, WoodTone, 2), new Vector3(0f, half, 0f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(size + 0.2f, 0.1f, 0.06f), 0.03f, WoodTone, 2), new Vector3(0f, -half, 0f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(0.1f, size + 0.2f, 0.06f), 0.03f, WoodTone, 2), new Vector3(half, 0f, 0f), Quaternion.identity, Vector3.one);
            frame.Append(Shapes.RoundedBox(new Vector3(0.1f, size + 0.2f, 0.06f), 0.03f, WoodTone, 2), new Vector3(-half, 0f, 0f), Quaternion.identity, Vector3.one);
            GeneratedAssets.MeshObject("PictureFrame", room, Save(frame, $"PictureFrame{Mathf.RoundToInt(size * 10f)}"), kit.Painted, center + new Vector3(0f, 0f, -0.01f));
        }

        static Mesh Save(MeshBuilder builder, string name) => GeneratedAssets.SaveMesh(builder.ToMesh(name), name);
    }
}
