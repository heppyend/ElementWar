using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冲刺状态（仅 FPS 式移动 useFPSMovement=true 时使用）。
/// Speed→Sprint（Locomotion 混合树 Dash 段），水平位移代码驱动（horizontalVelocity → LateUpdate 的 cc.Move）。
/// 松开冲刺或停止输入回 Move；可冲刺跳跃 / 冲刺滑铲。
/// </summary>
public class PlayerSprintState : PlayerStateBase
{
    public override void Enter()
    {
        base.Enter();
        // CrossFade 到 Move 状态（Locomotion 混合树），Speed→1 自动走 Dash 段
        playerModel.PlayStateAnimation("Move", 0.15f);
    }

    public override void Update()
    {
        base.Update();
        if (IsBeControl())
        {
            #region 悬空状态监听
            if (playerController.isJumping)
            {
                SwitchToHover();
                return;
            }
            #endregion

            #region 滑铲状态监听（冲刺中按 C）
            if (playerController.isSlide)
            {
                playerModel.SwitchState(PlayerState.Slide);
                return;
            }
            #endregion

            #region 退出冲刺监听（松开冲刺或停止输入 → 回 Move）
            if (!playerController.isSprint || playerController.moveInput.magnitude < 0.1f)
            {
                playerModel.SwitchState(PlayerState.Move);
                return;
            }
            #endregion

            #region FPS 式冲刺移动
            playerModel.LerpSpeedTo(PlayerModel.SPRINT_BLEND);
            playerModel.isSprinting = true;
            playerModel.horizontalVelocity = playerController.worldMovement * playerModel.GetMoveSpeed(playerModel.speedBlend);
            playerModel.SetBoolParam(PlayerModel.IsSprintingHash, true);
            playerModel.SetBoolParam(PlayerModel.IsGroundedHash, true);
            #endregion

            #region 处理方向（与 Move 共用）
            float rad = Mathf.Atan2(playerController.localMovement.x, playerController.localMovement.z);
            playerModel.transform.Rotate(0, rad * playerController.rotationSpeed * Time.deltaTime, 0);
            #endregion
        }
        else
        {
            // 人机不会进入 Sprint（Idle/Move 人机分支直接跟随），防御性回 Move 让人机逻辑接管
            playerModel.SwitchState(PlayerState.Move);
        }
    }
}
