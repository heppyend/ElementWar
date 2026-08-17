using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 滑铲状态
/// 由移动状态按 C 键触发。滑铲的横向位移由本状态自控（朝玩家主视角），
/// Running Slide 动画仅作为"姿态"（腿部滑铲姿势）使用，其根位移被 PlayerModel.OnAnimatorMove 屏蔽。
/// 可与奔跑/行走/跳跃自由衔接：
/// - 按跳跃 → 切 Hover（跳跃中断）
/// - 滑铲计时结束 → 有移动输入回 Move，否则回 Idle
/// - 滑铲中按右键 → 切 Aiming（滑铲中可切瞄准）
/// </summary>
public class PlayerSlideState : PlayerStateBase
{
    // 滑铲参数集中在 PlayerModel（Inspector 可调）：
    // playerModel.slideDuration / slideStartSpeed / slideEndSpeed / sprintSlideBoost

    private float slideTimer;//滑铲剩余时间
    private Vector3 slideDirection;//滑铲方向（朝主视角）

    public override void Enter()
    {
        base.Enter();
        playerModel.PlayStateAnimation("RunningSlide", 0.15f);

        // 滑铲方向：优先取相机-输入方向（朝主视角），否则沿用当前朝向
        // 若相机方向与角色朝向相差过大（诊断值 -X 说明相机 forward 异常），则回退到角色当前朝向
        Vector3 move = playerController.worldMovement;
        slideDirection = move.sqrMagnitude > 0.01f ? move.normalized : playerModel.transform.forward;
        slideDirection.y = 0;
        slideDirection.Normalize();

        // 校验：相机-输入方向与角色朝向夹角 > 60° 时，判定方向异常，改用角色朝向
        if (move.sqrMagnitude > 0.01f)
        {
            Vector3 forward = playerModel.transform.forward;
            forward.y = 0;
            forward.Normalize();
            if (Vector3.Angle(slideDirection, forward) > 60f)
            {
                slideDirection = forward;
            }
        }

        slideTimer = playerModel.slideDuration;
        // 强制贴地：锁定垂直速度，避免动画根位移造成"空中飞踢"
        playerModel.verticalSpeed = -2f;
        playerModel.ungroundedFrameCount = 0;
    }

    public override void Update()
    {
        // 不调用基类重力/悬空切换（滑铲强制贴地），只保留瞄准监听（滑铲中可切瞄准）
        if (IsBeControl() && (playerController.isAiming || playerController.isFire))
        {
            playerModel.SwitchState(PlayerState.Aiming);
            return;
        }

        // 强制贴地
        playerModel.verticalSpeed = -2f;
        playerModel.ungroundedFrameCount = 0;

        if (IsBeControl())
        {
            #region 跳跃中断滑铲
            if (playerController.isJumping)
            {
                SwitchToHover();
                return;
            }
            #endregion
        }

        #region 滑铲中离开地面（如滑出平台边缘）→ 正常下落
        if (!playerModel.cc.isGrounded && playerModel.IsHover())
        {
            playerModel.SwitchState(PlayerState.Hover);
            return;
        }
        #endregion

        #region 计时结束 → 回 Move / Idle
        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0)
        {
            ExitSlide();
            return;
        }
        #endregion

        #region 水平滑铲位移（自控方向 + 速度衰减）
        float t = slideTimer / playerModel.slideDuration;
        float speed = Mathf.Lerp(playerModel.slideEndSpeed, playerModel.slideStartSpeed, t);
        // 冲刺时滑铲更有力
        if (playerController.isSprint)
            speed += playerModel.sprintSlideBoost * t;
        if (playerModel.useFPSMovement)
        {
            // FPS 式：写 horizontalVelocity，由 PlayerModel.LateUpdate 统一位移（等效于直接 cc.Move）
            playerModel.horizontalVelocity = slideDirection * speed;
        }
        else
        {
            playerModel.cc.Move(slideDirection * speed * Time.deltaTime);
        }
        #endregion
    }

    /// <summary>
    /// 滑铲结束，回到移动或待机
    /// </summary>
    private void ExitSlide()
    {
        if (IsBeControl() && playerController.moveInput.magnitude > 0.1f)
            playerModel.SwitchState(PlayerState.Move);
        else
            playerModel.SwitchState(PlayerState.Idle);
    }
}
