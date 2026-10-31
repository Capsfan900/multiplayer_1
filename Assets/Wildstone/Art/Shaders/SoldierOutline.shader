// Remote soldier silhouette (readability): ivory armour on ivory stone loses its edge at range, so every soldier on
// the Enemy layer gets a thin dark inverted-hull outline. RenderingSetup's "Soldier outline" RenderObjects pass
// draws it as an override material after opaques (both tiers). The hull grows a fixed number of pixels (scaled to 1080p) in clip
// space, so it stays readable at range without thickening up close; it takes scene fog like the body it rims.
Shader "Wildstone/SoldierOutline"
{
    Properties
    {
        _OutlineColor ("Outline colour", Color) = (0.05, 0.055, 0.065, 1)
        _Width ("Width (pixels at 1080p)", Range(0, 4)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _Width;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Pushed 6 cm away from the camera, the hulls of overlapping armour plates hide behind the plates
                // in front of them: only the outer silhouette, metres in front of the background, keeps its rim.
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                positionVS.z -= 0.06;
                float4 positionCS = TransformWViewToHClip(positionVS);
                float2 normalCS = TransformWorldToHClipDir(TransformObjectToWorldNormal(input.normalOS)).xy;
                float len = length(normalCS);
                // Clip-space xy spans 2 per screen; multiplying by w keeps the push constant in pixels at any depth.
                if (len > 1e-5)
                    positionCS.xy += normalCS / len * (_Width * 2 * _ScreenParams.y / 1080) / _ScreenParams.xy * positionCS.w;
                output.positionCS = positionCS;
                output.fog = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
