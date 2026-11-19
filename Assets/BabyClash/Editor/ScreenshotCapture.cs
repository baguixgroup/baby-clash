using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BabyClash.EditorTools
{
    /// <summary>
    /// Renders the Arena scene from a few camera angles to PNG files, for press shots and for reviewing art from the
    /// command line:
    /// Unity -batchmode -projectPath . -executeMethod BabyClash.EditorTools.ScreenshotCapture.CaptureFromCommandLine -shotDir /path
    /// </summary>
    public static class ScreenshotCapture
    {
        const int Width = 1600;
        const int Height = 900;

        [MenuItem("Baby Clash/Capture Screenshots", priority = 20)]
        static void CaptureFromMenu()
        {
            string folder = Path.GetFullPath("Screenshots");
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string folder = Path.GetFullPath("Screenshots");
            bool rebuild = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-shotDir" && i + 1 < args.Length)
                    folder = args[i + 1];
                if (args[i] == "-rebuild")
                    rebuild = true;
            }
            if (rebuild || !BabyClashContentBuilder.IsContentCurrent())
                BabyClashContentBuilder.BuildAll();
            Capture(folder);
        }

        static void Capture(string folder)
        {
            EditorSettings.asyncShaderCompilation = false;
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(BabyClashContentBuilder.ScenePath);
            Camera camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Arena scene has no main camera.");

            Render(camera, Path.Combine(folder, "arena.png"));

            // Wide establishing shot of the nursery.
            camera.transform.SetPositionAndRotation(new Vector3(0f, 9f, -17f), Quaternion.Euler(26f, 0f, 0f));
            Render(camera, Path.Combine(folder, "nursery.png"));

            BabyController[] babies = UnityEngine.Object.FindObjectsByType<BabyController>();
            foreach (BabyController baby in babies)
            {
                Transform target = baby.transform;
                ApplyLooks(baby);
                Vector3 look = target.position + Vector3.up * 0.75f;
                camera.transform.SetPositionAndRotation(look + target.forward * 2.8f + Vector3.up * 0.5f,
                    Quaternion.LookRotation(-target.forward * 2.8f - Vector3.up * 0.5f));
                Render(camera, Path.Combine(folder, $"closeup_{baby.name}.png"));
                camera.transform.SetPositionAndRotation(look + target.forward * 1.2f + Vector3.up * 0.35f,
                    Quaternion.LookRotation(-target.forward * 1.2f - Vector3.up * 0.1f));
                Render(camera, Path.Combine(folder, $"face_{baby.name}.png"));
            }
            Debug.Log($"[Baby Clash] Screenshots written to {folder}");
        }

        /// <summary>Edit mode doesn't run BabyAnimator.Start, so apply its colours for the shot.</summary>
        static void ApplyLooks(BabyController baby)
        {
            var skin = baby.GetComponentInChildren<SkinnedMeshRenderer>();
            var serialized = new SerializedObject(baby.GetComponentInChildren<BabyAnimator>());
            var block = new MaterialPropertyBlock();
            block.SetColor("_AccentColor", baby.PlayerColor);
            block.SetColor("_BaseColor", serialized.FindProperty("skinTint").colorValue);
            skin.SetPropertyBlock(block);
            var hairBlock = new MaterialPropertyBlock();
            hairBlock.SetColor("_BaseColor", serialized.FindProperty("hairColor").colorValue);
            foreach (MeshRenderer part in baby.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (part.name.StartsWith("Hair") || part.name == "BrowMesh")
                    part.SetPropertyBlock(hairBlock);
            }
        }

        static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
