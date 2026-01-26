Shader "Custom/QuakeSkyURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        
        _ScrollSpeed1 ("Layer 1 Scroll (XY)", Vector) = (0.05, 0.01, 0, 0)
        _ScrollSpeed2 ("Layer 2 Scroll (XY)", Vector) = (0.02, 0.03, 0, 0)
        _Repeat ("Horizontal Repeat", Float) = 4.0
    }

    SubShader
    {
        // Changed RenderType to Background so it draws behind everything
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ScrollSpeed1;
                float4 _ScrollSpeed2;
                float _Repeat;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Get world position to calculate view direction in the fragment shader
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(IN.positionWS - _WorldSpaceCameraPos);

                // Planar projection
                float2 planarUV = viewDir.xz / (max(0.01, abs(viewDir.y)));
                float2 skyUV = planarUV * _Repeat * 0.1;

                // --- LAYER 1 (Left Half: 0.0 to 0.5) ---
                float2 uv1 = skyUV + (_Time.y * _ScrollSpeed1.xy);
                // Wrap the X coordinate to 0..1 then compress to 0..0.5
                uv1.x = frac(uv1.x) * 0.5; 
                uv1 = TRANSFORM_TEX(uv1, _BaseMap);
                half4 tex1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1);

                // --- LAYER 2 (Right Half: 0.5 to 1.0) ---
                float2 uv2 = (skyUV * 1.5) + (_Time.y * _ScrollSpeed2.xy);
                // Wrap the X coordinate to 0..1, compress to 0..0.5, then shift right by 0.5
                uv2.x = (frac(uv2.x) * 0.5) + 0.5;
                uv2 = TRANSFORM_TEX(uv2, _BaseMap);
                half4 tex2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2);

                // Blending - Quake often used additive or simple lerp
                // If your texture has transparency, use tex1.a. 
                // Otherwise, lerp(tex1, tex2, 0.5) is the standard Quake look.
                half4 skyColor = lerp(tex1, tex2, 0.5);

                // Horizon fade
                float horizonMask = saturate(abs(viewDir.y) * 5.0);
                return skyColor * _BaseColor * horizonMask;
            }
            ENDHLSL
        }
    }
}