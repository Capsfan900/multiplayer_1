// Wildstone daylight sky (D028): zenith/horizon gradient, sun disc and halo, drifting cloud band.
// The horizon colour equals the scene fog colour so fogged geometry dissolves into the sky.
Shader "Wildstone/Day Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.32, 0.50, 0.74, 1)
        _HorizonColor ("Horizon haze (= fog colour)", Color) = (0.69, 0.73, 0.75, 1)
        _GroundColor ("Below horizon", Color) = (0.45, 0.45, 0.43, 1)
        _SunDirection ("Direction toward the sun (world)", Vector) = (0.42, 0.69, 0.59, 0)
        [HDR] _SunColor ("Sun", Color) = (7, 6.3, 5.4, 1)
        _SunSize ("Sun disc (cosine)", Range(0.999, 0.99999)) = 0.99965
        _HaloStrength ("Halo", Range(0, 2)) = 0.7
        _CloudColor ("Clouds", Color) = (0.96, 0.95, 0.93, 1)
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.42
        _CloudSpeed ("Cloud drift", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudColor;
                float4 _SunDirection;
                float _SunSize, _HaloStrength, _CloudCover, _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float sum = 0, amplitude = 0.5;
                for (int octave = 0; octave < 5; octave++) { sum += Noise(p) * amplitude; p = p * 2.03 + 17.1; amplitude *= 0.5; }
                return sum;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float3 sun = normalize(_SunDirection.xyz);
                float h = d.y;
                float mu = saturate(dot(d, sun));
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(h), 0.5));
                float3 color = h >= 0 ? sky : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-h * 5));
                // Warm forward scattering around the sun, broad then tight.
                color += _SunColor.rgb * _HaloStrength * (0.012 * pow(mu, 6) + 0.05 * pow(mu, 90));
                if (h > 0)
                {
                    float2 uv = d.xz / (h + 0.1) * 0.55 + _Time.y * _CloudSpeed;
                    float n = Fbm(uv);
                    float cover = smoothstep(1 - _CloudCover, 1.25 - _CloudCover, n) * smoothstep(0.0, 0.15, h);
                    float3 cloud = _CloudColor.rgb * (0.82 + 0.3 * pow(mu, 4)) * lerp(1.0, 0.86, saturate(n * 1.4 - 0.5));
                    color = lerp(color, cloud, cover * 0.85);
                }
                color += _SunColor.rgb * smoothstep(_SunSize, _SunSize + 0.00015, dot(d, sun));
                color = lerp(color, _HorizonColor.rgb, exp(-abs(h) * 16) * 0.55);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
