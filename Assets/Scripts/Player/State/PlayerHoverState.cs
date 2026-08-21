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

        // 斜抛：记录起跳水平初速度 = 当前移动方向 × 当前速度（替代旧 averageDeltaMovement 动画缓存惯性）。
        // 站立跳（无移动输入）水平为 0 → 竖直跳；跑动/冲刺跳 → 向前上方斜抛。
        Vector3 move = playerController != null ? playerController.worldMovement : Vector3.zero;
        move.y = 0f;
        float speed = playerModel.isSprinting ? playerModel.sprintSpeed : playerModel.jogSpeed;
        playerModel.jumpHorizontalVelocity = move.sqrMagnitude > 0.01f ? move.normalized * speed : Vector3.zero;
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
            // ⚠️ 仅在空中时才做水平微调：贴地后若仍调 cc.Move(水平, Y=0)，会把 isGrounded 刷成 false，
            // 导致下方落地检测失效 → 角色落地后卡在 Hover 悬停（bug 根因，见 08-19 诊断）
            // 叠加在 OnAnimatorMove 的跳跃惯性之上，可微调空中方向
            if (IsBeControl() && !playerModel.cc.isGrounded)
            {
                playerModel.cc.Move(playerController.worldMovement * airControlSpeed * Time.deltaTime);
            }
            #endregion
        }

        #region 检测角色是否落在地面上
        // 落地用 cc.isGrounded（接触检测）+ IsHover()（距离）双保险：
        // - cc.isGrounded：主检测（接触）
        // - !IsHover() 兜底（离地 < fallHeight 即视为落地），防水平 cc.Move 刷新 isGrounded 误判
        // ⚠️ 兜底必须限定在下落段（verticalSpeed <= 0）：起跳后尚未超过 fallHeight 的升空前几帧，
        //    !IsHover() 会误判"落地"→ 切回 Idle → verticalSpeed 被重置 → 跳不起来（52d271c 引入的回归，08-21 修复）
        // 起飞用 IsHover()（距离检测），落地主用 cc.isGrounded（接触）——不对称保留，但补距离兜底
        if (playerModel.cc.isGrounded || (playerModel.verticalSpeed <= 0f && !playerModel.IsHover()))
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
