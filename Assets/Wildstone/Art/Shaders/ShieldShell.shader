// Active-shield read (2026-10-30): an additive energy shell around a shielded soldier. A fresnel rim keeps its
// centre clear, so the shell never hides the body it protects; faint bands climb it on the C#-owned unscaled
// _Clock (Vfx rule: the shader never reads _Time). ArenaEffects drives _Strength per player through a property
// block from the remaining shield, so the shell dims as it is drained. Front faces only, so a shielded camera
// inside its own shell sees nothing.
Shader "Wildstone/ShieldShell"
{
    Properties
    {
        [HDR] _BaseColor ("Colour", Color) = (0.8, 1.2, 1.3, 1)
        _Power ("Rim power", Range(0.5, 8)) = 2.6
        _Strength ("Strength", Range(0, 1)) = 1
        _Clock ("Clock (unscaled seconds)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Shell"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Power, _Strength, _Clock;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float height : TEXCOORD2; float fog : TEXCOORD3; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.positionOS.y;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1 - saturate(abs(dot(normalize(input.normalWS), view))), _Power);
                // Thin bands rising up the shell, strongest toward the rim.
                half bands = smoothstep(0.82, 1.0, frac(input.height * 9 - _Clock * 0.8)) * (0.25 + rim);
                half3 color = _BaseColor.rgb * (rim + bands * 0.35) * _Strength;
                // Additive fog fades toward black, never toward the fog colour (the Vfx rule).
                return half4(color * ComputeFogIntensity(input.fog), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
