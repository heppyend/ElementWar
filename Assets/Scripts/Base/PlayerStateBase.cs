using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 玩家状态基类
/// </summary>
public class PlayerStateBase : StateBase
{
    protected PlayerController playerController;
    protected PlayerModel playerModel;//当前状态的角色模型


    public override void Init(IStateMachineOwner owner)
    {
        playerController = PlayerController.INSTANCE;//单例模式,所以可以直接点出
        playerModel=(PlayerModel)owner;
    }
    public override void Destory()
    {
        
    }

    public override void Enter()
    {
        MonoManager.INSTANCE.AddUpdateAction(Update);
    }

    public override void Exit()
    {
        MonoManager.INSTANCE.RemoveUpdateAction(Update);

    }

    public override void Update()
    {
        #region 重力计算（含斜坡稳定性缓冲）—— 仅主控执行
        // 人机（非主控）位移/落地由 NavMeshAgent 全权驱动，cc.isGrounded 不更新；
        // 若走此逻辑会误判悬空卡在 Hover。因此人机跳过重力，且不 return（各状态的 else 人机分支在 base.Update 之后执行）
        if (IsBeControl())
        {
            if (!playerModel.cc.isGrounded)//模型不在地面
            {
                playerModel.ungroundedFrameCount++;

                // 稳定性窗口期内：不累积重力，维持贴地速度，过滤 isGrounded 瞬时抖动
                if (playerModel.ungroundedFrameCount < PlayerModel.HOVER_STABILITY_FRAMES)
                {
                    playerModel.verticalSpeed = -2f;
                }
                else//超过稳定性窗口 → 真离地，施加重力
                {
                    playerModel.verticalSpeed += playerModel.gravity * Time.deltaTime;

                    // 离地距离超过 fallHeight 阈值 → 切换悬空状态
                    if (playerModel.IsHover())
                        playerModel.SwitchState(PlayerState.Hover);
                }
            }
            else//模型在地面
            {
                playerModel.ungroundedFrameCount = 0;
                playerModel.verticalSpeed = -2f;//重置垂直速度（保持小值贴地）
            }
        }
        #endregion

        #region 瞄准状态监听（仅主控）
        if (IsBeControl()&&(playerController.isAiming||playerController.isFire))
        {
            playerModel.SwitchState(PlayerState.Aiming);
        }
        #endregion
    }

    /// <summary>
    /// 当前模型是否被玩家所控制
    /// </summary>
    /// <returns></returns>
    public bool IsBeControl()
    {
        return playerModel == playerController.currentPlayerModel;
    }


    /// <summary>
    /// 切换到跳跃状态
    /// </summary>
    public void SwitchToHover() {
    //计算跳跃初速度
    playerModel.verticalSpeed = Mathf.Sqrt(-2 * playerModel.gravity * playerModel.jumpHeight);
    //主动跳跃跳过稳定性延迟，直接进入重力计算
    playerModel.ungroundedFrameCount = PlayerModel.HOVER_STABILITY_FRAMES;
    //切换到悬空状态
    playerModel.SwitchState(PlayerState.Hover);
    }
}
