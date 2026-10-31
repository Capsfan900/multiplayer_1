// Ported from vibegame1 Assets/Shaders/VfxAdditive.shader (commit 76002b41, sha256 c004e90c...) under D028.
// Wildstone change: premultiplied blending. _DstBlend One (default) is the original additive look;
// OneMinusSrcAlpha draws occluding smoke/dust, whose fog fades toward the fog colour instead of black.
// _MaskChannel picks the mask (the EnemyForgeLab_vfx smoke/fire flipbooks keep it in A); _TopLight shades
// six-way smoke from its G (top-lit) channel so puffs read lit from the sun side.
// The one additive shader every player-side line, trail and particle draws with (graphics overhaul wave 2,
// lane vfx). It replaces two stand-ins: URP/Unlit (ignores vertex colour, so every line was a flat,
// hard-edged ribbon) and the 32 px procedural radial falloff the mist shipped with.
//
//   colour = min(vertexColour * _BaseColor, _PeakCap) * alpha * mask
//   mask   = _BaseMap.r (a flipbook frame, blended with the next when _FlipbookBlend = 1)
//          * lerp(1, _NoiseMap.r, _NoiseParams.x)      energy filaments that crawl along a line
//          * soft-particle and camera-near fades
//
// Every texture in Assets/Textures/Vfx is <= 1, so a mask can only take light away: the colour the
// caller budgets (SlashFx's 1.0 peak, FireSlashFx's 1.05, the dust's 0.55) stays the ceiling, and
// _PeakCap clamps it structurally on top of that. Additive fog fades toward BLACK, never toward the fog
// colour, which would add light (the SparkAdditive rule).
//
// _NoiseParams: x = strength (0 = off), y = tiling along u, z = tiling across v, w = u offset. The C#
// owner scrolls w on UNSCALED time (rule 1: hitstop must not freeze an effect), so the shader never
// reads _Time.
//
// Flipbook blending uses the particle system's custom vertex streams (UV, UV2, AnimBlend), laid out as
// TEXCOORD0.xy / TEXCOORD0.zw / TEXCOORD1.x — the same layout URP's Particles/Unlit uses. A LineRenderer
// supplies only TEXCOORD0.xy; with _FlipbookBlend = 0 nothing else is read.
//
// Camera fading keeps URP Particles' property names (_CameraFadeParams = near, 1/(far-near)) and keyword
// (_FADING_ON), so AmbientMist's pinned fade reads identically on either shader.
Shader "Wildstone/Vfx"
{
    Properties
    {
        _BaseMap ("Mask (R)", 2D) = "white" {}
        [HDR] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _NoiseMap ("Energy Noise (R)", 2D) = "white" {}
        _NoiseParams ("Noise: strength, tile u, tile v, offset u", Vector) = (0, 1, 1, 0)
        _PeakCap ("Peak Cap", Float) = 8
        _SoftFade ("Soft Particle Distance (0 = off)", Float) = 0
        _FlipbookBlend ("Flipbook Blend (0/1)", Float) = 0
        _FlipbookTiles ("Flipbook tiles (x, y), read by ArenaVfx", Vector) = (1, 1, 0, 0)
        _MaskChannel ("Mask channel weights", Vector) = (1, 0, 0, 0)
        _TopLight ("Six-way top light shading (G)", Range(0, 1)) = 0
        _CameraFadingEnabled ("Camera Fading", Float) = 0
        _CameraNearFadeDistance ("Camera Near Fade", Float) = 1
        _CameraFarFadeDistance ("Camera Far Fade", Float) = 2
        _CameraFadeParams ("Camera Fade Params", Vector) = (0, 1, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend (One additive, OneMinusSrcAlpha smoke)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Name "VfxForward"
            Tags { "LightMode"="UniversalForward" }
            Blend One [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_local _ _FADING_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                half4 _BaseColor;
                float4 _NoiseParams;
                half _PeakCap;
                float _SoftFade;
                half _FlipbookBlend;
                half _CameraFadingEnabled;
                float _CameraNearFadeDistance;
                float _CameraFarFadeDistance;
                float4 _CameraFadeParams;
                half _DstBlend;
                float4 _FlipbookTiles;
                half4 _MaskChannel;
                half _TopLight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;      // xy frame (or line uv), zw next frame
                float blend : TEXCOORD1;    // AnimBlend
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float3 extra : TEXCOORD1;   // x blend, y fog, z eye depth
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = float4(TRANSFORM_TEX(input.uv.xy, _BaseMap), TRANSFORM_TEX(input.uv.zw, _BaseMap));
                output.extra = float3(input.blend, ComputeFogFactor(output.positionCS.z),
                                      -TransformWorldToView(world).z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 frame = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                UNITY_BRANCH
                if (_FlipbookBlend > 0.5h)
                {
                    half4 next = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.zw);
                    frame = lerp(frame, next, (half)saturate(input.extra.x));
                }
                half mask = dot(frame, _MaskChannel);
                half shade = lerp(1.0h, lerp(0.55h, 1.2h, frame.g), _TopLight);

                UNITY_BRANCH
                if (_NoiseParams.x > 0.0)
                {
                    float2 nuv = float2(input.uv.x * _NoiseParams.y + _NoiseParams.w, input.uv.y * _NoiseParams.z);
                    half n = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, nuv).r;
                    mask *= lerp(1.0h, n, (half)_NoiseParams.x);
                }

                float eye = input.extra.z;
                UNITY_BRANCH
                if (_SoftFade > 0.0)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float scene = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    mask *= (half)saturate((scene - eye) / _SoftFade);
                }

                #if defined(_FADING_ON)
                    mask *= (half)saturate((eye - _CameraFadeParams.x) * _CameraFadeParams.y);
                #endif

                half alpha = input.color.a * _BaseColor.a * mask;
                half3 color = min(input.color.rgb * _BaseColor.rgb * shade, (half3)_PeakCap) * alpha;
                half occluding = (half)step(1.5, _DstBlend); // BlendMode.One is 1; OneMinusSrcAlpha is 10
                color = MixFogColor(color, unity_FogColor.rgb * alpha * occluding, (half)input.extra.y);
                return half4(color, alpha * occluding);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
