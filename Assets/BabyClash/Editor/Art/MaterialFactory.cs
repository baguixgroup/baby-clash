using UnityEditor;
using UnityEngine;

namespace BabyClash.EditorTools.Art
{
    /// <summary>Creates/updates the game's materials (toon for everything solid, Sprites/Default for particles).</summary>
    public sealed class MaterialFactory
    {
        public const string ToonShaderName = "BabyClash/Toon";

        readonly string folder;
        readonly Shader toon;

        public MaterialFactory(string folder)
        {
            this.folder = folder;
            toon = Shader.Find(ToonShaderName);
            if (toon == null)
                Debug.LogError($"[Baby Clash] Shader '{ToonShaderName}' not found; materials will be pink.");
        }

        /// <summary>Solid toon material. Colour multiplies vertex colours; outline width in metres.</summary>
        public Material Toon(string name, Color color, float outline = 0f, Color? shade = null, float rim = 0.2f,
            float shine = 0.1f, Texture2D texture = null, float textureTile = 1f, Color? emission = null)
        {
            Material material = LoadOrCreate(name, toon);
            material.SetColor("_BaseColor", color);
            material.SetColor("_AccentColor", Color.white);
            material.SetColor("_ShadeColor", shade ?? new Color(0.72f, 0.64f, 0.8f));
            material.SetFloat("_OutlineWidth", outline);
            material.SetColor("_OutlineColor", new Color(0.25f, 0.16f, 0.2f));
            material.SetFloat("_RimStrength", rim);
            material.SetFloat("_SpecStrength", shine);
            material.SetColor("_EmissionColor", emission ?? Color.black);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one * textureTile);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Alpha-blended, unlit, vertex-coloured (particles and trails).</summary>
        public Material Particle(string name, Texture2D texture)
        {
            Material material = LoadOrCreate(name, Shader.Find("Sprites/Default"));
            material.mainTexture = texture;
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }
    }
}
