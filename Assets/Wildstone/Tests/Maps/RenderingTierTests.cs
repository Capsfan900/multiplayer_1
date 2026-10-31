using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Wildstone.Tests
{
    // D028 quality tiers: the quality setting must change rendering, not only its label.
    [TestFixture,Category("Native")]
    public sealed class RenderingTierTests
    {
        [Test]
        public void LowerQualityLevelsUseTheCheaperPipelineAndHigherOnesTheFullStack()
        {
            var high=GraphicsSettingsDefault();
            Assert.That(high,Is.Not.Null);
            Assert.That(high.name,Is.EqualTo("WildstonePipeline"));
            int levels=QualitySettings.names.Length;
            Assert.That(levels,Is.GreaterThan(3));
            for(int i=0;i<levels;i++)
            {
                var asset=QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                Assert.That(asset,Is.Not.Null,QualitySettings.names[i]);
                Assert.That(asset.name,Is.EqualTo(i<3?"WildstonePipelineLow":"WildstonePipeline"),QualitySettings.names[i]);
            }
            var low=(UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(0);
            Assert.That(low.renderScale,Is.LessThan(1));
            Assert.That(low.shadowCascadeCount,Is.LessThan(high.shadowCascadeCount));
            Assert.That(high.shadowCascadeCount,Is.EqualTo(4));
            Assert.That(high.supportsCameraDepthTexture && high.supportsHDR,Is.True);
        }
        // Ivory soldiers on ivory stone: every tier draws the dark outline pass over the Enemy layer only.
        [Test]
        public void BothTiersOutlineSoldiersOnTheEnemyLayer()
        {
            var high=GraphicsSettingsDefault();
            var low=(UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(0);
            foreach(var asset in new[]{ high,low })
            {
                var renderer=asset.rendererDataList[0];
                RenderObjects outline=null;
                foreach(var feature in renderer.rendererFeatures)
                    if(feature is RenderObjects found && found.name==Wildstone.Editor.RenderingSetup.OutlineFeature) outline=found;
                Assert.That(outline,Is.Not.Null,renderer.name);
                Assert.That(outline.isActive,Is.True,renderer.name);
                Assert.That(outline.settings.filterSettings.LayerMask.value,Is.EqualTo(1<<Wildstone.Presentation.PlayerVisual.OutlineLayer),renderer.name);
                Assert.That(LayerMask.LayerToName(Wildstone.Presentation.PlayerVisual.OutlineLayer),Is.EqualTo("Enemy"));
                Assert.That(outline.settings.overrideMaterial,Is.Not.Null,renderer.name);
                Assert.That(outline.settings.overrideMaterial.shader.name,Is.EqualTo("Wildstone/SoldierOutline"));
                Assert.That(outline.settings.overrideMaterial.shader.isSupported,Is.True);
            }
        }
        static UniversalRenderPipelineAsset GraphicsSettingsDefault() =>
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
    }
}
