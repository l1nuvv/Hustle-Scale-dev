Shader "HustleScale/CityMatteSurface"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Metres ("Texture repeat in metres", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Metres;
            CBUFFER_END
            struct Attributes { float4 position : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 position : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position.xyz);
                output.position = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.fog = ComputeFogFactor(output.position.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normal);
                half3 weights = pow(abs(normal), 8);
                weights /= max(dot(weights, half3(1,1,1)), .001);
                float3 uv = input.world / max(_Metres, .01);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.zy).rgb * weights.x
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.xz).rgb * weights.y
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.xy).rgb * weights.z;
                InputData lighting = (InputData)0;
                lighting.positionWS = input.world;
                lighting.positionCS = input.position;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.world);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.world);
                lighting.bakedGI = SampleSH(normal);
                lighting.vertexLighting = VertexLighting(input.world, normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.position);
                lighting.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo * _BaseColor.rgb;
                surface.alpha = 1;
                surface.occlusion = 1;
                half4 color = UniversalFragmentBlinnPhong(lighting, surface);
                return half4(MixFog(color.rgb, input.fog), 1);
            }
            ENDHLSL
        }
        // Opaque architectural meshes cast the same shadows as the existing URP geometry.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
