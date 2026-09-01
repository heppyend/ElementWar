using UnityEngine;

namespace FPS
{
    /// <summary>
    /// FPS 状态基类（复用项目原有 StateBase / MonoManager 集中式 Update 机制）。
    /// 通用逻辑：重力 + 离地检测（稳定窗口）、瞄准输入监听、跳跃切换。
    /// 地面状态（Idle/Move/Sprint）调用 base.Update()；Air / Slide / Aim 各自覆盖 Update。
    /// </summary>
    public abstract class FPSStateBase : StateBase
    {
        protected FPSModel model;            // 状态宿主
        protected FPSController controller;  // 输入 + 相机（单例）

        public override void Init(IStateMachineOwner owner)
        {
            model = (FPSModel)owner;
            controller = FPSController.INSTANCE;
        }

        public override void Enter()
        {
            MonoManager.INSTANCE.AddUpdateAction(Update);
        }

        public override void Exit()
        {
            MonoManager.INSTANCE.RemoveUpdateAction(Update);
        }

        public override void Destory()
        {
        }

        public override void Update()
        {
            HandleGravity();
            HandleAimInput();
        }

        /// <summary>
        /// 重力计算（含斜坡稳定性缓冲，同 PlayerStateBase 思路）。
        /// 离地超过稳定窗口且距离超阈值 → 切 Air。
        /// </summary>
        protected void HandleGravity()
        {
            if (model.cc == null) return;

            if (!model.cc.isGrounded)
            {
                model.ungroundedFrameCount++;
                if (model.ungroundedFrameCount < FPSModel.HOVER_STABILITY_FRAMES)
                {
                    model.verticalSpeed = -2f; // 稳定窗口内维持贴地，过滤抖动
                }
                else
                {
                    model.verticalSpeed += model.gravity * Time.deltaTime;
                    if (model.IsHover())
                        model.SwitchState(FPSState.Air);
                }
            }
            else
            {
                model.ungroundedFrameCount = 0;
                model.verticalSpeed = -2f;
            }
        }

        /// <summary>
        /// 瞄准输入监听：任意地面/空中状态在瞄准或开火时进入瞄准状态。
        /// Aim 状态自身覆盖为空，避免自我触发（StateMachine 有防重入兜底）。
        /// </summary>
        protected virtual void HandleAimInput()
        {
            if (controller == null) return;
            if (controller.isAiming || controller.isFire)
                model.SwitchState(FPSState.Aim);
        }

        /// <summary>
        /// 主动跳跃：计算初速度，跳过稳定性延迟，直接切 Air。
        /// </summary>
        protected void SwitchToAir()
        {
            model.verticalSpeed = Mathf.Sqrt(-2f * model.gravity * model.jumpHeight);
            model.ungroundedFrameCount = FPSModel.HOVER_STABILITY_FRAMES;
            model.SwitchState(FPSState.Air);
        }

        /// <summary>面朝移动方向（地面移动状态用）</summary>
        protected void FaceMovementDirection()
        {
            if (controller == null) return;
            model.FaceDirection(controller.worldMovement);
        }
    }
}
