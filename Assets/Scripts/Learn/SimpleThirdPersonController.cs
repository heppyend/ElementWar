using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 精简第三人称控制器（New Scene 学习用）
/// 依赖组件：CharacterController（角色移动）、Animator（BlendTree 动画）
/// 实现功能：WASD 相机相对移动 / 鼠标旋转相机 / Speed 参数驱动混合树 / 左键触发攻击
/// 说明：从零编写，不依赖 Game 场景的 PlayerController / 状态机框架。
///       相机通过一个空的 cameraRig 作为旋转载体，Main Camera 挂在它下面。
/// </summary>
public class SimpleThirdPersonController : MonoBehaviour
{
    #region 相机参数（Inspector 可调）
    [Tooltip("相机载体（空物体，Main Camera 作为其子物体）")]
    public Transform cameraRig;
    [Tooltip("相机相对角色的高度")]
    public float cameraHeight = 1.6f;
    [Tooltip("相机到角色的距离")]
    public float cameraDistance = 3.5f;
    [Tooltip("鼠标灵敏度")]
    public float mouseSensitivity = 2f;
    [Tooltip("相机俯仰角下限（度）")]
    public float pitchMin = -30f;
    [Tooltip("相机俯仰角上限（度）")]
    public float pitchMax = 60f;
    #endregion

    #region 移动参数（Inspector 可调）
    [Tooltip("步行速度")]
    public float walkSpeed = 2.2f;
    [Tooltip("跑步速度")]
    public float runSpeed = 5.5f;
    [Tooltip("角色转向速度（度/秒）")]
    public float rotateSpeed = 540f;
    [Tooltip("重力")]
    public float gravity = -15f;
    #endregion

    private CharacterController cc;
    private Animator animator;

    private Vector2 moveInput;//WASD 输入（x=左右, y=前后）
    private bool isSprint;//是否冲刺
    private bool attackPressed;//本帧是否触发攻击
    private float yaw;//相机水平角（绕 Y 轴）
    private float pitch;//相机俯仰角（绕 X 轴）
    private float verticalSpeed;//垂直速度（重力）

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        Cursor.lockState = CursorLockMode.Locked;

        // 禁用 Animator 的 Root Motion：
        // 安比动画由 3ds Max 烘焙，根骨骼 Bip01 带位移关键帧（尤其 Y 轴），
        // 若开启 Apply Root Motion，角色会被动画位移顶着乱飞/飞天。
        // 关闭后移动完全由本脚本 + CharacterController 控制，动画只提供姿态。
        if (animator != null)
            animator.applyRootMotion = false;

        // 初始 yaw 取角色当前朝向，避免相机刚启动时角度跳变
        yaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        ReadInput();
        UpdateCamera();
        UpdateMovement();
        UpdateAnimation();
    }

    /// <summary>
    /// 读取输入（New Input System 直接轮询，无需 .inputactions 资产）
    /// </summary>
    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        moveInput = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),//左右
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f) //前后
        );
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();//防止斜向加速

        isSprint = kb.leftShiftKey.isPressed;
        attackPressed = mouse.leftButton.wasPressedThisFrame;

        // 鼠标增量 → 视角旋转
        Vector2 mouseDelta = mouse.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
    }

    /// <summary>
    /// 相机跟随：cameraRig 定位到角色头顶并旋转，Main Camera 挂在下方自动跟随
    /// </summary>
    private void UpdateCamera()
    {
        if (cameraRig == null) return;
        cameraRig.position = transform.position + Vector3.up * cameraHeight;
        cameraRig.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// 移动：相机相对方向 + 手动重力，攻击时定身
    /// </summary>
    private void UpdateMovement()
    {
        if (cc == null) return;

        bool attacking = IsAttacking();

        // 相机相对移动方向（投影到水平面）
        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraRig.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraRig.right, Vector3.up).normalized;
        Vector3 worldMove = cameraForward * moveInput.y + cameraRight * moveInput.x;

        // 攻击时定身，不移动不转向（动画自带动作为主）
        float speed = 0f;
        if (!attacking)
        {
            speed = isSprint ? runSpeed : walkSpeed;
            // 角色转向移动方向
            if (worldMove.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(worldMove);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            }
        }

        // 重力
        if (cc.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;//踩地时轻微下压，保持贴合
        else
            verticalSpeed += gravity * Time.deltaTime;

        Vector3 motion = worldMove * speed * Time.deltaTime;
        motion.y = verticalSpeed * Time.deltaTime;
        cc.Move(motion);
    }

    /// <summary>
    /// 动画：Speed 参数驱动 BlendTree（0=Idle 0.5=Walk 1=Run），左键触发攻击
    /// </summary>
    private void UpdateAnimation()
    {
        if (animator == null) return;

        // BlendTree 三片混合：Idle(0) / Walk(0.5) / Run(1)
        float animSpeed = moveInput.magnitude > 0.01f
            ? (isSprint ? 1f : 0.5f)
            : 0f;
        animator.SetFloat(SpeedHash, animSpeed, 0.1f, Time.deltaTime);

        if (attackPressed)
            animator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// 是否正在播放攻击动画（Animator 中攻击状态需设置 Tag = "Attack"）
    /// </summary>
    private bool IsAttacking()
    {
        if (animator == null) return false;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.IsTag("Attack");
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
    }
}
