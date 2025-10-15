Shader "Hidden/Shader/GaussianBlurSinglePass"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Radius ("Blur Radius", Range(0, 60)) = 3
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 vulkan metal

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    TEXTURE2D_X(_MainTex);
    
    float4 _MainTex_TexelSize;
    float _Radius;
    
    float _NearStart;
    float _NearEnd;
    float _FarStart;
    float _FarEnd;
    

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

    float CacBlurFactor(float linearEyeDepth)
    {
        // 近景
        if(linearEyeDepth >= _NearStart && linearEyeDepth <= _NearEnd)
        {
            return 1.0;
            //return 1.0 - smoothstep(_NearStart, _NearEnd, linearEyeDepth);
        }

        // 远景
        if(linearEyeDepth >= _FarStart && linearEyeDepth <= _FarEnd)
        {
            return 1.0;
            //return smoothstep(_FarStart, _FarEnd, linearEyeDepth);
        }
        
        return 0.0;
    }

    float CacBlurFactor_V2(float linearEyeDepth)
    {
        // 计算近景模糊因子
        // smoothstep(_NearEnd, _NearStart, depth) 的意思是：
        // 当 depth <= _NearStart 时, 结果为 1.0 (完全模糊)
        // 当 depth >= _NearEnd 时,   结果为 0.0 (清晰)
        // 在两者之间平滑插值
        float nearFactor = smoothstep(_NearEnd, _NearStart, linearEyeDepth);

        // 计算远景模糊因子
        // smoothstep(_FarStart, _FarEnd, depth) 的意思是：
        // 当 depth <= _FarStart 时, 结果为 0.0 (清晰)
        // 当 depth >= _FarEnd 时,   结果为 1.0 (完全模糊)
        // 在两者之间平滑插值
        float farFactor = smoothstep(_FarStart, _FarEnd, linearEyeDepth);

        // 将两个因子合并。因为清晰区域nearFactor和farFactor都为0，所以取最大值即可。
        return max(nearFactor, farFactor);
    }

    float4 GaussianBlur(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;

        float sigma = max(_Radius * 0.25, 0.5);
        int halfWidth = min((int)(sigma * 3.0), 3);   // 7×7 上限
        float3 blurColor = 0.0;
        float sum = 0.0;

        for (int dy = -halfWidth; dy <= halfWidth; ++dy)
        {
            for (int dx = -halfWidth; dx <= halfWidth; ++dx)
            {
                float2 offset = float2(dx, dy) * _MainTex_TexelSize.xy;
                float w = exp(-(dx*dx + dy*dy) / (2.0 * sigma * sigma));
                blurColor += LOAD_TEXTURE2D_X(_MainTex, uint2((uv + offset) * _ScreenSize.xy)).rgb * w;
                sum += w;
            }
        }
        blurColor /= sum;
        
        // --- 计算模糊因子 ---
        float depth = LoadCameraDepth(input.positionCS.xy);
        float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
        float blurFactor = CacBlurFactor_V2(linearDepth);
        
        
        float3 oriColor = LOAD_TEXTURE2D_X(_MainTex, input.texcoord * _ScreenSize.xy).rgb;
        
        /*
        // --- 清晰mask ---
        float mask = 0.0f;
        if(linearDepth >= _NearEnd && linearDepth <= _FarStart)
            mask = 1.0f;
        */

        /*
        // --- 近景mask ---
        float mask = 0.0f;
        if(linearDepth <= _NearEnd)
            mask = 1.0f;
        */

        /*
        // --- 远景mask ---
        float mask = 0.0f;
        if(linearDepth >= _FarStart)
            mask = 1.0f;
        */

        /*
        // --- 可视化深度 ---
        float linear01Depth = Linear01Depth(depth, _ZBufferParams);
        return float4(linear01Depth, linear01Depth, linear01Depth, 1.0);
        */
        
        return float4(lerp(oriColor, blurColor, blurFactor), 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GaussianBlur
            ENDHLSL
        }
    }
    Fallback Off
}