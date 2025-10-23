// Shader "Custom/HDRP_MapShader"
Shader "Custom/HDRP_MapShader"
{
    Properties
    {
        // This texture will be bound automatically by the Custom Pass framework.
        _MainTex ("Input Texture", 2D) = "white" {}

        // 保留你所有的原始参数
        _EyeDist("Eye Distance", Float) = 0.1
        _FOV_Width("Fov Width", Float) = 20
        _FOV_Height("Fov Height", Float) = 20
        _Screen_Width("Screen Width", Float) = 100
        _Screen_Height("Screen Height", Float) = 100
        _K_R("K_R", Vector) = (1, 1, 1)
        _K_G("K_G", Vector) = (1, 1, 1)
        _K_B("K_B", Vector) = (1, 1, 1)
    }

    SubShader
    {
        // 标记为 HDRP 管线
        Tags { "RenderPipeline"="HighDefinition" }

        Pass
        {
            Name "CustomPass"
            
            // 这是全屏后处理，关闭深度和剔除
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            // 使用 HLSL 替换 CGPROGRAM
            HLSLPROGRAM
            
            // 定义 Shader Target 和顶点/片元着色器
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            // 包含 HDRP Custom Pass 所需的核心库
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            // 声明输入纹理和采样器 (使用 HDRP 宏)
            TEXTURE2D_X(_MainTex);

            // 声明你的其他 uniform 变量
            float _EyeDist;
            float _FOV_Width;
            float _FOV_Height;
            float _Screen_Width;
            float _Screen_Height;
            float3 _K_R;
            float3 _K_G;
            float3 _K_B;

             struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            // HDRP Custom Pass 的标准顶点着色器
            // 它会为我们生成一个全屏三角面
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            // 你的畸变计算函数 (原封不动)
            // 我只加了一个 max(r, 1e-6) 来防止在中心点出现除以 0 的情况
            float2 calculate_uv(float2 uv, float2 center, float2 screenSize, float2 fovSize, float3 K)
            {
                float2 duv = (uv - center) * screenSize;
                float r = sqrt(duv.x * duv.x + duv.y * duv.y);
                
                // 防止在 r=0 时除以 0 导致 NaN
                r = max(r, 1e-6);

                float R = K.x * pow(r, 3) + K.y * pow(r, 2) + K.z * r;
                return (duv / r * R) / fovSize + center;
            }

            // 片元着色器
            // Varyings i 结构体由 CustomPass.hlsl 提供
            // i.texcoord 对应你原来的 i.uv
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 使用 i.texcoord 替换 i.uv
                float uv_x;
                float2 center;

                // left eye
                if (input.uv.x < 0.5) {
                    uv_x = input.uv.x * (1 - _EyeDist) * 2;
                    center = float2(0.5 * (1 - _EyeDist), 0.5);
                }
                // right eye
                else {
                    uv_x = (input.uv.x - 0.5) * (1 - _EyeDist) * 2 + _EyeDist;
                    center = float2(0.5 * (1 + _EyeDist), 0.5);
                }

                float2 uv = float2(uv_x, input.uv.y);
                float2 screenSize = float2(_Screen_Width, _Screen_Height);
                float2 fovSize = float2(_FOV_Width, _FOV_Height);
                
                // 注意：我保留了你原始的逻辑，即 uv_r 使用 _K_G, uv_g 使用 _K_R
                float2 uv_r = calculate_uv(uv, center, screenSize, fovSize, _K_G);
                float2 uv_g = calculate_uv(uv, center, screenSize, fovSize, _K_R);
                float2 uv_b = calculate_uv(uv, center, screenSize, fovSize, _K_B);

                // 使用 HDRP 的采样宏来采样纹理
                float r = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, uv_r).r;
                float g = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, uv_g).g;
                float b = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, uv_b).b;

                // HDRP 中使用 float4, 1.0
                //return LOAD_TEXTURE2D_X(_MainTex, input.positionCS.xy);
                float3 oriColor = SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, input.uv).rgb;
                //return float4(oriColor, 1.0);
                return float4(r, g, b, 1.0);
            }

            ENDHLSL
        }
    }
}