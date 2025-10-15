using UnityEngine;
using UnityEngine.Rendering;

public class ATWController : MonoBehaviour
{
    [Tooltip("The main camera that drives the simulation.")]
    public Camera mainCamera;
    
    [Tooltip("The Volume Profile containing the ATW Simulation effect.")]
    public VolumeProfile volumeProfile;

    [Tooltip("Enable or disable the simulation effect.")]
    public bool enableSimulation = true;

    // The ATW Volume component instance we will control
    private ATWSimulationVolume atwVolumeComponent;

    // Stores the camera rotation at the time the scene was conceptually "rendered"
    private Quaternion renderTimeRotation;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("ATWController: Main Camera not found! Disabling script.", this);
                enabled = false;
                return;
            }
        }
        
        if (volumeProfile == null || !volumeProfile.TryGet(out atwVolumeComponent))
        {
            Debug.LogError("ATWController: ATWSimulationVolume not found in the provided Volume Profile! Please add it. Disabling script.", this);
            enabled = false;
            return;
        }

        // Initialize rotation
        renderTimeRotation = mainCamera.transform.rotation;
    }

    // LateUpdate is called after all Update functions.
    // We use this to capture the rotation that the main scene render *would* use.
    void LateUpdate()
    {
        renderTimeRotation = mainCamera.transform.rotation;
    }

    // Subscribe to the render pipeline event when the component is enabled.
    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    // Unsubscribe and reset the matrix when the component is disabled.
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (atwVolumeComponent != null)
        {
            atwVolumeComponent.atwInverseMatrix = Matrix4x4.identity;
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // Only run for our target camera
        if (camera != mainCamera) return;

        if (atwVolumeComponent == null || !enableSimulation)
        {
            if (atwVolumeComponent != null) atwVolumeComponent.atwInverseMatrix = Matrix4x4.identity;
            return;
        }

        // 1. Get the "latest" rotation, simulating the pose read right before display.
        Quaternion vsyncTimeRotation = mainCamera.transform.rotation;
        
        // 2. Calculate the small rotation that occurred between rendering and VSync.
        // delta = new_rotation * inverse(old_rotation)
        Quaternion deltaRotation = vsyncTimeRotation * Quaternion.Inverse(renderTimeRotation);
        
        // 3. For our shader, we need the INVERSE of this delta.
        // This is the rotation that transforms the new view back to the old one.
        Quaternion inverseDelta = Quaternion.Inverse(deltaRotation);
        
        // 4. Convert the quaternion to a matrix and update the Volume Component.
        // The shader will pick this up during the post-processing phase.
        atwVolumeComponent.atwInverseMatrix = Matrix4x4.TRS(Vector3.zero, inverseDelta, Vector3.one);
    }
}