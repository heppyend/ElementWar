# Assets/Scripts/Enemy — 敌人系统

> 根 `AGENTS.md` 的“只改代码”边界在本目录生效。敌人 Prefab、NavMesh、Animator、特效与血条引用由用户在 Unity GUI 配置。

## 模块职责

`EnemyBase` 是状态机宿主，集中处理 Animator、NavMeshAgent、目标搜索、受击/减速/血条/死亡和攻击参数；`ZombieEnemy` 只负责把 `EnemyState` 分发至 `ZombieIdle/Move/Attack/DeadState`。状态实例复用，不要在每次切换时重复 new。

## 行为契约

- 目标应从存活玩家中选最近者，刷新周期由 `attackTargetRefreshInterval` 控制。
- 追击前保持 `NavMeshAgent.isOnNavMesh` 等防御；不能为绕过异常直接取消 Agent 生命周期。
- 攻击闭环是：距离判定 → Attack 动画 → 伤害结算 → `attackCooldown` → 重新判定。默认攻击距离 1、伤害 10、冷却 1.5 秒。
- `Hurt()`、血条、受击特效与死亡必须保证同一敌人不会重复死亡或继续攻击。
- 敌人状态动画通过 `PlayStateAnimation` 统一 CrossFade；变更状态逻辑时保持 Animator 参数/片段由用户在 GUI 配置的边界。

## GUI 交接

若实现需要新增敌人、调整 NavMesh、修改血条/特效/动画引用，只列出用户在 Unity 中的操作步骤；不要运行或改写能写入 Scene/Prefab 的工具。
