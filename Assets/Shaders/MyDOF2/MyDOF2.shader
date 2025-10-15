Shader "Hidden/Shader/CustomDepthOfField"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    HLSLINCLUDE
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
    
    // --- 通用参数 ---
    float _BlurRadius;
    float4 _BlurDirection; // (1,0) for horizontal, (0,1) for vertical
    
    // --- 仅用于合成Pass的参数 ---
    TEXTURE2D_X(_BlurredTex); // 完全模糊的图像
    SAMPLER(sampler_BlurredTex);
    TEXTURE2D_X(_MainTex);  
    float _NearBlurStart;
    float _NearBlurEnd;
    float _FarBlurStart;
    float _FarBlurEnd;

    float4 _MainTex_TexelSize;

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }
    
    // --- 高斯权重计算函数 ---
    float GetGaussianWeight(float offset, float sigma)
    {
        return exp(-0.5 * (offset * offset) / (sigma * sigma));
    }

    // --- Pass 0 & 1: 纯粹的模糊处理函数 ---
    float4 BlurFragmentPass(Varyings i) : SV_Target
    {
        float3 blurredColor = float3(0.0, 0.0, 0.0);
        float totalWeight = 0.0;
        
        float sigma = _BlurRadius / 2.0;
        // 将 float 半径转换为 int 循环边界
        int radiusInt = (int)ceil(_BlurRadius);

        for (int j = -radiusInt; j <= radiusInt; j++)
        {
            float2 offset = _BlurDirection.xy * _MainTex_TexelSize.xy * j;
            float2 sampleUV = i.texcoord + offset;
            
            float weight = GetGaussianWeight(j, sigma);
            
            blurredColor += LOAD_TEXTURE2D_X(_MainTex, sampleUV).rgb * weight;
            totalWeight += weight;
        }

        //return float4(1.0, 0.0, 0.0, 1.0);
        return LOAD_TEXTURE2D_X(_MainTex, i.texcoord);
        return float4(blurredColor / totalWeight, 1.0);
    }

    // --- Pass 2: 合成处理函数 ---
    float4 CompositionFragment(Varyings i) : SV_Target
    {
        // 获取原始清晰图像的颜色
        float4 originalColor = LOAD_TEXTURE2D_X(_MainTex, i.texcoord);
        // 获取预先计算好的完全模糊图像的颜色
        float4 blurredColor = SAMPLE_TEXTURE2D_X(_BlurredTex, sampler_BlurredTex, i.texcoord);
        
        // --- 计算模糊因子 ---
        float depth = LoadCameraDepth(i.texcoord);
        float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
        
        float nearFactor = 1.0 - smoothstep(_NearBlurStart, _NearBlurEnd, linearEyeDepth);
        float farFactor = smoothstep(_FarBlurStart, _FarBlurEnd, linearEyeDepth);
        float blurFactor = max(nearFactor, farFactor);

        // 根据模糊因子，在清晰和模糊图像之间进行插值
        return lerp(originalColor, blurredColor, blurFactor);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        // Pass 0: 水平模糊
        Pass
        {
            Name "Horizontal Blur"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma fragment BlurFragmentPass
                #pragma vertex Vert
            ENDHLSL
        }

        // Pass 1: 垂直模糊
        Pass
        {
            Name "Vertical Blur"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma fragment BlurFragmentPass
                #pragma vertex Vert
            ENDHLSL
        }

        // Pass 2: 根据深度进行合成
        Pass
        {
            Name "Composition"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma fragment CompositionFragment
                #pragma vertex Vert
            ENDHLSL
        }
    }
    Fallback Off
}