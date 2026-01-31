Shader "BreakingHue/Dissolve"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.25, 0.25, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (2, 0.5, 0, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0.1, 0.1, 0.1, 1)
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Float) = 5.0
        _EdgeWidth ("Edge Width", Range(0.0, 0.2)) = 0.05
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Dissolve"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float fogFactor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _EmissionColor;
                float _DissolveAmount;
                float _NoiseScale;
                float _EdgeWidth;
                float _EdgeIntensity;
            CBUFFER_END

            // Hash function
            float hash(float3 p)
            {
                p = frac(p * float3(123.34, 456.21, 789.92));
                p += dot(p, p.yxz + 45.32);
                return frac(p.x * p.y * p.z);
            }

            // 3D noise
            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                
                float a = hash(i);
                float b = hash(i + float3(1.0, 0.0, 0.0));
                float c = hash(i + float3(0.0, 1.0, 0.0));
                float d = hash(i + float3(1.0, 1.0, 0.0));
                float e = hash(i + float3(0.0, 0.0, 1.0));
                float f1 = hash(i + float3(1.0, 0.0, 1.0));
                float g = hash(i + float3(0.0, 1.0, 1.0));
                float h = hash(i + float3(1.0, 1.0, 1.0));
                
                float x1 = lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
                float x2 = lerp(lerp(e, f1, u.x), lerp(g, h, u.x), u.y);
                
                return lerp(x1, x2, u.z);
            }

            // FBM for more organic dissolve
            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise3D(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value;
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
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Generate dissolve noise
                float dissolveNoise = fbm(input.worldPos * _NoiseScale);
                
                // Calculate dissolve threshold
                // Remap dissolve amount to make the transition more dramatic
                float dissolveThreshold = _DissolveAmount * 1.2 - 0.1;
                
                // Clip pixels below threshold
                float dissolveValue = dissolveNoise - dissolveThreshold;
                clip(dissolveValue);
                
                // Calculate edge glow
                float edge = 1.0 - smoothstep(0.0, _EdgeWidth, dissolveValue);
                
                // Get main light
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 normal = normalize(input.worldNormal);
                float NdotL = saturate(dot(normal, lightDir));
                
                // Base color with lighting
                float3 baseColor = _BaseColor.rgb;
                float3 diffuse = baseColor * (NdotL * 0.5 + 0.5) * mainLight.color;
                
                // Add ambient
                diffuse += baseColor * 0.2;
                
                // Add emission
                diffuse += _EmissionColor.rgb;
                
                // Add edge glow
                float3 edgeGlow = _EdgeColor.rgb * edge * _EdgeIntensity;
                
                // Animate edge brightness
                float edgePulse = sin(_Time.y * 10.0) * 0.3 + 0.7;
                edgeGlow *= edgePulse;
                
                float3 finalColor = diffuse + edgeGlow;
                
                // Apply fog
                half4 color = half4(finalColor, 1.0);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
