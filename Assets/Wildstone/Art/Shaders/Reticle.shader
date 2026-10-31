// Wildstone optic glass (D029 sights): a parallax-free holo/red-dot reticle projected at infinity.
// The glass face normal is the weapon's bore axis (weapon-kit contract), so the dot sits where the bore
// points from any eye position; off-axis the dot slides across the glass instead of sticking to it.
// Drawn additively with Cull Off because the authored face points away from the eye.
Shader "Wildstone/Reticle"
{
    Properties
    {
        [HDR] _ReticleColor ("Reticle", Color) = (3.2, 0.55, 0.35, 1)
        _GlassTint ("Glass tint", Color) = (0.05, 0.09, 0.1, 1)
        _DotRadius ("Dot radius (rad)", Float) = 0.0022
        _RingRadius ("Ring radius (rad, 0 off)", Float) = 0.016
        _RingWidth ("Ring width (rad)", Float) = 0.0016
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "Reticle"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ReticleColor, _GlassTint;
                float _DotRadius, _RingRadius, _RingWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 bore = normalize(input.normalWS);
                float3 view = normalize(input.positionWS - GetCameraPositionWS());
                if (dot(view, bore) < 0) bore = -bore;
                float3 right = normalize(cross(abs(bore.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0), bore));
                float3 up = cross(bore, right);
                float along = max(dot(view, bore), 1e-3);
                float2 angle = float2(dot(view, right), dot(view, up)) / along;
                float r = length(angle);
                float aa = fwidth(r) + 1e-5;
                float dotMask = 1 - smoothstep(_DotRadius - aa, _DotRadius + aa, r);
                float ring = _RingRadius > 0 ? 1 - smoothstep(_RingWidth - aa, _RingWidth + aa, abs(r - _RingRadius)) : 0;
                float glow = exp(-r / (_DotRadius * 3)) * 0.25;
                return half4(_ReticleColor.rgb * saturate(dotMask + ring * 0.8 + glow) + _GlassTint.rgb * 0.15, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
