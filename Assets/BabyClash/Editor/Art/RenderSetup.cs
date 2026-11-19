using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BabyClash.EditorTools.Art
{
    /// <summary>
    /// Switches the project to URP with settings for a soft cartoon look: MSAA, soft shadows, SSAO, and a
    /// post-processing profile (tonemapping, bloom, colour grading, vignette).
    /// </summary>
    public static class RenderSetup
    {
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        public static UniversalRenderPipelineAsset EnsurePipeline()
        {
            string rendererPath = $"{GeneratedAssets.Rendering}/BabyClashRenderer.asset";
            string pipelinePath = $"{GeneratedAssets.Rendering}/BabyClashURP.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            EnsureAmbientOcclusion(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            // The camera sees at most ~25 m (the far arena corner); one cascade keeps shadow draws to a single pass.
            pipeline.shadowDistance = 30f;
            pipeline.shadowCascadeCount = 1;
            pipeline.mainLightShadowmapResolution = 2048;
            var serializedPipeline = new SerializedObject(pipeline);
            SetBool(serializedPipeline, "m_SoftShadowsSupported", true);
            SetBool(serializedPipeline, "m_AdditionalLightShadowsSupported", false);
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            return pipeline;
        }

        /// <summary>Adds the SSAO renderer feature the same way URP's own renderer inspector does.</summary>
        static void EnsureAmbientOcclusion(UniversalRendererData renderer)
        {
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                    return;
            }

            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "ScreenSpaceAmbientOcclusion";
            ssao.hideFlags |= HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);

            var serialized = new SerializedObject(renderer);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var settings = new SerializedObject(ssao);
            SetFloat(settings, "m_Settings.Intensity", 1.1f);
            SetFloat(settings, "m_Settings.Radius", 0.3f);
            SetFloat(settings, "m_Settings.DirectLightingStrength", 0.2f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        public static VolumeProfile EnsurePostProcessing()
        {
            string path = $"{GeneratedAssets.Rendering}/NurseryPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(10f);
            color.saturation.Override(14f);

            var white = GetOrAdd<WhiteBalance>(profile);
            white.temperature.Override(6f);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags |= HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void SetBool(SerializedObject target, string property, bool value)
        {
            SerializedProperty prop = target.FindProperty(property);
            if (prop != null)
                prop.boolValue = value;
            else
                Debug.LogWarning($"[Baby Clash] URP setting '{property}' not found; skipped.");
        }

        static void SetFloat(SerializedObject target, string property, float value)
        {
            SerializedProperty prop = target.FindProperty(property);
            if (prop != null)
                prop.floatValue = value;
            else
                Debug.LogWarning($"[Baby Clash] URP setting '{property}' not found; skipped.");
        }
    }
}
