using System.IO;
using UnityEditor;
using UnityEngine;

namespace BabyClash.EditorTools.Art
{
    /// <summary>Where generated content lives, and helpers that update assets in place (keeping their GUIDs).</summary>
    public static class GeneratedAssets
    {
        public const string Root = "Assets/BabyClash";
        public const string Generated = Root + "/Generated";
        public const string Meshes = Generated + "/Meshes";
        public const string Materials = Generated + "/Materials";
        public const string Textures = Generated + "/Textures";
        public const string Icons = Generated + "/Icons";
        public const string Rendering = Generated + "/Rendering";
        public const string Balance = Root + "/Balance";
        public const string Prefabs = Root + "/Prefabs";
        public const string Scenes = Root + "/Scenes";

        public static void EnsureFolders()
        {
            foreach (string folder in new[] { Generated, Meshes, Materials, Textures, Icons, Rendering, Balance, Prefabs, Scenes })
                EnsureFolder(folder);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Saves a mesh asset, overwriting the existing one in place so prefabs keep their references.</summary>
        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{Meshes}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        /// <summary>A mesh renderer child. Scale/rotation are local.</summary>
        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition,
            Quaternion? localRotation = null, Vector3? localScale = null, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
