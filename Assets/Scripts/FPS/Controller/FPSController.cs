using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPS
{
    /// <summary>
    /// FPS 输入 + 相机控制器（单例）。
    /// 复用 MyInputSystem（无需改动输入资产）。
    /// 职责：轮询输入 → 提供 moveInput/worldMovement 等；管理双 CinemachineFreeLook 相机（腰射/肩射/开镜）；屏幕中心瞄准射线。
    /// 相机旋转由 Cinemachine 处理。
    /// </summary>
    public class FPSController : SingleMonoBase<FPSController>
    {
        [Header("相机")]
        [Tooltip("正常（腰射）视角相机")]
        public CinemachineFreeLook freeLookCamera;
        [Tooltip("瞄准（肩射/开镜）视角相机")]
        public CinemachineFreeLook aimingCamera;

        [Header("瞄准")]
        [Tooltip("屏幕中心射线检测目标")]
        public Transform AimTarget;
        [Tooltip("瞄准射线最大距离")]
        public float maxRayDistance = 1000f;
        [Tooltip("瞄准射线检测层级")]
        public LayerMask aimLayerMask = ~0;
        [Tooltip("肩射 FOV")]
        public float shoulderFov = 55f;
        [Tooltip("开镜 FOV")]
        public float adsFov = 40f;
        [Tooltip("开镜切换键")]
        public Key adsKey = Key.V;

        [Header("移动")]
        [Tooltip("转向速度（度/秒）")]
        public float rotationSpeed = 300f;
        [Tooltip("瞄准时移动速度上限")]
        public float aimMoveSpeed = 2.5f;

        [Header("相机灵敏度")]
        [Tooltip("水平灵敏度（每像素 Look delta 增加的水平角度，X 轴值域 0~360°）")]
        public float xSensitivity = 0.15f;
        [Tooltip("固定视角 Y 轴值（0=底部rig / 0.5=中部 / 1=顶部，正常第三人称通常 0.55~0.65）")]
        [Range(0f, 1f)]
        public float fixedLookY = 0.6f;

        // —— 输入（状态机每帧读取）——
        [HideInInspector] public Vector2 moveInput;
        [HideInInspector] public Vector2 lookDelta;
        [HideInInspector] public bool isSprint;
        [HideInInspector] public bool isAiming;
        [HideInInspector] public bool isJumping;
        [HideInInspector] public bool isFire;
        [HideInInspector] public bool isSlide;
        [HideInInspector] public bool isAds;
        [HideInInspector] public int switchWeaponNumber;   // 0=无切换，1/2/3=对应武器槽位（复用 First/Second/Third 数字键）

        // —— 方向 ——
        [HideInInspector] public Vector3 localMovement;
        [HideInInspector] public Vector3 worldMovement;

        private MyInputSystem input;

        /// <summary>相机水平朝向（瞄准时角色回正用）</summary>
        public float cameraYaw
        {
            get
            {
                var cam = Camera.main;
                return cam == null ? transform.eulerAngles.y : cam.transform.eulerAngles.y;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            input = new MyInputSystem();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Start()
        {
            // 默认使用自由相机
            if (freeLookCamera != null) freeLookCamera.Priority = 100;
            if (aimingCamera != null) aimingCamera.Priority = 0;

            // 关键：禁用两架 FreeLook 的自动读轴（m_InputAxisName 置空），
            // 避免 Cinemachine 默认轴输入 与 我们手动驱动 m_XAxis/m_YAxis 双重叠加（灵敏度翻倍/镜头乱跳）。
            DisableAutoAxis(freeLookCamera);
            DisableAutoAxis(aimingCamera);
        }

        /// <summary>禁用 FreeLook 相机的自动轴输入读取，仅保留手动驱动</summary>
        private void DisableAutoAxis(CinemachineFreeLook cam)
        {
            if (cam == null) return;
            // m_InputAxisName="" 使 Cinemachine 不再读取系统轴名输入；
            // m_InputAxisValue 是 float，置 0 表示"无额外固定输入"，彻底避免双重叠加。
            cam.m_XAxis.m_InputAxisName = "";
            cam.m_XAxis.m_InputAxisValue = 0f;
            cam.m_YAxis.m_InputAxisName = "";
            cam.m_YAxis.m_InputAxisValue = 0f;
        }

        private void OnEnable() { input?.Enable(); }
        private void OnDisable() { input?.Disable(); }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            input?.Dispose();
        }

        private void Update()
        {
            #region 读取输入
            moveInput = input.Player.Move.ReadValue<Vector2>().normalized;
            lookDelta = input.Player.Look.ReadValue<Vector2>();
            isSprint = input.Player.IsSprint.IsPressed();
            isAiming = input.Player.IsAiming.IsPressed();
            isJumping = input.Player.IsJumping.triggered;
            isFire = input.Player.Fire.IsPressed();
            isSlide = input.Player.IsSlide.triggered;
            isAds = Keyboard.current[adsKey].isPressed;

            // 武器切换：数字键 1/2/3（复用 First/Second/Third；FPS 场景不用切角色）
            switchWeaponNumber = 0;
            if (input.Player.First.triggered) switchWeaponNumber = 1;
            else if (input.Player.Second.triggered) switchWeaponNumber = 2;
            else if (input.Player.Third.triggered) switchWeaponNumber = 3;
            #endregion

            #region 计算相机相对移动方向
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 cameraForwardProjection = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;
                worldMovement = cameraForwardProjection * moveInput.y + cam.transform.right * moveInput.x;
            }
            else
            {
                worldMovement = Vector3.zero;
            }
            #endregion

            #region 接管 FreeLook 相机旋转（鼠标 Look 驱动，摆脱 Cinemachine axis 名映射问题）
            // 两架相机 X 轴（左右）始终保持同步——鼠标增量同时加到两架，
            // 避免切换 Priority 时另一架 X 轴停留在旧值 → 朝向突变 → 角色被拽转（"卡帧+平移+上下抖"）。
            // Y 轴（上下）固定在最舒服的角度，只对当前激活相机生效（另一架在 SetAimCamera 切换时同步）。
            if (freeLookCamera != null)
                freeLookCamera.m_XAxis.Value += lookDelta.x * xSensitivity;
            if (aimingCamera != null)
                aimingCamera.m_XAxis.Value += lookDelta.x * xSensitivity;

            // 当前激活相机（Priority 高者）的 Y 固定为 fixedLookY
            CinemachineFreeLook active = aimingCamera != null && aimingCamera.Priority > 0 ? aimingCamera : freeLookCamera;
            if (active != null)
                active.m_YAxis.Value = fixedLookY;
            #endregion
        }

        /// <summary>重置两架相机目标（进入场景 / 重设角色时调用）</summary>
        public void ResetCameraTarget(FPSModel model)
        {
            if (model == null) return;
            if (freeLookCamera != null) { freeLookCamera.Follow = model.transform; freeLookCamera.LookAt = model.transform; }
            if (aimingCamera != null) { aimingCamera.Follow = model.transform; aimingCamera.LookAt = model.transform; }
        }

        /// <summary>切换瞄准相机生效状态（并双向同步两架相机角度，Y 轴固定视角防抖）</summary>
        public void SetAimCamera(bool active)
        {
            if (freeLookCamera == null || aimingCamera == null) return;
            if (active)
            {
                // 切到瞄准相机：同步角度，且两架 Y 轴都归位 fixedLookY（固定视角，杜绝上下跳变）
                aimingCamera.m_XAxis.Value = freeLookCamera.m_XAxis.Value;
                aimingCamera.m_YAxis.Value = fixedLookY;
                freeLookCamera.m_YAxis.Value = fixedLookY;
                freeLookCamera.Priority = 0;
                aimingCamera.Priority = 100;
            }
            else
            {
                // 切回自由相机：同步角度，且两架 Y 轴都归位 fixedLookY
                freeLookCamera.m_XAxis.Value = aimingCamera.m_XAxis.Value;
                freeLookCamera.m_YAxis.Value = fixedLookY;
                aimingCamera.m_YAxis.Value = fixedLookY;
                freeLookCamera.Priority = 100;
                aimingCamera.Priority = 0;
            }
        }

        /// <summary>设置瞄准相机 FOV（肩射/开镜用）</summary>
        public void SetAimFov(float fov)
        {
            if (aimingCamera == null) return;
            aimingCamera.m_Lens.FieldOfView = fov;
        }

        /// <summary>屏幕中心射线更新瞄准目标位置</summary>
        public void UpdateAimTarget()
        {
            if (AimTarget == null || Camera.main == null) return;
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, aimLayerMask))
                AimTarget.position = hit.point;
            else
                AimTarget.position = ray.origin + ray.direction * maxRayDistance;
        }
    }
}
