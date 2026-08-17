using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieIdleState : EnemyStateBase
{
    public override void Enter()
    {
        base.Enter();
        enemyModel.PlayStateAnimation("Idle");
        enemyModel.navMeshAgent.velocity= Vector3.zero;
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
        // 目标在攻击范围内
        if (enemyModel.IsAttackTargetInAttackRange())
        {
            // 冷却完毕 → 攻击；冷却中保持待机等待
            if (Time.time - enemyModel.lastAttackTime >= enemyModel.attackCooldown)
            {
                enemyModel.SwitchState(EnemyState.Attack);
            }
        }
        else// 目标不在攻击范围 → 追击
        {
            enemyModel.SwitchState(EnemyState.Move);
        }
    }

}
