Shader "Hidden/RobotCamera/LinearDepth"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;

            float4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_CameraDepthTexture_TexelSize.y < 0)
                    uv.y = 1 - uv.y;
                #endif

                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float linear01 = Linear01Depth(rawDepth);
                if (linear01 >= 0.999999)
                    return 0;

                return LinearEyeDepth(rawDepth);
            }
            ENDCG
        }
    }
    Fallback Off
}
