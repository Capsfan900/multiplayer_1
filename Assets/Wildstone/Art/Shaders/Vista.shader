// Wildstone distant skyline (D028): sun-shaded silhouettes that fade toward the horizon haze with
// distance (aerial perspective), independent of the arena's linear fog which would erase them.
Shader "Wildstone/Vista"
{
    Properties
    {
        _BaseColor ("Stone", Color) = (0.66, 0.64, 0.6, 1)
        _HazeColor ("Haze (= sky horizon)", Color) = (0.69, 0.73, 0.75, 1)
        _HazeDistance ("Full haze distance (m)", Float) = 900
        _HazeMax ("Maximum haze", Range(0, 1)) = 0.82
        _SunDirection ("Direction toward the sun (world)", Vector) = (0.42, 0.69, 0.59, 0)
        [HDR] _WindowColor ("Window glints", Color) = (1.2, 0.8, 0.45, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+400" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _HazeColor, _WindowColor;
                float4 _SunDirection;
                float _HazeDistance, _HazeMax;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float lit = 0.55 + 0.45 * saturate(dot(n, normalize(_SunDirection.xyz))) + 0.08 * n.y;
                // Sparse warm window bands on side faces, by world height.
                float band = step(0.8, frac(input.positionWS.y / 3.2)) * step(0.5, frac(dot(input.positionWS.xz, 0.21))) * (1 - abs(n.y));
                float3 color = _BaseColor.rgb * lit + _WindowColor.rgb * band * 0.35;
                float haze = saturate(distance(input.positionWS, _WorldSpaceCameraPos) / _HazeDistance) * _HazeMax;
                haze = max(haze, saturate(1 - input.positionWS.y / 60) * 0.35); // ground mist at the tower feet
                return half4(lerp(color, _HazeColor.rgb, haze), 1);
            }
            ENDHLSL
        }
    }
}
