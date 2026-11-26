using System.Collections.Generic;
using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Small runtime shape builder for props, hats and power-ups (the editor sculpts the big meshes). Shapes are added
    /// to one mesh with vertex colours, so a whole prop is a single draw with the game's toon material.
    /// </summary>
    public sealed class ProcMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        /// <summary>The game's toon material, found once from a scene prop.</summary>
        public static Material Toon { get; private set; }
        static Material transparent;

        public static void UseMaterial(Material toon) => Toon = toon;

        /// <summary>Simple alpha-blended, vertex-coloured material (bubbles, zones, water).</summary>
        public static Material Transparent
        {
            get
            {
                if (transparent == null)
                    transparent = new Material(Shader.Find("Sprites/Default")) { name = "RuntimeTransparent" };
                return transparent;
            }
        }

        /// <summary>Surface of revolution around Y: points are (radius, height) from bottom to top.</summary>
        public ProcMesh Lathe(Vector3 center, Color color, params Vector2[] profile) => Lathe(center, Quaternion.identity, Vector3.one, color, 20, profile);

        public ProcMesh Lathe(Vector3 center, Quaternion rotation, Vector3 scale, Color color, int segments, params Vector2[] profile)
        {
            int start = vertices.Count;
            color.a = 1f;
            for (int p = 0; p < profile.Length; p++)
            {
                Vector2 before = profile[Mathf.Max(0, p - 1)];
                Vector2 after = profile[Mathf.Min(profile.Length - 1, p + 1)];
                Vector2 slope = after - before;
                var normal2 = new Vector2(slope.y, -slope.x).normalized;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 point = dir * profile[p].x + Vector3.up * profile[p].y;
                    Vector3 normal = dir * normal2.x + Vector3.up * normal2.y;
                    vertices.Add(center + rotation * Vector3.Scale(point, scale));
                    normals.Add((rotation * new Vector3(normal.x / scale.x, normal.y / scale.y, normal.z / scale.z)).normalized);
                    colors.Add(color);
                }
            }
            for (int p = 0; p < profile.Length - 1; p++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = start + p * (segments + 1) + s, b = a + 1, c = a + segments + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            return this;
        }

        public ProcMesh Sphere(Vector3 center, float radius, Color color) => Ellipsoid(center, Vector3.one * radius, color);

        public ProcMesh Ellipsoid(Vector3 center, Vector3 radii, Color color, Quaternion? rotation = null)
        {
            const int rings = 12;
            var profile = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * i / rings;
                profile[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return Lathe(center, rotation ?? Quaternion.identity, radii, color, 20, profile);
        }

        public ProcMesh Cylinder(Vector3 bottom, float radius, float height, Color color, float topRadius = -1f)
        {
            if (topRadius < 0f)
                topRadius = radius;
            float bevel = Mathf.Min(0.02f, height * 0.2f);
            return Lathe(bottom, color, Vector2.zero, new Vector2(radius - bevel * 0.5f, 0f), new Vector2(radius, bevel),
                new Vector2(topRadius, height - bevel), new Vector2(Mathf.Max(0f, topRadius - bevel * 0.5f), height), new Vector2(0f, height));
        }

        public ProcMesh Cone(Vector3 bottom, float radius, float height, Color color) =>
            Lathe(bottom, color, Vector2.zero, new Vector2(radius, 0f), new Vector2(radius, 0.01f), new Vector2(0.008f, height), new Vector2(0f, height));

        public ProcMesh Torus(Vector3 center, float radius, float thickness, Color color, Quaternion? rotation = null, Vector3? scale = null)
        {
            const int steps = 10;
            var profile = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                profile[i] = new Vector2(radius + Mathf.Sin(a) * thickness, -Mathf.Cos(a) * thickness);
            }
            return Lathe(center, rotation ?? Quaternion.identity, scale ?? Vector3.one, color, 28, profile);
        }

        public ProcMesh Box(Vector3 center, Vector3 size, Color color, Quaternion? rotation = null)
        {
            Quaternion q = rotation ?? Quaternion.identity;
            color.a = 1f;
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in axes)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                int start = vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    float su = i == 0 || i == 3 ? -0.5f : 0.5f;
                    float sv = i < 2 ? -0.5f : 0.5f;
                    vertices.Add(center + q * Vector3.Scale(n * 0.5f + u * su + v * sv, size));
                    normals.Add(q * n);
                    colors.Add(color);
                }
                // Wind each face so it looks along its own normal.
                Vector3 face = Vector3.Cross(vertices[start + 1] - vertices[start], vertices[start + 2] - vertices[start]);
                if (Vector3.Dot(face, q * n) > 0f)
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else
                    triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            return this;
        }

        /// <summary>Flat disc or ring lying on the floor, keeping the colour's alpha (for transparent materials).</summary>
        public ProcMesh Disc(Vector3 center, float inner, float outer, Color color, int segments = 40)
        {
            int start = vertices.Count;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(center + dir * inner);
                vertices.Add(center + dir * outer);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                colors.Add(color);
                colors.Add(color);
            }
            for (int s = 0; s < segments; s++)
            {
                int a = start + s * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            return this;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public GameObject Spawn(string name, Transform parent, Vector3 localPosition, Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null ? material : Toon;
            if (material != null && material == transparent)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
