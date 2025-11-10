using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable, VolumeComponentMenu("Post-processing/Custom/ATW Simulation")]
public sealed class ATWSimulationVolume : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    // A Matrix4x4 parameter to hold our transformation matrix.
    // It is not displayed in the UI because it's controlled entirely by the C# controller script.
    [Tooltip("The ATW inverse matrix, controlled by script.")]
    public Matrix4x4 atwInverseMatrix = Matrix4x4.identity;

    [Header("Manual Rotation Delta (Degrees)")]
    [Tooltip("左右扭曲效果 (偏航角)")]
    [Range(-10f, 10f)]
    public ClampedFloatParameter yaw = new ClampedFloatParameter(0.0f, -10.0f, 10.0f);

    [Tooltip("上下扭曲效果 (俯仰角)")]
    [Range(-10f, 10f)]
    public ClampedFloatParameter pitch = new ClampedFloatParameter(0.0f, -10.0f, 10.0f);

    [Tooltip("倾斜扭曲效果 (翻滚角)")]
    [Range(-10f, 10f)]
    public ClampedFloatParameter roll = new ClampedFloatParameter(0.0f, -10.0f, 10.0f);

    public BoolParameter enabled = new BoolParameter(true);
    
    // This effect is always considered "active" when present on the Volume stack.
    // Its visual effect is enabled/disabled by the ATWController script which sets the matrix.
    public bool IsActive() => true && enabled.value;

    //public FrameRateLimiter m_frameRateLimiter = null;

    // We inject this effect after all other standard post-processing to ensure it's the last thing applied.
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;
    
    
    private Material m_Material;

    // Shader property IDs
    private static readonly int k_ATWInverseMatrixID = Shader.PropertyToID("_ATW_InverseMatrix");
    // Add new property IDs for the matrices we'll pass manually
    private static readonly int k_NonJitteredProjMatrixID = Shader.PropertyToID("_Custom_NonJitteredProjection");
    private static readonly int k_NonJitteredInverseProjMatrixID = Shader.PropertyToID("_Custom_NonJitteredInverseProjection");

    public override void Setup()
    {
        // The shader path must match the "Hidden/..." name in the shader file
        m_Material = CoreUtils.CreateEngineMaterial("Hidden/ATW_Simulation");
        /*
        m_frameRateLimiter = GameObject.Find("FrameLimiter").GetComponent<FrameRateLimiter>();
        if (m_frameRateLimiter == null)
        {
            Debug.LogError("Failed to find FrameRateLimiter");
        }
        */
    }
    

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        using (new ProfilingScope(cmd, new ProfilingSampler("ATW Simulation"))) ;
        if (m_Material == null)
            return;
        
        // --- CORE MODIFICATION ---
        // Get the required matrices directly from the HDCamera object.
        // This is the robust way to get projection matrices in a custom post process.
        var projMatrix = camera.mainViewConstants.projMatrix;
        // Using GL.GetGPUProjectionMatrix is a robust way to get the inverse that handles platform differences.
        var invProjMatrix = camera.mainViewConstants.invProjMatrix;
        
        // Send all required data to the shader
        // 1. 直接使用Inspector中设置的yaw, pitch, roll值创建一个旋转增量
        // random test
        bool wrap = UnityEngine.Random.Range(0f, 1.0f) > 0.7f;
        float rPitch = UnityEngine.Random.Range(0f, 0.2f);
        float rYaw = UnityEngine.Random.Range(0f, 0.2f);
        float rRoll = UnityEngine.Random.Range(0f, 0.1f);
        Quaternion deltaRotation = Quaternion.Euler(rPitch, rYaw, rRoll);
        if(!wrap) deltaRotation = Quaternion.Euler(0, 0, 0);
        deltaRotation = Quaternion.Euler(0, 0, 0); // zero it
        
        // 2. 计算其逆旋转，这是Shader重投影所需要的
        Quaternion inverseDelta = Quaternion.Inverse(deltaRotation);
        
        // 3. 将逆旋转转换为矩阵，并更新到Volume参数中
        atwInverseMatrix = Matrix4x4.TRS(Vector3.zero, inverseDelta, Vector3.one);
        m_Material.SetMatrix(k_ATWInverseMatrixID, atwInverseMatrix);
        m_Material.SetMatrix(k_NonJitteredProjMatrixID, projMatrix);
        m_Material.SetMatrix(k_NonJitteredInverseProjMatrixID, invProjMatrix);
        
        // Execute the shader pass to render the effect
        cmd.Blit(source, destination, m_Material);
        //HDUtils.DrawFullScreen(cmd, m_Material, destination, null, 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}