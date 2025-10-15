Shader "FullScreen/CustomGaussianBlurV"
{
     Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _BlurTex("Blurred Texture", 2D) = "white" {} // 新增：用于接收模糊后的结果
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
    TEXTURE2D_X(_MainTex);   // 原图
    float4 _MainTex_TexelSize;
    
    
    // 预计算权重 (C# 脚本传入)
    static const int MAX_TAP = 35;
    float _Weights[MAX_TAP];
    int _TapCount;

    // ... (PrepareKernel 函数省略，因为它在 C# 中计算更高效，但为了兼容您原结构，保留在 HLSL 中)
    void PrepareKernel()
    {
        // 建议在 C# 脚本中计算权重并传入
        // ... (保持您原有的 PrepareKernel 实现)
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
            // 注意：这里读取的 _MainTex 应该是在 C# 中绑定到 Pass 0 输出的纹理
            color += LOAD_TEXTURE2D_X(_MainTex, uint2(uvShift * _ScreenSize.xy)).rgb * _Weights[i];
            //color += tex2D(_MainTex, uint2(uvShift * _ScreenSize.xy)).rgb * _Weights[i]; 
        }
        return float4(color, 1.0);
    }
    

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" }
        

        // Pass 0: Vertical Blur (Input: TempRT, Output: BlurredRT)
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
