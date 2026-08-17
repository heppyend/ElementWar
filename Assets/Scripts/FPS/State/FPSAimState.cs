using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 瞄准状态：调度 腰射/肩射/开镜 三种模式（见 Aim 文件夹）。
    /// 同时处理：相机跟随、瞄准射线、瞄准移动、开火入口、Animator 瞄准参数。
    /// 框架就绪：后续补枪械动画/武器系统后，模式内直接生效。
    /// </summary>
    public class FPSAimState : FPSStateBase
    {
        private FPSAimMode currentMode;
        private FPSAimModeBase modeInstance;

        private FPSHipFireMode hipFireMode;
        private FPSShoulderMode shoulderMode;
        private FPSAdsMode adsMode;

        public override void Init(IStateMachineOwner owner)
        {
            base.Init(owner);
            // 三种模式实例缓存复用（无状态实例化开销）
            hipFireMode = new FPSHipFireMode();
            shoulderMode = new FPSShoulderMode();
            adsMode = new FPSAdsMode();
        }

        public override void Enter()
        {
            base.Enter();
            currentMode = ResolveMode();
            modeInstance = GetMode(currentMode);
            modeInstance.Init(model, controller);
            modeInstance.Enter();

            model.SetBoolParam(FPSAnimatorParams.IsAimingHash, true);
            model.SetFloatParam(FPSAnimatorParams.AimModeHash, (int)currentMode);
        }

        public override void Update()
        {
            // 重力累积（不切 Air：瞄准中保持下落姿态，退出瞄准后自然进入 Air）
            HandleAimGravity();

            #region 瞄准模式切换
            FPSAimMode desired = ResolveMode();
            if (desired != currentMode)
            {
                modeInstance.Exit();
                currentMode = desired;
                modeInstance = GetMode(desired);
                // 关键：新模式实例必须重新 Init（绑定 model/controller），否则 Enter 里访问 controller 会 NRE
                modeInstance.Init(model, controller);
                modeInstance.Enter();
                model.SetFloatParam(FPSAnimatorParams.AimModeHash, (int)currentMode);
            }
            #endregion

            #region 退出瞄准监听
            if (!controller.isAiming && !controller.isFire)
            {
                model.SwitchState(ResolveExitState());
                return;
            }
            #endregion

            // 模式行为（相机/FOV 等）
            modeInstance.Update();

            // 模型快速回正到相机朝向
            model.FaceCameraYaw();

            // 瞄准时：站定（Speed→Idle 段），仅在真实输入时极慢微移。
            // 关键：不要 LerpSpeedTo(WALK_BLEND)——那样会把混合树切到走路段但无动画推进 → 卡在走路前 12 帧 + 位移。
            // 用角色自身前向移动（而非 cameraForward），避免相机 blend 期间 worldMovement 方向突变。
            float inputMag = controller.moveInput.magnitude;
            if (inputMag > 0.01f)
            {
                Vector3 moveDir = controller.moveInput.y * model.transform.forward
                                + controller.moveInput.x * model.transform.right;
                model.horizontalVelocity = moveDir.normalized * controller.aimMoveSpeed;
                model.LerpSpeedTo(FPSModel.WALK_BLEND); // 有输入才微移，动画走到走路段（缓慢推进，正常）
            }
            else
            {
                model.horizontalVelocity = Vector3.zero; // 无输入：彻底站定，不触发走路动画
                model.LerpSpeedTo(FPSModel.IDLE_BLEND);
            }

            // 屏幕中心瞄准目标
            controller.UpdateAimTarget();

            // 开火入口（武器系统接入后在此触发开火）
            if (controller.isFire)
            {
                // TODO: 武器开火（射速/后坐力/音效）—— 后续接入武器系统 + KINEMATION 后坐力
            }

            // 瞄准混合参数（后续瞄准混合树用）
            model.SetFloatParam(FPSAnimatorParams.AimingXHash, controller.moveInput.x);
            model.SetFloatParam(FPSAnimatorParams.AimingYHash, controller.moveInput.y);
            model.SetBoolParam(FPSAnimatorParams.IsGroundedHash, model.cc.isGrounded);
        }

        public override void Exit()
        {
            base.Exit();
            modeInstance?.Exit();
            model.SetBoolParam(FPSAnimatorParams.IsAimingHash, false);
            model.SetFloatParam(FPSAnimatorParams.AimModeHash, 0f);
        }

        /// <summary>依据输入决定瞄准模式：开镜 &gt; 肩射 &gt; 腰射（仅开火）</summary>
        private FPSAimMode ResolveMode()
        {
            if (controller.isAds) return FPSAimMode.Ads;
            if (controller.isAiming) return FPSAimMode.Shoulder;
            return FPSAimMode.HipFire;
        }

        private FPSAimModeBase GetMode(FPSAimMode mode)
        {
            switch (mode)
            {
                case FPSAimMode.Ads: return adsMode;
                case FPSAimMode.Shoulder: return shoulderMode;
                default: return hipFireMode;
            }
        }

        /// <summary>退出瞄准后的去向：空中→Air，地面按输入回 Move/Sprint/Idle</summary>
        private FPSState ResolveExitState()
        {
            if (!model.cc.isGrounded) return FPSState.Air;
            if (controller.moveInput.magnitude > 0.1f)
                return controller.isSprint ? FPSState.Sprint : FPSState.Move;
            return FPSState.Idle;
        }

        /// <summary>瞄准期间的重力：只累积，不切 Air（空中瞄准不被打断）</summary>
        private void HandleAimGravity()
        {
            if (model.cc == null) return;
            if (!model.cc.isGrounded)
            {
                model.ungroundedFrameCount++;
                if (model.ungroundedFrameCount < FPSModel.HOVER_STABILITY_FRAMES)
                    model.verticalSpeed = -2f;
                else
                    model.verticalSpeed += model.gravity * Time.deltaTime;
            }
            else
            {
                model.ungroundedFrameCount = 0;
                model.verticalSpeed = -2f;
            }
        }
    }
}
