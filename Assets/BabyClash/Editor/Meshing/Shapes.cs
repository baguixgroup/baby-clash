using System.Collections.Generic;
using UnityEngine;

namespace BabyClash.EditorTools.Meshing
{
    /// <summary>Analytic mesh generators with smooth normals, for props and furniture.</summary>
    public static class Shapes
    {
        /// <summary>Box with rounded edges and corners (radius clamped to half the smallest side).</summary>
        public static MeshBuilder RoundedBox(Vector3 size, float radius, Color color, int segments = 6)
        {
            var builder = new MeshBuilder();
            Vector3 half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) - 1e-4f);
            Vector3 inner = half - Vector3.one * radius;

            // Each face is a grid; vertices are pushed out from the inner box, which rounds edges and corners.
            int n = Mathf.Max(2, segments * 2 + 2);
            AddRoundedFace(builder, Vector3.right, Vector3.forward, Vector3.up, half, inner, radius, n, color);
            AddRoundedFace(builder, Vector3.left, Vector3.back, Vector3.up, half, inner, radius, n, color);
            AddRoundedFace(builder, Vector3.up, Vector3.right, Vector3.forward, half, inner, radius, n, color);
            AddRoundedFace(builder, Vector3.down, Vector3.right, Vector3.back, half, inner, radius, n, color);
            AddRoundedFace(builder, Vector3.forward, Vector3.left, Vector3.up, half, inner, radius, n, color);
            AddRoundedFace(builder, Vector3.back, Vector3.right, Vector3.up, half, inner, radius, n, color);
            FixWinding(builder);
            return builder;
        }

        static void AddRoundedFace(MeshBuilder builder, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector3 half,
            Vector3 inner, float radius, int n, Color color)
        {
            int start = builder.VertexCount;
            float uHalf = Mathf.Abs(Vector3.Dot(half, uAxis));
            float vHalf = Mathf.Abs(Vector3.Dot(half, vAxis));
            float nHalf = Mathf.Abs(Vector3.Dot(half, normal));
            for (int j = 0; j <= n; j++)
            {
                for (int i = 0; i <= n; i++)
                {
                    // Non-uniform spacing puts more vertices near the rounded borders.
                    float u = Remap(i / (float)n);
                    float v = Remap(j / (float)n);
                    Vector3 p = normal * nHalf + uAxis * (u * 2f - 1f) * uHalf + vAxis * (v * 2f - 1f) * vHalf;
                    Vector3 clamped = new Vector3(
                        Mathf.Clamp(p.x, -inner.x, inner.x),
                        Mathf.Clamp(p.y, -inner.y, inner.y),
                        Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 dir = p - clamped;
                    Vector3 n0 = dir.sqrMagnitude > 1e-10f ? dir.normalized : normal;
                    builder.AddVertex(clamped + n0 * radius, n0, color, new Vector2(u, v));
                }
            }
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    int a = start + j * (n + 1) + i;
                    int b = a + 1;
                    int c = a + n + 1;
                    int d = c + 1;
                    builder.AddTriangle(a, c, d);
                    builder.AddTriangle(a, d, b);
                }
            }
        }

        static float Remap(float t)
        {
            // Dense near 0 and 1: two quick ramps joined by a straight middle.
            const float edge = 0.18f;
            if (t < edge)
                return Mathf.Lerp(0f, 0.08f, t / edge);
            if (t > 1f - edge)
                return Mathf.Lerp(0.92f, 1f, (t - (1f - edge)) / edge);
            return Mathf.Lerp(0.08f, 0.92f, (t - edge) / (1f - 2f * edge));
        }

        /// <summary>
        /// Surface of revolution around Y. Profile points are (radius, height) from bottom to top;
        /// a radius of 0 at either end closes the shape.
        /// </summary>
        public static MeshBuilder Lathe(IReadOnlyList<Vector2> profile, int radialSegments, Color color)
        {
            var builder = new MeshBuilder();
            int rows = profile.Count;
            var profileNormals = new Vector2[rows];
            for (int i = 0; i < rows; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)];
                Vector2 next = profile[Mathf.Min(rows - 1, i + 1)];
                Vector2 tangent = (next - prev).normalized;
                profileNormals[i] = new Vector2(tangent.y, -tangent.x); // outward (points away from the axis)
            }

            for (int i = 0; i < rows; i++)
            {
                for (int s = 0; s <= radialSegments; s++)
                {
                    float angle = s / (float)radialSegments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    var position = new Vector3(profile[i].x * cos, profile[i].y, profile[i].x * sin);
                    var normal = new Vector3(profileNormals[i].x * cos, profileNormals[i].y, profileNormals[i].x * sin);
                    builder.AddVertex(position, normal.normalized, color, new Vector2(s / (float)radialSegments, i / (float)(rows - 1)));
                }
            }
            int ring = radialSegments + 1;
            for (int i = 0; i < rows - 1; i++)
            {
                for (int s = 0; s < radialSegments; s++)
                {
                    int a = i * ring + s;
                    int b = a + 1;
                    int c = a + ring;
                    int d = c + 1;
                    builder.AddTriangle(a, c, d);
                    builder.AddTriangle(a, d, b);
                }
            }
            FixWinding(builder);
            return builder;
        }

        /// <summary>Cylinder with softly rounded rims, standing on y = 0.</summary>
        public static MeshBuilder SoftCylinder(float radius, float height, float rimRadius, Color color, int segments = 28)
        {
            rimRadius = Mathf.Min(rimRadius, Mathf.Min(radius, height * 0.5f) - 1e-4f);
            var profile = new List<Vector2> { new Vector2(0f, 0f) };
            AddArc(profile, new Vector2(radius - rimRadius, rimRadius), rimRadius, -90f, 0f, 5);
            AddArc(profile, new Vector2(radius - rimRadius, height - rimRadius), rimRadius, 0f, 90f, 5);
            profile.Add(new Vector2(0f, height));
            return Lathe(profile, segments, color);
        }

        public static MeshBuilder Sphere(float radius, Color color, int segments = 20)
        {
            var profile = new List<Vector2>();
            int rings = Mathf.Max(4, segments / 2);
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.Lerp(-90f, 90f, i / (float)rings) * Mathf.Deg2Rad;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            profile[0] = new Vector2(0f, -radius);
            profile[rings] = new Vector2(0f, radius);
            return Lathe(profile, segments, color);
        }

        public static void AddArc(List<Vector2> profile, Vector2 center, float radius, float fromDegrees, float toDegrees, int steps)
        {
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps) * Mathf.Deg2Rad;
                profile.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        /// <summary>Ring in the XZ plane.</summary>
        public static MeshBuilder Torus(float majorRadius, float minorRadius, Color color, int majorSegments = 32, int minorSegments = 12)
        {
            var builder = new MeshBuilder();
            for (int i = 0; i <= majorSegments; i++)
            {
                float u = i / (float)majorSegments * Mathf.PI * 2f;
                var ringCenter = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u)) * majorRadius;
                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = j / (float)minorSegments * Mathf.PI * 2f;
                    Vector3 normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    builder.AddVertex(ringCenter + normal * minorRadius, normal, color, new Vector2(i / (float)majorSegments, j / (float)minorSegments));
                }
            }
            int stride = minorSegments + 1;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * stride + j;
                    int b = a + stride;
                    builder.AddTriangle(a, a + 1, b + 1);
                    builder.AddTriangle(a, b + 1, b);
                }
            }
            FixWinding(builder);
            return builder;
        }

        /// <summary>Tube swept along a path with a radius tapering from start to end, capped with half-spheres.</summary>
        public static MeshBuilder Tube(IReadOnlyList<Vector3> path, float startRadius, float endRadius, Color color, int sides = 10)
        {
            var builder = new MeshBuilder();
            int count = path.Count;
            Vector3 previousNormal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = (path[Mathf.Min(count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                // Parallel-transport a normal along the curve to avoid twisting.
                Vector3 normal = i == 0
                    ? Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                    : Vector3.ProjectOnPlane(previousNormal, tangent).normalized;
                previousNormal = normal;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                float radius = Mathf.Lerp(startRadius, endRadius, i / (float)(count - 1));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 n = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    builder.AddVertex(path[i] + n * radius, n, color, new Vector2(s / (float)sides, i / (float)(count - 1)));
                }
            }
            int ring = sides + 1;
            for (int i = 0; i < count - 1; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = i * ring + s;
                    builder.AddTriangle(a, a + ring, a + ring + 1);
                    builder.AddTriangle(a, a + ring + 1, a + 1);
                }
            }
            FixWinding(builder);
            builder.Append(Sphere(startRadius, color, sides), Matrix4x4.Translate(path[0]));
            builder.Append(Sphere(Mathf.Max(0.001f, endRadius), color, sides), Matrix4x4.Translate(path[count - 1]));
            return builder;
        }

        /// <summary>Arc of a circle in the XY plane (angles in degrees, 0 = +X, counter-clockwise), as a tube.</summary>
        public static MeshBuilder Arc(float radius, float thickness, float fromDegrees, float toDegrees, Color color, int steps = 16)
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps) * Mathf.Deg2Rad;
                path.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            return Tube(path, thickness, thickness, color, 8);
        }

        /// <summary>Puffy five-pointed star lying in the XY plane, facing -Z (for wall stickers and mobiles).</summary>
        public static MeshBuilder Star(float outerRadius, float innerRadius, float thickness, Color color, int points = 5)
        {
            var builder = new MeshBuilder();
            int count = points * 2;
            var outline = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                float r = i % 2 == 0 ? outerRadius : innerRadius;
                outline[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            float halfDepth = thickness * 0.5f;
            // Front and back caps, slightly domed in the middle.
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -halfDepth : halfDepth;
                var faceNormal = side == 0 ? Vector3.back : Vector3.forward;
                int center = builder.AddVertex(new Vector3(0f, 0f, z * 1.8f), faceNormal, color, new Vector2(0.5f, 0.5f));
                int first = builder.VertexCount;
                for (int i = 0; i < count; i++)
                {
                    Vector3 edgeNormal = (faceNormal + (Vector3)outline[i].normalized * 0.6f).normalized;
                    builder.AddVertex(new Vector3(outline[i].x, outline[i].y, z), edgeNormal, color, outline[i]);
                }
                for (int i = 0; i < count; i++)
                {
                    int a = first + i;
                    int b = first + (i + 1) % count;
                    if (side == 0)
                        builder.AddTriangle(center, b, a);
                    else
                        builder.AddTriangle(center, a, b);
                }
            }
            // Side walls.
            for (int i = 0; i < count; i++)
            {
                Vector2 p0 = outline[i];
                Vector2 p1 = outline[(i + 1) % count];
                Vector2 edge = (p1 - p0).normalized;
                var n = new Vector3(edge.y, -edge.x, 0f);
                int a = builder.AddVertex(new Vector3(p0.x, p0.y, -halfDepth), n, color, Vector2.zero);
                int b = builder.AddVertex(new Vector3(p1.x, p1.y, -halfDepth), n, color, Vector2.zero);
                int c = builder.AddVertex(new Vector3(p1.x, p1.y, halfDepth), n, color, Vector2.zero);
                int d = builder.AddVertex(new Vector3(p0.x, p0.y, halfDepth), n, color, Vector2.zero);
                builder.AddTriangle(a, b, c);
                builder.AddTriangle(a, c, d);
            }
            FixWinding(builder);
            return builder;
        }

        /// <summary>Flat rectangle in the XZ plane facing up, with UVs tiled every <paramref name="tileSize"/> metres.</summary>
        public static MeshBuilder Plane(Vector2 size, float tileSize, Color color, int subdivisions = 1)
        {
            var builder = new MeshBuilder();
            for (int j = 0; j <= subdivisions; j++)
            {
                for (int i = 0; i <= subdivisions; i++)
                {
                    float u = i / (float)subdivisions;
                    float v = j / (float)subdivisions;
                    var p = new Vector3((u - 0.5f) * size.x, 0f, (v - 0.5f) * size.y);
                    builder.AddVertex(p, Vector3.up, color, new Vector2(p.x / tileSize, p.z / tileSize));
                }
            }
            for (int j = 0; j < subdivisions; j++)
            {
                for (int i = 0; i < subdivisions; i++)
                {
                    int a = j * (subdivisions + 1) + i;
                    int b = a + 1;
                    int c = a + subdivisions + 1;
                    int d = c + 1;
                    builder.AddTriangle(a, c, d);
                    builder.AddTriangle(a, d, b);
                }
            }
            FixWinding(builder);
            return builder;
        }

        /// <summary>Single rectangle in the XZ plane facing up, UVs 0..1 across it (pictures, window panes).</summary>
        public static MeshBuilder Quad(Vector2 size, Color color)
        {
            var builder = new MeshBuilder();
            int a = builder.AddVertex(new Vector3(-size.x * 0.5f, 0f, -size.y * 0.5f), Vector3.up, color, new Vector2(0f, 0f));
            int b = builder.AddVertex(new Vector3(size.x * 0.5f, 0f, -size.y * 0.5f), Vector3.up, color, new Vector2(1f, 0f));
            int c = builder.AddVertex(new Vector3(size.x * 0.5f, 0f, size.y * 0.5f), Vector3.up, color, new Vector2(1f, 1f));
            int d = builder.AddVertex(new Vector3(-size.x * 0.5f, 0f, size.y * 0.5f), Vector3.up, color, new Vector2(0f, 1f));
            builder.AddTriangle(a, c, b);
            builder.AddTriangle(a, d, c);
            FixWinding(builder);
            return builder;
        }

        /// <summary>Makes every triangle face the same way as its (averaged) vertex normals.</summary>
        public static void FixWinding(MeshBuilder builder)
        {
            List<int> tris = builder.Triangles;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = builder.Vertices[tris[t]], b = builder.Vertices[tris[t + 1]], c = builder.Vertices[tris[t + 2]];
                Vector3 n = builder.Normals[tris[t]] + builder.Normals[tris[t + 1]] + builder.Normals[tris[t + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f)
                {
                    int swap = tris[t + 1];
                    tris[t + 1] = tris[t + 2];
                    tris[t + 2] = swap;
                }
            }
        }
    }
}
