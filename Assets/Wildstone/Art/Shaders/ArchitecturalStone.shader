// Adapted from vibegame1 Assets/Shaders/ArchitecturalStone.shader @ 2cb6e57 (read-only source).
// Wildstone additions: emissive inlay (_InlayEmission) and a Meta pass for baked bounce.
Shader "Wildstone/Architectural Stone"
{
    Properties
    {
        _BaseColor ("Cold Stone", Color) = (0.28, 0.32, 0.38, 1)
        _EmissionColor ("Existing Readability Floor", Color) = (0, 0, 0, 1)
        _Smoothness ("Stone Smoothness", Range(0, 1)) = 0.22
        _Metallic ("Metallic", Range(0, 1)) = 0
        _SpecularHighlights ("Specular Highlights", Float) = 1
        _BlockSize ("Slab Width / Depth / Course Height (m)", Vector) = (2.8, 1.4, 0.7, 0)
        _JointWidth ("Joint Width (m)", Range(0.005, 0.06)) = 0.018
        _GrainStrength ("Stone Grain", Range(0, 0.3)) = 0.12
        _EdgeWear ("Edge Wear", Range(0, 0.3)) = 0.14
        _ReliefDepth ("Surface Relief (m)", Range(0, 0.02)) = 0.008

        // ---- Face-aware value blocking (surfaces spec 2026-09-17, section 3.2) ------------------
        // Every world pixel is one of three ROLES and the role is decided here, from the face normal:
        // DECK (you stand on it, pale, calm), WALL (you run past it, dark, plain) and VOID (an
        // underside, darkest). One material, three values - the Neon White "white deck, everything
        // else recedes" read delivered inside this game's brightness-coded tell budget.
        _DeckColor ("Deck (top face) colour", Color) = (0.35, 0.40, 0.47, 1)
        _VoidColor ("Underside colour", Color) = (0.06, 0.07, 0.09, 1)
        _DeckSmoothness ("Deck smoothness", Range(0, 1)) = 0.22
        _DeckBlock ("Deck slab size m (0 = use _BlockSize)", Vector) = (2.8, 2.8, 0, 0)
        _WallBlock ("Wall panel w / h m (0 = use _BlockSize)", Vector) = (2.8, 1.4, 0, 0)
        // ---- Real bevels (VFX pass 2026-09-22, items 3-4) ----------------------------------------
        // A generated LevelMesh carries its chamfer as GEOMETRY: its bevel strips arrive with u <= -1 and
        // are the band outright; on a flat face _EdgeBand is only the hairline cut line at the strip's
        // boundary (0.015 m; legacy primitive cubes with no strips keep whatever band their material sets).
        _EdgeBand ("Band width m on flat faces (hairline on bevelled meshes)", Float) = 0.015
        _EdgeBandTint ("Chamfer band brightness", Float) = 1.28
        _SideLight ("Side light (masonry-only azimuthal ambient)", Color) = (0, 0, 0, 1)
        _SideDir ("Side light direction (world, unit)", Vector) = (-0.807, 0.202, 0.555, 0)
        _BaseShade ("Wall foot shade (first 3 m)", Range(0, 1)) = 0.72
        _EdgeSheen ("Bevel edge sheen", Range(0, 0.5)) = 0.12
        _InlayHeight ("Wall inlay line height m (0 off)", Float) = 1.40
        _InlayWidth ("Inlay width m", Float) = 0.05
        [HDR] _InlayEmission ("Inlay light strip (emissive, walls only)", Color) = (0, 0, 0, 1)
        _DetailTiling ("Metres per detail tile", Float) = 2.5
        _NormalStrength ("Normal", Range(0, 1)) = 0.30
        _CavityStrength ("Cavity", Range(0, 1)) = 0.25
        _TopDetailScale ("Detail on decks", Range(0, 1)) = 0.35
        _ChipStrength ("Edge chipping accent", Range(0, 1)) = 0.20
        _ZoneAccent ("Zone accent (inlay + chamfer tint)", Color) = (1, 1, 1, 1)
        _CourseBand ("Inset every Nth wall course (0 off)", Float) = 0
        [Normal] _DetailNormal ("Surface normal (tiling)", 2D) = "bump" {}
        _DetailMask ("Mask R=cavity G=height B=noise A=chip", 2D) = "white" {}

        // ---- Photo surfaces (graphics overhaul 2026-09-24, surfaces lane) ------------------------
        // CC0 photo-scanned stone sampled in world metres per face ROLE (deck set on DECK, wall set on
        // WALL and VOID). MEAN-NORMALISED (global contract 4): each albedo is divided by its own linear
        // mean (_DeckAlbedoMean / _WallAlbedoMean, measured from the shipped file by PhotoSurfaceFactory),
        // so _DeckColor / _BaseColor still ARE the average albedo and every luminance test keeps holding.
        // The photo adds detail, never brightness. _PhotoBlend 0 is the procedural shader exactly as it
        // was; 1 swaps the procedural joint / course / slab grid (which would clash) for the photo.
        _PhotoBlend ("Photo surface (0 = procedural joints)", Range(0, 1)) = 0
        [NoScaleOffset] _DeckAlbedo ("Deck photo albedo (sRGB)", 2D) = "grey" {}
        [NoScaleOffset][Normal] _DeckNormal ("Deck photo normal (OpenGL +Y)", 2D) = "bump" {}
        [NoScaleOffset] _DeckMask ("Deck photo mask R=rough G=AO B=height", 2D) = "grey" {}
        [NoScaleOffset] _WallAlbedo ("Wall photo albedo (sRGB)", 2D) = "grey" {}
        [NoScaleOffset][Normal] _WallNormal ("Wall photo normal (OpenGL +Y)", 2D) = "bump" {}
        [NoScaleOffset] _WallMask ("Wall photo mask R=rough G=AO B=height", 2D) = "grey" {}
        _DeckAlbedoMean ("Deck albedo linear mean (rgb) + mean roughness (a)", Vector) = (0.5, 0.5, 0.5, 0.5)
        _WallAlbedoMean ("Wall albedo linear mean (rgb) + mean roughness (a)", Vector) = (0.5, 0.5, 0.5, 0.5)
        _PhotoAOMean ("Scan AO mean (x deck, y wall): the AO pivots about it, the ambient keeps its mean", Vector) = (1, 1, 0, 0)
        _PhotoTiling ("Metres per photo tile (x deck, y wall)", Vector) = (2.8, 2.8, 0, 0)
        _PhotoRows ("Courses per tile: stochastic shifts are whole courses (x deck, y wall; 0 none)", Vector) = (0, 0, 0, 0)
        _PhotoCells ("Stochastic cells per tile", Float) = 1.35
        _PhotoContrast ("Photo contrast (x deck, y wall; 1 = as scanned)", Vector) = (1, 1, 0, 0)
        _PhotoNormal ("Photo normal strength (x deck, y wall)", Vector) = (1, 1, 0, 0)
        _PhotoSaturation ("Photo saturation (0 = luminance only: the zone tint rules)", Range(0, 1)) = 0.45
        _PhotoRoughness ("Photo roughness variation", Range(0, 2)) = 1
        _PhotoOcclusion ("Photo AO", Range(0, 1)) = 0.8
        _PhotoHeightBlend ("Stochastic seams follow the scan's relief", Range(0, 2)) = 0.5
        _MacroVariation ("Low-frequency value variation", Range(0, 0.5)) = 0.12
        _MacroScale ("Macro variation period (m)", Float) = 9
        _FarVariation ("Far-scale photo overlay", Range(0, 1)) = 0.25
        _GrimeStrength ("Cavity grime (patchy; x1.7 at most)", Range(0, 0.55)) = 0.28
        _GrimeStreaks ("Rain streaks down walls", Range(0, 0.6)) = 0.50
        _GrimeColor ("Grime tint (multiplies; cold)", Color) = (0.52, 0.57, 0.66, 1)
        _MossColor ("Cold moss", Color) = (0.26, 0.44, 0.28, 1)
        _MossStrength ("Moss in upward crevices", Range(0, 1)) = 0.80
        _SmoothnessCeiling ("Photo smoothness ceiling", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        // One identical material buffer in every pass keeps SRP batching valid. Pattern coordinates
        // come from world position and the primitive's UVs, both preserved by Unity static batching.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            float4 _BlockSize;
            half _Smoothness;
            half _Metallic;
            half _SpecularHighlights;
            float _JointWidth;
            float _GrainStrength;
            float _EdgeWear;
            float _ReliefDepth;
            half4 _DeckColor;
            half4 _VoidColor;
            half4 _ZoneAccent;
            float4 _DeckBlock;
            float4 _WallBlock;
            float4 _DetailNormal_ST;
            float4 _DetailMask_ST;
            half _DeckSmoothness;
            float _EdgeBand;
            float _EdgeBandTint;
            half4 _SideLight;
            float4 _SideDir;
            float _BaseShade;
            float _EdgeSheen;
            float _InlayHeight;
            float _InlayWidth;
            half4 _InlayEmission;
            float _DetailTiling;
            float _NormalStrength;
            float _CavityStrength;
            float _TopDetailScale;
            float _ChipStrength;
            float _CourseBand;
            float4 _DeckAlbedoMean;
            float4 _WallAlbedoMean;
            float4 _PhotoTiling;
            float4 _PhotoRows;
            float4 _PhotoContrast;
            float4 _PhotoNormal;
            float4 _PhotoAOMean;
            half4 _GrimeColor;
            half4 _MossColor;
            float _PhotoBlend;
            float _PhotoCells;
            float _PhotoSaturation;
            float _PhotoRoughness;
            float _PhotoOcclusion;
            float _PhotoHeightBlend;
            float _MacroVariation;
            float _MacroScale;
            float _FarVariation;
            float _GrimeStrength;
            float _GrimeStreaks;
            float _MossStrength;
            float _SmoothnessCeiling;
        CBUFFER_END

        // Textures live OUTSIDE UnityPerMaterial: a texture handle in the buffer breaks SRP batching.
        TEXTURE2D(_DetailNormal);   SAMPLER(sampler_DetailNormal);
        TEXTURE2D(_DetailMask);     SAMPLER(sampler_DetailMask);
        // The six photo maps share ONE sampler (PhotoSurfaceFactory imports them identically: repeat,
        // trilinear, anisotropic 8), so the pass spends three sampler slots, not eight.
        TEXTURE2D(_DeckAlbedo);     SAMPLER(sampler_DeckAlbedo);
        TEXTURE2D(_DeckNormal);
        TEXTURE2D(_DeckMask);
        TEXTURE2D(_WallAlbedo);
        TEXTURE2D(_WallNormal);
        TEXTURE2D(_WallMask);
        ENDHLSL

        Pass
        {
            Name "ArchitecturalForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StoneVertex
            #pragma fragment StoneFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct StoneAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct StoneVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 fogAndVertexLight : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                half4 color : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            StoneVaryings StoneVertex(StoneAttributes input)
            {
                StoneVaryings output = (StoneVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                output.fogAndVertexLight = half4(ComputeFogFactor(pos.positionCS.z),
                    VertexLighting(pos.positionWS, output.normalWS));
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            float StoneHash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float StoneNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(StoneHash(cell), StoneHash(cell + float2(1, 0)), f.x),
                            lerp(StoneHash(cell + float2(0, 1)), StoneHash(cell + 1), f.x), f.y);
            }

            // ---- Photo surfaces: stochastic tiling (mirrored function-for-function by
            // Tools/env_models/surface_photo/photo_math.py, which the lane's preview and mean check run) -
            #define PHOTO_BLEND_DEPTH 0.12
            #define PHOTO_FAR_SCALE 0.27
            #define PHOTO_FAR_BLUR 24.0
            static const half3 PhotoLuma = half3(0.2126h, 0.7152h, 0.0722h);

            // Triangle grid (Heitz & Neyret 2018 / Mikkelsen 2022 "hex tiling"): barycentric weights and
            // the three vertex ids around st.
            void PhotoTriangleGrid(float2 st, out float3 w, out float2 v1, out float2 v2, out float2 v3)
            {
                float2 k = float2(st.x - 0.57735027 * st.y, 1.15470054 * st.y);
                float2 b = floor(k);
                float3 t = float3(k - b, 0.0);
                t.z = 1.0 - t.x - t.y;
                float s = step(0.0, -t.z);
                float s2 = 2.0 * s - 1.0;
                w = float3(-t.z * s2, s - t.y * s2, s - t.x * s2);
                v1 = b + float2(s, s);
                v2 = b + float2(s, 1.0 - s);
                v3 = b + float2(1.0 - s, s);
            }

            // Rain streaks: runs of dark staining down a wall, darkest where the water leaves a joint or ledge
            // and fading out below it. 22 cm columns of random width; 1-3.5 m runs; how many runs are wet
            // follows a ~3 m "wet zone" noise, so the streaks gather in drainage bands instead of a barcode.
            // Ported line for line to PhotoSurfaceFactory.Streak (the normaliser samples it).
            float PhotoStreak(float2 chart)
            {
                float cx = chart.x / 0.22;
                float col = floor(cx);
                float width = lerp(0.2, 0.5, StoneHash(float2(col, 19.3)));
                float across = 1.0 - smoothstep(width * 0.4, width, abs(frac(cx) - 0.5));
                float len = lerp(1.0, 3.5, StoneHash(float2(col, 7.1)));
                float y = chart.y / len + StoneHash(float2(col, 2.3));
                float run = floor(y);
                float along = frac(y);
                float wetZone = StoneNoise(float2(col * 0.07 + 4.1, run * 0.3 + 8.2));   // constant along a run
                float on = step(lerp(0.95, 0.25, wetZone), StoneHash(float2(col, run + 13.7)));
                return on * across * along * along * lerp(0.6, 1.0, StoneHash(float2(run, col + 5.9)));
            }

            struct PhotoTap { half3 albedo; half3 normalTS; half3 mask; };

            // One stochastic copy of the tile: a random offset (vertically only by whole courses, so bed
            // joints stay level and deck rows stay straight) and a mirror (u always; v on decks only - a
            // wall's weathering runs downward). Explicit gradients from the CONTINUOUS uv: a copy boundary
            // never selects a blurred mip, and this is safe inside the role branch.
            PhotoTap PhotoTapAt(TEXTURE2D_PARAM(albedoMap, albedoSampler), TEXTURE2D_PARAM(normalMap, normalSampler),
                                TEXTURE2D_PARAM(maskMap, maskSampler), float2 vtx, float2 uv, float2 dx, float2 dy,
                                float rows, float mirrorV, float normalStrength)
            {
                float4 r = float4(StoneHash(vtx + float2(17.13, 3.71)), StoneHash(vtx + float2(-5.31, 29.9)),
                                  StoneHash(vtx * 1.618 + float2(7.7, -2.9)), StoneHash(vtx.yx + float2(41.2, -11.7)));
                float2 m = float2(r.z < 0.5 ? -1.0 : 1.0, (mirrorV > 0.5 && r.w < 0.5) ? -1.0 : 1.0);
                float2 o = float2(r.x, rows > 0.5 ? floor(r.y * rows) / rows : 0.0);
                float2 tuv = uv * m + o;
                float2 tdx = dx * m;
                float2 tdy = dy * m;
                PhotoTap tap;
                tap.albedo = SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, tuv, tdx, tdy).rgb;
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, tuv, tdx, tdy), normalStrength);
                tap.normalTS = half3(nt.xy * m, nt.z);   // back into the chart's own u/v
                tap.mask = SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, tuv, tdx, tdy).rgb;
                return tap;
            }

            // Three copies blended by barycentric weight SHARPENED BY THE SCAN'S HEIGHT, so a transition
            // runs along a joint or a pit instead of ghosting two stones over each other.
            void PhotoHexSample(TEXTURE2D_PARAM(albedoMap, albedoSampler), TEXTURE2D_PARAM(normalMap, normalSampler),
                                TEXTURE2D_PARAM(maskMap, maskSampler), float2 uv, float2 dx, float2 dy,
                                float rows, float mirrorV, float normalStrength,
                                out half3 albedo, out half3 normalTS, out half3 mask)
            {
                float3 w;
                float2 v1, v2, v3;
                PhotoTriangleGrid(uv * _PhotoCells, w, v1, v2, v3);
                PhotoTap a = PhotoTapAt(TEXTURE2D_ARGS(albedoMap, albedoSampler), TEXTURE2D_ARGS(normalMap, normalSampler),
                                        TEXTURE2D_ARGS(maskMap, maskSampler), v1, uv, dx, dy, rows, mirrorV, normalStrength);
                PhotoTap b = PhotoTapAt(TEXTURE2D_ARGS(albedoMap, albedoSampler), TEXTURE2D_ARGS(normalMap, normalSampler),
                                        TEXTURE2D_ARGS(maskMap, maskSampler), v2, uv, dx, dy, rows, mirrorV, normalStrength);
                PhotoTap c = PhotoTapAt(TEXTURE2D_ARGS(albedoMap, albedoSampler), TEXTURE2D_ARGS(normalMap, normalSampler),
                                        TEXTURE2D_ARGS(maskMap, maskSampler), v3, uv, dx, dy, rows, mirrorV, normalStrength);
                float3 hb = float3(a.mask.b, b.mask.b, c.mask.b) * _PhotoHeightBlend + w;
                float3 bw = max(hb - (max(hb.x, max(hb.y, hb.z)) - PHOTO_BLEND_DEPTH), 0.0);
                bw /= bw.x + bw.y + bw.z;
                albedo = a.albedo * bw.x + b.albedo * bw.y + c.albedo * bw.z;
                normalTS = normalize(a.normalTS * bw.x + b.normalTS * bw.y + c.normalTS * bw.z);
                mask = a.mask * bw.x + b.mask * bw.y + c.mask * bw.z;
            }

            // The far-scale overlay: the same scan 3.7x larger and ~4.6 mips blurred, as a luminance ratio
            // against the texture's OWN 1x1 mip - unit mean whatever space the mip chain was averaged in.
            half PhotoFarRatio(TEXTURE2D_PARAM(albedoMap, albedoSampler), float2 uv, float2 dx, float2 dy)
            {
                float k = PHOTO_FAR_SCALE * PHOTO_FAR_BLUR;
                half3 farTap = SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, uv * PHOTO_FAR_SCALE + float2(0.31, 0.67), dx * k, dy * k).rgb;
                half3 mipMean = SAMPLE_TEXTURE2D_LOD(albedoMap, albedoSampler, float2(0.5, 0.5), 16.0).rgb;
                return dot(farTap, PhotoLuma) / max(dot(mipMean, PhotoLuma), 1e-4h);
            }

            half4 StoneFragment(StoneVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 ng = n;                      // the geometric normal: every photo chart is built on it
                float3 an = abs(n);
                // A LevelMesh bevel strip or corner: u in [-2, -1]. Flat faces are 0..1, so nothing else
                // can raise it (a legacy cube never goes negative).
                float strip = step(input.uv.x, -0.5);
                // Large paving slabs above; narrower masonry courses down the sides. World metres
                // prevent a 30 m causeway stretching the same block over its entire length. The +0.01
                // breaks the exact tie a 45-degree strip makes (n.y == n.x) toward DECK, the same way on
                // every pixel: a top-edge strip is the pale lip, never a per-ulp flicker between roles.
                float top = step(max(an.x, an.z), an.y + 0.01);
                // (1) ROLE SELECT. deck = you stand on it, wall = you run past it, void = an underside.
                // The three are mutually exclusive and sum to 1, so every term below is one lerp.
                float under = step(n.y, -0.5);
                float deck = top * (1.0 - under);
                float wall = saturate(1.0 - deck - under);
                bool sideX = an.x > an.z + 0.01;   // same tie-break for a vertical strip
                float2 plane = top > 0.5 ? input.positionWS.xz :
                    (sideX ? input.positionWS.zy : input.positionWS.xy);
                // (4) Panels, not bricks: 2.8 x 2.8 slabs up top, 2.8 x 1.4 panels down the sides.
                // _BlockSize keeps its slot (and its test) and is read only when these are left at zero.
                float2 deckBlock = _DeckBlock.x > 0.001 ? _DeckBlock.xy : float2(_BlockSize.x, _BlockSize.y);
                float2 wallBlock = _WallBlock.x > 0.001 ? _WallBlock.xy : float2(_BlockSize.x, _BlockSize.z);
                float2 blockSize = max(lerp(wallBlock, deckBlock, top), 0.1);
                float2 grid = plane / blockSize;
                grid.x += frac(floor(grid.y) * 0.5);
                float2 cell = floor(grid);
                float2 edge = min(frac(grid), 1.0 - frac(grid)) * blockSize;
                float jointDistance = min(edge.x, edge.y);
                float footprint = max(length(ddx(plane)), length(ddy(plane)));
                float aa = max(footprint * 0.65, 0.002);
                // Under a photo surface the procedural grid (joints, courses, slab tones, grain, panel
                // grooves) is off - two block patterns would clash - but every property keeps its slot.
                float procedural = 1.0 - _PhotoBlend;
                float joint = (1.0 - smoothstep(_JointWidth, _JointWidth + aa, jointDistance)) * procedural;
                float grainFade = 1.0 - smoothstep(0.025, 0.10, footprint);
                float grain = (StoneNoise(plane * 18.0) - 0.5) * grainFade * procedural;
                float slabTone = lerp(1.0, lerp(0.94, 1.04, StoneHash(cell)), procedural);

                // (6) Course band: every Nth wall panel row sits back with a wider joint, so a 30 m
                // tower reads as coursed masonry instead of one tall face. Walls only. Under the photo the
                // band snaps to the SCAN's courses (process_photo_sets.py puts its bed joints exactly on
                // k * tile / rows), about one panel row tall, sits 25 % darker (the scan's own course-to-course
                // variation is +-20 %) and both its bed joints are shadowed grooves.
                float courseHit = 0.0;
                float courseShadow = 0.0;
                if (_CourseBand > 0.5)
                {
                    bool photoCourses = _PhotoBlend > 0.001;
                    float courseH = _PhotoRows.y > 0.5 ? _PhotoTiling.y / _PhotoRows.y : wallBlock.y;
                    float bandH = courseH * max(1.0, floor(wallBlock.y / courseH + 0.5));
                    float bandY = photoCourses ? plane.y / bandH : grid.y;
                    courseHit = wall * step(fmod(abs(floor(bandY)), _CourseBand), 0.5);
                    if (photoCourses)
                    {
                        float bandEdge = min(frac(bandY), 1.0 - frac(bandY)) * bandH;   // to its nearer bed joint
                        courseShadow = courseHit * _PhotoBlend *
                            (1.0 - smoothstep(_JointWidth, _JointWidth * 3.0 + aa, bandEdge));
                    }
                }
                joint = saturate(max(joint * lerp(1.0, 1.6, courseHit), courseShadow));

                // (5) Minimum surface texture. Decks stay the calmest surface in the game, so every
                // detail term is scaled by _TopDetailScale there; grainFade kills all of it at speed.
                float detailFade = lerp(1.0, _TopDetailScale, deck);
                float2 duv = plane / max(_DetailTiling, 0.01);
                half4 mask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, duv);
                half3 dn = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, duv),
                                             _NormalStrength * grainFade * detailFade);

                // Recover the face's physical UV dimensions from derivatives. Unlike object scale,
                // these survive static batching and work on pitched ramp slabs too.
                float3 dpdx = ddx(input.positionWS);
                float3 dpdy = ddy(input.positionWS);
                float2 duvdx = ddx(input.uv);
                float2 duvdy = ddy(input.uv);
                float detUV = duvdx.x * duvdy.y - duvdx.y * duvdy.x;
                float invDetUV = rcp(max(abs(detUV), 1e-12));
                float2 faceSize = float2(length(dpdx * duvdy.y - dpdy * duvdx.y),
                    length(dpdy * duvdx.x - dpdx * duvdy.x)) * invDetUV;
                float2 faceEdge = min(input.uv, 1.0 - input.uv) * faceSize;
                float borderDistance = min(faceEdge.x, faceEdge.y);
                // (2) CHAMFER BAND. On a generated mesh the bevel strip IS the chamfer (band 1) and the flat
                // face keeps a hairline cut line at its boundary; on a legacy cube _EdgeBand is the only
                // edge the block has.
                float band = max(strip, 1.0 - smoothstep(_EdgeBand, _EdgeBand + aa, borderDistance));
                float chip = _ChipStrength * band * mask.a;
                // The recessed panel line sits 0.15 m inside a big face and a fifth of the short side on a
                // small one (B1: a fixed 0.15 crossed the middle of a 0.35 m rubble block). Never on a strip.
                float grooveAt = min(0.15, 0.2 * min(faceSize.x, faceSize.y));
                float inset = (1.0 - smoothstep(0.012, 0.012 + aa, abs(borderDistance - grooveAt))) * 0.12 * (1.0 - strip)
                            * procedural + lerp(0.10, 0.25, _PhotoBlend) * courseHit;
                // (3) One pale inlay datum per wall bay at wall-run height. Non-emissive on purpose:
                // the emissive dashed bars (LevelDressingBuilder) are what says "run along this".
                float localHeight = input.uv.y * faceSize.y;
                float inlay = step(0.001, _InlayHeight) * wall *
                    (1.0 - smoothstep(_InlayWidth, _InlayWidth + aa, abs(localHeight - _InlayHeight)));

                // Millimetres of shading relief, never displacement. The collider, silhouette and
                // NavMesh therefore retain the exact authored cube. Suppress subpixel grain at speed.
                float height = (-joint + grain * 0.18) * _ReliefDepth;
                float3 r1 = cross(dpdy, n);
                float3 r2 = cross(n, dpdx);
                float det = dot(dpdx, r1);
                float3 gradient = (ddx(height) * r1 + ddy(height) * r2) *
                    (sign(det) / max(abs(det), 1e-8));
                n = normalize(n - clamp(gradient, -0.35, 0.35));
                // (The derivative-recovered chamfer TILT that lived here is gone: it flipped at the min()
                // switch and drew an X seam across every corner. The bevel is real geometry now.)

                // (10) PHOTO SURFACE. Every flat face gets an isometric chart from its own normal: level decks
                // map world x/z, walls run horizontally along the face and straight up it, and a PITCHED deck
                // (a ramp, a chamfer lip) lays its slab rows across the slope like real paving. A yawed ramp
                // or a rotated realm slab is therefore textured exactly like an axis-aligned box - no stretch,
                // no triplanar ghosting - and on axis-aligned faces the chart IS the procedural grid's plane,
                // so the 2.8 m tiles sit on the 2.8 m block grid. A smooth mesh has a varying normal (its chart
                // would swim), so it falls back to that dominant-axis plane. "Smooth" is measured as CURVATURE
                // (normal change per metre along each screen axis), never per pixel: a per-pixel test called a
                // 5 m-radius mesh flat once the camera was ~5 m away and the chart popped with distance.
                float3 axisU = top > 0.5 ? float3(1, 0, 0) : (sideX ? float3(0, 0, 1) : float3(1, 0, 0));
                float3 axisV = top > 0.5 ? float3(0, 0, 1) : float3(0, 1, 0);
                bool pitched = top > 0.5 && dot(ng.xz, ng.xz) > 0.0025;         // steeper than ~2.9 degrees
                float3 faceT = top > 0.5 ? (pitched ? float3(-ng.z, 0.0, ng.x) : axisU - ng * ng.x)
                                         : cross(float3(0, 1, 0), ng);
                faceT = normalize(faceT) * (dot(faceT, axisU) < 0.0 ? -1.0 : 1.0);
                float3 faceB = pitched ? normalize(cross(ng, faceT)) : normalize(axisV - ng * dot(ng, axisV));
                float curvature = max(length(ddx(ng)) / max(length(dpdx), 1e-6), length(ddy(ng)) / max(length(dpdy), 1e-6));
                bool curved = curvature > 0.02;     // 1/m: anything tighter than a 50 m radius
                float3 photoT = curved ? normalize(axisU - ng * dot(ng, axisU)) : faceT;
                float3 photoB = curved ? normalize(axisV - ng * dot(ng, axisV)) : faceB;
                float2 chart = curved ? plane : float2(dot(input.positionWS, faceT), dot(input.positionWS, faceB));
                bool useDeck = deck > 0.5;          // DECK samples the deck set; WALL and VOID the wall set
                float2 photoUV = chart / max(useDeck ? _PhotoTiling.x : _PhotoTiling.y, 0.01);
                // arch lane H-stone: the kit writes a per-stone id in vertex alpha (colour = (AO, 1, 0, id)); each stone
                // shows its own part of the scan, shifted by whole rows. Other meshes (white or no colour) are unchanged.
                float stoneId = input.color.a;
                float isStone = step(stoneId, 0.998) * saturate(input.color.g * (1.0 - input.color.b));
                float rowsN = max(useDeck ? _PhotoRows.x : _PhotoRows.y, 1.0);
                photoUV += isStone * float2(frac(stoneId * (useDeck ? 41.3 : 97.31)),
                                            floor(frac(stoneId * (useDeck ? 23.9 : 61.7)) * rowsN) / rowsN);
                float2 photoDx = ddx(photoUV);      // outside every branch: the taps use explicit gradients
                float2 photoDy = ddy(photoUV);
                half3 photoDetail = 1.0h;           // unit-mean albedo multiplier
                half3 photoMask = half3(0.5h, 1.0h, 0.5h);
                float photoMeanRough = 0.5;
                float photoAOMean = 1.0;
                float grime = 0.0;
                float moss = 0.0;
                float mossTone = 1.0;
                [branch] if (_PhotoBlend > 0.001)
                {
                    half3 pa, pn;
                    half farRatio;
                    // A branch, not a ?: select - HLSL evaluates both sides of ?:, which would sample both sets.
                    [branch] if (useDeck)
                    {
                        PhotoHexSample(TEXTURE2D_ARGS(_DeckAlbedo, sampler_DeckAlbedo), TEXTURE2D_ARGS(_DeckNormal, sampler_DeckAlbedo),
                                       TEXTURE2D_ARGS(_DeckMask, sampler_DeckAlbedo), photoUV, photoDx, photoDy,
                                       _PhotoRows.x, 1.0, _PhotoNormal.x, pa, pn, photoMask);
                        farRatio = PhotoFarRatio(TEXTURE2D_ARGS(_DeckAlbedo, sampler_DeckAlbedo), photoUV, photoDx, photoDy);
                    }
                    else
                    {
                        PhotoHexSample(TEXTURE2D_ARGS(_WallAlbedo, sampler_DeckAlbedo), TEXTURE2D_ARGS(_WallNormal, sampler_DeckAlbedo),
                                       TEXTURE2D_ARGS(_WallMask, sampler_DeckAlbedo), photoUV, photoDx, photoDy,
                                       _PhotoRows.y, 0.0, _PhotoNormal.y, pa, pn, photoMask);
                        farRatio = PhotoFarRatio(TEXTURE2D_ARGS(_WallAlbedo, sampler_DeckAlbedo), photoUV, photoDx, photoDy);
                    }
                    float4 stats = useDeck ? _DeckAlbedoMean : _WallAlbedoMean;
                    photoMeanRough = stats.a;
                    photoAOMean = useDeck ? _PhotoAOMean.x : _PhotoAOMean.y;
                    // Mean-normalise, then contrast and saturation about that unit mean (both keep it 1).
                    photoDetail = pa / max((half3)stats.rgb, 1e-4h);
                    photoDetail = 1.0h + (photoDetail - 1.0h) * (half)(useDeck ? _PhotoContrast.x : _PhotoContrast.y);
                    half l = dot(photoDetail, PhotoLuma);
                    photoDetail = lerp(l.xxx, photoDetail, (half)_PhotoSaturation);
                    // Anti-tiling beyond the stochastic copies: a far-scale blurred overlay of the scan
                    // (the two-scale blend) and two octaves of low-frequency value noise, all unit-mean.
                    photoDetail *= 1.0h + (farRatio - 1.0h) * (half)_FarVariation;
                    float macro = StoneNoise(chart / _MacroScale) * 0.6 +
                                  StoneNoise(chart / (_MacroScale * 3.1) + float2(7.3, 0.0)) * 0.4;
                    photoDetail *= (half)(1.0 + (macro - 0.5) * 2.0 * _MacroVariation);

                    // The scan's relief replaces the flat face normal; the tiling detail normal still
                    // rides on top below (the two combine in the same whiteout form as before).
                    // The course band's belt is DRESSED stone set back from the rock face: calmer tone and
                    // flatter relief (both lerps keep the unit mean), so it reads as a string course.
                    photoDetail = lerp(photoDetail, 1.0h, (half)(0.45 * courseHit));
                    float3 photoNormal = normalize(photoT * pn.x + photoB * pn.y + ng * pn.z);
                    photoNormal = normalize(lerp(photoNormal, ng, 0.7 * courseHit));
                    n = normalize(lerp(n, photoNormal, _PhotoBlend));

                    // Cavity grime (cold, darkens only), clustered in 2-3 m patches, plus rain streaks down
                    // every wall; cold moss gathers in UPWARD crevices: the joints and pits of a deck, the lower
                    // block's top arris in a wall's bed joints, and a wall's crevices in its first 0.9 m, where
                    // it creeps up from the deck. PhotoSurfaceFactory's normaliser evaluates these same noise
                    // functions at the same chart positions as the scan, so the mean stays exactly 1. Grime
                    // peaks at 1.7 x _GrimeStrength + _GrimeStreaks <= 1 (PhotoSurfaceTests): no overshoot.
                    float cavity = saturate((1.0 - photoMask.g) * 2.5 + (0.5 - photoMask.b) * 0.6);
                    // arch lane H-cav: on the kit's stones (vertex alpha = a stone id < 1) the baked vertex AO marks the
                    // MODELED joints and arrises; grime and moss gather there (the joint-free maps have no painted joints).
                    // Joint walls and faces only: on a flat top the AO, interpolated over a big slab, would draw wedges.
                    float kitStone = step(input.color.a, 0.998) * saturate(input.color.g * (1.0 - input.color.b));
                    float jointFace = 1.0 - smoothstep(0.9, 0.98, abs(ng.y));
                    cavity = max(cavity, kitStone * jointFace * saturate((0.85 - input.color.r) * 5.0));
                    float grimePatch = 0.3 + 1.4 * StoneNoise(chart / 2.7 + float2(3.1, 11.7));
                    float streak = useDeck ? 0.0 : PhotoStreak(chart);
                    grime = (_GrimeStrength * cavity * grimePatch + _GrimeStreaks * streak) * _PhotoBlend;
                    float patchNoise = StoneNoise(chart / 4.3 + float2(19.1, 3.3));
                    float upward = max(smoothstep(0.12, 0.6, n.y), wall * (1.0 - saturate(localHeight / 0.9)));
                    moss = _MossStrength * _PhotoBlend * upward *
                           smoothstep(0.08, 0.45, cavity) * smoothstep(0.3, 0.7, patchNoise);
                    mossTone = 0.75 + 0.5 * StoneNoise(chart * 3.7);
                }
                // Detail normal in the dominant-axis frame: two taps, seams land on cube edges.
                float3 tdir = top > 0.5 ? float3(1, 0, 0) : (sideX ? float3(0, 0, 1) : float3(1, 0, 0));
                float3 bdir = top > 0.5 ? float3(0, 0, 1) : float3(0, 1, 0);
                n = normalize(n * dn.z + tdir * dn.x + bdir * dn.y);

                SurfaceData surface = (SurfaceData)0;
                half3 roleColor = _DeckColor.rgb * deck + _BaseColor.rgb * wall + _VoidColor.rgb * under;
                float finish = slabTone * (1.0 + grain * _GrainStrength * detailFade) * (1.0 - joint * 0.34 - inset);
                half3 albedo = roleColor * (photoDetail * (finish * lerp(1.0, _EdgeBandTint, band)) + _EdgeWear * (band + chip));
                albedo *= lerp(1.0, mask.r, _CavityStrength * detailFade * (1.0 - 0.6 * _PhotoBlend));
                albedo *= lerp(1.0h, (half)(0.85 + 0.25 * frac(stoneId * 13.37)), (half)isStone);   // arch lane H-stone tone
                albedo *= lerp(1.0h, _GrimeColor.rgb, (half)grime);
                albedo = lerp(albedo, _MossColor.rgb * (half)mossTone, (half)moss);
                albedo = lerp(albedo, albedo * _EdgeBandTint * _ZoneAccent.rgb, inlay * 0.8);
                // (7) A wall darkens toward its foot over the first 3 m. Walls only: decks stay calm.
                albedo *= lerp(1.0, lerp(_BaseShade, 1.0, saturate(localHeight / 3.0)), wall);
                albedo = saturate(albedo);          // a photo highlight on a pale deck may not exceed 1
                // (8) Edge sheen: the bevel glints as you move past it (Fresnel, band only).
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                albedo += _EdgeSheen * pow(1.0 - saturate(dot(n, viewWS)), 3.0) * band;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.specular = half3(0.04, 0.04, 0.04);
                float smoothBase = saturate(lerp(_Smoothness, _DeckSmoothness, deck) + band * 0.08 - joint * 0.12);
                // The scan's roughness varies the material's value about ITS mean (so _Smoothness and
                // _DeckSmoothness keep meaning the average) and never polishes past the ceiling: with no
                // skybox a glossier deck reflects a 128 px probe, not wet stone. Moss is matte.
                float photoSmooth = ((photoMeanRough - photoMask.r) * _PhotoRoughness - moss * 0.15) * _PhotoBlend;
                surface.smoothness = min(saturate(smoothBase + photoSmooth), max(smoothBase, _SmoothnessCeiling));
                surface.normalTS = half3(0, 0, 1);
                // Baked vertex AO (LevelMesh: R = AO, G = 1, B = 0). A mesh with no colour stream reads white
                // or black depending on the platform, and G x (1 - B) is 0 for both: no AO, never a black wall.
                half vertexAO = lerp(1.0h, input.color.r, saturate(input.color.g * (1.0h - input.color.b)));
                // The scan's AO PIVOTS ABOUT ITS OWN MEAN (luminance contract 4: the photo adds detail, not
                // darkness): crevices lose ambient, open stone gains it, the average is unchanged. URP clamps
                // surface.occlusion to <= 1 (min with SSAO), so the part above 1 is applied to the ambient
                // directly below; the diffuse ambient therefore receives exactly photoAO, and reflections are
                // only ever occluded.
                float aoK = _PhotoOcclusion * _PhotoBlend;
                half photoAO = (half)((1.0 + (photoMask.g - 1.0) * aoK) / (1.0 + (photoAOMean - 1.0) * aoK));
                surface.occlusion = (1.0 - joint * 0.16) * vertexAO * min(photoAO, 1.0h);
                surface.emission = _EmissionColor.rgb + _InlayEmission.rgb * inlay;
                surface.alpha = 1;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = n;
                lighting.viewDirectionWS = viewWS;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                lighting.fogCoord = input.fogAndVertexLight.x;
                lighting.vertexLighting = input.fogAndVertexLight.yzw;
                lighting.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, n);
                // (9) Azimuthal side term: Trilight has no azimuth, so two adjacent vertical faces of a box
                // rendered the same value. Masonry carries its own side light (MaterialFactory: equator x
                // 0.45 from (-x, +z)); Character Block never sees it, so enemy torsos are unchanged. Added to
                // the AMBIENT, so vertex AO and SSAO occlude it like the rest of the sky.
                lighting.bakedGI += _SideLight.rgb * saturate(dot(n, _SideDir.xyz));
                lighting.bakedGI *= max(photoAO, 1.0h);     // the scan AO's above-mean half (see occlusion)
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                return color;
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
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
        // Wildstone: the maps are lightmapped, so bounce must see each face role's albedo.
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StoneMetaVertex
            #pragma fragment StoneMetaFragment
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct MetaAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct MetaVaryings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            MetaVaryings StoneMetaVertex(MetaAttributes input)
            {
                MetaVaryings output;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 StoneMetaFragment(MetaVaryings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 an = abs(n);
                half top = step(max(an.x, an.z), an.y + 0.01h);
                half under = step(n.y, -0.5h);
                half deck = top * (1.0h - under);
                MetaInput meta = (MetaInput)0;
                meta.Albedo = _DeckColor.rgb * deck + _BaseColor.rgb * saturate(1.0h - deck - under) + _VoidColor.rgb * under;
                meta.Emission = _EmissionColor.rgb;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
