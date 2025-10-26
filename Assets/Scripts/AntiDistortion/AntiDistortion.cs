using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// 为你的自定义效果提供菜单项
[Serializable, VolumeComponentMenu("Post-processing/Custom/AntiDistortion")]
public sealed class AntiDistortion : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    // --- 1. 声明所有 Shader 参数 ---
    
    // 对应 Shader 中的 _EyeDist
    [Tooltip("Distance between the two 'eyes'.")]
    public FloatParameter eyeDist = new FloatParameter(0.1f);

    // 对应 Shader 中的 _FOV_Width
    [Tooltip("Field of View Width for distortion calculation.")]
    public FloatParameter fovWidth = new FloatParameter(20f);

    // 对应 Shader 中的 _FOV_Height
    [Tooltip("Field of View Height for distortion calculation.")]
    public FloatParameter fovHeight = new FloatParameter(20f);

    // 对应 Shader 中的 _Screen_Width
    [Tooltip("Screen Width for distortion calculation.")]
    public FloatParameter screenWidth = new FloatParameter(100f);

    // 对应 Shader 中的 _Screen_Height
    [Tooltip("Screen Height for distortion calculation.")]
    public FloatParameter screenHeight = new FloatParameter(100f);

    // 对应 Shader 中的 _K_R
    [Tooltip("Red channel distortion coefficients (x=k1, y=k2, z=k3).")]
    public Vector3Parameter kR = new Vector3Parameter(new Vector3(1, 1, 1));

    // 对应 Shader 中的 _K_G
    [Tooltip("Green channel distortion coefficients (x=k1, y=k2, z=k3).")]
    public Vector3Parameter kG = new Vector3Parameter(new Vector3(1, 1, 1));

    // 对应 Shader 中的 _K_B
    [Tooltip("Blue channel distortion coefficients (x=k1, y=k2, z=k3).")]
    public Vector3Parameter kB = new Vector3Parameter(new Vector3(1, 1, 1));


    public BoolParameter enabled = new BoolParameter(true);
    
    // --- 2. 核心方法实现 ---

    // 告诉 HDRP 这个效果应该在何时注入
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    // IsActive 决定了 Render 方法是否会被调用
    // 只有当材质存在并且用户在 Volume 中勾选了 "enable" 时，才激活
    public bool IsActive() => m_Material != null && enabled.value;

    private Material m_Material;

    public override void Setup()
    {
        // 找到并创建一个使用你的 Shader 的材质实例
        m_Material = CoreUtils.CreateEngineMaterial("Custom/HDRP_MapShader");
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        
        // 再次检查材质，以防万一
        if (m_Material == null)
        {
            Debug.LogError("m_Material for anti-distortion is null");
            return;
        }
            

        // 使用 ProfilingScope 来在 Frame Debugger 和 Profiler 中正确标记
        using (new ProfilingScope(cmd, new ProfilingSampler("AntiDistortion")))
        {
            // --- 3. 将参数从 C# 传递到 Shader ---
            // 注意：字符串名称必须与 Shader Properties 中的名称完全一致

            m_Material.SetFloat("_EyeDist", eyeDist.value);
            m_Material.SetFloat("_FOV_Width", fovWidth.value);
            m_Material.SetFloat("_FOV_Height", fovHeight.value);
            m_Material.SetFloat("_Screen_Width", screenWidth.value);
            m_Material.SetFloat("_Screen_Height", screenHeight.value);

            // SetVector 可以接受 Vector3, 它会自动转换为 shader 需要的 float4 (w=0)
            m_Material.SetVector("_K_R", kR.value);
            m_Material.SetVector("_K_G", kG.value);
            m_Material.SetVector("_K_B", kB.value);

            // 执行 Shader
            //cmd.Blit(source, destination);
            cmd.Blit(source, destination, m_Material);
        }
    }

    public override void Cleanup()
    {
        // 销毁我们创建的材质实例
        CoreUtils.Destroy(m_Material);
    }
}