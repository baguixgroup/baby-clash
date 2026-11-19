using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BabyClash.EditorTools.Art
{
    /// <summary>
    /// Renders 3D objects to transparent sprites for the HUD. Renders twice (black and white backgrounds) and derives
    /// alpha from the difference, so it works whatever the pipeline does with the alpha channel.
    /// </summary>
    public static class IconBaker
    {
        const int Size = 256;
        static readonly Vector3 StudioPosition = new Vector3(0f, 300f, 0f);

        /// <summary>Temporarily moves <paramref name="subject"/> to an empty spot and photographs it.</summary>
        public static Sprite Bake(string name, GameObject subject, Vector3 lookAtLocal, Vector3 viewDirection, float distance, float fieldOfView)
        {
            Vector3 originalPosition = subject.transform.position;
            subject.transform.position = StudioPosition;

            var cameraObject = new GameObject("IconCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 20f;
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Vector3 target = subject.transform.TransformPoint(lookAtLocal);
            camera.transform.position = target + viewDirection.normalized * distance;
            camera.transform.LookAt(target);

            Color[] onBlack = Render(camera, Color.black);
            Color[] onWhite = Render(camera, Color.white);
            Object.DestroyImmediate(cameraObject);
            subject.transform.position = originalPosition;

            var pixels = new Color[onBlack.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color b = onBlack[i];
                Color w = onWhite[i];
                float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                pixels[i] = alpha > 0.004f ? new Color(b.r / alpha, b.g / alpha, b.b / alpha, alpha) : Color.clear;
            }

            string path = $"{GeneratedAssets.Icons}/{name}.png";
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Color[] Render(Camera camera, Color background)
        {
            camera.backgroundColor = background;
            var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            Color[] pixels = image.GetPixels();
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return pixels;
        }
    }
}
