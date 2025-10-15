using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
// GraphicsFormat 位于 UnityEngine.Experimental.Rendering.GraphicsFormat，但通常引用 UnityEngine.Rendering 即可
// 如果还是报错，请确保 using UnityEngine.Experimental.Rendering; 在顶部
using UnityEngine.Experimental.Rendering; // 确保包含此命名空间，以防 GraphicsFormat 报错

[System.Serializable, VolumeComponentMenu("Post-processing/Custom/Gaussian DOF")]
public sealed class CustomGaussianDOF : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Tooltip("模糊半径（像素）")]
    public ClampedFloatParameter radius = new ClampedFloatParameter(5, 0, 20);

    [Header("Depth Range (m)")]
    public MinFloatParameter nearStart = new MinFloatParameter(1, 0.1f);
    public MinFloatParameter nearEnd   = new MinFloatParameter(3, 0.1f);
    public MinFloatParameter farStart  = new MinFloatParameter(10, 0.1f);
    public MinFloatParameter farEnd    = new MinFloatParameter(15, 0.1f);

    // 运行时材质
    Material m_Mat;
    private Material m_MatBlurV;

    // RTHandles 声明 (用于存储临时结果)
    private RTHandle m_TempRT;
    private RTHandle m_TempRT2;
    private RTHandle m_BlurRT;
    
    private int m_TempRT_ID = Shader.PropertyToID("_TempBlurTexture");
    private int m_BlurRT_ID = Shader.PropertyToID("_BlurResTexture");
    
    // 权重数组和计数
    float[] m_Weights;
    int m_TapCount;
    float m_LastRadius = -1f; // 用于检测 radius 是否变化

    // Shader Property IDs (推荐使用 ID 代替字符串，效率更高)
    private static readonly int _MainTex    = Shader.PropertyToID("_MainTex");
    //private static readonly int _TemTex    = Shader.PropertyToID("_TemTex");
    private static readonly int _BlurTex    = Shader.PropertyToID("_BlurTex");
    private static readonly int _TemTex    = Shader.PropertyToID("_TemTex");
    private static readonly int _Radius     = Shader.PropertyToID("_Radius");
    private static readonly int _NearStart  = Shader.PropertyToID("_NearStart");
    private static readonly int _NearEnd    = Shader.PropertyToID("_NearEnd");
    private static readonly int _FarStart   = Shader.PropertyToID("_FarStart");
    private static readonly int _FarEnd     = Shader.PropertyToID("_FarEnd");
    private static readonly int _Weights    = Shader.PropertyToID("_Weights");
    private static readonly int _TapCount   = Shader.PropertyToID("_TapCount");


    // 判断本 effect 是否生效
    public bool IsActive() => radius.value > 0 && m_Mat != null;

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.AfterPostProcess;

    // --- 资源管理 ---

    // 第一次使用前初始化
    public override void Setup()
    {
        // 1. 创建材质
        m_Mat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Shader/CustomGaussianDOF"));
        m_MatBlurV = CoreUtils.CreateEngineMaterial(Shader.Find("FullScreen/CustomGaussianBlurV"));
        
        m_TempRT = RTHandles.Alloc(
            Vector2.one
        );
        m_TempRT2 = RTHandles.Alloc(
            Vector2.one
        );
        m_BlurRT = RTHandles.Alloc(
            Vector2.one
        );
    }

    // 清理
    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Mat);
        // 释放 RTHandles
        RTHandles.Release(m_TempRT);
        RTHandles.Release(m_BlurRT);
    }

    // --- 核心渲染逻辑 ---
    
    // 每帧渲染
    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle src, RTHandle dest)
    {
        if (m_Mat == null) return;

        // 1. 计算 1D 高斯核（仅当半径变化时）
        if (Mathf.Abs(radius.value - m_LastRadius) > 0.001f)
        {
            //CalculateKernel();
            m_LastRadius = radius.value;
        }

        // 2. 传参
        m_Mat.SetFloat(_Radius, radius.value);
        m_Mat.SetFloat(_NearStart, nearStart.value);
        m_Mat.SetFloat(_NearEnd, nearEnd.value);
        m_Mat.SetFloat(_FarStart, farStart.value);
        m_Mat.SetFloat(_FarEnd, farEnd.value);
        //m_Mat.SetFloatArray(_Weights, m_Weights);
        m_Mat.SetInt(_TapCount, m_TapCount);
        
        m_MatBlurV.SetFloat(_Radius, radius.value);
        m_MatBlurV.SetFloat(_TapCount, m_TapCount);

        /*
        // ---Combine Test---
        var desc = src.rt.descriptor;
        desc.depthBufferBits = 0; // 后处理通常不需要深度缓冲
        cmd.GetTemporaryRT(m_TempRT_ID, desc, FilterMode.Bilinear);
        cmd.Blit(src, m_TempRT_ID, m_Mat, 0);

        
        cmd.GetTemporaryRT(m_BlurRT_ID, desc, FilterMode.Bilinear);
        cmd.Blit(m_TempRT_ID, m_BlurRT_ID, m_Mat, 1);
        
        m_Mat.SetTexture(_BlurTex, m_BlurRT);
        cmd.Blit(src, dest, m_Mat, 2);
        
        
        cmd.ReleaseTemporaryRT(m_TempRT_ID);
        cmd.ReleaseTemporaryRT(m_BlurRT_ID);
        */
        
        
        
        // --- 三 Pass 渲染 ---
        // 3. 横 Pass (Pass 0)
        //HDUtils.DrawFullScreen(cmd, m_Mat, m_TempRT, shaderPassId: 0);
        cmd.Blit(src, m_TempRT, m_Mat, 0);

        // 4. 纵 Pass (Pass 1)
        //m_Mat.SetTexture(_TemTex, m_TempRT);
        //HDUtils.DrawFullScreen(cmd, m_Mat, m_BlurRT, shaderPassId: 1);
        cmd.Blit(m_TempRT, m_BlurRT, m_MatBlurV ,0);
        
        cmd.Blit(m_BlurRT, dest);

        // 5. 合成 Pass (Pass 2)
        //m_Mat.SetTexture(_BlurTex, m_BlurRT); 
        //HDUtils.DrawFullScreen(cmd, m_Mat, dest, shaderPassId: 2);
        
        
        
        /*
        // --- Gaussian Blur Test---
        var desc = src.rt.descriptor;
        desc.depthBufferBits = 0; // 后处理通常不需要深度缓冲
        cmd.GetTemporaryRT(m_TempRT_ID, desc, FilterMode.Bilinear);
        cmd.Blit(src, m_TempRT_ID, m_Mat, 0);

        
        cmd.GetTemporaryRT(m_BlurRT_ID, desc, FilterMode.Bilinear);
        cmd.Blit(m_TempRT_ID, dest, m_Mat, 1);
        
        
        cmd.ReleaseTemporaryRT(m_TempRT_ID);
        cmd.ReleaseTemporaryRT(m_BlurRT_ID);
        */
        
    }

    // 计算高斯权重
    void CalculateKernel()
    {
        // 保持与原脚本一致的计算逻辑
        float sigma = Mathf.Max(radius.value * 0.25f, 0.5f);
        // 确保 TapCount 不超过 35
        int half = Mathf.Min(Mathf.CeilToInt(sigma * 3f), 17); 
        m_TapCount = half * 2 + 1;

        if (m_Weights == null || m_Weights.Length < m_TapCount)
            m_Weights = new float[m_TapCount];

        float sum = 0f;
        for (int i = 0; i < m_TapCount; ++i)
        {
            int x = i - half;
            float w = Mathf.Exp(-(x * x) / (2f * sigma * sigma));
            m_Weights[i] = w;
            sum += w;
        }
        for (int i = 0; i < m_TapCount; ++i)
            m_Weights[i] /= sum;
    }
}