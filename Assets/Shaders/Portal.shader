Shader "BreakingHue/Portal"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0, 0.8, 1, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 1.6, 2, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 1.0
        _VortexStrength ("Vortex Strength", Float) = 0.3
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15
        _NoiseScale ("Noise Scale", Float) = 3.0
        _InnerRadius ("Inner Radius", Range(0, 0.5)) = 0.1
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
            Name "Portal"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off // Double-sided rendering

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _ScrollSpeed;
                float _VortexStrength;
                float _PulseSpeed;
                float _PulseIntensity;
                float _EdgeSoftness;
                float _NoiseScale;
                float _InnerRadius;
            CBUFFER_END

            // Simple hash function for noise
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                
                // Smooth interpolation
                float2 u = f * f * (3.0 - 2.0 * f);
                
                // Four corners
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Fractal Brownian Motion for more interesting noise
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Get object scale from the model matrix
                float3 scale;
                scale.x = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                scale.y = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                scale.z = length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z));

                // Get the world position of the object's center
                float3 worldCenter = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);

                // Get camera vectors in world space
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 camUp = normalize(UNITY_MATRIX_V[1].xyz);

                // Scale the vertex position and apply billboard transformation
                float3 scaledPos = input.positionOS.xyz * scale;
                float3 worldPos = worldCenter + camRight * scaledPos.x + camUp * scaledPos.y;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Center UVs around (0.5, 0.5) for radial effects
                float2 centeredUV = input.uv - 0.5;
                float dist = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x);

                // Time-based animation
                float time = _Time.y;

                // Vortex swirl effect - rotate based on distance and time
                float swirl = _VortexStrength * (1.0 - dist * 2.0);
                angle += swirl * time * _ScrollSpeed;

                // Convert back to UV for noise sampling
                float2 polarUV = float2(
                    cos(angle) * dist + 0.5,
                    sin(angle) * dist + 0.5
                );

                // Animated noise for the portal effect
                float2 noiseUV = polarUV * _NoiseScale;
                noiseUV += float2(time * _ScrollSpeed * 0.5, time * _ScrollSpeed * 0.3);
                
                float noiseValue = fbm(noiseUV);
                
                // Second layer of noise for more detail
                float2 noiseUV2 = polarUV * _NoiseScale * 2.0;
                noiseUV2 -= float2(time * _ScrollSpeed * 0.7, time * _ScrollSpeed * 0.2);
                float noiseValue2 = fbm(noiseUV2);
                
                // Combine noise layers
                float combinedNoise = (noiseValue + noiseValue2 * 0.5) / 1.5;

                // Radial gradient for circular portal shape
                float innerMask = smoothstep(_InnerRadius, _InnerRadius + 0.1, dist);
                float outerMask = 1.0 - smoothstep(0.5 - _EdgeSoftness, 0.5, dist);
                float portalMask = innerMask * outerMask;

                // Pulsing effect
                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseIntensity;

                // Ring highlights that move inward
                float rings = sin((dist * 10.0 - time * _ScrollSpeed * 2.0) * 3.14159) * 0.5 + 0.5;
                rings = pow(rings, 3.0) * 0.5;

                // Combine all effects
                float intensity = (combinedNoise * 0.7 + rings * 0.3) * pulse;

                // Color
                float3 baseColor = _BaseColor.rgb;
                float3 emissionColor = _EmissionColor.rgb;
                
                // Blend between base and emission based on intensity
                float3 finalColor = lerp(baseColor, emissionColor, intensity);
                
                // Add extra brightness at the center edge (inner glow)
                float innerGlow = 1.0 - smoothstep(_InnerRadius, _InnerRadius + 0.2, dist);
                finalColor += emissionColor * innerGlow * 0.5;

                // Final alpha with portal mask
                float alpha = portalMask * _BaseColor.a;

                // Apply fog
                half4 color = half4(finalColor, alpha);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
