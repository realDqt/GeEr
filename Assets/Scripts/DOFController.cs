using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class DOFController : MonoBehaviour
{

    public Volume globalVolume; // 用于在 Inspector 中直接指定 Volume，更高效
    public GameObject focusGameObject;
    public Camera dofCamera;
    // 等待EyeTracking bug修复
    public bool useEyeTracking = false;
    public Vector3 eyeTrackingPosition = Vector3.zero;

    // 调整这两个参数，让模糊与清晰边界合适
    public float nearOffset = 1.0f;
    public float farOffset = 1.0f;
    
    private MyGaussianBlurSinglePass myGaussianBlur; // 缓存自定义效果的引用
    private Vector3 focusPosition;
    
    void Start()
    {
        // 检查是否在 Inspector 中指定了 Volume
        if (globalVolume == null)
        {
            // 如果没有指定，尝试在场景中自动查找
            globalVolume = FindObjectOfType<Volume>();
        }

        if (globalVolume == null)
        {
            Debug.LogError("场景中没有找到 Volume 组件！请确保场景中有一个 Volume。");
            return;
        }

        // 从 Volume Profile 中获取我们的自定义后处理效果
        // 为了安全地在运行时修改，我们最好操作 profile 的一个实例 (profile)，
        // 而不是直接修改资源文件 (sharedProfile)。
        // 注意：第一次访问 .profile 会自动创建一个实例。
        if (globalVolume.profile.TryGet<MyGaussianBlurSinglePass>(out var customEffect))
        {
            myGaussianBlur = customEffect;
            Debug.Log("成功找到 MyGaussianBlurSinglePass 效果！");
        }
        else
        {
            Debug.LogError("在指定的 Volume Profile 中没有找到 MyGaussianBlurSinglePass！请检查 Volume Profile 的设置。");
        }
        
    }

    void Update()
    {
        focusPosition = useEyeTracking ? eyeTrackingPosition : focusGameObject.GetComponent<Transform>().position;
        float depth = CalcDepthFromDOFCamera(dofCamera, focusPosition);
        Debug.Log("focus game object's depth = " + depth);

        myGaussianBlur.nearBlurEnd.value = depth - nearOffset;
        myGaussianBlur.nearBlurStart.value = myGaussianBlur.nearBlurEnd.value - 1.2f;

        myGaussianBlur.farBlurStart.value = depth + farOffset;
        myGaussianBlur.farBlurEnd.value = myGaussianBlur.farBlurStart.value + 1.2f;
    }

    float CalcDepthFromDOFCamera(Camera dofCamera, Vector3 worldPosition)
    {
        // 1. 获取摄像机的 Transform 组件
        Transform cameraTransform = dofCamera.transform;

        // 2. 计算从摄像机位置指向目标世界位置的向量
        //    worldPosition: 目标点的位置
        //    cameraTransform.position: 摄像机的位置
        Vector3 cameraToWorldPosition = worldPosition - cameraTransform.position;

        // 3. 获取摄像机的前向向量 (Z 轴)
        //    这是一个已经标准化的单位向量，代表了摄像机正对着的方向。
        Vector3 cameraForward = cameraTransform.forward;

        // 4. 使用点积 (Dot Product) 计算投影距离
        //    点积 A·B 的几何意义是向量 A 在向量 B 上的投影长度乘以 B 的模长。
        //    因为 cameraForward 是一个单位向量（模长为 1），
        //    所以这里的点积结果直接就是 cameraToWorldPosition 在 cameraForward 上的投影长度。
        //    这个长度就是我们需要的线性深度。
        float depth = Vector3.Dot(cameraToWorldPosition, cameraForward);

        return depth;
    }
}
