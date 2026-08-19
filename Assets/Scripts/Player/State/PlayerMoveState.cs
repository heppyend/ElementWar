using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// 移动状态
/// </summary>
public class PlayerMoveState : PlayerStateBase
{

    #region 动画器相关
    private int moveBlendHash;//属性
    private float moveBlend;//参数
    private float runThreshold=0;//奔跑阈值
    private float sprintThreshold=1;//冲刺阈值
    private float transitionSpeed = 5;//过渡速度
    #endregion

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);
        moveBlendHash = Animator.StringToHash("MoveBlend");
    }



    public override void Enter()
    {
        base.Enter();

        playerModel.PlayStateAnimation("Move");
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

            #region 待机状态监听
            if (playerController.moveInput.magnitude == 0)
            {
                playerModel.SwitchState(PlayerState.Idle);
                return;
            }
            #endregion

            #region 滑铲状态监听（移动中按 C 触发）
            if (playerController.isSlide)
            {
                playerModel.SwitchState(PlayerState.Slide);
                return;
            }
            #endregion

            if (playerModel.useFPSMovement)
            {
                #region 冲刺状态监听（FPS 式：冲刺是独立状态，走 Locomotion 树 Dash 段）
                if (playerController.isSprint)
                {
                    playerModel.SwitchState(PlayerState.Sprint);
                    return;
                }
                #endregion

                #region FPS 式慢跑移动（代码驱动 horizontalVelocity，由 PlayerModel.LateUpdate 的 cc.Move 位移）
                playerModel.LerpSpeedTo(PlayerModel.JOG_BLEND);
                playerModel.isSprinting = false;
                playerModel.horizontalVelocity = playerController.worldMovement * playerModel.GetMoveSpeed(playerModel.speedBlend);
                playerModel.SetBoolParam(PlayerModel.IsSprintingHash, false);
                playerModel.SetBoolParam(PlayerModel.IsGroundedHash, true);
                #endregion
            }
            else
            {
                #region 处理移动速度（旧 root motion 方案）
                if (playerController.isSprint)
                {
                    moveBlend=Mathf.Lerp(moveBlend,sprintThreshold,transitionSpeed*Time.deltaTime);
                }
                else
                {
                    moveBlend=Mathf.Lerp(moveBlend,runThreshold,transitionSpeed*Time.deltaTime);

                }
                playerModel.animator.SetFloat(moveBlendHash, moveBlend);
                #endregion

                #region 冲刺代码位移（旧 root motion 方案：MoveBlend→1 播跑酷 Mvm_Dash 原地动画无根运动，位移由 horizontalVelocity 驱动，OnAnimatorMove 消费）
                playerModel.isSprinting = playerController.isSprint;
                playerModel.horizontalVelocity = playerController.isSprint
                    ? playerController.worldMovement * playerModel.sprintSpeed
                    : Vector3.zero;
                #endregion
            }

            #region 处理方向（两方案共用）
            //计算本地空间移动方向与模型正前方之间的夹角
            float rad=Mathf.Atan2(playerController.localMovement.x,playerController.localMovement.z);
            //旋转到移动方向
            playerModel.transform.Rotate(0, rad * playerController.rotationSpeed * Time.deltaTime, 0);
            #endregion
        }
        //人机模式
        else
        {
            #region 自动跟随玩家（目标 = 主控周围的随从专属偏移点，错开成队列避免挤在一起）
            Vector3 target = playerModel.GetFollowerTargetPosition();
            float distToTarget = Vector3.Distance(playerModel.transform.position, target);
            if (distToTarget <= playerModel.stoppingDistance) {
                playerModel.SwitchState(PlayerState.Idle);
                return;
            }
            // NavMeshAgent 刚启用（enabled=true 的下一帧才真正落到网格）或不在烘焙网格上时
            // SetDestination 会抛 "active agent / placed on NavMesh" 错误，先校验再调用
            if (playerModel.navMeshAgent != null && playerModel.navMeshAgent.isOnNavMesh)
            {
                playerModel.navMeshAgent.SetDestination(target);
            }
            #endregion

            if (playerModel.useFPSMovement)
            {
                #region FPS 式人机速度（Speed 混合按距离；位移由 NavMeshAgent 驱动）
                if (distToTarget - playerModel.stoppingDistance < 2f)
                {
                    playerModel.LerpSpeedTo(PlayerModel.JOG_BLEND);
                    playerModel.SetBoolParam(PlayerModel.IsSprintingHash, false);
                }
                else
                {
                    playerModel.LerpSpeedTo(PlayerModel.SPRINT_BLEND);
                    playerModel.SetBoolParam(PlayerModel.IsSprintingHash, true);
                }
                playerModel.horizontalVelocity = Vector3.zero;//位移由 NavMeshAgent 驱动，避免与 LateUpdate 的 cc.Move 打架
                playerModel.SetBoolParam(PlayerModel.IsGroundedHash, true);
                #endregion
            }
            else
            {
                #region 处理移动速度（旧人机，MoveBlend 混合）
                if (distToTarget - playerModel.stoppingDistance < 2f)
                {
                    moveBlend = Mathf.Lerp(moveBlend, runThreshold, transitionSpeed * Time.deltaTime);

                }
                else
                {
                    moveBlend=Mathf.Lerp(moveBlend,sprintThreshold,transitionSpeed*Time.deltaTime);
                }
                playerModel.animator.SetFloat(moveBlendHash, moveBlend);
                #endregion
            }

        }
    }
}
