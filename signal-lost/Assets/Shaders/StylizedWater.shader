// Stylized water for URP: the tutorial pond and the sea around every island (11-07 rework, matched to the reference's
// clear turquoise shallows). Low-poly like the rest of the game: crossing swells move the mesh and the lighting is
// partly faceted, with crisp toon foam and glints rather than soft gradients.
//
// Depth comes from the camera depth texture (on in the PC pipeline asset): the water is see-through in the shallows,
// turns deep blue as it gets deeper, shows moving caustics on the sea floor, and draws foam where it meets anything:
// the beach, rocks, dock posts. Shoreline wash lines roll in toward the sand. Without a depth texture (or with
// Use Depth off) it falls back to the mesh's vertex colour red channel, how close each vertex is to the shore (0 open
// water, 1 the bank), which the builders bake; that also calms the swell along the banks so the edge never lifts off.
Shader "Signal Lost/Stylized Water"
{
    Properties
    {
        _DeepColor ("Deep Colour", Color) = (0.03, 0.3, 0.52, 0.96)
        _ShallowColor ("Shallow Colour", Color) = (0.32, 0.86, 0.82, 0.3)
        _HorizonColor ("Sky Reflection Colour", Color) = (0.78, 0.88, 0.94, 1)
        _FoamColor ("Foam Colour", Color) = (0.97, 0.99, 0.97, 1)
        _FoamWidth ("Bank Foam Width (vertex shore)", Range(0, 0.5)) = 0.12
        _WaveHeight ("Wave Height", Range(0, 0.6)) = 0.05
        _WaveScale ("Wave Scale", Float) = 0.25
        _WaveSpeed ("Wave Speed", Float) = 0.8
        _Sparkle ("Sparkle", Range(0, 2)) = 0.8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.85
        [Toggle] _UseDepth ("Use Depth Texture", Float) = 1
        _DepthFade ("Depth To Deep Colour (m)", Float) = 3.5
        _Clarity ("See-Through Depth (m)", Float) = 2.2
        _EdgeFoam ("Edge Foam Width (m)", Float) = 0.45
        _WashLines ("Shore Wash Lines", Range(0, 1)) = 0.7
        _CausticStrength ("Caustics", Range(0, 2)) = 0.75
        _CausticScale ("Caustic Scale", Float) = 0.45
        _Facets ("Faceted Shading", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _HorizonColor;
                half4 _FoamColor;
                float _FoamWidth;
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
                float _Sparkle;
                float _Smoothness;
                float _UseDepth;
                float _DepthFade;
                float _Clarity;
                float _EdgeFoam;
                float _WashLines;
                float _CausticStrength;
                float _CausticScale;
                float _Facets;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half shore : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            // Three crossing swells: x is the height, yz its slope along world x and z (for the normal).
            float3 Waves(float2 p, float t)
            {
                float2 d1 = normalize(float2(1.0, 0.35));
                float2 d2 = normalize(float2(-0.4, 1.0));
                float2 d3 = normalize(float2(0.7, -0.8));
                float k1 = _WaveScale * 6.2831, k2 = _WaveScale * 4.1, k3 = _WaveScale * 9.7;
                float a1 = dot(p, d1) * k1 + t * _WaveSpeed * 1.3;
                float a2 = dot(p, d2) * k2 + t * _WaveSpeed;
                float a3 = dot(p, d3) * k3 + t * _WaveSpeed * 1.9;
                float height = sin(a1) + 0.6 * sin(a2) + 0.25 * sin(a3);
                float2 slope = d1 * cos(a1) * k1 + d2 * 0.6 * cos(a2) * k2 + d3 * 0.25 * cos(a3) * k3;
                return float3(height, slope);
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 Hash2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float bottom = lerp(Hash(i), Hash(i + float2(1.0, 0.0)), u.x);
                float top = lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), u.x);
                return lerp(bottom, top, u.y);
            }

            // Distance to the nearest edge between drifting cells: thin bright lines where it is small (caustics).
            float CellEdge(float2 p, float t)
            {
                float2 i = floor(p), f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = 0.5 + 0.45 * sin(t + 6.2831 * Hash2(i + g));
                    float d = length(g + o - f);
                    if (d < d1) { d2 = d1; d1 = d; }
                    else if (d < d2) { d2 = d; }
                }
                return d2 - d1;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half shore = input.color.r;
                // Calm at the banks, so the water's edge never lifts off the ground.
                float3 wave = Waves(positionWS.xz, _Time.y) * (_WaveHeight * (1.0 - shore));
                positionWS.y += wave.x;
                output.positionWS = positionWS;
                output.normalWS = normalize(float3(-wave.y, 1.0, -wave.z));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.shore = shore;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float3 positionWS = input.positionWS;
                float2 p = positionWS.xz;

                // ---- How deep the water is under this pixel ----
                float surfaceEye = input.screenPos.w;
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool nothingBehind = raw <= 0.00001;
                #else
                    bool nothingBehind = raw >= 0.99999;
                #endif
                float sceneEye = LinearEyeDepth(raw, _ZBufferParams);
                // The sea floor point behind this pixel, along the view ray; its height below the surface is the depth.
                float3 fromCamera = positionWS - _WorldSpaceCameraPos;
                float3 floorWS = _WorldSpaceCameraPos + fromCamera * (sceneEye / max(surfaceEye, 0.0001));
                float depth = max(0.0, positionWS.y - floorWS.y);
                float thickness = max(0.0, sceneEye - surfaceEye);
                bool useDepth = _UseDepth > 0.5;
                if (!useDepth || nothingBehind)
                {
                    // Fallback (or open sea with nothing under it): depth from the baked shore value.
                    depth = useDepth ? 100.0 : (1.0 - input.shore) * _DepthFade * 2.5;
                    thickness = depth * 2.0;
                    floorWS = positionWS - float3(0.0, depth, 0.0);
                }

                // ---- Surface normal: swell, low-poly facets and ripples ----
                float3 flat = normalize(cross(ddy(positionWS), ddx(positionWS)));
                flat *= flat.y < 0.0 ? -1.0 : 1.0;
                float3 normalWS = normalize(lerp(normalize(input.normalWS), flat, _Facets));
                float n1 = ValueNoise(p * 1.7 + float2(t * 0.25, t * 0.11));
                float n2 = ValueNoise(p * 2.9 - float2(t * 0.17, -t * 0.21));
                // Fine detail fades with distance, so the far sea reads as calm swell rather than speckle.
                half near = saturate(1.0 - surfaceEye / 90.0);
                normalWS = normalize(normalWS + float3(n1 - 0.5, 0.0, n2 - 0.5) * 0.22 * (0.25 + 0.75 * near));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // ---- Colour: clear turquoise over sand, deep blue further out ----
                half deep = saturate((1.0 - exp(-depth / max(_DepthFade, 0.01))) * 1.08); // fully deep by ~8 m, matching open sea
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, deep);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, 1.0 - exp(-thickness / max(_Clarity, 0.01)));

                // Grazing angles reflect the sky.
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 4.0);
                water = lerp(water, _HorizonColor.rgb, fresnel * 0.6);
                alpha = saturate(alpha + fresnel * 0.35);

                // ---- Light: soft diffuse, a crisp sun glint and sparkles; dock and tree shadows dim it ----
                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half shadow = lerp(0.55, 1.0, sun.shadowAttenuation);
                half3 colour = water * (0.6 + 0.4 * saturate(dot(normalWS, sun.direction))) * sun.color * shadow;
                colour += SampleSH(normalWS) * water * 0.35;
                float3 halfway = normalize(sun.direction + viewWS);
                half highlight = pow(saturate(dot(normalWS, halfway)), exp2(10.0 * _Smoothness + 1.0));
                highlight = smoothstep(0.35, 0.5, highlight) * _Smoothness; // toon glint, not a soft blob
                half sparkle = step(0.93, ValueNoise(p * 9.0 + t * 0.6) * n1 * 1.8) * _Sparkle * near;
                half glint = (highlight + sparkle * highlight * 3.0 + sparkle * 0.25) * sun.shadowAttenuation;
                colour += sun.color * glint;
                alpha = saturate(alpha + glint * 0.5);

                // ---- Caustics: drifting light lines on the floor, strongest in the clear shallows ----
                float2 cp = floorWS.xz * _CausticScale;
                float c1 = CellEdge(cp + float2(t * 0.05, t * 0.03), t * 0.8);
                float c2 = CellEdge(cp * 1.37 + float2(-t * 0.04, t * 0.06) + 4.1, t * 0.7 + 2.0);
                half caustic = smoothstep(0.1, 0.0, min(c1, c2)) * _CausticStrength;
                caustic *= (1.0 - deep) * saturate(depth * 2.5) * shadow;
                colour = lerp(colour, colour + sun.color * 0.9, caustic);
                alpha = saturate(alpha + caustic * 0.35);

                // ---- Foam: where the water meets the beach, rocks and posts, plus wash lines rolling in ----
                half wobble = (n2 - 0.5) * 0.5 + sin(t * 1.4 + p.x * 0.8 + p.y * 0.5) * 0.15;
                half edge = 1.0 - saturate((depth + wobble * 0.25) / max(_EdgeFoam, 0.01));
                half foam = step(0.5, edge);
                half wash = step(0.88, frac(depth * 0.9 - t * 0.22 + n1 * 0.25)) * step(0.45, n2) * saturate(1.0 - depth / 2.2);
                foam = max(foam, wash * _WashLines);
                // The baked bank foam (the pond's fallback, and calm edges where the depth texture is off).
                half bank = smoothstep(1.0 - _FoamWidth, 1.0, input.shore + (n2 - 0.5) * 0.12 + sin(t * 1.4 + p.x * 0.8) * 0.02);
                foam = useDepth ? foam : max(foam, step(0.5, bank));
                colour = lerp(colour, _FoamColor.rgb * (0.75 + 0.25 * shadow) * sun.color, foam * 0.92);
                alpha = saturate(max(alpha, foam * 0.95));

                colour = MixFog(colour, input.fog);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
