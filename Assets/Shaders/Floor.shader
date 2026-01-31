Shader "BreakingHue/Floor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.15, 0.15, 0.18, 1)
        _GridColor ("Grid Color", Color) = (0.3, 0.35, 0.4, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.2, 0.6, 0.8, 1)
        _GridScale ("Grid Scale", Float) = 2.0
        _GridLineWidth ("Grid Line Width", Range(0.01, 0.2)) = 0.05
        _GlowSpeed ("Glow Animation Speed", Float) = 0.5
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5
        _CircuitDensity ("Circuit Density", Float) = 8.0
        _CircuitGlow ("Circuit Glow", Range(0, 1)) = 0.3
        _Metallic ("Metallic", Range(0, 1)) = 0.7
        _Smoothness ("Smoothness", Range(0, 1)) = 0.6
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
            Name "Floor"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

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
                float4 _GridColor;
                float4 _GlowColor;
                float _GridScale;
                float _GridLineWidth;
                float _GlowSpeed;
                float _GlowIntensity;
                float _CircuitDensity;
                float _CircuitGlow;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

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

            // Grid pattern
            float gridPattern(float2 uv, float lineWidth)
            {
                float2 grid = abs(frac(uv - 0.5) - 0.5);
                float2 lines = smoothstep(lineWidth, lineWidth * 0.5, grid);
                return max(lines.x, lines.y);
            }

            // Circuit trace pattern
            float circuitPattern(float2 uv, float time)
            {
                float2 cellUV = frac(uv * _CircuitDensity);
                float2 cellId = floor(uv * _CircuitDensity);
                
                // Random direction for each cell
                float rnd = hash(cellId);
                
                // Create traces
                float trace = 0.0;
                
                // Horizontal trace
                if (rnd > 0.5)
                {
                    float hLine = smoothstep(0.48, 0.45, abs(cellUV.y - 0.5));
                    trace = max(trace, hLine * step(0.2, cellUV.x) * step(cellUV.x, 0.8));
                }
                
                // Vertical trace
                if (rnd < 0.6)
                {
                    float vLine = smoothstep(0.48, 0.45, abs(cellUV.x - 0.5));
                    trace = max(trace, vLine * step(0.2, cellUV.y) * step(cellUV.y, 0.8));
                }
                
                // Node points
                float node = 1.0 - smoothstep(0.0, 0.15, length(cellUV - 0.5));
                trace = max(trace, node * step(0.7, rnd));
                
                // Animated glow pulse traveling along traces
                float pulsePos = frac(time * _GlowSpeed + rnd);
                float pulse = 1.0 - smoothstep(0.0, 0.3, abs(cellUV.x - pulsePos));
                pulse *= trace;
                
                return trace + pulse * 0.5;
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
                float time = _Time.y;
                
                // Use world position for seamless tiling
                float2 worldUV = input.worldPos.xz * _GridScale;
                
                // Main grid
                float grid = gridPattern(worldUV, _GridLineWidth);
                
                // Secondary finer grid
                float fineGrid = gridPattern(worldUV * 4.0, _GridLineWidth * 0.5) * 0.3;
                
                // Circuit pattern
                float circuit = circuitPattern(worldUV * 0.5, time) * _CircuitGlow;
                
                // Animated glow waves
                float wave1 = sin(input.worldPos.x * 2.0 + time * _GlowSpeed) * 0.5 + 0.5;
                float wave2 = sin(input.worldPos.z * 2.0 - time * _GlowSpeed * 0.7) * 0.5 + 0.5;
                float waveGlow = (wave1 * wave2) * _GlowIntensity * 0.3;
                
                // Combine patterns
                float pattern = saturate(grid + fineGrid + circuit);
                
                // Base color
                float3 baseColor = _BaseColor.rgb;
                float3 gridColor = _GridColor.rgb;
                float3 glowColor = _GlowColor.rgb;
                
                // Mix colors
                float3 surfaceColor = lerp(baseColor, gridColor, pattern * 0.7);
                
                // Add glow
                float glowAmount = (pattern * 0.5 + circuit + waveGlow) * _GlowIntensity;
                surfaceColor += glowColor * glowAmount;
                
                // Simple lighting
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float NdotL = saturate(dot(input.worldNormal, lightDir));
                float3 diffuse = surfaceColor * (NdotL * 0.5 + 0.5) * mainLight.color;
                
                // Add some ambient
                float3 ambient = surfaceColor * 0.2;
                
                float3 finalColor = diffuse + ambient;
                
                // Emission from glow
                finalColor += glowColor * glowAmount * 0.5;
                
                // Apply fog
                half4 color = half4(finalColor, 1.0);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
