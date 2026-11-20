// Flowing oil for Pitchclaw's cracks (docs/desert-bosses.md, 11-10), in the spirit of the Core Worm's energy veins:
// a dark, glossy fuel with faceted low-poly lighting, and bright pulses that run along the cracks and swell them as
// they pass. The meshes carry the flow in UV.x (metres along each limb, counting outward from the fuel tank), so
// the pulses stream out from the tank down the body, the tail, the claws and the legs.
//
// The boss script drives _Glow (brighter in later phases, blazing when the oil burns) and the colours.
Shader "Signal Lost/Flowing Oil"
{
    Properties
    {
        _BaseColor ("Oil Colour", Color) = (0.03, 0.025, 0.03, 1)
        _SheenColor ("Sheen Colour", Color) = (0.35, 0.3, 0.5, 1)
        _GlowColor ("Pulse Colour", Color) = (1, 0.45, 0.1, 1)
        _Glow ("Glow", Range(0, 6)) = 1
        _FlowSpeed ("Flow Speed (m/s)", Float) = 2.2
        _PulseSpacing ("Pulse Spacing (m)", Float) = 2.6
        _PulseSharpness ("Pulse Sharpness", Range(1, 16)) = 5
        _Swell ("Swell (m)", Range(0, 0.2)) = 0.05
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Facets ("Faceted Shading", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Oil"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SheenColor;
                half4 _GlowColor;
                half _Glow;
                float _FlowSpeed;
                float _PulseSpacing;
                half _PulseSharpness;
                float _Swell;
                half _Smoothness;
                half _Facets;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float pulse : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            // Pulses: travelling bands along UV.x, with a little wobble from position so neighbouring cracks don't march
            // in lockstep.
            float Pulse(float u, float3 p)
            {
                float wobble = sin(p.x * 1.7 + p.z * 1.3) * 0.35 + sin(p.y * 2.3) * 0.2;
                float phase = (u - _Time.y * _FlowSpeed) / max(0.1, _PulseSpacing) * 6.2831853 + wobble;
                return pow(saturate(sin(phase) * 0.5 + 0.5), _PulseSharpness);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float pulse = Pulse(input.uv.x, positionWS);
                positionWS += normalWS * (_Swell * pulse);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.pulse = pulse;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 faceted = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 n = normalize(lerp(normalize(input.normalWS), faceted, _Facets));
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                if (dot(n, v) < 0) n = -n;
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 l = sun.direction;
                float atten = sun.shadowAttenuation * sun.distanceAttenuation;
                half lambert = saturate(dot(n, l)) * 0.6 + 0.4;
                float3 h = normalize(l + v);
                half spec = pow(saturate(dot(n, h)), lerp(8, 160, _Smoothness)) * _Smoothness * 1.6;
                half rim = pow(1 - saturate(dot(n, v)), 3);

                half3 ambient = SampleSH(n) * _BaseColor.rgb;
                half3 colour = _BaseColor.rgb * sun.color * lambert * atten + ambient;
                colour += sun.color * spec * atten;                                 // the wet highlight
                colour += _SheenColor.rgb * rim * 0.6;                              // an oily, slightly iridescent edge
                half glow = _Glow * (0.12 + input.pulse * 1.4);
                colour += _GlowColor.rgb * glow;
                colour = MixFog(colour, input.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        // Shadows and depth come from URP's own passes (the swell is too small to matter for them).
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
