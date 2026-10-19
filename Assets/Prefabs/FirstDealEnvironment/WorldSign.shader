Shader "HustleScale/WorldSign"
{
    Properties
    {
        _MainTex ("Font atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            Output Vert(Input input)
            {
                Output output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Output input) : SV_Target
            {
                return half4(input.color.rgb, input.color.a * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a);
            }
            ENDHLSL
        }
    }
}
