using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 玩家待机状态
/// </summary>
public class PlayerIdleState : PlayerStateBase
{
    public override void Enter()
    {
        base.Enter();
        playerModel.PlayStateAnimation("Idle");
    }

    public override void Update()
    {
        base.Update();

        // FPS 式移动的待机表现：Speed→0、清除水平速度（主控/人机通用）。
        // 先于下方状态切换执行，避免切到 Move 的那一帧把 Speed 反向拉回 0 造成抖动
        if (playerModel.useFPSMovement)
        {
            playerModel.LerpSpeedTo(PlayerModel.IDLE_BLEND);
            playerModel.isSprinting = false;
            playerModel.horizontalVelocity = Vector3.zero;
            playerModel.SetBoolParam(PlayerModel.IsSprintingHash, false);
            playerModel.SetBoolParam(PlayerModel.IsGroundedHash, true);
        }

        if (IsBeControl())
        {
            #region 移动状态监听
            if (playerController.moveInput.magnitude != 0)
            {
                // FPS 式：按住冲刺直接进 Sprint（Speed→Dash 段）；旧方案：进 Move（Move 内用 MoveBlend 混合）
                playerModel.SwitchState(playerModel.useFPSMovement && playerController.isSprint ? PlayerState.Sprint : PlayerState.Move);
            }
            #endregion

            #region 悬空状态监听
            if (playerController.isJumping)
                SwitchToHover();
            #endregion
        }
        //人机模式
        else
        {
            // 判断是否离自己的跟随目标（主控周围偏移点）过远，需要走动
            float distToTarget = Vector3.Distance(playerModel.transform.position, playerModel.GetFollowerTargetPosition());
            if (distToTarget > playerModel.stoppingDistance)
            {
                playerModel.SwitchState(PlayerState.Move);
            }
        }
    }

}
