// Mặt phẳng tàng hình: không vẽ màu (nên vẫn thấy ảnh camera),
// nhưng che mọi vật nằm phía sau nó, tức phần tim còn ở dưới mặt giấy.
Shader "Custom/DepthMask"
{
    SubShader
    {
        // Vẽ trước tim (Geometry-10) để tim bị che đúng chỗ
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ColorMask 0   // không tô màu
            ZWrite On     // nhưng vẫn ghi chiều sâu để che vật phía sau

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}