using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// FPS 角色模型（宿主）。
    /// 复用项目原有状态机框架：StateMachine + StateBase + MonoManager。
    /// 职责：持有 CharacterController / Animator，驱动状态机，统一移动（OnAnimatorMove）+ 重力 + 地面检测。
    /// 移动采用"手动移动"：各状态每帧写入 horizontalVelocity 与 verticalSpeed，OnAnimatorMove 统一 Move。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FPSModel : MonoBehaviour, IStateMachineOwner
    {
        [Header("组件")]
        public Animator animator;
        public CharacterController cc;

        [Header("Locomotion 混合树速度（Inspector 可调）")]
        [Tooltip("行走速度")]
        public float walkSpeed = 2.2f;
        [Tooltip("移动(慢跑)速度")]
        public float jogSpeed = 5f;
        [Tooltip("冲刺速度")]
        public float sprintSpeed = 8f;
        [Tooltip("Speed 参数变化速率（越大越灵敏）")]
        public float speedLerpSpeed = 4f;

        [Header("重力 / 跳跃")]
        [Tooltip("重力加速度")]
        public float gravity = -20f;
        [Tooltip("跳跃高度")]
        public float jumpHeight = 1.2f;
        [Tooltip("离地距离超过该阈值判定悬空")]
        public float fallHeight = 0.2f;
        [Tooltip("空中水平控制速度")]
        public float airControlSpeed = 6f;

        [Header("旋转")]
        [Tooltip("旋转速度（度/秒）")]
        public float rotationSpeed = 540f;

        [Header("滑铲")]
        [Tooltip("滑铲持续时间")]
        public float slideDuration = 0.8f;
        [Tooltip("滑铲初速度")]
        public float slideStartSpeed = 9f;
        [Tooltip("滑铲末速度")]
        public float slideEndSpeed = 2.5f;
        [Tooltip("冲刺时滑铲速度加成")]
        public float sprintSlideBoost = 2f;

        [Header("地面检测")]
        [Tooltip("地面检测层级（IsHover 球体投射用）")]
        public LayerMask groundLayerMask = ~0;

        /// <summary>混合树关键速度节点（与 Animator Locomotion 树阈值对应）</summary>
        public const float IDLE_BLEND = 0f;
        public const float WALK_BLEND = 0.33f;
        public const float JOG_BLEND = 0.66f;
        public const float SPRINT_BLEND = 1f;

        /// <summary>连续离地帧数稳定性窗口（防斜坡抖动，同 PlayerModel.HOVER_STABILITY_FRAMES 思路）</summary>
        public const int HOVER_STABILITY_FRAMES = 5;

        /// <summary>Animator 状态名（生成器 / 状态类共用）</summary>
        public const string ANIM_IDLE = "Idle";
        public const string ANIM_LOCOMOTION = "Locomotion";
        public const string ANIM_AIR = "Air";
        public const string ANIM_SLIDE = "Slide";
        public const string ANIM_AIM = "Aim";

        // —— 运行时状态（由各状态类读写）——
        [HideInInspector] public float speedBlend;              // Locomotion 混合树 Speed 参数当前值
        [HideInInspector] public Vector3 horizontalVelocity;    // 水平移动速度（状态类每帧写入）
        [HideInInspector] public float verticalSpeed;           // 垂直速度（重力/跳跃）
        [HideInInspector] public int ungroundedFrameCount;      // 连续离地帧数
        [HideInInspector] public bool isSprinting;              // 是否冲刺

        private StateMachine stateMachine;
        private FPSState currentState;

        private void Awake()
        {
            stateMachine = new StateMachine(this);
            if (animator == null) animator = GetComponent<Animator>();
            if (cc == null) cc = GetComponent<CharacterController>();
            // 关键：关闭 Apply Root Motion。
            // 否则 Animator root motion 会绕过 CharacterController 直接写 transform → 穿模 + 位移不受控 + 相机抖。
            // 移动完全由代码控制（horizontalVelocity → cc.Move），动画只负责表现。
            if (animator != null) animator.applyRootMotion = false;
        }

        private void Start()
        {
            // 保证状态机的集中式 Update 有宿主（无 MonoManager 时自动创建，同 EffectPool 自动建单例的约定）
            if (MonoManager.INSTANCE == null)
            {
                var go = new GameObject("MonoManager");
                go.AddComponent<MonoManager>();
            }
            if (FPSController.INSTANCE == null)
            {
                // 自动补挂，避免状态类访问 controller 空引用；
                // 但相机槽位需在 Inspector 手动拖入（见开发文档阶段 5）
                Debug.LogWarning("[FPSModel] 未找到 FPSController，已自动补挂。请把 freeLookCamera/aimingCamera/AimTarget 拖入其槽位。", this);
                gameObject.AddComponent<FPSController>();
            }
            SwitchState(FPSState.Idle);
        }

        private void LateUpdate()
        {
            // 统一移动：水平来自各状态写入的 horizontalVelocity，垂直来自 verticalSpeed。
            // 用 LateUpdate 而非 Update：确保状态类（经 MonoManager 集中式 Update，通常早于本帧）
            // 先写入 horizontalVelocity，本 LateUpdate 再 Move——避免"先移动后写值"导致位移被吞（原地移动）。
            // 位移完全走 CharacterController 碰撞，杜绝 root motion 绕过 CC 导致的穿模/位移不受控。
            if (cc == null) return;

            // 面向移动方向（applyRootMotion=false 后 Animator 不再自动转向，这里兜底）：
            // 有水平移动时让角色平滑转向移动方向，保证"角色随移动/相机转向"。
            Vector3 hDir = horizontalVelocity;
            hDir.y = 0f;
            if (hDir.sqrMagnitude > 0.001f)
                FaceDirection(hDir);

            Vector3 delta = horizontalVelocity * Time.deltaTime;
            delta.y = verticalSpeed * Time.deltaTime;
            cc.Move(delta);
        }

        /// <summary>切换状态（分发到 StateMachine）</summary>
        public void SwitchState(FPSState state)
        {
            switch (state)
            {
                case FPSState.Idle: stateMachine.EnterState<FPSIdleState>(); break;
                case FPSState.Move: stateMachine.EnterState<FPSMoveState>(); break;
                case FPSState.Sprint: stateMachine.EnterState<FPSSprintState>(); break;
                case FPSState.Air: stateMachine.EnterState<FPSAirState>(); break;
                case FPSState.Slide: stateMachine.EnterState<FPSSlideState>(); break;
                case FPSState.Aim: stateMachine.EnterState<FPSAimState>(); break;
            }
            currentState = state;
        }

        /// <summary>播放动画（CrossFade 到 Animator 状态）</summary>
        public void PlayStateAnimation(string animationName, float transition = 0.2f, int layer = 0)
        {
            if (animator == null) return;
            animator.CrossFadeInFixedTime(animationName, transition, layer);
        }

        /// <summary>Locomotion Speed 参数按速率平滑逼近目标值</summary>
        public void LerpSpeedTo(float target)
        {
            speedBlend = Mathf.MoveTowards(speedBlend, target, speedLerpSpeed * Time.deltaTime);
            SetSpeed(speedBlend);
        }

        /// <summary>立即设置 Speed 参数</summary>
        public void SetSpeed(float value)
        {
            speedBlend = value;
            if (animator != null) animator.SetFloat(FPSAnimatorParams.SpeedHash, speedBlend);
        }

        /// <summary>把混合树 Speed 值映射为世界移动速度（与 Locomotion 树阈值线性对应）</summary>
        public float GetMoveSpeed(float blend)
        {
            if (blend <= WALK_BLEND)
                return Mathf.Lerp(0f, walkSpeed, blend / WALK_BLEND);
            if (blend <= JOG_BLEND)
                return Mathf.Lerp(walkSpeed, jogSpeed, (blend - WALK_BLEND) / (JOG_BLEND - WALK_BLEND));
            return Mathf.Lerp(jogSpeed, sprintSpeed, (blend - JOG_BLEND) / (SPRINT_BLEND - JOG_BLEND));
        }

        // —— 动画参数便捷写入 ——
        public void SetBoolParam(int hash, bool value) { if (animator != null) animator.SetBool(hash, value); }
        public void SetFloatParam(int hash, float value) { if (animator != null) animator.SetFloat(hash, value); }

        /// <summary>是否悬空：从 CC 真实底部用球形投射检测离地距离（同 PlayerModel.IsHover 思路）</summary>
        public bool IsHover()
        {
            if (cc == null) return false;
            float ccBottom = transform.position.y + cc.center.y - cc.height * 0.5f + cc.skinWidth;
            Vector3 bottomPoint = new Vector3(transform.position.x, ccBottom, transform.position.z);
            float sphereRadius = cc.radius * 0.6f;
            float castDistance = fallHeight + cc.skinWidth;
            return !Physics.SphereCast(
                bottomPoint + Vector3.up * sphereRadius,
                sphereRadius,
                Vector3.down,
                out _,
                castDistance,
                groundLayerMask);
        }

        /// <summary>面朝世界方向（水平面，角速度限制）</summary>
        public void FaceDirection(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            dir.y = 0f;
            dir.Normalize();
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }

        /// <summary>面朝相机朝向（瞄准用，温和回正，避免把角色拽得东倒西歪）</summary>
        public void FaceCameraYaw()
        {
            if (FPSController.INSTANCE == null) return;
            float yaw = FPSController.INSTANCE.cameraYaw;
            Quaternion target = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }
    }
}
