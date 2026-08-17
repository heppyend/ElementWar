using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 冲刺状态：Speed→Sprint（Locomotion 混合树 Sprint 段）。
    /// 松开冲刺或停止输入回 Move；可冲刺跳跃 / 冲刺滑铲。
    /// </summary>
    public class FPSSprintState : FPSStateBase
    {
        public override void Enter()
        {
            base.Enter();
            model.PlayStateAnimation(FPSModel.ANIM_LOCOMOTION, 0.15f);
        }

        public override void Update()
        {
            base.Update(); // 重力 + 瞄准监听
            if (!model.cc.isGrounded) return;

            float inputMag = controller.moveInput.magnitude;

            #region 状态监听
            if (controller.isJumping) { SwitchToAir(); return; }
            if (controller.isSlide) { model.SwitchState(FPSState.Slide); return; }
            if (!controller.isSprint || inputMag < 0.1f) { model.SwitchState(FPSState.Move); return; }
            #endregion

            // 冲刺移动：Speed 平滑升到 Sprint 段
            model.LerpSpeedTo(FPSModel.SPRINT_BLEND);
            model.isSprinting = true;
            model.horizontalVelocity = controller.worldMovement * model.GetMoveSpeed(model.speedBlend);
            FaceMovementDirection();
            model.SetBoolParam(FPSAnimatorParams.IsSprintingHash, true);
            model.SetBoolParam(FPSAnimatorParams.IsGroundedHash, true);
        }
    }
}
