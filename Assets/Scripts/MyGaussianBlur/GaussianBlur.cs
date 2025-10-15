using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 为 Volume 组件设置菜单路径和显示名称
[System.Serializable, VolumeComponentMenu("Post-processing/Custom/My Gaussian Blur")]
public sealed class MyGaussianBlur : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Tooltip("模糊半径（像素），值越大，模糊程度越高。")]
    public ClampedFloatParameter radius = new ClampedFloatParameter(10f, 0f, 20f);

    // 当 radius 大于 0 时，此效果才激活
    public bool IsActive() => m_Material != null && radius.value > 0f;

    // 定义后处理的注入点，AfterPostProcess 表示在所有内置后处理之后执行
    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.AfterPostProcess;

    // 材质实例
    private Material m_Material;
    
    // 用于临时渲染纹理的属性 ID
    private int m_TempRT_ID = Shader.PropertyToID("_TempBlurTexture");
    private bool m_HasSaved = false;
    

    // 当效果首次启用时调用，用于初始化资源
    public override void Setup()
    {
        // 从 "Hidden/Shader/GaussianBlurFixed" 创建材质实例
        // 请确保你的 Shader 文件名与这里一致
        m_Material = CoreUtils.CreateEngineMaterial("Hidden/Shader/GaussianBlur");
    }

    // HDRP 的渲染循环每帧都会调用此方法
    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle src, RTHandle dest)
    {
        // 如果材质未成功创建，则直接将源纹理拷贝到目标，不做任何处理
        if (m_Material == null)
        {
            HDUtils.BlitCameraTexture(cmd, src, dest);
            return;
        }

        // --- 这是核心修改部分 ---

        // 1. 将 C# 中的参数值传递给 Shader
        m_Material.SetFloat("_Radius", radius.value);

        // 2. 获取一个与源纹理（src）描述符一致的临时渲染纹理（RT）
        // 这个 RT 用于存储第一遍（横向）模糊的结果
        var desc = src.rt.descriptor;
        desc.depthBufferBits = 0; // 后处理通常不需要深度缓冲
        cmd.GetTemporaryRT(m_TempRT_ID, desc, FilterMode.Bilinear);

        // 3. 执行第一遍处理（Pass 0: 横向模糊）
        // 从 src 读取数据，经过横向模糊后，将结果写入到我们的临时 RT 中
        cmd.Blit(src, m_TempRT_ID, m_Material, 0);

        // 4. 执行第二遍处理（Pass 1: 纵向模糊）
        // 从临时 RT 读取数据（Blit 会自动将其绑定到 _MainTex），经过纵向模糊后，
        // 将最终结果写入到目标纹理 dest
        m_Material.SetTexture("_OriginalTex", src);
        cmd.Blit(m_TempRT_ID, dest, m_Material, 1);

        // 5. 释放不再需要的临时渲染纹理，防止内存泄漏
        cmd.ReleaseTemporaryRT(m_TempRT_ID);
    }
    
    

    // 当效果被禁用或销毁时调用，用于释放资源
    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}