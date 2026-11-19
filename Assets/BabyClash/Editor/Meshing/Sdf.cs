using System;
using System.Collections.Generic;
using UnityEngine;

namespace BabyClash.EditorTools.Meshing
{
    public enum SdfOp
    {
        /// <summary>Smoothly adds the shape (Blend = smoothing radius).</summary>
        Union,

        /// <summary>Smoothly carves the shape away.</summary>
        Subtract,

        /// <summary>Smoothly keeps only the overlap.</summary>
        Intersect,

        /// <summary>Doesn't change the surface: tints it where the shape overlaps (Blend = soft edge width).</summary>
        Paint,
    }

    /// <summary>One signed-distance primitive of a sculpt. Negative inside, positive outside (metres).</summary>
    public abstract class SdfShape
    {
        public SdfOp Op = SdfOp.Union;
        public float Blend;
        public Color Color = Color.white;

        /// <summary>Skinning bone this shape belongs to (only used by skinned sculpts).</summary>
        public int Bone;

        public abstract float Distance(Vector3 p);
    }

    public sealed class SdfSphere : SdfShape
    {
        readonly Vector3 center;
        readonly float radius;

        public SdfSphere(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }

        public override float Distance(Vector3 p) => (p - center).magnitude - radius;
    }

    public sealed class SdfEllipsoid : SdfShape
    {
        readonly Vector3 center;
        readonly Vector3 radii;
        readonly Quaternion inverseRotation;

        public SdfEllipsoid(Vector3 center, Vector3 radii, Quaternion? rotation = null)
        {
            this.center = center;
            this.radii = radii;
            inverseRotation = Quaternion.Inverse(rotation ?? Quaternion.identity);
        }

