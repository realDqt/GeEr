using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

[Serializable, VolumeComponentMenu("Post-processing/Custom/Custom Depth Of Field")]
public sealed class CustomDepthOfField2 : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    #region Parameters

    [Tooltip("模糊的半径，值越大模糊效果越强，但性能开销也越大。支持小数。")]
    public ClampedFloatParameter blurRadius = new ClampedFloatParameter(5f, 1f, 25f);

    [Tooltip("近景模糊开始的距离。")]
    public MinFloatParameter nearBlurStart = new MinFloatParameter(0.1f, 0f);

    [Tooltip("近景模糊最强的距离（在此距离内模糊达到最大）。")]
    public MinFloatParameter nearBlurEnd = new MinFloatParameter(5f, 0f);

    [Tooltip("远景模糊开始的距离。")]
    public MinFloatParameter farBlurStart = new MinFloatParameter(20f, 0f);

    [Tooltip("远景模糊最强的距离（超过此距离模糊达到最大）。")]
    public MinFloatParameter farBlurEnd = new MinFloatParameter(50f, 0f);

    #endregion

    #region Internal
    
    private Material m_Material;
    private const string kShaderName = "Hidden/Shader/CustomDepthOfField";

    public bool IsActive() => m_Material != null && 
                               blurRadius.value > 0 && 
                               (nearBlurEnd.value > nearBlurStart.value || farBlurStart.value < farBlurEnd.value);

    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    #endregion

    #region Setup

    public override void Setup()
    {
        if (Shader.Find(kShaderName) != null)
        {
            m_Material = new Material(Shader.Find(kShaderName));
        }
        else
        {
            Debug.LogError($"无法找到名为 '{kShaderName}' 的 Shader。请确保 Shader 文件存在并且没有编译错误。");
        }
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (!IsActive())
        {
            HDUtils.BlitCameraTexture(cmd, source, destination);
            return;
        }

        // --- 传递通用参数 ---
        m_Material.SetFloat("_BlurRadius", blurRadius.value);
        
        // --- 执行三遍处理 ---
        var descriptor = new RenderTextureDescriptor(source.rt.width, source.rt.height, source.rt.format);
        // 为了优化，可以降低模糊图的分辨率，这里我们使用原分辨率
        // descriptor.width /= 2;
        // descriptor.height /= 2;

        // 获取两个临时的 RT
        RTHandle tempRT1 = RTHandles.Alloc(descriptor, name: "TempBlurRT1");
        RTHandle tempRT2 = RTHandles.Alloc(descriptor, name: "TempBlurRT2");

        // --- Pass 0: 水平模糊 (source -> tempRT1) ---
        // 这个 Pass 只做纯粹的模糊，不关心深度
        m_Material.SetVector("_BlurDirection", new Vector4(1, 0, 0, 0));
        HDUtils.BlitCameraTexture(cmd, source, destination, m_Material, 0);

        /*
        // --- Pass 1: 垂直模糊 (tempRT1 -> tempRT2) ---
        // 这个 Pass 同样只做纯粹的模糊，现在 tempRT2 中是完全模糊的图像
        m_Material.SetVector("_BlurDirection", new Vector4(0, 1, 0, 0));
        HDUtils.BlitCameraTexture(cmd, tempRT1, tempRT2, m_Material, 1);

        // --- Pass 2: 合成 (source + tempRT2 -> destination) ---
        // 将 C# 脚本中的景深距离参数传递给 Shader
        m_Material.SetFloat("_NearBlurStart", nearBlurStart.value);
        m_Material.SetFloat("_NearBlurEnd", nearBlurEnd.value);
        m_Material.SetFloat("_FarBlurStart", farBlurStart.value);
        m_Material.SetFloat("_FarBlurEnd", farBlurEnd.value);
        // 将完全模糊的图像 (tempRT2) 传递给 Shader
        m_Material.SetTexture("_BlurredTex", tempRT2);
        // 执行合成，源是原始清晰图像 source，最终输出到 destination
        HDUtils.BlitCameraTexture(cmd, source, destination, m_Material, 2);
        */
        
        // 释放临时的 RT
        tempRT1.Release();
        tempRT2.Release();
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }

    #endregion
}