using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 悬空状态
/// </summary>
public class PlayerHoverState : PlayerStateBase
{
    [Tooltip("空中水平控制速度（0 表示不可空中操控；旧 root motion 方案用此值，FPS 式用 PlayerModel.airControlSpeed）")]
    public float airControlSpeed = 3f;

    public override void Enter()
    {
        base.Enter();
        playerModel.PlayStateAnimation("Hover");
        // Hunter 跑酷包：进入跳跃时在 3 个跳跃片段（jmp_base_B / jmp_Move_left / jmp_BackAir）中随机选 1 个，
        // 对应 Hunter_Parkour.controller 的 HoverJump 混合树阈值 0/1/2。仅 randomJumpClips=true 的角色（Hunter）生效。
        if (playerModel.randomJumpClips)
            playerModel.SetFloatParam(PlayerModel.HoverClipHash, Random.Range(0, 3));
    }

    public override void Update()
    {
        base.Update();
        if (playerModel.useFPSMovement)
        {
            #region FPS 式空中水平控制（写 horizontalVelocity，由 PlayerModel.LateUpdate 位移；VerticalSpeed 驱动 Air 混合树）
            if (IsBeControl())
            {
                float inputMag = playerController.moveInput.magnitude;
                if (inputMag > 0.1f)
                {
                    playerModel.horizontalVelocity = Vector3.Lerp(
                        playerModel.horizontalVelocity,
                        playerController.worldMovement * playerModel.airControlSpeed,
                        Time.deltaTime * 4f);
                }
                else
                {
                    // 无输入：保留起跳动量并轻微空气阻力衰减（同 FPSAirState）
                    playerModel.horizontalVelocity = Vector3.Lerp(playerModel.horizontalVelocity, Vector3.zero, Time.deltaTime * 0.5f);
                }
            }
            playerModel.SetFloatParam(PlayerModel.VerticalSpeedHash, playerModel.verticalSpeed);
            playerModel.SetBoolParam(PlayerModel.IsGroundedHash, false);
            #endregion
        }
        else
        {
            #region 空中水平移动控制（旧 root motion 方案：直接 cc.Move）
            // 仅在玩家操控时生效，叠加在 OnAnimatorMove 的跳跃惯性之上，可微调空中方向
            if (IsBeControl())
            {
                playerModel.cc.Move(playerController.worldMovement * airControlSpeed * Time.deltaTime);
            }
            #endregion
        }

        #region 检测角色是否落在地面上
        // 落地用 cc.isGrounded（接触检测），起飞用 IsHover()（距离检测）
        // 二者不对称是有意为之：
        // - 起飞需要 fallHeight 阈值防止地面小颠簸误触发
        // - 落地需要 cc.Move() 的精确碰撞检测，SphereCast 在 CC 落地瞬间可能不可靠
        if (playerModel.cc.isGrounded)
        {
            playerModel.SwitchState(PlayerState.Idle);
        }
        // 人机（非主控）：CC 不调用 cc.Move → isGrounded 永不更新。
        // 此时用 NavMeshAgent 是否在网格上判定落地（agent 在地面则视为已着地，回 Idle 交给 Move 人机跟随逻辑）
        else if (!IsBeControl() && playerModel.navMeshAgent != null && playerModel.navMeshAgent.isOnNavMesh)
        {
            playerModel.SwitchState(PlayerState.Idle);
        }
        #endregion
    }
}
