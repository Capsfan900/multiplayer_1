using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wildstone.Editor
{
    /// <summary>
    /// D028 render tiers. High adapts the audited vibegame1 PC stack (SSAO, four soft cascades, HDR grade);
    /// Low keeps the readable floor at a lower render scale. Renderer index 1 (no SSAO) serves the viewmodel overlay.
    /// Values are rewritten on every configure so an existing asset cannot keep stale settings.
    /// </summary>
    public static class RenderingSetup
    {
        public const string Settings = "Assets/Wildstone/Settings";
        public const string LowRendererPath = Settings + "/WildstoneRendererLow.asset";
        public const string LowPipelinePath = Settings + "/WildstonePipelineLow.asset";
        public const string OutlinePath = Settings + "/SoldierOutline.mat";
        public const string OutlineFeature = "Soldier outline";
        public const int OverlayRendererIndex = 1;
        // Quality levels below this index use the Low pipeline (Very Low, Low, Medium).
        public const int FirstHighQuality = 3;

        public static void Configure(UniversalRendererData renderer, UniversalRenderPipelineAsset high)
        {
            var lowRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LowRendererPath);
            if (lowRenderer == null)
            {
                lowRenderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(lowRenderer, LowRendererPath);
            }
            lowRenderer.postProcessData = renderer.postProcessData;
            ConfigureRenderer(renderer, true);
            ConfigureRenderer(lowRenderer, false);

            var low = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowPipelinePath);
            if (low == null)
            {
                low = UniversalRenderPipelineAsset.Create(lowRenderer);
                AssetDatabase.CreateAsset(low, LowPipelinePath);
            }
            ConfigurePipeline(high, true, renderer, lowRenderer);
            ConfigurePipeline(low, false, lowRenderer, lowRenderer);

            GraphicsSettings.defaultRenderPipeline = high;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue =
                    i < FirstHighQuality ? low : high;
            quality.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline = QualitySettings.GetQualityLevel() < FirstHighQuality ? low : high;
        }

        static void ConfigurePipeline(UniversalRenderPipelineAsset asset, bool high, ScriptableRendererData main, ScriptableRendererData overlay)
        {
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = main;
            list.GetArrayElementAtIndex(OverlayRendererIndex).objectReferenceValue = overlay;
            Set(so, "m_DefaultRendererIndex", 0);
            Set(so, "m_RequireDepthTexture", true);      // projected zone and scorch decals read scene depth
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 1);                        // SMAA on the camera instead
            so.FindProperty("m_RenderScale").floatValue = high ? 1f : .77f;
            Set(so, "m_UpscalingFilter", (int)(high ? UpscalingFilterSelection.Auto : UpscalingFilterSelection.FSR));
            Set(so, "m_MainLightShadowmapResolution", high ? 2048 : 1024);
            so.FindProperty("m_ShadowDistance").floatValue = high ? 60 : 40;
            Set(so, "m_ShadowCascadeCount", high ? 4 : 2);
            so.FindProperty("m_Cascade2Split").floatValue = .25f;
            so.FindProperty("m_Cascade4Split").vector3Value = new Vector3(.05f, .15f, .35f);
            so.FindProperty("m_ShadowDepthBias").floatValue = .1f;
            so.FindProperty("m_ShadowNormalBias").floatValue = .5f;
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", high ? 2 : 1); // Medium : Low
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_ReflectionProbeBlending", true);
            Set(so, "m_ReflectionProbeBoxProjection", true);
            Set(so, "m_ColorGradingMode", 1);            // HDR grading on both tiers keeps one grade
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureRenderer(UniversalRendererData renderer, bool ssao)
        {
            var so = new SerializedObject(renderer);
            Set(so, "m_RenderingMode", 2);               // Forward+: pooled muzzle/blast lights without a per-object cap
            Set(so, "m_CopyDepthMode", 0);               // after opaques, so transparent decals can read it
            so.ApplyModifiedPropertiesWithoutUndo();

            ScreenSpaceAmbientOcclusion feature = null;
            foreach (var existing in renderer.rendererFeatures)
                if (existing is ScreenSpaceAmbientOcclusion found) feature = found;
            if (ssao && feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            else if (!ssao && feature != null)
            {
                renderer.rendererFeatures.Remove(feature);
                AssetDatabase.RemoveObjectFromAsset(feature);
                feature = null;
            }
            if (feature != null)
            {
                // vibegame1 PC_Renderer values: blue noise, half resolution, medium samples.
                var settings = new SerializedObject(feature);
                Set(settings, "m_Settings.AOMethod", 0);
                Set(settings, "m_Settings.Downsample", true);
                Set(settings, "m_Settings.AfterOpaque", false);
                Set(settings, "m_Settings.Source", 0);
                Set(settings, "m_Settings.NormalSamples", 1);
                settings.FindProperty("m_Settings.Intensity").floatValue = .45f;
                settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .25f;
                settings.FindProperty("m_Settings.Radius").floatValue = .35f;
                Set(settings, "m_Settings.Samples", 1);
                Set(settings, "m_Settings.BlurQuality", 1);
                settings.FindProperty("m_Settings.Falloff").floatValue = 60;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            // Both tiers: soldier readability against ivory stone must not depend on the quality setting.
            RenderObjects outline = null;
            foreach (var existing in renderer.rendererFeatures)
                if (existing is RenderObjects found && found.name == OutlineFeature) outline = found;
            if (outline == null)
            {
                outline = ScriptableObject.CreateInstance<RenderObjects>();
                outline.name = OutlineFeature;
                AssetDatabase.AddObjectToAsset(outline, renderer);
                renderer.rendererFeatures.Add(outline);
            }
            outline.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            outline.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
            outline.settings.filterSettings.LayerMask = 1 << Presentation.PlayerVisual.OutlineLayer;
            outline.settings.filterSettings.PassNames = Array.Empty<string>();
            outline.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
            outline.settings.overrideMaterial = OutlineMaterial();
            outline.settings.overrideMaterialPassIndex = 0;
            outline.SetActive(true);
            EditorUtility.SetDirty(outline);
            var map = new SerializedObject(renderer).FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long id))
                    throw new InvalidOperationException("Renderer feature is not saved in " + renderer.name);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            map.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        static Material OutlineMaterial()
        {
            var shader = Shader.Find("Wildstone/SoldierOutline") ?? throw new InvalidOperationException("Missing Wildstone/SoldierOutline shader");
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (material == null)
            {
                material = new Material(shader) { name = "SoldierOutline" };
                AssetDatabase.CreateAsset(material, OutlinePath);
            }
            material.shader = shader;
            material.SetColor("_OutlineColor", new Color(.05f, .055f, .065f, 1));
            material.SetFloat("_Width", 2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Set(SerializedObject so, string path, int value)
        {
            var property = so.FindProperty(path) ?? throw new InvalidOperationException("Missing serialized field " + path);
            property.intValue = value; // enum fields store their value, not their index
        }

        static void Set(SerializedObject so, string path, bool value)
        {
            var property = so.FindProperty(path) ?? throw new InvalidOperationException("Missing serialized field " + path);
            property.boolValue = value;
        }
    }
}
