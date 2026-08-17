using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 僵尸攻击状态
/// </summary>
public class ZombieAttackState : EnemyStateBase
{
    private bool hasDealtDamage;//本次攻击是否已造成伤害

    public override void Enter()
    {
        base.Enter();
        enemyModel.PlayStateAnimation("Attack");
        enemyModel.navMeshAgent.velocity = Vector3.zero;//攻击时停止移动
        hasDealtDamage = false;
    }

    public override void Update()
    {
        base.Update();
        // 攻击动画播放完毕后结算伤害并进入冷却
        if (IsAnimationBreak(0))
        {
            // 动画播完且目标仍存活 → 造成一次伤害
            if (enemyModel.HasAttackTarget() && !enemyModel.attackTarget.isDead && !hasDealtDamage)
            {
                enemyModel.attackTarget.TakeDamage(enemyModel.attackDamage);
                hasDealtDamage = true;
            }
            // 记录攻击完成时间，进入冷却
            enemyModel.lastAttackTime = Time.time;
            // 切回待机，由 Idle 重新判断距离与冷却
            enemyModel.SwitchState(EnemyState.Idle);
        }
    }
}
