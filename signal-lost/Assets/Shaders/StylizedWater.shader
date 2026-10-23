// Stylized pond water for URP (the tutorial meadow's pond, built by TutorialLevelBuilder).
// The mesh's vertex colour red channel is how close each vertex is to the shore (0 open water, 1 the bank), baked by
// the builder, so it needs no depth texture: shallow colour, still water and foam along the banks all come from it.
// Gentle crossing swells move the surface; scrolling noise ripples catch the low sun as a highlight and sparkles,
// and grazing angles reflect the sky.
Shader "Signal Lost/Stylized Water"
{
    Properties
    {
        _DeepColor ("Deep Colour", Color) = (0.05, 0.37, 0.56, 0.93)
        _ShallowColor ("Shallow Colour", Color) = (0.22, 0.7, 0.74, 0.72)
        _HorizonColor ("Sky Reflection Colour", Color) = (0.78, 0.86, 0.92, 1)
        _FoamColor ("Foam Colour", Color) = (0.95, 0.97, 0.95, 1)
        _FoamWidth ("Foam Width", Range(0, 0.5)) = 0.12
        _WaveHeight ("Wave Height", Range(0, 0.3)) = 0.05
        _WaveScale ("Wave Scale", Float) = 0.25
        _WaveSpeed ("Wave Speed", Float) = 0.8
        _Sparkle ("Sparkle", Range(0, 2)) = 0.8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.85
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half shore : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            // Two crossing sine swells: x is the height, yz its slope along world x and z (for the normal).
            float3 Waves(float2 p, float t)
            {
                float2 d1 = normalize(float2(1.0, 0.35));
                float2 d2 = normalize(float2(-0.4, 1.0));
                float k1 = _WaveScale * 6.2831;
                float k2 = _WaveScale * 4.1;
                float a1 = dot(p, d1) * k1 + t * _WaveSpeed * 1.3;
                float a2 = dot(p, d2) * k2 + t * _WaveSpeed;
                float height = sin(a1) + 0.6 * sin(a2);
                float2 slope = d1 * cos(a1) * k1 + d2 * 0.6 * cos(a2) * k2;
                return float3(height, slope);
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float bottom = lerp(Hash(i), Hash(i + float2(1.0, 0.0)), u.x);
                float top = lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), u.x);
                return lerp(bottom, top, u.y);
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
                output.shore = shore;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 p = input.positionWS.xz;

                // Ripples: two scrolling noise layers tilt the normal a little.
                float n1 = ValueNoise(p * 1.7 + float2(t * 0.25, t * 0.11));
                float n2 = ValueNoise(p * 2.9 - float2(t * 0.17, -t * 0.21));
                float3 normalWS = normalize(normalize(input.normalWS) + float3(n1 - 0.5, 0.0, n2 - 0.5) * 0.25);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Deep in the middle, clearer and lighter toward the banks.
                half depth = saturate(1.0 - input.shore);
                half4 water = lerp(_ShallowColor, _DeepColor, smoothstep(0.0, 0.8, depth));

                // Grazing angles reflect the sky.
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), 4.0);
                water.rgb = lerp(water.rgb, _HorizonColor.rgb, fresnel * 0.65);
                water.a = saturate(water.a + fresnel * 0.3);

                // The sun: soft diffuse, a tight highlight, and sparkles where the ripples catch it.
                Light sun = GetMainLight();
                half3 colour = water.rgb * (0.55 + 0.45 * saturate(dot(normalWS, sun.direction))) * sun.color;
                float3 halfway = normalize(sun.direction + viewWS);
                half highlight = pow(saturate(dot(normalWS, halfway)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness;
                half sparkle = step(0.93, ValueNoise(p * 9.0 + t * 0.6) * n1 * 1.8) * _Sparkle;
                colour += sun.color * highlight * (1.0 + sparkle * 4.0);
                colour += SampleSH(normalWS) * water.rgb * 0.35;

                // Foam: a broken, gently moving band along the shore.
                half band = smoothstep(1.0 - _FoamWidth, 1.0, input.shore + (n2 - 0.5) * 0.12 + sin(t * 1.4 + p.x * 0.8) * 0.02);
                colour = lerp(colour, _FoamColor.rgb, band * 0.85);
                half alpha = saturate(max(water.a, band * 0.9));

                colour = MixFog(colour, input.fog);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
