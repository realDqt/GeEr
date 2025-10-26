using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable, VolumeComponentMenu("Post-processing/Custom/My Gaussian Blur (Single Pass)")]
public sealed class MyGaussianBlurSinglePass : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Tooltip("模糊半径（像素），值越大，模糊程度越高。")]
    public ClampedFloatParameter radius = new ClampedFloatParameter(10f, 0f, 60f);
    
    [Tooltip("近景模糊开始的距离。")]
    public MinFloatParameter nearBlurStart = new MinFloatParameter(0.1f, 0f);

    [Tooltip("近景模糊最强的距离（在此距离内模糊达到最大）。")]
    public MinFloatParameter nearBlurEnd = new MinFloatParameter(5f, 0f);

    [Tooltip("远景模糊开始的距离。")]
    public MinFloatParameter farBlurStart = new MinFloatParameter(20f, 0f);

    [Tooltip("远景模糊最强的距离（超过此距离模糊达到最大）。")]
    public MinFloatParameter farBlurEnd = new MinFloatParameter(50f, 0f);
    
    public BoolParameter enabled = new BoolParameter(true);
    

    public bool IsActive() => m_Material != null && 
                              radius.value > 0 && 
                              nearBlurEnd.value > nearBlurStart.value && 
                              farBlurStart.value < farBlurEnd.value && 
                              nearBlurEnd.value < farBlurStart.value &&
                              enabled.value;

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.AfterPostProcess;

    private Material m_Material;

    public override void Setup()
    {
        m_Material = CoreUtils.CreateEngineMaterial("Hidden/Shader/GaussianBlurSinglePass");
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle src, RTHandle dest)
    {
        using (new ProfilingScope(cmd, new ProfilingSampler("My Gaussian Blur (Single Pass)"))) ;
        if (m_Material == null)
        {
            HDUtils.BlitCameraTexture(cmd, src, dest);
            return;
        }

        m_Material.SetFloat("_Radius", radius.value);
        m_Material.SetFloat("_NearStart", nearBlurStart.value);
        m_Material.SetFloat("_NearEnd", nearBlurEnd.value);
        m_Material.SetFloat("_FarStart", farBlurStart.value);
        m_Material.SetFloat("_FarEnd", farBlurEnd.value);
        
        // 单 Pass → 一次 Blit 即可
        cmd.Blit(src, dest, m_Material, 0);
        //Blitter.BlitCameraTexture(cmd, src, dest, m_Material, 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}