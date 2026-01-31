Shader "BreakingHue/Character"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.7, 0.7, 0.7, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.5, 0.8, 1, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0.2, 0.4, 0.5, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 1.0
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 0.5)) = 0.15
        _ScanlineSpeed ("Scanline Speed", Float) = 1.0
        _ScanlineScale ("Scanline Scale", Float) = 20.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.7
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
            Name "Character"
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
                float3 viewDir : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float4 _EmissionColor;
                float _RimPower;
                float _RimIntensity;
                float _PulseSpeed;
                float _PulseIntensity;
                float _ScanlineSpeed;
                float _ScanlineScale;
                float _ScanlineIntensity;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

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
                
                // Normalize vectors
                float3 normal = normalize(input.worldNormal);
                float3 viewDir = normalize(input.viewDir);
                
                // Fresnel rim lighting
                float NdotV = saturate(dot(normal, viewDir));
                float rim = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                
                // Pulsing effect
                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseIntensity;
                
                // Holographic scanlines
                float scanline = sin(input.worldPos.y * _ScanlineScale + time * _ScanlineSpeed) * 0.5 + 0.5;
                scanline = pow(scanline, 4.0) * _ScanlineIntensity;
                
                // Moving highlight band
                float highlightBand = sin(input.worldPos.y * 3.0 - time * 2.0) * 0.5 + 0.5;
                highlightBand = pow(highlightBand, 8.0) * 0.3;
                
                // Get main light
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float NdotL = saturate(dot(normal, lightDir));
                
                // Half vector for specular
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfDir));
                float specular = pow(NdotH, 32.0 * _Smoothness) * _Metallic;
                
                // Base color
                float3 baseColor = _BaseColor.rgb;
                float3 rimColor = _RimColor.rgb;
                float3 emissionColor = _EmissionColor.rgb;
                
                // Diffuse lighting with slight toon shading
                float diffuseStep = smoothstep(0.0, 0.1, NdotL);
                float3 diffuse = baseColor * (diffuseStep * 0.6 + 0.4) * mainLight.color;
                
                // Add specular
                diffuse += specular * mainLight.color * 0.5;
                
                // Apply pulse to base
                diffuse *= pulse;
                
                // Add rim glow
                float3 rimGlow = rimColor * rim * pulse;
                
                // Add emission with scanlines
                float3 emission = emissionColor * (0.3 + scanline + highlightBand) * pulse;
                
                // Combine
                float3 finalColor = diffuse + rimGlow + emission;
                
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
