using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 滑铲状态：横向位移自控方向（朝主视角）+ 速度衰减，姿态用 Slide 动画。
    /// 可被跳跃/瞄准中断，滑出边缘正常下落，计时结束按输入回 Move/Sprint/Idle。
    /// </summary>
    public class FPSSlideState : FPSStateBase
    {
        private float slideTimer;
        private Vector3 slideDirection;

        public override void Enter()
        {
            base.Enter();
            model.PlayStateAnimation(FPSModel.ANIM_SLIDE, 0.12f);

            // 滑铲方向：优先相机-输入方向，否则沿用当前朝向
            Vector3 move = controller.worldMovement;
            slideDirection = move.sqrMagnitude > 0.01f ? move.normalized : model.transform.forward;
            slideDirection.y = 0f;
            slideDirection.Normalize();

            slideTimer = model.slideDuration;
            // 强制贴地
            model.verticalSpeed = -2f;
            model.ungroundedFrameCount = 0;
            model.isSprinting = false;
            model.SetBoolParam(FPSAnimatorParams.IsSprintingHash, false);
        }

        public override void Update()
        {
            // 强制贴地（滑铲不进入重力逻辑）
            model.verticalSpeed = -2f;
            model.ungroundedFrameCount = 0;

            // 滑铲中可切瞄准
            HandleAimInput();

            #region 跳跃中断滑铲
            if (model.cc.isGrounded && controller.isJumping) { SwitchToAir(); return; }
            #endregion

            #region 滑出平台边缘 → 正常下落
            if (!model.cc.isGrounded && model.IsHover()) { model.SwitchState(FPSState.Air); return; }
            #endregion

            #region 计时结束 → 回移动/待机
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                if (controller.moveInput.magnitude > 0.1f)
                    model.SwitchState(controller.isSprint ? FPSState.Sprint : FPSState.Move);
                else
                    model.SwitchState(FPSState.Idle);
                return;
            }
            #endregion

            #region 水平滑铲位移（速度衰减）
            float t = slideTimer / model.slideDuration;
            float speed = Mathf.Lerp(model.slideEndSpeed, model.slideStartSpeed, t);
            if (controller.isSprint) speed += model.sprintSlideBoost * t;
            model.horizontalVelocity = slideDirection * speed;
            #endregion
        }
    }
}
