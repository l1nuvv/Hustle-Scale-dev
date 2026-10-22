Shader "HustleScale/PickupOutline"
{
    Properties
    {
        _OutlineColor ("Outline", Color) = (1, 0.72, 0.2, 1)
        _OutlinePixels ("Width in pixels", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Transparent+10" }
        Pass
        {
            Name "Pickup outline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 1 ReadMask 1 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(world);
                float4 expanded = TransformWorldToHClip(world + normal * 0.01);
                float2 direction = expanded.xy / max(expanded.w, 0.0001) - clip.xy / max(clip.w, 0.0001);
                direction /= max(length(direction), 0.00001);
                clip.xy += direction * (2 * _OutlinePixels / _ScreenParams.xy) * clip.w;
                output.positionCS = clip;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
