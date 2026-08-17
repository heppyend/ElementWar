using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieMoveState : EnemyStateBase
{
    public override void Enter()
    {
        base.Enter();
        enemyModel.PlayStateAnimation("Move");
       

    }

    public override void Update()
    {
        base.Update();
        // 目标失效（丢失或已死亡）→ 重新寻找
        if (!enemyModel.HasAttackTarget() || enemyModel.attackTarget.isDead)
        {
            enemyModel.FIndAttackTarget();
            return;
        }
        // 进入攻击范围 → 回 Idle（由 Idle 统一判断攻击冷却）
        if (enemyModel.IsAttackTargetInAttackRange())
        {
            enemyModel.SwitchState(EnemyState.Idle);
            return;
        }
        // 目标不在攻击范围 → 继续追击
        enemyModel.chaseTarget();
    }
}
