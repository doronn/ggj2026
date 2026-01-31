Shader "BreakingHue/Barrier"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1, 0, 0, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (2, 0, 0, 1)
        _PhaseAmount ("Phase Amount", Range(0, 1)) = 0
        _LightningScale ("Lightning Scale", Float) = 5.0
        _LightningSpeed ("Lightning Speed", Float) = 3.0
        _LightningIntensity ("Lightning Intensity", Range(0, 2)) = 1.0
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 0.5)) = 0.2
        _EdgeGlow ("Edge Glow", Range(0, 2)) = 0.5
        _TransparentAlpha ("Transparent Alpha", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Barrier"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _PhaseAmount;
                float _LightningScale;
                float _LightningSpeed;
                float _LightningIntensity;
                float _PulseSpeed;
                float _PulseIntensity;
                float _EdgeGlow;
                float _TransparentAlpha;
            CBUFFER_END

            // Hash functions for noise
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float hash3(float3 p)
            {
                p = frac(p * float3(123.34, 456.21, 789.92));
                p += dot(p, p.yxz + 45.32);
                return frac(p.x * p.y * p.z);
            }

            // 2D noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3D noise
            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                
                float a = hash3(i);
                float b = hash3(i + float3(1.0, 0.0, 0.0));
                float c = hash3(i + float3(0.0, 1.0, 0.0));
                float d = hash3(i + float3(1.0, 1.0, 0.0));
                float e = hash3(i + float3(0.0, 0.0, 1.0));
                float f1 = hash3(i + float3(1.0, 0.0, 1.0));
                float g = hash3(i + float3(0.0, 1.0, 1.0));
                float h = hash3(i + float3(1.0, 1.0, 1.0));
                
                float x1 = lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
                float x2 = lerp(lerp(e, f1, u.x), lerp(g, h, u.x), u.y);
                
                return lerp(x1, x2, u.z);
            }

            // FBM for lightning effect
            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise3D(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value;
            }

            // Lightning bolt pattern
            float lightning(float3 p, float time)
            {
                // Create multiple lightning layers
                float bolt1 = fbm(p * _LightningScale + float3(time * _LightningSpeed, 0, 0));
                float bolt2 = fbm(p * _LightningScale * 1.5 + float3(0, time * _LightningSpeed * 0.7, 0));
                float bolt3 = fbm(p * _LightningScale * 0.8 + float3(time * _LightningSpeed * 1.3, time * _LightningSpeed * 0.5, 0));
                
                // Create sharp lightning pattern
                bolt1 = pow(bolt1, 3.0) * 4.0;
                bolt2 = pow(bolt2, 3.0) * 4.0;
                bolt3 = pow(bolt3, 3.0) * 4.0;
                
                // Random flicker
                float flicker = noise(float2(time * 10.0, 0.0));
                flicker = step(0.7, flicker);
                
                return (bolt1 + bolt2 * 0.5 + bolt3 * 0.3) * (0.5 + flicker * 0.5);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.worldNormal = normalInput.normalWS;
                output.uv = input.uv;
                output.viewDir = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                
                // Lightning effect
                float lightningValue = lightning(input.worldPos, time) * _LightningIntensity;
                
                // Pulsing effect
                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseIntensity;
                
                // Fresnel edge glow
                float fresnel = 1.0 - saturate(dot(normalize(input.worldNormal), normalize(input.viewDir)));
                fresnel = pow(fresnel, 2.0) * _EdgeGlow;
                
                // Combine effects
                float intensity = (lightningValue + fresnel) * pulse;
                
                // Base color with lightning highlights
                float3 baseColor = _BaseColor.rgb;
                float3 emissionColor = _EmissionColor.rgb;
                
                float3 finalColor = lerp(baseColor, emissionColor, saturate(intensity));
                finalColor += emissionColor * lightningValue * 0.5;
                
                // Calculate alpha based on phase amount
                // PhaseAmount 0 = solid (alpha 1), PhaseAmount 1 = transparent (alpha = _TransparentAlpha)
                float baseAlpha = lerp(1.0, _TransparentAlpha, _PhaseAmount);
                
                // Add some alpha variation from lightning for visual interest
                float alphaVariation = lightningValue * 0.1 * (1.0 - _PhaseAmount);
                float finalAlpha = saturate(baseAlpha + alphaVariation);
                
                // Apply fog
                half4 color = half4(finalColor, finalAlpha);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
