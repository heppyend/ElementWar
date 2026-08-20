using UnityEngine;
using UnityEngine.Animations.Rigging;

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
        // ⚠️ 滑铲常量必须与服务器 GameWorldSettings 一致
        public const float SlideDuration = 0.8f;
        public const float SlideStartSpeed = 7f;
        public const float SlideEndSpeed = 1.5f;
        public const float SprintSlideBoost = 2f;
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
        private MultiAimConstraint[] _aimConstraints;   // 缓存：枪指准心 IK（PVP 无 PlayerController，需自行管理）
        private TwoBoneIKConstraint _hipIK;            // 缓存：髋部握枪 IK
        private float _aimIKWeight;                    // 当前瞄准 IK 权重（平滑过渡）
        private float _speedBlend;
        private int _animState = -1;   // 0=Idle 1=Move 2=Aiming 3=Hover 4=RunningSlide
        private float _groundY = GroundY;   // 每角色脚底偏移（Awake 按 CC 精确算）
        // 滑铲状态（边沿触发：isSlide 只在首个 30Hz 帧为 true，NetClient 发完即清锁存）
        private bool _isSliding;
        private float _slideTimer;
        private Vector3 _slideDirection = Vector3.forward;
        private bool _slideSprintBoost;

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
            _isSliding = false;   // 重生复位：避免死亡时正在滑铲，重生后残留继续滑（服务器 Respawn 同步复位）
            _slideTimer = 0f;
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
            if (_model.isDead) return; // 死亡冻结：停止位移/动画驱动，让死亡动画完整播放（否则每帧 CrossFade 把死亡动画打断）
            if (_animator == null) _animator = _model.animator; // 懒加载，避免 Awake 顺序问题

            float dt = Time.deltaTime;

            // 滑铲触发（边沿：isSlide 只在首个 30Hz 帧为 true，NetClient 发完即清锁存）。
            // 方向：优先移动输入方向，无输入沿用当前朝向（与服务器 SlideDirection 同一规则）
            if (!_isSliding && isSlide && _isGrounded)
            {
                _isSliding = true;
                _slideTimer = SlideDuration;
                Vector3 mv = new Vector3(worldMove.x, 0f, worldMove.z);
                _slideDirection = mv.sqrMagnitude > 0.01f ? mv.normalized : transform.forward;
                _slideDirection.y = 0f;
                _slideDirection.Normalize();
                _slideSprintBoost = isSprint;
            }

            // 速度选择
            Vector3 move = new Vector3(worldMove.x, 0f, worldMove.z);
            float mag = move.magnitude;
            Vector3 inputDir = mag > 0.01f ? move / mag : Vector3.zero;
            bool moving = mag > 0.01f;
            float speed = moving ? (isSprint ? SprintSpeed : isAiming ? AimMoveSpeed : JogSpeed) : 0f;
            bool aiming = isAiming || isFire;

            // 朝向先算（滑铲→滑铲方向；瞄准→瞬时面向相机；移动→平滑转向输入方向；待机保持当前朝向）。
            // ⚠️ 必须比位移先算：非瞄准时位移沿当前朝向走（换向成弧线，面朝=移动方向），
            //    否则换向/反方向时面朝（720°/s 平滑转向）追不上位移 → 倒车/侧跑 = 对方视角"乱跑"（08-21 诊断 166° 朝向差实证）。
            // ⚠️ 瞄准必须【瞬时】面向相机（PVE 同款）：用 720°/s 平滑会让手停下来后角色还惯性转到相机朝向
            //    （最长 0.5s），对方视角看"玩家在移动/打转"（08-21 用户反馈）。
            if (_isSliding)
            {
                float slideYaw = Mathf.Atan2(_slideDirection.x, _slideDirection.z) * Mathf.Rad2Deg;
                bodyYawDeg = Mathf.MoveTowardsAngle(bodyYawDeg, slideYaw, TurnSpeedDeg * dt);
            }
            else if (aiming)
            {
                Vector3 camF = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                camF.y = 0f;
                if (camF.sqrMagnitude > 0.01f)
                    bodyYawDeg = Mathf.Atan2(camF.x, camF.z) * Mathf.Rad2Deg;
            }
            else if (moving)
            {
                float targetYaw = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg;
                bodyYawDeg = Mathf.MoveTowardsAngle(bodyYawDeg, targetYaw, TurnSpeedDeg * dt);
            }
            // 待机（不瞄准不移动）：保持当前朝向（复刻 PVE，原地转视角不打转）
            transform.rotation = Quaternion.Euler(0f, bodyYawDeg, 0f);

            // 位移方向：瞄准沿输入（可侧移）；非瞄准沿当前朝向（面朝=移动方向，换向走弧线，不倒车/侧跑）
            Vector3 moveDir;
            if (_isSliding)
            {
                moveDir = _slideDirection;
            }
            else if (aiming)
            {
                moveDir = inputDir;
            }
            else
            {
                moveDir = new Vector3(Mathf.Sin(bodyYawDeg * Mathf.Deg2Rad), 0f, Mathf.Cos(bodyYawDeg * Mathf.Deg2Rad));
            }

            // 水平位移（transform 直接移动）
            Vector3 delta = moveDir * (speed * dt);
            Vector3 newPos = transform.position + delta;

            // 垂直：重力 + 跳跃（跳跃中断滑铲）
            if (_isGrounded && isJumping)
            {
                _verticalSpeed = JumpVelocity;
                _isGrounded = false;
                _isSliding = false;
            }
            else if (!_isGrounded)
            {
                _verticalSpeed += Gravity * dt;
                newPos = new Vector3(newPos.x, transform.position.y + _verticalSpeed * dt, newPos.z);
            }

            // 滑铲位移覆盖：方向锁定、速度衰减、强制贴地（与服务器 StepSlide 数学一致）
            if (_isSliding)
            {
                float t = _slideTimer / SlideDuration;   // 1→0
                float slideSpeed = Mathf.Lerp(SlideEndSpeed, SlideStartSpeed, t);
                if (_slideSprintBoost) slideSpeed += SprintSlideBoost * t;
                Vector3 sdelta = _slideDirection * (slideSpeed * dt);
                newPos = new Vector3(transform.position.x + sdelta.x, _groundY, transform.position.z + sdelta.z);
                _verticalSpeed = 0f;
                _isGrounded = true;
                _slideTimer -= dt;
                if (_slideTimer <= 0f) _isSliding = false;
            }

            if (newPos.y <= _groundY)
            {
                newPos = new Vector3(newPos.x, _groundY, newPos.z);
                _verticalSpeed = 0f;
                _isGrounded = true;
            }
            transform.position = newPos;
            _isGrounded = newPos.y <= _groundY + 0.001f;

            // 复刻 PVE 瞄准 IK：只有【瞄准/开火】时 MultiAim(枪指准心)=1，平时髋部握枪(TwoBoneIK)。
            // 每帧严格按状态设权重（缓存引用 + 平滑过渡），消除"移动时枪还指着准心"的状态残留。
            // （朝向已在位移前算好，见上；不再重复计算 bodyYawDeg/rotation）
            if (_aimConstraints == null)
            {
                _aimConstraints = _model.GetComponentsInChildren<MultiAimConstraint>(true);
                _hipIK = _model.GetComponentInChildren<TwoBoneIKConstraint>(true);
            }
            _aimIKWeight = Mathf.MoveTowards(_aimIKWeight, aiming ? 1f : 0f, 8f * dt);
            foreach (var c in _aimConstraints) c.weight = _aimIKWeight;
            if (_hipIK != null) _hipIK.weight = 1f - _aimIKWeight;

            // Animator 状态切换：PVE 由状态机 CrossFade 到 Idle/Move/Aiming/Hover，PvPMotor 复刻
            // （只设 Speed 参数不会切出 Idle 状态 → 角色平移不摆腿、射击不进瞄准姿势）
            int desired = _isSliding ? 4 : !_isGrounded ? 3 : aiming ? 2 : moving ? 1 : 0;
            if (desired != _animState)
            {
                _animState = desired;
                string stateName = desired == 4 ? "RunningSlide"
                    : desired == 3 ? "Hover"
                    : desired == 2 ? "Aiming"
                    : desired == 1 ? "Move"
                    : "Idle";
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
                _animator.SetFloat(PlayerModel.VerticalSpeedHash, _verticalSpeed);
                _animator.SetBool(PlayerModel.IsGroundedHash, _isGrounded);
                _animator.SetBool(PlayerModel.IsSprintingHash, isSprint);
                _animator.SetFloat(PlayerModel.AimingXHash, moveInput.x);
                _animator.SetFloat(PlayerModel.AimingYHash, moveInput.y);
            }
        }
    }
}
