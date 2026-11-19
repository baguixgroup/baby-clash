using BabyClash.EditorTools.Meshing;
using UnityEngine;

namespace BabyClash.EditorTools.Art
{
    /// <summary>Shared materials, textures and meshes handed to the factories.</summary>
    public sealed class ArtKit
    {
        public MaterialFactory Factory;

        // Characters
        public Material BabySkin;
        public Material Hair;
        public Material EyeWhite;
        public Material EyeDark;
        public Material EyeShine;
        public Material MouthDark;
        public Material Tongue;
        public Material PlayerRing;
        public Material StarGold;

        // Items
        public Material Cloth;
        public Material Plastic;

        // Room
        public Material Floor;
        public Material Mat;
        public Material Wallpaper;
        public Material Trim;
        public Material Painted;
        public Material Fabric;
        public Material Glass;
        public Material LampShade;
        public Material PictureSun;
        public Material PictureMoon;
        public Material PictureRainbow;

        // Effects
        public Material Particle;
        public Material Trail;

        public Texture2D SoftDot;
        public Mesh UnitSphere;

        public static Mesh CreateUnitSphere() =>
            GeneratedAssets.SaveMesh(Shapes.Sphere(0.5f, Color.white, 24).ToMesh("UnitSphere"), "UnitSphere");
    }
}
