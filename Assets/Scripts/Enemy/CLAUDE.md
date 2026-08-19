# Assets/Scripts/Enemy — 敌人系统

> 根级架构总览见根目录 `CLAUDE.md`。本文件只记录本模块专属约定与坑（改此目录代码必读）。

## 目录内容

| 文件 | 职责 |
|------|------|
| `ZombieEnemy.cs` | `EnemyBase` 具体实现：`SwitchState(EnemyState)` 分发到 4 个状态（Idle/Move/Attack/Dead） |
| `State/ZombieIdleState.cs` | 待机：有目标且在攻击范围且冷却结束 → Attack；有目标不在范围 → Move |
| `State/ZombieMoveState.cs` | 追击：`chaseTarget()` 每帧 SetDestination；进攻击范围 → Attack |
| `State/ZombieAttackState.cs` | 攻击：播 "Attack" 动画 + 停止移动；`IsAnimationBreak(0)` 播完后**结算一次伤害** → 记 `lastAttackTime` → 切 Idle 重判 |
| `State/ZombieDeadState.cs` | 死亡：播 "Dead" 动画 → 播完 `Clear()` 销毁 |

抽象基类 `EnemyBase` / `EnemyStateBase` 在 `Assets/Scripts/Base/`。

## 状态流转

```
Idle ──(有目标且不在攻击范围)──→ Move ──(进入攻击范围)──→ Attack ──(播完伤害 + 冷却)──→ Idle
  ↑                                                                          │
  └───────────────────────────(冷却中 / 无目标 在 Idle 重判)────────────────────┘
                                            Dead ←──(Hurt 扣血归零 → 禁碰撞/寻路 → 播 Dead → Clear)
```

- **目标选择 `FIndAttackTarget()`**：遍历 `GameManager.INSTANCE.playerModels[]` 找**最近的存活角色**（排除 `isDead` 与 null）。
- **定期刷新**：`EnemyBase.Update` 每 `attackTargetRefreshInterval=0.5s` 重找最近目标，避免锁定尸体/过远目标。
- **攻击闭环**：进范围（`IsAttackTargetInAttackRange` = 距离 < `minAttackDistance=1`）→ Attack 动画 → **动画播完**（`IsAnimationBreak`）对 `attackTarget.TakeDamage(attackDamage=10)` → `attackCooldown=1.5s` 冷却 → Idle 重判。

## 关键约定

- **`chaseTarget()` 先校验 `navMeshAgent.isOnNavMesh`** 再 `SetDestination`——刚启用代理/未烘焙区会抛 "not close enough to the NavMesh"/"active agent"。
- **受击 `Hurt(bullet, damageMultiplier=1)`**：Hit 动画触发（`animator.SetTrigger(hitHash)`）+ 减速动画（`MoveSpeed` 0.5×，0.5s 协程恢复）→ 喷血/滴血特效（走 `EffectPool`，喷血按子弹方向 `LookRotation(-bulletDir)`）→ 扣 `bullet.damage * multiplier` → 更新血条。**当前为绿色血**（`Green/Blood_Smash_Small_Green` + `Green/Blood_Dripping_Green`，2026-08-19 由 `EnemyBloodGreenWizard` 换色，Red/RedBright/RedDark/Blue/Lava/Black 全套可用）。**血花停在命中点**（08-19）：`PlayerWeaponBullet.CheckCollision` 调用 Hurt 前把子弹位置锚定到 `hit.point`；且 `Green/Blood_Smash_Small_Green` 的 Droplets 子发射器已调低重力(1→0.2)/初速(4~6→2，`BloodSmashLocalizeWizard`)，不再掉落地面与滴血混叠。**滴血只留血滩**（08-19）：`Green/Blood_Dripping_Green` 血滩生成链 = DropletsWithBloodMarks（碰撞地面）→Cone→BloodMarks（sub-emitter，properties=0 不继承颜色）；Cone / DropletsWithBloodMarks 粒子已透明化（隐藏血面喷血），Droplets / DropletsWithBloodMarks 已改一次性(1s)+初速减半（6~15→3~7.5，不再持续滴血），BloodMarks 血滩保留（`BloodDripCleanWizard`）。
- **死亡**：`SwitchState(Dead)` + 禁用 NavMeshAgent + `bodyCollider.enabled=false`（**null 防御**，兼容无 BoxCollider 的敌人）+ 销毁血条 → `ZombieDeadState` 播完 `Clear()`（`stateMachine.Stop()` + `Destroy`）。
- **血条**：`Start` 实例化到 `UIManager.INSTANCE.WorldSpaceCanvas`，受击后显示 `healthBarShowTime=6s`，Billboard 面向相机（`EnemyHealthBarUI`）。
- **扩展新敌人**：继承 `EnemyBase` 只需实现 `SwitchState(EnemyState)`；状态类继承 `EnemyStateBase`（`Init` 时缓存 `enemyModel` = owner 强转）。动画统一 `PlayStateAnimation(name, transition)`（CrossFadeInFixedTime）。
- **动画完成判断 `IsAnimationBreak`**：在 `EnemyStateBase`（动画播完判定），攻击/死亡结算都靠它。

## 命名

- `EnemyState` 枚举：`Idle / Move / Attack / Dead`。
- 状态走 MonoManager 集中式 Update（`Enter` 注册 / `Exit` 注销）。
