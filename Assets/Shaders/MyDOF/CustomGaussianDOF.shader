Shader "Hidden/Shader/CustomGaussianDOF"
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
    TEXTURE2D_X(_BlurTex);   // 模糊图
    TEXTURE2D_X(_TemTex);    // 单次模糊图
    //sampler2D _MainTex;
    //sampler2D _BlurTex;
    float4 _MainTex_TexelSize;
    
    // 景深参数 (C# 脚本传入)
    float _NearStart;
    float _NearEnd;
    float _FarStart;
    float _FarEnd;
    
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

    // Pass 0: 横向模糊 (使用原 MainTex 作为输入)
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
            //color += tex2D(_MainTex, uint2(uvShift * _ScreenSize.xy)).rgb * _Weights[i];
        }

        return float4(color, 1.0);
    }
    
    // Pass 1: 纵向模糊 (输入是 Pass 0 的结果)
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
    
    // Pass 2: 混合 Pass (Composite)
    float4 CustomDOFComposite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        

        // 1. 获取深度
        // 使用正确的宏从深度纹理中采样原始非线性深度
        float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, s_linear_clamp_sampler, uv).r; 
        // Convert the Z buffer value to linear view space depth (距离摄像机的距离)
        float viewDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        
        // 2. 计算模糊系数 (Blur Factor)
        float blurFactor = 0.0;
        
        // A. 近景模糊
        // 在 [NearStart, NearEnd] 范围内，从 1 (模糊) 渐变到 0 (清晰)
        // 目标：当 viewDepth <= NearStart, blurFactor = 1.0; 当 viewDepth >= NearEnd, blurFactor = 0.0
        float nearRange = _NearEnd - _NearStart;
        // 如果 nearRange 接近于 0 或 NearEnd < NearStart，使用一个安全值
        nearRange = max(nearRange, 0.001); 
        float nearBlur = saturate((_NearEnd - viewDepth) / nearRange); // 1.0 在近处，0.0 在远处
        
        // B. 远景模糊
        // 在 [FarStart, FarEnd] 范围内，从 0 (清晰) 渐变到 1 (模糊)
        // 目标：当 viewDepth <= FarStart, blurFactor = 0.0; 当 viewDepth >= FarEnd, blurFactor = 1.0
        float farRange = _FarEnd - _FarStart;
        farRange = max(farRange, 0.001);
        float farBlur = saturate((viewDepth - _FarStart) / farRange); // 0.0 在近处，1.0 在远处
        
        // C. 组合模糊系数
        // 取两者最大值，确保在任一模糊区域内都应用模糊。
        // 在 [NearEnd, FarStart] 之间，nearBlur 和 farBlur 都是 0，因此 blurFactor = 0 (清晰)。
        blurFactor = max(nearBlur, farBlur);
        
        // 3. 混合
        float3 originalColor = LOAD_TEXTURE2D_X(_MainTex, uint2(uv * _ScreenSize.xy)).rgb; // 原始清晰图
        float3 blurredColor = LOAD_TEXTURE2D_X(_BlurTex, uint2(uv * _ScreenSize.xy)).rgb;  // 模糊图
        //float3 originalColor = tex2D(_MainTex, uint2(uv * _ScreenSize.xy)).rgb; // 原始清晰图
        //float3 blurredColor = tex2D(_BlurTex, uint2(uv * _ScreenSize.xy)).rgb;  // 模糊图
        
        // lerp(清晰图, 模糊图, 模糊系数)
        float3 finalColor = lerp(originalColor, blurredColor, blurFactor);
        finalColor = blurredColor;
        return float4(finalColor, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" }

        // Pass 0: Horizontal Blur (Input: SourceRT, Output: TempRT)
        Pass
        {
            Name "GaussianBlurH"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GaussianBlurH
            ENDHLSL
        }

        // Pass 1: Vertical Blur (Input: TempRT, Output: BlurredRT)
        Pass
        {
            Name "GaussianBlurV"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GaussianBlurV
            ENDHLSL
        }
        
        // Pass 2: Composite (Input: SourceRT, BlurredRT; Output: FinalRT)
        Pass
        {
            Name "Composite"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CustomDOFComposite
            ENDHLSL
        }
    }
    Fallback Off
}