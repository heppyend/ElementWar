using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 控制头部瞄准目标：始终在相机位置附近，鼠标可向左/右/上/后偏移，禁止向前偏移
/// </summary>
public class HeadAimTarget : MonoBehaviour
{
    [Tooltip("回中速度")]
    [SerializeField] private float returnSpeed = 3f;
    [Tooltip("鼠标灵敏度")]
    [SerializeField] private float sensitivity = 0.01f;
    [Tooltip("左右最大偏移")]
    [SerializeField] private float maxHorizontal = 0.3f;
    [Tooltip("上下最大偏移")]
    [SerializeField] private float maxVertical = 0.15f;
    [Tooltip("向后最大偏移（禁止向前，所以此值 ≥ 0，0 表示完全不能前后移动）")]
    [SerializeField] private float maxBackward = 0.1f;
    [Tooltip("默认静止偏移（相机本地空间：X=右, Y=上, Z=前），调此值让角色自然看向期望方向")]
    [SerializeField] private Vector3 restOffset = new Vector3(-0.5f, 0f, 0f);

    private Camera mainCamera;
    private Vector2 currentOffset;

    void Start()
    {
        mainCamera = Camera.main;
        // 静止位置：相机位置 + 默认偏移
        Vector3 worldRest = mainCamera.transform.position
                          + mainCamera.transform.right   * restOffset.x
                          + mainCamera.transform.up      * restOffset.y
                          + mainCamera.transform.forward * restOffset.z;
        transform.position = worldRest;
    }

    void LateUpdate()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // 累加鼠标位移
        currentOffset += mouseDelta * sensitivity;
        currentOffset.x = Mathf.Clamp(currentOffset.x, -maxHorizontal, maxHorizontal);
        currentOffset.y = Mathf.Clamp(currentOffset.y, -maxVertical,   maxVertical);

        // 回中
        currentOffset = Vector2.Lerp(currentOffset, Vector2.zero, returnSpeed * Time.deltaTime);

        // 鼠标偏移 → 世界空间：水平=右轴，垂直=上轴
        Vector3 mouseWorldOffset = mainCamera.transform.right * currentOffset.x
                                 + mainCamera.transform.up    * currentOffset.y;

        // 前后：仅允许向后（-forward），禁止向前
        float backward = Mathf.Clamp(currentOffset.y * 0.5f, -maxBackward, 0f);
        mouseWorldOffset += mainCamera.transform.forward * backward;

        // 静止位置：相机位置 + 默认偏移（相机本地 → 世界）
        Vector3 worldRest = mainCamera.transform.position
                          + mainCamera.transform.right   * restOffset.x
                          + mainCamera.transform.up      * restOffset.y
                          + mainCamera.transform.forward * restOffset.z;

        transform.position = worldRest + mouseWorldOffset;
    }
}
