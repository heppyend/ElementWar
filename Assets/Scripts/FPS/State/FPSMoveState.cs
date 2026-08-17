using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 移动状态：Speed→Jog（Locomotion 混合树 Jog 段），监听冲刺/跳跃/滑铲/瞄准。
    /// 冲刺通过切换至 FPSSprintState 实现（Speed 目标升至 Sprint 段），混合树保证衔接丝滑。
    /// </summary>
    public class FPSMoveState : FPSStateBase
    {
        public override void Enter()
        {
            base.Enter();
            model.PlayStateAnimation(FPSModel.ANIM_LOCOMOTION, 0.2f);
        }

        public override void Update()
        {
            base.Update(); // 重力 + 瞄准监听
            if (!model.cc.isGrounded) return;

            float inputMag = controller.moveInput.magnitude;

            #region 状态监听
            if (controller.isJumping) { SwitchToAir(); return; }
            if (controller.isSlide) { model.SwitchState(FPSState.Slide); return; }
            if (controller.isSprint) { model.SwitchState(FPSState.Sprint); return; }
            if (inputMag < 0.1f) { model.SwitchState(FPSState.Idle); return; }
            #endregion

            // 慢跑移动：Speed 平滑升到 Jog 段，水平速度由混合树速度换算
            model.LerpSpeedTo(FPSModel.JOG_BLEND);
            model.isSprinting = false;
            model.horizontalVelocity = controller.worldMovement * model.GetMoveSpeed(model.speedBlend);
            FaceMovementDirection();
            model.SetBoolParam(FPSAnimatorParams.IsSprintingHash, false);
            model.SetBoolParam(FPSAnimatorParams.IsGroundedHash, true);
        }
    }
}
