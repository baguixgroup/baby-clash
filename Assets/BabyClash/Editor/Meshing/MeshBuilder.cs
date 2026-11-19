using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabyClash.EditorTools.Meshing
{
    /// <summary>Accumulates vertices/triangles (with normals, colours, uvs and optional bone weights) into a Mesh.</summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<BoneWeight> BoneWeights = new List<BoneWeight>();

        public int VertexCount => Vertices.Count;

        public int AddVertex(Vector3 position, Vector3 normal, Color color, Vector2 uv)
        {
            Vertices.Add(position);
            Normals.Add(normal);
            Colors.Add(color);
            Uvs.Add(uv);
            return Vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Adds another mesh's geometry, transformed, optionally recoloured.</summary>
        public void Append(MeshBuilder other, Matrix4x4 transform, Color? tint = null)
        {
            int offset = Vertices.Count;
            Matrix4x4 normalMatrix = transform.inverse.transpose;
            for (int i = 0; i < other.Vertices.Count; i++)
            {
                Vertices.Add(transform.MultiplyPoint3x4(other.Vertices[i]));
                Normals.Add(normalMatrix.MultiplyVector(other.Normals[i]).normalized);
                Colors.Add(tint.HasValue ? tint.Value * other.Colors[i] : other.Colors[i]);
                Uvs.Add(other.Uvs[i]);
            }
            foreach (int index in other.Triangles)
                Triangles.Add(index + offset);
        }

        public void Append(MeshBuilder other, Vector3 position, Quaternion rotation, Vector3 scale, Color? tint = null) =>
            Append(other, Matrix4x4.TRS(position, rotation, scale), tint);

        public void SetColor(Color color)
        {
            for (int i = 0; i < Colors.Count; i++)
                Colors[i] = color;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Triangles, 0);
            if (BoneWeights.Count == Vertices.Count)
                mesh.boneWeights = BoneWeights.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
