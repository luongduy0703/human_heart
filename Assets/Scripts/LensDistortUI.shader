// Shader cho RawImage của HeadsetView: méo thùng trước (barrel pre-distortion) để bù méo gối
// của thấu kính kính VR. Mỗi điểm hiển thị ở bán kính r lấy mẫu ảnh tại r·(1 + k1·r² + k2·r⁴),
// tính trong khung của chính RawImage (tâm = giữa mỗi mắt). Ngoài vùng ảnh thì trong suốt.
// _UvRect = uvRect của RawImage (để biết khung 0..1 khi ảnh bị cắt), _Aspect = rộng/cao của mỗi mắt.
Shader "Custom/LensDistortUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _K1 ("K1", Float) = 0.22
        _K2 ("K2", Float) = 0.24
        _UvRect ("UV Rect", Vector) = (0, 0, 1, 1)
        _Aspect ("Aspect", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _UvRect;
                float _K1;
                float _K2;
                float _Aspect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Đổi uv về khung -1..1 của mắt
                float2 c = (i.uv - _UvRect.xy) / _UvRect.zw * 2.0 - 1.0;

                // Bán kính tính theo đơn vị thật (bù tỉ lệ khung), chuẩn hóa theo cạnh dài
                float2 p = c * float2(_Aspect, 1.0) / max(_Aspect, 1.0);
                float r2 = dot(p, p);
                float2 src = c * (1.0 + _K1 * r2 + _K2 * r2 * r2);

                if (any(abs(src) > 1.0)) return half4(0, 0, 0, 0);

                float2 uv = _UvRect.xy + (src * 0.5 + 0.5) * _UvRect.zw;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * i.color;
            }
            ENDHLSL
        }
    }
}
