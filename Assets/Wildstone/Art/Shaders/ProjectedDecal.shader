// Wildstone box-projected decal (D028): drawn on a unit cube, it reconstructs the scene position from the
// camera depth texture and paints only the geometry inside the box, so a hot-zone disc or a scorch mark
// follows steps and ramps without URP's decal feature. Only surfaces facing the box's up axis (_MinUp), so a box
// turned onto a wall marks the wall.
//   _Mode 0: hot zone. Soft rim, hex lattice, outward scan pulse, travel chevron and contested flicker.
//   _Mode 1: texture decal (scorch). _BaseMap alpha (or red) darkens the surface.
Shader "Wildstone/Projected Decal"
{
    Properties
    {
        _BaseMap ("Mask (scorch)", 2D) = "white" {}
        [HDR] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        [HDR] _RimColor ("Rim", Color) = (1, 1, 1, 1)
        _Mode ("Mode (0 zone, 1 texture)", Float) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        _MinUp ("Minimum surface up (cos)", Range(0, 1)) = 0.55
        _Travel ("Travel direction (object xz) + speed", Vector) = (0, 1, 0, 0)
        _Pulse ("Contested pulse", Range(0, 1)) = 0
        _Clock ("Unscaled clock (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" "IgnoreProjector"="True" }
        Pass
        {
            Name "ProjectedDecal"
            Tags { "LightMode"="UniversalForward" }
            Cull Front
            ZTest Always
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _RimColor;
                float _Mode, _Opacity, _MinUp, _Pulse, _Clock;
                float4 _Travel;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // Distance to the nearest hex-cell edge (0 on the edge), cells of unit size.
            float HexEdge(float2 p)
            {
                const float2 s = float2(1, 1.7320508);
                float4 c = floor(float4(p, p - float2(0.5, 1)) / s.xyxy) + 0.5;
                float4 h = float4(p - c.xy * s, p - (c.zw + 0.5) * s);
                float2 q = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw;
                q = abs(q);
                return 0.5 - max(dot(q, s * 0.5), q.x);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 local = TransformWorldToObject(world);
                clip(0.5 - abs(local));
                float3 normal = normalize(cross(ddy(world), ddx(world)));
                float3 up = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0)));
                float facing = saturate((dot(normal, up) - _MinUp) / max(1 - _MinUp, 0.01) * 3);
                float fog = ComputeFogFactor(TransformWorldToHClip(world).z);

                half3 color;
                half alpha;
                if (_Mode < 0.5)
                {
                    float2 p = local.xz * 2;                 // unit disc
                    float r = length(p);
                    float edgeWidth = fwidth(r) * 1.5 + 0.004;
                    float disc = 1 - smoothstep(1 - edgeWidth, 1, r);
                    float rim = smoothstep(0.86, 0.985, r) * disc;
                    float metres = length(mul(GetObjectToWorldMatrix(), float4(1, 0, 0, 0)).xyz);
                    float hex = 1 - smoothstep(0, 0.06, HexEdge(local.xz * metres / 0.9)); // 0.9 m cells
                    float scan = frac(r - _Clock * 0.35);
                    float pulse = smoothstep(0.0, 0.08, scan) * (1 - smoothstep(0.08, 0.2, scan)) * smoothstep(0.15, 0.6, r);
                    float2 travel = _Travel.xy;
                    float along = dot(p, travel), across = abs(p.x * travel.y - p.y * travel.x);
                    float chevronPhase = frac(along * 2.2 - _Clock * 1.2 - across * 2.2);
                    float chevron = (1 - smoothstep(0.0, 0.18, abs(chevronPhase - 0.5) - 0.3)) * step(across, 0.35) *
                        smoothstep(0.1, 0.4, along) * (1 - smoothstep(0.62, 0.8, along)) * saturate(_Travel.z * 4);
                    float flicker = 1 + _Pulse * (0.5 + 0.5 * sin(_Clock * 18));
                    float fill = 0.10 + 0.22 * hex * (1 - r * 0.4) + 0.35 * pulse + 0.45 * chevron;
                    color = (_BaseColor.rgb * fill + _RimColor.rgb * rim * 1.4) * flicker;
                    alpha = saturate((0.16 + 0.12 * hex) * disc + rim * 0.5) * 0.35;
                    color *= disc;
                }
                else
                {
                    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, local.xz + 0.5);
                    alpha = tex.a * tex.r;
                    color = _BaseColor.rgb * alpha;
                }
                half fade = facing * _Opacity;
                color *= fade;
                alpha *= fade;
                color = MixFogColor(color, unity_FogColor.rgb * alpha, fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
