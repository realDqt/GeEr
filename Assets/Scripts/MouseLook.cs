using UnityEngine;

/// <summary>
/// 一个简单而强大的摄像机鼠标视角控制脚本
/// </summary>
public class MouseLook : MonoBehaviour
{
    [Header("灵敏度设置")]
    [Tooltip("鼠标灵敏度，数值越高，视角转动越快")]
    [Range(50f, 500f)]
    public float mouseSensitivity = 150f;

    // 用于累积水平方向的旋转角度 (Yaw)
    private float yaw = 0.0f;
    // 用于累积垂直方向的旋转角度 (Pitch)
    private float pitch = 0.0f;

    void Start()
    {
        // 在游戏开始时锁定并隐藏鼠标光标
        Cursor.lockState = CursorLockMode.Locked;
        
        // 初始化旋转角度为摄像机当前的朝向，以防止开始时视角突变
        Vector3 initialEulerAngles = transform.eulerAngles;
        yaw = initialEulerAngles.y;
        pitch = initialEulerAngles.x;
    }

    void Update()
    {
        // --- 1. 获取鼠标输入 ---
        // Time.deltaTime 使得旋转速度与帧率无关，保证在任何电脑上体验一致
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // --- 2. 累积旋转角度 ---
        
        // 累积水平旋转（绕Y轴）
        yaw += mouseX;

        // 累积垂直旋转（绕X轴）
        // 注意这里用 -= 是因为鼠标向上移动时，GetAxis("Mouse Y")返回正值，
        // 但我们希望摄像机向上看，也就是绕X轴做负向旋转。
        pitch -= mouseY;

        // --- 3. 限制垂直旋转角度 ---
        // 使用 Mathf.Clamp 将俯仰角限制在-90度（直视下方）和90度（直视上方）之间
        // 这样可以防止摄像机无限翻转。
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        // --- 4. 应用最终旋转 ---
        // 根据累积的yaw和pitch值，创建一个新的旋转。
        // Quaternion.Euler 会根据我们提供的欧拉角(绕X, Y, Z轴的旋转)创建一个四元数。
        // 我们不希望有任何Z轴的倾斜（roll），所以Z值设为0。
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}