        // Inigo Quilez's bound-preserving ellipsoid approximation.
        public override float Distance(Vector3 p)
        {
            Vector3 q = inverseRotation * (p - center);
            float k0 = new Vector3(q.x / radii.x, q.y / radii.y, q.z / radii.z).magnitude;
            float k1 = new Vector3(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y), q.z / (radii.z * radii.z)).magnitude;
            return k1 < 1e-6f ? -Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z)) : k0 * (k0 - 1f) / k1;
        }
    }

    /// <summary>Capsule whose radius changes linearly from a to b (limbs).</summary>
    public sealed class SdfTaperedCapsule : SdfShape
    {
        readonly Vector3 a;
        readonly Vector3 ab;
        readonly float abLengthSqr;
        readonly float radiusA;
        readonly float radiusB;

        public SdfTaperedCapsule(Vector3 a, Vector3 b, float radiusA, float radiusB)
        {
            this.a = a;
            ab = b - a;
            abLengthSqr = Mathf.Max(1e-8f, ab.sqrMagnitude);
            this.radiusA = radiusA;
            this.radiusB = radiusB;
        }

        public override float Distance(Vector3 p)
        {
            Vector3 ap = p - a;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abLengthSqr);
            return (ap - ab * t).magnitude - Mathf.Lerp(radiusA, radiusB, t);
        }
    }

    public sealed class SdfRoundBox : SdfShape
    {
        readonly Vector3 center;
        readonly Vector3 halfSize;
        readonly float rounding;
        readonly Quaternion inverseRotation;

        public SdfRoundBox(Vector3 center, Vector3 halfSize, float rounding, Quaternion? rotation = null)
        {
            this.center = center;
            this.halfSize = halfSize;
            this.rounding = rounding;
            inverseRotation = Quaternion.Inverse(rotation ?? Quaternion.identity);
        }

        public override float Distance(Vector3 p)
        {
            Vector3 q = inverseRotation * (p - center);
            Vector3 d = new Vector3(Mathf.Abs(q.x), Mathf.Abs(q.y), Mathf.Abs(q.z)) - (halfSize - Vector3.one * rounding);
            Vector3 outside = Vector3.Max(d, Vector3.zero);
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f) - rounding;
        }
    }

    public sealed class SdfTorus : SdfShape
    {
        readonly Vector3 center;
        readonly float majorRadius;
        readonly float minorRadius;
        readonly Quaternion inverseRotation;

        /// <summary>Ring lies in the local XZ plane.</summary>
        public SdfTorus(Vector3 center, float majorRadius, float minorRadius, Quaternion? rotation = null)
        {
            this.center = center;
            this.majorRadius = majorRadius;
            this.minorRadius = minorRadius;
            inverseRotation = Quaternion.Inverse(rotation ?? Quaternion.identity);
        }

        public override float Distance(Vector3 p)
        {
            Vector3 q = inverseRotation * (p - center);
            var ring = new Vector2(new Vector2(q.x, q.z).magnitude - majorRadius, q.y);
            return ring.magnitude - minorRadius;
        }
    }

    /// <summary>Everything on the side the normal points to is "inside" (use with Intersect/Subtract to slice).</summary>
    public sealed class SdfHalfSpace : SdfShape
    {
        readonly Vector3 point;
        readonly Vector3 normal;

        public SdfHalfSpace(Vector3 point, Vector3 normal)
        {
            this.point = point;
            this.normal = normal.normalized;
        }

        public override float Distance(Vector3 p) => -Vector3.Dot(p - point, normal);
    }

    /// <summary>Arbitrary distance function, for one-off shapes.</summary>
    public sealed class SdfCustom : SdfShape
    {
        readonly Func<Vector3, float> distance;

        public SdfCustom(Func<Vector3, float> distance) => this.distance = distance;

        public override float Distance(Vector3 p) => distance(p);
    }

    /// <summary>
    /// A sculpt: shapes combined in order with smooth blending, plus colour painting. Polygonized with naive
    /// surface nets (one vertex per surface cell, snapped onto the surface), which gives smooth, watertight meshes.
    /// </summary>
    public sealed class SdfModel
    {
        readonly List<SdfShape> shapes = new List<SdfShape>();
        public Color BaseColor = Color.white;

        public IReadOnlyList<SdfShape> Shapes => shapes;

        public T Add<T>(T shape, SdfOp op = SdfOp.Union, float blend = 0f, Color? color = null, int bone = 0) where T : SdfShape
        {
            shape.Op = op;
            shape.Blend = blend;
            shape.Color = color ?? BaseColor;
            shape.Bone = bone;
            shapes.Add(shape);
            return shape;
        }

        public float Distance(Vector3 p) => Evaluate(p, false, out _);

        public Color ColorAt(Vector3 p)
        {
            Evaluate(p, true, out Color color);
            return color;
        }

        float Evaluate(Vector3 p, bool wantColor, out Color color)
        {
            float d = float.MaxValue;
            color = BaseColor;
            bool first = true;
            foreach (SdfShape shape in shapes)
            {
                if (shape.Op == SdfOp.Paint)
                {
                    if (wantColor)
                    {
                        float inside = shape.Distance(p);
                        float amount = shape.Blend > 0f ? Mathf.Clamp01(0.5f - inside / shape.Blend) : (inside <= 0f ? 1f : 0f);
                        if (amount > 0f)
                            color = Color.Lerp(color, shape.Color, amount);
                    }
                    continue;
                }

                float s = shape.Distance(p);
                if (first)
                {
                    d = s;
                    color = shape.Color;
                    first = false;
                    continue;
                }

                float k = Mathf.Max(1e-5f, shape.Blend);
                float h;
                switch (shape.Op)
                {
                    case SdfOp.Union:
                        h = Mathf.Clamp01(0.5f + 0.5f * (d - s) / k); // 1 = the new shape dominates
                        d = Mathf.Lerp(d, s, h) - k * h * (1f - h);
                        break;
                    case SdfOp.Subtract:
                        h = Mathf.Clamp01(0.5f - 0.5f * (d + s) / k);
                        d = Mathf.Lerp(d, -s, h) + k * h * (1f - h);
                        break;
                    default: // Intersect: smooth max
                        h = Mathf.Clamp01(0.5f - 0.5f * (s - d) / k);
                        d = Mathf.Lerp(s, d, h) + k * h * (1f - h);
                        break;
                }
                if (wantColor && shape.Op == SdfOp.Union)
                    color = Color.Lerp(color, shape.Color, h);
            }
            return d;
        }

        public Vector3 Gradient(Vector3 p, float step)
        {
            var dx = new Vector3(step, 0f, 0f);
            var dy = new Vector3(0f, step, 0f);
            var dz = new Vector3(0f, 0f, step);
            var g = new Vector3(
                Distance(p + dx) - Distance(p - dx),
                Distance(p + dy) - Distance(p - dy),
                Distance(p + dz) - Distance(p - dz));
            return g.sqrMagnitude > 1e-12f ? g.normalized : Vector3.up;
        }

        /// <summary>Distance to the shapes of one skinning bone only (no blending), for weight computation.</summary>
        public float BoneDistance(Vector3 p, int bone)
        {
            float d = float.MaxValue;
            foreach (SdfShape shape in shapes)
            {
                if (shape.Op == SdfOp.Union && shape.Bone == bone)
                    d = Mathf.Min(d, shape.Distance(p));
            }
            return d;
        }

        static readonly int[,] CornerOffsets =
        {
            { 0, 0, 0 }, { 1, 0, 0 }, { 0, 1, 0 }, { 1, 1, 0 },
            { 0, 0, 1 }, { 1, 0, 1 }, { 0, 1, 1 }, { 1, 1, 1 },
        };

        static readonly int[,] CellEdges =
        {
            { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 },
            { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
        };

        public MeshBuilder Polygonize(Bounds bounds, float cellSize)
        {
            Vector3 min = bounds.min - Vector3.one * cellSize;
            Vector3 size = bounds.size + Vector3.one * (cellSize * 2f);
            int nx = Mathf.CeilToInt(size.x / cellSize) + 1;
            int ny = Mathf.CeilToInt(size.y / cellSize) + 1;
            int nz = Mathf.CeilToInt(size.z / cellSize) + 1;

            var field = new float[nx * ny * nz];
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                        field[x + nx * (y + ny * z)] = Distance(min + new Vector3(x, y, z) * cellSize);

            var builder = new MeshBuilder();
            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            var cellVertex = new int[cx * cy * cz];
            var corner = new float[8];
            for (int z = 0; z < cz; z++)
            {
                for (int y = 0; y < cy; y++)
                {
                    for (int x = 0; x < cx; x++)
                    {
                        int cell = x + cx * (y + cy * z);
                        cellVertex[cell] = -1;
                        int inside = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            corner[c] = field[(x + CornerOffsets[c, 0]) + nx * ((y + CornerOffsets[c, 1]) + ny * (z + CornerOffsets[c, 2]))];
                            if (corner[c] < 0f)
                                inside++;
                        }
                        if (inside == 0 || inside == 8)
                            continue;

                        Vector3 sum = Vector3.zero;
                        int crossings = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            int c0 = CellEdges[e, 0], c1 = CellEdges[e, 1];
                            float d0 = corner[c0], d1 = corner[c1];
                            if ((d0 < 0f) == (d1 < 0f))
                                continue;
                            float t = d0 / (d0 - d1);
                            var p0 = new Vector3(CornerOffsets[c0, 0], CornerOffsets[c0, 1], CornerOffsets[c0, 2]);
                            var p1 = new Vector3(CornerOffsets[c1, 0], CornerOffsets[c1, 1], CornerOffsets[c1, 2]);
                            sum += Vector3.Lerp(p0, p1, t);
                            crossings++;
                        }

                        Vector3 position = min + (new Vector3(x, y, z) + sum / crossings) * cellSize;
                        // Snap onto the true surface for smooth silhouettes.
                        for (int i = 0; i < 2; i++)
                            position -= Gradient(position, cellSize * 0.5f) * Distance(position);

                        Vector3 normal = Gradient(position, cellSize * 0.5f);
                        cellVertex[cell] = builder.AddVertex(position, normal, ColorAt(position), Vector2.zero);
                    }
                }
            }

            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        float d = field[x + nx * (y + ny * z)];
                        if (x < cx && y > 0 && z > 0 && y < cy && z < cz)
                            TryQuad(builder, cellVertex, cx, cy, d, field[(x + 1) + nx * (y + ny * z)], Vector3.right,
                                Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z));
                        if (y < cy && x > 0 && z > 0 && x < cx && z < cz)
                            TryQuad(builder, cellVertex, cx, cy, d, field[x + nx * ((y + 1) + ny * z)], Vector3.up,
                                Cell(x - 1, y, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x - 1, y, z));
                        if (z < cz && x > 0 && y > 0 && x < cx && y < cy)
                            TryQuad(builder, cellVertex, cx, cy, d, field[x + nx * (y + ny * (z + 1))], Vector3.forward,
                                Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z));
                    }
                }
            }
            return builder;

            int Cell(int x, int y, int z) => x + cx * (y + cy * z);
        }

        static void TryQuad(MeshBuilder builder, int[] cellVertex, int cx, int cy, float d0, float d1, Vector3 axis,
            int a, int b, int c, int d)
        {
            if ((d0 < 0f) == (d1 < 0f))
                return;
            int va = cellVertex[a], vb = cellVertex[b], vc = cellVertex[c], vd = cellVertex[d];
            if (va < 0 || vb < 0 || vc < 0 || vd < 0)
                return;

            // Outward normal points from inside (negative) to outside along the edge axis.
            Vector3 outward = d0 < 0f ? axis : -axis;
            Vector3 pa = builder.Vertices[va], pb = builder.Vertices[vb], pc = builder.Vertices[vc];
            bool flip = Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), outward) < 0f;
            if (flip)
            {
                builder.AddTriangle(va, vc, vb);
                builder.AddTriangle(va, vd, vc);
            }
            else
            {
                builder.AddTriangle(va, vb, vc);
                builder.AddTriangle(va, vc, vd);
            }
        }
    }
}
