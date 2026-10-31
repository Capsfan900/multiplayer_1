// Wildstone derez (D028 death read): the eliminated soldier freezes and breaks into world-space voxels from the
// helmet down. Each 4.5 cm cell gets a hash; a cell clips once the sweep passes it, and the cells just ahead of the
// front glow in the team colour on a pixel grid. ArenaVfx emits the matching voxel particles (PlayerVisual.Derez).
// _Progress, _Bottom, _Height and _EdgeColor arrive per renderer through a MaterialPropertyBlock.
Shader "Wildstone/Derez"
{
    Properties
    {
        _BaseColor ("Base colour", Color) = (0.7, 0.7, 0.7, 1)
        [HDR] _EdgeColor ("Edge colour", Color) = (0.4, 2.2, 2.6, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        _Bottom ("Body bottom (world y)", Float) = 0
        _Height ("Body height", Float) = 1.8
        _Cell ("Voxel size (m)", Float) = 0.045
        _Band ("Noise band", Range(0, 1)) = 0.35
        _Edge ("Glow band", Range(0, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _EdgeColor;
            float _Progress, _Bottom, _Height, _Cell, _Band, _Edge;
        CBUFFER_END

        // Distance of this voxel past the sweep front: > 0 clipped, (-_Edge, 0] glowing.
        float DerezFront(float3 positionWS, out float3 cellFraction)
        {
            float3 cell = floor(positionWS / _Cell);
            cellFraction = frac(positionWS / _Cell);
            float hash = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            float height = saturate((cell.y * _Cell - _Bottom) / max(_Height, 0.1));
            return height * (1 - _Band) + hash * _Band - (1 - _Progress);
        }
        ENDHLSL

        Pass
        {
            Name "Derez"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 fraction;
                float front = DerezFront(input.positionWS, fraction);
                clip(-front);
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lit = _BaseColor.rgb * (SampleSH(normal) + light.color * saturate(dot(normal, light.direction)) * light.shadowAttenuation);
                // Cells about to go read as lit pixels: a solid team tint with a darker grid line between voxels.
                float glow = saturate(1 + front / max(_Edge, 1e-3));
                float3 grid = step(0.12, fraction) * step(fraction, 0.88);
                half pixel = lerp(0.35, 1, grid.x * grid.y * grid.z);
                half3 color = lerp(lit, _EdgeColor.rgb * pixel, glow * glow);
                // A faint scan tint over the whole body once the derez has begun.
                color += _EdgeColor.rgb * 0.08 * step(0.001, _Progress) * (0.5 + 0.5 * sin(input.positionWS.y * 90 - _Time.y * 40));
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 fraction;
                clip(-DerezFront(input.positionWS, fraction));
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half Frag(Varyings input) : SV_Target
            {
                float3 fraction;
                clip(-DerezFront(input.positionWS, fraction));
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
