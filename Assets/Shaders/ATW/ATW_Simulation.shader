// A simple shader to simulate the reprojection effect of ATW.
// This shader is intended for use with a Custom Post Process in Unity's HDRP.
Shader "Hidden/ATW_Simulation"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
    }

    SubShader
    {
        // For Custom Post Processing
        Tags{ "RenderPipeline" = "HighDefinitionRenderPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D_X(_MainTex);
            float4 _MainTex_TexelSize;
            
            // The correctional rotation matrix (inverse of the delta rotation)
            float4x4 _ATW_InverseMatrix;
            
            // Uniform variables to receive matrices explicitly passed from the C# script
            float4x4 _Custom_NonJitteredInverseProjection;
            float4x4 _Custom_NonJitteredProjection;


            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 1. Convert screen UV to Normalized Device Coordinates (NDC) [-1, 1]
                float2 ndc = input.uv * 2.0 - 1.0;

                // 2. Unproject from screen space to a 3D direction vector in view space.
                // We use the inverse projection matrix passed from C#.
                float4 viewPos = mul(_Custom_NonJitteredInverseProjection, float4(ndc, 0.0, 1.0));
                viewPos.xyz /= viewPos.w; // Perspective divide

                // 3. Apply the inverse of the delta rotation. This transforms the view ray
                // to where it *would have been* before the head's micro-rotation.
                float4 oldViewPos = mul(_ATW_InverseMatrix, viewPos);
                
                // 4. Project this "old" view space vector back to clip space.
                // We use the projection matrix passed from C#.
                float4 oldClipPos = mul(_Custom_NonJitteredProjection, oldViewPos);

                // 5. Perspective divide to get the "old" NDC.
                oldClipPos.xyz /= oldClipPos.w;

                // 6. Convert "old" NDC back to UV coordinates [0, 1]
                float2 oldUV = oldClipPos.xy * 0.5 + 0.5;

                // If the reprojected UV is outside the original screen bounds,
                // it means this part of the view wasn't rendered. This is what causes
                // the black borders in real ATW when moving too fast. We simulate this.
                if (oldUV.x < 0.0 || oldUV.x > 1.0 || oldUV.y < 0.0 || oldUV.y > 1.0)
                {
                    //return float4(0.0, 0.0, 0.0, 1.0); // Black border
                }

                // 7. Sample the original render target at the calculated "old" UV.
                //return SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, input.uv);
                float3 oriColor = LOAD_TEXTURE2D_X(_MainTex, input.uv * _ScreenSize.xy).rgb;
                return SAMPLE_TEXTURE2D_X(_MainTex, s_linear_clamp_sampler, oldUV);
                return float4(oriColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}