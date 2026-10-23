Shader "HustleScale/M6PowderCoating"
{
    Properties
    {
        _PowderMask ("Authored spray mask", 2D) = "white" {}
        _Tint ("Luminous powder", Color) = (0.04,0.78,1,1)
        _Pattern ("Random offsets and scale", Vector) = (0,0.4,1,1)
        _Coverage0 ("Sections 0-3", Vector) = (0,0,0,0)
        _Coverage1 ("Sections 4-7", Vector) = (0,0,0,0)
        _Coverage2 ("Sections 8-11", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_PowderMask); SAMPLER(sampler_PowderMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint, _Pattern, _Coverage0, _Coverage1, _Coverage2;
            CBUFFER_END
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv;
                return output;
            }
            float Coverage(int index)
            {
                if (index <= 0) return _Coverage0.x;
                if (index == 1) return _Coverage0.y;
                if (index == 2) return _Coverage0.z;
                if (index == 3) return _Coverage0.w;
                if (index == 4) return _Coverage1.x;
                if (index == 5) return _Coverage1.y;
                if (index == 6) return _Coverage1.z;
                if (index == 7) return _Coverage1.w;
                if (index == 8) return _Coverage2.x;
                if (index == 9) return _Coverage2.y;
                if (index == 10) return _Coverage2.z;
                return _Coverage2.w;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float position = input.uv.x * 12 - 0.5;
                int section = (int)floor(position);
                float progress = lerp(Coverage(section), Coverage(section + 1), smoothstep(0, 1, frac(position)));
                float2 uv = float2(frac(input.uv.x * _Pattern.z + _Pattern.x), input.uv.y * _Pattern.w);
                float2 second = float2(frac(input.uv.x * _Pattern.z * 1.31 + _Pattern.y), input.uv.y * _Pattern.w * 1.17);
                // Variation comes from the generated powder bitmap, not geometric strips/noise painting.
                half powder = max(SAMPLE_TEXTURE2D(_PowderMask, sampler_PowderMask, uv).a,
                    SAMPLE_TEXTURE2D(_PowderMask, sampler_PowderMask, second).a * 0.6);
                half alpha = powder * (1 - saturate(progress)) * _Tint.a;
                clip(alpha - 0.002);
                return half4(_Tint.rgb * 1.35, alpha);
            }
            ENDHLSL
        }
    }
}
