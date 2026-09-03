using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 瞄准状态
/// </summary>
public class PlayerAimingState : PlayerStateBase
{

    #region 动画器相关
    private int aimingXHash;
    private int aimingYHash;
    private float aimingX = 0;
    private float aimingY = 0;
    private float transitionSpeed = 5;
    #endregion

    public override void Init(IStateMachineOwner owner)
    {
        base.Init(owner);
        aimingXHash = Animator.StringToHash("AimingX");
        aimingYHash = Animator.StringToHash("AimingY");
    }

    public override void Enter()
    {
        base.Enter();
        playerModel.PlayStateAnimation("Aiming");
        if (IsBeControl())
        {
            UpdateAimingTarget();
            playerController.EnterAim();
        }
    }

    public override void Update()
    {
        base.Update();
        if (IsBeControl()) {

            //让模型立刻选择至相机方向
            playerModel.transform.rotation = Quaternion.Euler(0, Camera.main.transform.rotation.eulerAngles.y, 0);
            UpdateAimingTarget();

            #region 退出瞄准监听
            if (!playerController.isAiming&& !playerController.isFire)
            {
                playerModel.SwitchState(PlayerState.Idle);
                return;
            }
            #endregion

            #region 开火监听
            if (playerController.isFire)
            {
                if (playerModel.weapon != null)
                {
                    if (playerModel.weapon.Fire(playerController.AimTarget.position))
                        playerController.ShakeCamera();//只在规则层接受开火后播放镜头反馈
                }
                else
                {
                    Debug.LogWarning("当前角色未配置武器（PlayerModel.weapon 为空），无法开火。");
                }
            }
            #endregion

            #region 处理移动输入
            aimingX = Mathf.Lerp(aimingX, playerController.moveInput.x, transitionSpeed * Time.deltaTime);
            aimingY = Mathf.Lerp(aimingY, playerController.moveInput.y, transitionSpeed * Time.deltaTime);
            playerModel.animator.SetFloat(aimingXHash, aimingX);
            playerModel.animator.SetFloat(aimingYHash, aimingY);
            #endregion

            #region 瞄准位移（FPS 式时代码驱动；旧方案靠 Aiming 2D 混合树 root motion 位移）
            if (playerModel.useFPSMovement)
            {
                float inputMag = playerController.moveInput.magnitude;
                if (inputMag > 0.01f)
                {
                    // 用角色自身前向/右向（而非相机方向），避免相机 blend 过渡时 worldMovement 方向突变（同 FPSAimState）
                    // 用平滑后的 aimingX/aimingY，保证位移与 Aiming 混合树动画一致
                    Vector3 moveDir = aimingY * playerModel.transform.forward
                                    + aimingX * playerModel.transform.right;
                    playerModel.horizontalVelocity = moveDir.normalized * playerModel.aimMoveSpeed;
                }
                else
                {
                    playerModel.horizontalVelocity = Vector3.zero;//无输入：彻底站定
                }
            }
            #endregion
        }

    }

    public override void Exit()
    {
            base.Exit();
        if (IsBeControl())
        {
            playerController.ExitAim();
        }
    }

    private void UpdateAimingTarget() {
        //发射射线
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        //如果射线击中了物体
        if (Physics.Raycast(ray, out hit, playerController.maxRayDistance, playerController.aimLayerMask))
        {
            //更新瞄准目标位置
            playerController.AimTarget.position = hit.point;
        }
        else
        {
            playerController.AimTarget.position=ray.origin+ray.direction*playerController.maxRayDistance;
        }
    }
}
