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

    // This effect is always considered "active" when present on the Volume stack.
    // Its visual effect is enabled/disabled by the ATWController script which sets the matrix.
    public bool IsActive() => true; 

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
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (m_Material == null)
            return;

        // --- CORE MODIFICATION ---
        // Get the required matrices directly from the HDCamera object.
        // This is the robust way to get projection matrices in a custom post process.
        var projMatrix = camera.mainViewConstants.projMatrix;
        // Using GL.GetGPUProjectionMatrix is a robust way to get the inverse that handles platform differences.
        var invProjMatrix = camera.mainViewConstants.invProjMatrix;
        
        // Send all required data to the shader
        m_Material.SetMatrix(k_ATWInverseMatrixID, atwInverseMatrix);
        m_Material.SetMatrix(k_NonJitteredProjMatrixID, projMatrix);
        m_Material.SetMatrix(k_NonJitteredInverseProjMatrixID, invProjMatrix);
        
        // Execute the shader pass to render the effect
        cmd.Blit(source, destination);
        //HDUtils.DrawFullScreen(cmd, m_Material, destination, null, 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}