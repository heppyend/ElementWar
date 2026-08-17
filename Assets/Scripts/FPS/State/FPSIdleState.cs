using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 待机状态：Speed→0（Locomotion 混合树 Idle 段），监听移动/冲刺/跳跃/滑铲/瞄准。
    /// </summary>
    public class FPSIdleState : FPSStateBase
    {
        public override void Enter()
        {
            base.Enter();
            model.PlayStateAnimation(FPSModel.ANIM_IDLE, 0.15f);
        }

        public override void Update()
        {
            base.Update(); // 重力 + 瞄准监听
            if (!model.cc.isGrounded) return; // 离地后交给 base 切 Air

            #region 状态监听
            if (controller.isJumping) { SwitchToAir(); return; }
            if (controller.isSlide) { model.SwitchState(FPSState.Slide); return; }
            if (controller.moveInput.magnitude > 0.1f)
            {
                model.SwitchState(controller.isSprint ? FPSState.Sprint : FPSState.Move);
                return;
            }
            #endregion

            // 静止：Speed 缓慢归零，清除水平速度
            model.LerpSpeedTo(FPSModel.IDLE_BLEND);
            model.isSprinting = false;
            model.horizontalVelocity = Vector3.zero;
            model.SetBoolParam(FPSAnimatorParams.IsSprintingHash, false);
            model.SetBoolParam(FPSAnimatorParams.IsGroundedHash, true);
        }
    }
}
