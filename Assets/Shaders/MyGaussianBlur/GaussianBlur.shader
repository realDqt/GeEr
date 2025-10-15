Shader "Hidden/Shader/GaussianBlur"
{
    Properties
    {
        _MainTex("Main Texture", 2DArray) = "grey" {}
        _Radius("Blur Radius", Range(0, 20)) = 5
    }
    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    // ——————  高斯模糊参数  —————— 
    float _Radius;
    TEXTURE2D_X(_MainTex);
    float4 _MainTex_TexelSize;

    // 预计算权重 (建议在 C# 端计算好后传入，以提高效率)
    static const int MAX_TAP = 35;
    float _Weights[MAX_TAP];
    int _TapCount;

    // 注意：PrepareKernel 在 GPU 每像素执行一次效率较低。
    // 更优化的方案是在 C# 脚本中计算一次，然后通过 SetFloatArray/SetInt 将 _Weights 和 _TapCount 传给 Shader。
    // 这里为了保持与原代码结构类似，暂时保留此函数。
    void PrepareKernel()
    {
        float sigma = max(_Radius * 0.25f, 0.5f);
        int halfWidth = min((int)(sigma * 3.0f), MAX_TAP / 2);
        _TapCount = halfWidth * 2 + 1;

        float sum = 0.0;
        for (int i = 0; i < _TapCount; ++i)
        {
            int x = i - halfWidth;
            float w = exp(-(x * x) / (2.0 * sigma * sigma));
            _Weights[i] = w;
            sum += w;
        }
        for (int i = 0; i < _TapCount; ++i)
            _Weights[i] /= sum;
    }

    // Pass 0: 横向模糊
    float4 GaussianBlurH(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        PrepareKernel();

        float3 color = 0.0f;
        int halfWidth = _TapCount / 2;
        float2 texelSize = _MainTex_TexelSize.xy;
        float2 uv = input.texcoord;

        for (int i = 0; i < _TapCount; ++i)
        {
            int offset = i - halfWidth;
            float2 uvShift = uv + float2(offset * texelSize.x, 0.0f);
            color += LOAD_TEXTURE2D_X(_MainTex, uint2(uvShift * _ScreenSize.xy)).rgb * _Weights[i];
        }

        //color = LOAD_TEXTURE2D_X(_MainTex, input.texcoord).rgb;
        return float4(color, 1.0);
    }
    
    // Pass 1: 纵向模糊
    float4 GaussianBlurV(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        PrepareKernel();

        float3 color = 0.0f;
        int halfWidth = _TapCount / 2;
        float2 texelSize = _MainTex_TexelSize.xy;
        float2 uv = input.texcoord;

        for (int i = 0; i < _TapCount; ++i)
        {
            int offset = i - halfWidth;
            float2 uvShift = uv + float2(0.0f, offset * texelSize.y);
            color += LOAD_TEXTURE2D_X(_MainTex, uint2(uvShift * _ScreenSize.xy)).rgb * _Weights[i];
        }
        return float4(color, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }

        // Pass 0 for Horizontal Blur
        Pass
        {
            Name "GaussianBlurH"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GaussianBlurH
            ENDHLSL
        }

        // Pass 1 for Vertical Blur
        Pass
        {
            Name "GaussianBlurV"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GaussianBlurV
            ENDHLSL
        }
    }
    Fallback Off
}