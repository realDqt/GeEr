using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

public class FrameRateLimiter : MonoBehaviour
{
    public Volume globalVolume;
    private ATWSimulationVolume atwSim;
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
        if (globalVolume.profile.TryGet<ATWSimulationVolume>(out var customEffect))
        {
           atwSim = customEffect;
            Debug.Log("成功找到 ATWSimulationVolume 效果！");
        }
        else
        {
            Debug.LogError("在指定的 Volume Profile 中没有找到 ATWSimulationVolume！请检查 Volume Profile 的设置。");
        }
    }

    private void Update()
    {
        SetFPS(atwSim.enabled.value ? 90 : 45);
    }

    public void SetFPS(int targetFPS)
    {
        // 告诉Unity我们期望的目标帧率
        Application.targetFrameRate = targetFPS;
        
        // 重要：禁用VSync（垂直同步）
        // 如果VSync开启（例如 vSyncCount = 1），
        // 它会强制帧率等于你的显示器刷新率（如60, 144）
        // 这会覆盖掉 Application.targetFrameRate 的设置。
        QualitySettings.vSyncCount = 0;
    }
    
}