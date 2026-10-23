Shader "HustleScale/CartographicMap"
{
    Properties
    {
        _MapColor ("Map color", Color) = (1, 1, 1, 1)
        _MapTexture ("Surface atlas", 2D) = "white" {}
        _MapTile ("Atlas offset and size", Vector) = (0, 0, 1, 1)
        _MapScale ("Surface repeat in metres", Float) = 12
        _MapDetail ("Surface strength", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _MapZTest ("Depth test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest [_MapZTest]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4x4 _MapMatrix;
            float4x4 _MapWorldMatrix;
            float4 _MapColor;
            sampler2D _MapTexture;
            float4 _MapTile;
            float _MapScale;
            float _MapDetail;
            struct MapVertex { float4 position : SV_POSITION; float2 surface : TEXCOORD0; };
            MapVertex Vert(float4 position : POSITION)
            {
                MapVertex output;
                output.position = mul(_MapMatrix, position);
                output.surface = mul(_MapWorldMatrix, position).xz / _MapScale;
                return output;
            }
            half4 Frag(MapVertex input) : SV_Target
            {
                float2 uv = _MapTile.xy + (0.002 + frac(input.surface) * 0.996) * _MapTile.zw;
                half3 surface = tex2D(_MapTexture, uv).rgb;
                return half4(lerp(_MapColor.rgb, surface, _MapDetail), 1);
            }
            ENDHLSL
        }
    }
}
