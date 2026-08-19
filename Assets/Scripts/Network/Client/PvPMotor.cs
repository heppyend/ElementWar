using UnityEngine;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 简化移动（本地预测用，数学与 .NET 服务器 GameWorld.SimulatePlayer 完全一致）：
    /// 直接驱动 transform.position（不用 CharacterController 位移，避免与服务器碰撞差异），
    /// 并按速度驱动 Animator 参数（Speed/IsGrounded/IsSprinting/AimingX/AimingY）。
    /// 挂在与 PlayerModel 同一物体上；PlayerModel 需置 disableStateMachine=true。
    /// </summary>
    [RequireComponent(typeof(PlayerModel))]
    public class PvPMotor : MonoBehaviour
    {
        // ⚠️ 这些常量必须与服务器 GameWorldSettings 一致（改任一端都要同步）
        public const float WalkSpeed = 2.2f;
        public const float JogSpeed = 5f;
        public const float SprintSpeed = 8f;
        public const float AimMoveSpeed = 2.5f;
        public const float GroundY = 0.05f;              // 默认脚底偏移（出生点用）；运行时按每角色 CC 精确计算 _groundY
        public const float TurnSpeedDeg = 720f;          // 转向速度（PVE 的 2.4 倍，消除"视角转了模型没转"）
        public const float Gravity = -15f;
        public const float JumpVelocity = 6.7f;
        public const float RotationSpeedDeg = 300f;

        [Tooltip("当前输入（由 NetClient 每帧写入）")]
        public Vector2 moveInput;
        public bool isSprint, isAiming, isJumping, isSlide, isFire;
        public Vector3 worldMove = Vector3.zero;   // 相机相对的世界移动方向（XZ）
        public Vector3 aimPoint = Vector3.zero;    // 世界瞄准点
        public float bodyYawDeg;                   // 当前朝向（服务器快照校正用）

        private PlayerModel _model;
        private Animator _animator;
        private float _verticalSpeed;
        private bool _isGrounded = true;
        private float _speedBlend;
        private int _animState = -1;   // 0=Idle 1=Move 2=Aiming 3=Hover
        private float _groundY = GroundY;   // 每角色脚底偏移（Awake 按 CC 精确算）

        // 供其他组件（血条/受击）查询
        public float VerticalSpeed => _verticalSpeed;
        public bool IsGrounded => _isGrounded;
        public float SpeedBlend => _speedBlend;

        private void Awake()
        {
            _model = GetComponent<PlayerModel>();
            // 每角色脚底偏移从 CC 精确算（中心 - 高度/2），与服务器 GetGroundY 一致（荧0.025/芙宁娜0.15）
            if (_model != null && _model.cc != null)
                _groundY = Mathf.Max(0.02f, _model.cc.height * 0.5f - _model.cc.center.y);
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            transform.position = pos;
            bodyYawDeg = yawDeg;
            _verticalSpeed = 0f;
            _isGrounded = true;
        }

        public void ApplyState(Vector3 pos, float yawDeg, float verticalSpeed, bool grounded)
        {
            transform.position = pos;
            bodyYawDeg = yawDeg;
            _verticalSpeed = verticalSpeed;
            _isGrounded = grounded;
        }

        private void Update()
        {
            if (_model == null) return;
            if (_animator == null) _animator = _model.animator; // 懒加载，避免 Awake 顺序问题

            float dt = Time.deltaTime;

            // 速度选择
            Vector3 move = new Vector3(worldMove.x, 0f, worldMove.z);
            float mag = move.magnitude;
            Vector3 moveDir = mag > 0.01f ? move / mag : Vector3.zero;
            bool moving = mag > 0.01f;
            float speed = moving ? (isSprint ? SprintSpeed : isAiming ? AimMoveSpeed : JogSpeed) : 0f;

            // 水平位移（transform 直接移动）
            Vector3 delta = moveDir * (speed * dt);
            Vector3 newPos = transform.position + delta;

            // 垂直：重力 + 跳跃
            if (_isGrounded && isJumping)
            {
                _verticalSpeed = JumpVelocity;
                _isGrounded = false;
            }
            else if (!_isGrounded)
            {
                _verticalSpeed += Gravity * dt;
                newPos = new Vector3(newPos.x, transform.position.y + _verticalSpeed * dt, newPos.z);
            }

            if (newPos.y <= _groundY)
            {
                newPos = new Vector3(newPos.x, _groundY, newPos.z);
                _verticalSpeed = 0f;
                _isGrounded = true;
            }
            transform.position = newPos;
            _isGrounded = newPos.y <= _groundY + 0.001f;

            // 朝向（PVE 主视角）：瞄准/待机→面向相机方向（永远看背面，视角转角色就跟着转）；移动→面向移动方向。
            // ⚠️ 不用 aimPoint：屏幕中心射线可能命中自身碰撞体 → 面向相机 → 看到正脸。
            bool aiming = isAiming || isFire;
            float targetYaw = bodyYawDeg;
            if (aiming || !moving)
            {
                Vector3 camF = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                camF.y = 0f;
                if (camF.sqrMagnitude > 0.01f)
                    targetYaw = Mathf.Atan2(camF.x, camF.z) * Mathf.Rad2Deg;
            }
            else
            {
                targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            }
            bodyYawDeg = Mathf.MoveTowardsAngle(bodyYawDeg, targetYaw, TurnSpeedDeg * dt);
            transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);

            // Animator 状态切换：PVE 由状态机 CrossFade 到 Idle/Move/Aiming/Hover，PvPMotor 复刻
            // （只设 Speed 参数不会切出 Idle 状态 → 角色平移不摆腿、射击不进瞄准姿势）
            int desired = !_isGrounded ? 3 : aiming ? 2 : moving ? 1 : 0;
            if (desired != _animState)
            {
                _animState = desired;
                string stateName = desired == 3 ? "Hover" : desired == 2 ? "Aiming" : desired == 1 ? "Move" : "Idle";
                if (_animator != null) _animator.CrossFadeInFixedTime(stateName, 0.25f);
            }

            // Animator 参数（与 PVE TPS_Movement.controller 对应）
            if (!_isGrounded)
            {
                _speedBlend = _speedBlend; // 空中保持
            }
            else if (moving)
            {
                _speedBlend = isSprint ? 1f : aiming ? 0.5f : 0.66f;
            }
            else
            {
                _speedBlend = 0f;
            }
            if (_animator != null)
            {
                _animator.SetFloat(PlayerModel.SpeedHash, _speedBlend);
                _animator.SetBool(PlayerModel.IsGroundedHash, _isGrounded);
                _animator.SetBool(PlayerModel.IsSprintingHash, isSprint);
                _animator.SetFloat(PlayerModel.AimingXHash, moveInput.x);
                _animator.SetFloat(PlayerModel.AimingYHash, moveInput.y);
            }
        }
    }
}
