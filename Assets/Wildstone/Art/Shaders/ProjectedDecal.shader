// Wildstone box-projected decal (D028): drawn on a unit cube, it reconstructs the scene position from the
// camera depth texture and paints only the geometry inside the box, so a hot-zone disc or a scorch mark
// follows steps and ramps without URP's decal feature. Only surfaces facing the box's up axis (_MinUp), so a box
// turned onto a wall marks the wall.
//   _Mode 0: hot zone. Hex lattice, outward scan pulse, travel chevron and contested flicker, painted (premultiplied hue,
//            not added light) as a solid hue ring over a dark track band just outside the rim, so it holds on ivory stone.
//            _Track is that band's width in metres; the box must be that much wider than the zone on each side.
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
        _Track ("Zone dark track width (m, mode 0)", Float) = 0
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
                float _Mode, _Opacity, _MinUp, _Track, _Pulse, _Clock;
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
                    float metres = length(mul(GetObjectToWorldMatrix(), float4(1, 0, 0, 0)).xyz);
                    float radiusM = max(metres * 0.5 - _Track, 0.01);   // the zone itself, inside the track band
                    float2 p = local.xz * 2 * (metres * 0.5 / radiusM); // unit disc of the zone
                    float r = length(p);
                    float edgeWidth = fwidth(r) * 1.5 + 0.004;
                    float disc = 1 - smoothstep(1 - edgeWidth, 1, r);
                    float rim = smoothstep(0.86, 0.93, r) * disc;
                    float trackEdge = _Track / radiusM;
                    float track = (1 - disc) * (1 - smoothstep(1 + trackEdge * 0.55, 1 + trackEdge * 0.97 + 0.0001, r));
                    float hex = 1 - smoothstep(0, 0.06, HexEdge(local.xz * metres / 0.9)); // 0.9 m cells
                    float scan = frac(r - _Clock * 0.35);
                    float pulse = smoothstep(0.0, 0.08, scan) * (1 - smoothstep(0.08, 0.2, scan)) * smoothstep(0.15, 0.6, r);
                    float2 travel = _Travel.xy;
                    float along = dot(p, travel), across = abs(p.x * travel.y - p.y * travel.x);
                    float chevronPhase = frac(along * 2.2 - _Clock * 1.2 - across * 2.2);
                    float chevron = (1 - smoothstep(0.0, 0.18, abs(chevronPhase - 0.5) - 0.3)) * step(across, 0.35) *
                        smoothstep(0.1, 0.4, along) * (1 - smoothstep(0.62, 0.8, along)) * saturate(_Travel.z * 4);
                    float flicker = 1 + _Pulse * (0.5 + 0.5 * sin(_Clock * 18));
                    // Layers, bottom to top, premultiplied: dark track (also under the rim), hue fill (about 20-30 % alpha),
                    // hue rim (about 85 %). Flicker lifts the fill's alpha and the rim's brightness.
                    half aDark = saturate(track + rim) * 0.6;
                    half aFill = saturate((0.20 + 0.10 * hex * (1 - r * 0.4) + 0.20 * pulse + 0.30 * chevron) * flicker) * disc;
                    half aRim = rim * 0.85;
                    color = half3(0.01, 0.012, 0.03) * aDark;
                    alpha = aDark;
                    color = _BaseColor.rgb * aFill + color * (1 - aFill);
                    alpha = aFill + alpha * (1 - aFill);
                    color = _RimColor.rgb * flicker * aRim + color * (1 - aRim);
                    alpha = aRim + alpha * (1 - aRim);
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
