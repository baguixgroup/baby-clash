using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BabyClash.EditorTools.Art
{
    /// <summary>Procedural textures (saved as PNGs so Unity compresses them and builds mipmaps).</summary>
    public static class TextureFactory
    {
        public static Texture2D WoodFloor(string folder) => Save(folder, "WoodFloor", 1024, 1024, false, (x, y, w, h) =>
        {
            // Planks run along X: 8 rows, staggered joints, per-plank tint, grain and dark seams.
            const int rows = 8;
            float rowHeight = h / (float)rows;
            int row = (int)(y / rowHeight);
            float inRow = (y - row * rowHeight) / rowHeight;
            float plankLength = w * 0.5f;
            float offset = Hash(row, 3) * plankLength;
            int plank = (int)Mathf.Floor((x + offset) / plankLength);
            float along = Mathf.Repeat(x + offset, plankLength) / plankLength;

            float tint = Hash(row * 31 + plank, 7);
            var baseColor = Color.Lerp(new Color(0.84f, 0.62f, 0.4f), new Color(0.93f, 0.74f, 0.5f), tint);
            float grain = Mathf.Sin((y * 0.9f + Mathf.Sin(x * 0.012f + row) * 18f) * 0.35f) * 0.5f + 0.5f;
            baseColor *= Mathf.Lerp(0.9f, 1.04f, grain);
            float knot = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(along, inRow), new Vector2(Hash(plank, row) * 0.8f + 0.1f, 0.5f)) * 9f);
            baseColor = Color.Lerp(baseColor, baseColor * 0.75f, knot * 0.6f);

            bool seam = inRow < 0.035f || along < 0.006f;
            return seam ? baseColor * 0.62f : baseColor;
        });

        public static Texture2D Wallpaper(string folder) => Save(folder, "Wallpaper", 512, 512, false, (x, y, w, h) =>
        {
            var mint = new Color(0.8f, 0.93f, 0.88f);
            var cream = new Color(0.97f, 0.95f, 0.88f);
            float stripe = Mathf.Repeat(x / (w / 4f), 1f);
            Color color = stripe < 0.42f ? cream : mint;
            // Little stars in the mint stripes, alternating heights.
            int column = (int)(x / (w / 4f));
            float cy = Mathf.Repeat(y / (h / 4f) + (column % 2) * 0.5f, 1f);
            float cx = stripe;
            if (stripe >= 0.42f)
            {
                float star = StarShape(new Vector2((cx - 0.71f) * 3.2f, (cy - 0.5f) * 3.2f));
                color = Color.Lerp(color, new Color(1f, 0.86f, 0.45f), star);
            }
            return color;
        });

        public static Texture2D PuzzleMat(string folder) => Save(folder, "PuzzleMat", 1024, 1024, false, (x, y, w, h) =>
        {
            // 2x2 interlocking foam tiles; each tile has jigsaw nubs on its edges.
            Color[] colors =
            {
                new Color(0.99f, 0.73f, 0.78f), new Color(0.72f, 0.86f, 1f),
                new Color(1f, 0.9f, 0.55f), new Color(0.7f, 0.93f, 0.78f),
            };
            float tile = w / 2f;
            float u = x / tile, v = y / tile;
            int tx = (int)u, ty = (int)v;
            float fx = u - tx, fy = v - ty;
            int owner = (tx + ty * 2) % 4;

            // Nubs: circles centred on edge midpoints belong to the neighbour on alternating sides.
            const float nubRadius = 0.11f;
            if (Vector2.Distance(new Vector2(fx, fy), new Vector2(1.02f, 0.5f)) < nubRadius)
                owner = ((tx + 1) % 2 + ty * 2) % 4;
            else if (Vector2.Distance(new Vector2(fx, fy), new Vector2(0.5f, -0.02f)) < nubRadius)
                owner = (tx + ((ty + 1) % 2) * 2) % 4;
            else if (Vector2.Distance(new Vector2(fx, fy), new Vector2(-0.02f, 0.5f)) < nubRadius * 0.8f)
                owner = ((tx + 1) % 2 + ty * 2) % 4;

            Color color = colors[owner];
            float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
            if (edge < 0.012f)
                color *= 0.88f;
            // Subtle foam texture.
            color *= 0.97f + Hash((int)x * 7 + (int)y * 13, 1) * 0.03f;
            return color;
        });

        public static Texture2D WindowSky(string folder) => Save(folder, "WindowSky", 256, 256, true, (x, y, w, h) =>
        {
            float v = y / h;
            Color color = Color.Lerp(new Color(0.78f, 0.9f, 1f), new Color(0.45f, 0.7f, 0.98f), v);
            float cloud = Cloud(x / w, v, 0.3f, 0.35f) + Cloud(x / w, v, 0.72f, 0.62f);
            return Color.Lerp(color, Color.white, Mathf.Clamp01(cloud));
        });

        public static Texture2D PictureSun(string folder) => Save(folder, "PictureSun", 256, 256, true, (x, y, w, h) =>
        {
            var p = new Vector2(x / w - 0.5f, y / h - 0.5f);
            Color color = new Color(0.62f, 0.85f, 0.98f);
            float angle = Mathf.Atan2(p.y, p.x);
            float rays = Mathf.Abs(Mathf.Sin(angle * 6f)) > 0.6f && p.magnitude < 0.36f ? 1f : 0f;
            color = Color.Lerp(color, new Color(1f, 0.8f, 0.3f), rays);
            if (p.magnitude < 0.22f)
                color = new Color(1f, 0.85f, 0.25f);
            return color;
        });

        public static Texture2D PictureMoon(string folder) => Save(folder, "PictureMoon", 256, 256, true, (x, y, w, h) =>
        {
            var p = new Vector2(x / w - 0.5f, y / h - 0.5f);
            Color color = new Color(0.2f, 0.25f, 0.5f);
            bool moon = p.magnitude < 0.26f && Vector2.Distance(p, new Vector2(0.1f, 0.06f)) > 0.22f;
            if (moon)
                color = new Color(1f, 0.93f, 0.6f);
            float stars = StarShape((p - new Vector2(0.28f, 0.28f)) * 12f) + StarShape((p - new Vector2(-0.3f, 0.2f)) * 14f)
                          + StarShape((p - new Vector2(0.25f, -0.3f)) * 16f);
            return Color.Lerp(color, new Color(1f, 0.95f, 0.7f), Mathf.Clamp01(stars));
        });

        public static Texture2D PictureRainbow(string folder) => Save(folder, "PictureRainbow", 256, 256, true, (x, y, w, h) =>
        {
            var p = new Vector2(x / w - 0.5f, y / h - 0.3f);
            Color color = new Color(0.98f, 0.95f, 0.88f);
            Color[] bands =
            {
                new Color(1f, 0.5f, 0.5f), new Color(1f, 0.75f, 0.4f), new Color(1f, 0.92f, 0.45f),
                new Color(0.6f, 0.88f, 0.55f), new Color(0.5f, 0.75f, 1f),
            };
            float r = p.magnitude;
            if (p.y > 0f && r > 0.17f && r < 0.42f)
                color = bands[Mathf.Clamp((int)((0.42f - r) / 0.05f), 0, bands.Length - 1)];
            if (Cloud(x / w, y / h, 0.25f, 0.3f) > 0.5f || Cloud(x / w, y / h, 0.75f, 0.3f) > 0.5f)
                color = Color.white;
            return color;
        });

        /// <summary>White rounded rectangle with soft anti-aliased corners (9-sliced UI panels and buttons).</summary>
        public static Texture2D RoundedRect(string folder, int size, float radius) => Save(folder, "UiRoundedRect", size, size, true, (x, y, w, h) =>
        {
            float px = Mathf.Min(x + 0.5f, w - x - 0.5f);
            float py = Mathf.Min(y + 0.5f, h - y - 0.5f);
            float d = px < radius && py < radius ? radius - Vector2.Distance(new Vector2(px, py), new Vector2(radius, radius)) : Mathf.Min(px, py);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(d));
        }, true);

        public static Texture2D Circle(string folder, int size) => Save(folder, "UiCircle", size, size, true, (x, y, w, h) =>
        {
            float r = w * 0.5f;
            float d = r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(d));
        }, true);

        public static Texture2D SoftDot(string folder) => Save(folder, "SoftDot", 64, 64, true, (x, y, w, h) =>
        {
            float r = w * 0.5f;
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
            return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, 1f - d));
        }, true);

        public static Texture2D StarSprite(string folder) => Save(folder, "UiStar", 128, 128, true, (x, y, w, h) =>
        {
            var p = new Vector2((x + 0.5f) / w - 0.5f, (y + 0.5f) / h - 0.5f) * 2.1f;
            return new Color(1f, 1f, 1f, StarShape(p));
        }, true);

        static float StarShape(Vector2 p)
        {
            float angle = Mathf.Atan2(p.y, p.x) + Mathf.PI * 0.5f;
            float wave = Mathf.Cos(angle * 5f) * 0.5f + 0.5f;
            float radius = Mathf.Lerp(0.42f, 0.95f, Mathf.Pow(wave, 2.2f));
            return Mathf.Clamp01((radius - p.magnitude) * 14f);
        }

        static float Cloud(float u, float v, float cu, float cv)
        {
            float c = 0f;
            c += Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(cu, cv)) * 9f);
            c += Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(cu + 0.08f, cv + 0.04f)) * 8f);
            c += Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(cu - 0.08f, cv + 0.02f)) * 10f);
            return c * 3f;
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        static Texture2D Save(string folder, string name, int width, int height, bool clamp,
            Func<float, float, int, int, Color> pixel, bool sprite = false)
        {
            string path = $"{folder}/{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y, width, height);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.anisoLevel = sprite ? 0 : 4;
            importer.mipmapEnabled = !sprite;
            importer.alphaIsTransparency = sprite;
            if (sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                if (name == "UiRoundedRect")
                    importer.spriteBorder = new Vector4(width * 0.4f, height * 0.4f, width * 0.4f, height * 0.4f);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Sprite SpriteAt(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
    }
}
