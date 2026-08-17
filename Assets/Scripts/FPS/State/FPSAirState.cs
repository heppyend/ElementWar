using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 空中状态（跳跃/下落）：VerticalSpeed 驱动 Jump_Up/Jump_Down 混合，可空中转向控制。
    /// 落地用 cc.isGrounded（接触检测），按输入回 Move/Sprint/Idle。
    /// </summary>
    public class FPSAirState : FPSStateBase
    {
        private Vector3 momentum; // 起跳时继承的水平动量（空中无输入时保留）

        public override void Enter()
        {
            base.Enter();
            model.PlayStateAnimation(FPSModel.ANIM_AIR, 0.1f);
            // 继承起跳瞬间的水平速度作为空中动量
            momentum = model.horizontalVelocity;
        }

        public override void Update()
        {
            // 不调用 base.Update()：空中重力自行累积，落地判定用 cc.isGrounded
            model.verticalSpeed += model.gravity * Time.deltaTime;

            #region 空中水平控制 + 动量保持
            // 有输入 → 朝输入方向转向操控；无输入 → 保留动量并轻微空气阻力
            float inputMag = controller.moveInput.magnitude;
            if (inputMag > 0.1f)
            {
                momentum = Vector3.Lerp(momentum, controller.worldMovement * model.airControlSpeed, Time.deltaTime * 4f);
            }
            else
            {
                momentum = Vector3.Lerp(momentum, Vector3.zero, Time.deltaTime * 0.5f);
            }
            model.horizontalVelocity = momentum;
            #endregion

            // 保持 Speed 混合（落地回到 Locomotion 时平滑；冲刺跳跃保持冲刺姿态）
            model.LerpSpeedTo(controller.isSprint ? FPSModel.SPRINT_BLEND : FPSModel.JOG_BLEND);
            model.SetFloatParam(FPSAnimatorParams.VerticalSpeedHash, model.verticalSpeed);
            model.SetBoolParam(FPSAnimatorParams.IsGroundedHash, false);

            // 瞄准监听（空中可瞄准，落地后保持瞄准状态）
            HandleAimInput();

            // 落地
            if (model.cc.isGrounded)
            {
                model.verticalSpeed = -2f;
                if (controller.moveInput.magnitude > 0.1f)
                    model.SwitchState(controller.isSprint ? FPSState.Sprint : FPSState.Move);
                else
                    model.SwitchState(FPSState.Idle);
            }
        }
    }
}
