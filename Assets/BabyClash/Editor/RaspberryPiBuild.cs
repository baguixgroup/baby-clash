using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BabyClash.EditorTools
{
    // Use a dedicated pipeline for the Pi build so desktop releases keep their rendering settings.
    public sealed class RaspberryPiBuild : IProcessSceneWithReport
    {
        static bool building;
        public int callbackOrder => 0;

        internal static BuildReport Build(string path)
        {
            if (!BabyClashContentBuilder.IsContentCurrent())
                BabyClashContentBuilder.BuildAll();

            string folderName = "BabyClashPiBuild-" + System.Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            var previousDefault = GraphicsSettings.defaultRenderPipeline;
            int qualityLevel = QualitySettings.GetQualityLevel();
            var previousPipelines = new RenderPipelineAsset[QualitySettings.names.Length];
            for (int level = 0; level < previousPipelines.Length; level++)
                previousPipelines[level] = QualitySettings.GetRenderPipelineAssetAt(level);
            var compression = PlayerSettings.WebGL.compressionFormat;
            int width = PlayerSettings.defaultWebScreenWidth;
            int height = PlayerSettings.defaultWebScreenHeight;
            try
            {
                AssetDatabase.CreateFolder("Assets", folderName);
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, folder + "/Renderer.asset");
                var pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                pipeline.renderScale = 0.75f;
                pipeline.shadowDistance = 0f;
                var settings = new SerializedObject(pipeline);
                settings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
                settings.FindProperty("m_MainLightShadowsSupported").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline, folder + "/Pipeline.asset");
                GraphicsSettings.defaultRenderPipeline = pipeline;
                SetQualityPipelines(qualityLevel, previousPipelines, pipeline);
                PlayerSettings.defaultWebScreenWidth = 640;
                PlayerSettings.defaultWebScreenHeight = 400;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                AssetDatabase.SaveAssets();
                building = true;
                return BabyClashRelease.Build(BuildTarget.WebGL, path);
            }
            finally
            {
                building = false;
                GraphicsSettings.defaultRenderPipeline = previousDefault;
                SetQualityPipelines(qualityLevel, previousPipelines);
                PlayerSettings.defaultWebScreenWidth = width;
                PlayerSettings.defaultWebScreenHeight = height;
                PlayerSettings.WebGL.compressionFormat = compression;
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.SaveAssets();
            }
        }

        static void SetQualityPipelines(int current, RenderPipelineAsset[] previous, RenderPipelineAsset replacement = null)
        {
            for (int level = 0; level < previous.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = replacement ?? previous[level];
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!building || report == null || report.summary.platform != BuildTarget.WebGL)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<UniversalAdditionalCameraData>(true))
                {
                    camera.renderPostProcessing = false;
                    camera.renderShadows = false;
                }
            }
        }
    }
}
