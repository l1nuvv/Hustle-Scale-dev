Shader "HustleScale/CartographicMap"
{
    Properties
    {
        _MapColor ("Map color", Color) = (1, 1, 1, 1)
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
            float4 _MapColor;
            float4 Vert(float4 position : POSITION) : SV_POSITION
            {
                return mul(_MapMatrix, position);
            }
            half4 Frag() : SV_Target { return _MapColor; }
            ENDHLSL
        }
    }
}
