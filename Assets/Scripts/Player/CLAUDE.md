# Assets/Scripts/Player — 玩家系统

> 根级架构总览见根目录 `CLAUDE.md`。本文件只记录本模块专属约定、状态流转与坑（改此目录代码必读）。

## 目录内容

| 文件 | 职责 |
|------|------|
| `PlayerController.cs` | 单例（`SingleMonoBase<PlayerController>`）：轮询输入 → `moveInput/isSprint/isAiming/isJumping/isFire/isSlide`；相机相对方向 `worldMovement/localMovement`；双相机切换 `EnterAim/ExitAim`；IK 权重；角色切换 `SwitchPlayerModel(0/1/2)`；死亡接管 `OnPlayerDied`；`ShakeCamera()` |
| `PlayerModel.cs` | `IStateMachineOwner`：CharacterController + Animator + StateMechaine；血量/受击/死亡；人机跟随；FPS 式移动参数；IK 约束引用（Hunter 为 null） |
| `PlayerWeapon.cs` | 武器：`Fire(targetPos)` 射速限制 `bulletInterval=0.15s`（≈6.67 发/秒）；子弹走 Queue 对象池 + 枪口火花走 `EffectPool`；成功发射一枪广播 `Fired` 事件（音效组件订阅）。**仅荧/芙宁娜挂载**，Hunter 无武器 |
| `WeaponAudio.cs` | 武器音效（**可替换组件**）：订阅 `PlayerWeapon.Fired` 播枪声；`fireClips` 数组在 Inspector 里自由替换/增删即换枪声；随机抽 1 个 + 随机音高避免重复感；AudioSource 3D 声场（随距离衰减）。**仅荧/芙宁娜挂载**，Hunter 无武器 |
| `PlayerWeaponBullet.cs` | 子弹：Rigidbody 飞行（`flyPower=30`）+ 帧间 Raycast 防穿透；命中播特效回池；命中 Enemy Tag 调 `Hurt()`（damage=10） |
| `State/` | 玩家状态实现：`PlayerIdleState` / `PlayerMoveState` / `PlayerHoverState` / `PlayerAimingState` / `PlayerSlideState` / `PlayerSprintState` |

状态基类 `PlayerStateBase` / `StateBase` / `StateMechaine` 在 `Assets/Scripts/Base/`、`Assets/Scripts/Utils/`。

## 状态流转

`PlayerModel.SwitchState(PlayerState)` → `stateMechaine.EnterState<T>()`（泛型状态机缓存状态实例，防重入）。

```
Idle ←→ Move ←→ Sprint（Move 按住 Shift 进 Sprint，松开回 Move）
Idle/Move/Sprint → Hover（按跳跃 或 跌落超过 fallHeight）
Move/Sprint → Slide（跑动/冲刺中按 C，播完回 Move）
Hover → Idle（cc.isGrounded）
任意地面状态 → Aiming（isAiming || isFire —— 在 PlayerStateBase.Update 每帧全局监听）
Aiming → Idle（松开右键且未开火）
```

## 关键约定（本模块必读）

- **FPS 式移动（`useFPSMovement=true`，当前全部角色）**：位移完全由 `PlayerModel.LateUpdate` 的 `cc.Move(horizontalVelocity + verticalSpeed)` 驱动。状态类（经 MonoManager 集中式 Update，早于本帧）先写 `horizontalVelocity`，本 LateUpdate 再 Move。**用 LateUpdate 而非 Update**——否则"先移动后写值"导致位移被吞。
- **⚠️ `applyRootMotion` 保持 `true`（两套方案都是）**：Animation Rigging 约束（TwoBoneIK/MultiAim）依赖它才求值，`applyRootMotion=false` 时约束根本不工作。FPS 式下根运动由 `OnAnimatorMove` 直接 return 丢弃，位移仍由 cc.Move 驱动，不穿模。
- **人机位移归 NavMeshAgent 独占**：非主控（`PlayerController.INSTANCE.currentPlayerModel != this`）时 LateUpdate/OnAnimatorMove **都不调 `cc.Move`**——否则 CharacterController 每帧改 transform 与 NavMeshAgent 抢位置，随从原地不动（动画照播）。
- **`PlayerStateBase.Update` 重力**：用 `if (IsBeControl())` 包裹但**不能 return**（人机 else 分支在 base 之后执行）。稳定性窗口 `HOVER_STABILITY_FRAMES=5` 内 `verticalSpeed` 锁定 `-2f`，超过才累积真实重力。
- **起飞/落地不对称（有意设计，勿改）**：起飞用 `IsHover()`（SphereCast 从 CC 真实底部 `pos.y + cc.center.y - cc.height*0.5f + cc.skinWidth` 出发，半径 `cc.radius*0.6`）；落地用 `cc.isGrounded`。改回 `!IsHover()` 会破坏落地动画切换。
- **`SwitchToHover()`**：主动跳跃时把 `ungroundedFrameCount = HOVER_STABILITY_FRAMES` 跳过稳定窗口，立即进入重力累积。
- **滑铲根运动清零**：`OnAnimatorMove` 中 `currentState == PlayerState.Slide` 时 `playerDeltaMovement = Vector3.zero`——Running Slide 动画自带根位移是骨骼侧向（非角色正前方），直接用会"向左滑铲/空中飞踢"。横向位移由 `PlayerSlideState` 自行驱动。
- **Hunter 例外（2026-08-17 起）**：Hunter **不挂武器**（`weapon==null`）、IK 约束全空。`PlayerModel.EnterAim/ExitAim` 已加 null 保护；`PlayerAimingState` 开火分支有 `weapon != null` 判断（否则 LogWarning）。改瞄准逻辑别假设武器/约束存在。
- **瞄准位移（FPS 式）**：`PlayerAimingState` 用平滑后的 `aimingX/aimingY` 乘角色自身前向/右向（非相机方向）写 `horizontalVelocity`——避免相机 blend 过渡时 `worldMovement` 方向突变。无输入则 `horizontalVelocity = Vector3.zero`（站定）。
- **角色切换**：数字键 1/2/3 → `SwitchPlayerModel(index)`。旧角色 `Exit()`：`NavMesh.SamplePosition` 校正到最近网格点后启用 NavMeshAgent + `SwitchState(Idle)`；新角色 `Enter()`：禁 NavMeshAgent + `ResetCameraTarget()`。**已死亡角色不可切换/访问**。
- **死亡流程**：`TakeDamage` 扣血 → 归零 `Die()`。`Die()`（2026-08-21 当前）：`isDead` 置位 → PVP 防护（`disableStateMachine` return，死亡/重生归服务器 `ApplyDeathNetwork`/`ApplyRespawnNetwork`）→ 停状态机/CC/Agent → **关闭全部武器 IK 权重**（防尸体手被 AimTarget 拽着，须在 `Stop()` 之后——`PlayerAimingState.Exit()` 的 `ExitAim()` 会把髋部 IK 恢复成 1）→ `OnPlayerDied`（主控死自动接管下一存活角色；全灭 → 动态挂 `GameOverUI`；角色死后不销毁留作静态尸体，`PlayerController.Update` 已有 currentPlayerModel null 守卫）。⚠️ **死亡动画当前临时关闭**（曾接入：`PlayerDeathAnimWizard` 给 TPS_Movement / Hunter_Parkour / **Player.controller（PVE 场景 荧/芙宁娜 实际用的旧控制器）** 加了 `Dead_B/L/F/R` 状态，MotusMan clip、speed=2、关 Loop Time；`Die()` 曾随机方向播放 + 绕脚底支点倒地 + 播完销毁，因倒地视觉反复调不好已停用，git 历史保留全部实现可恢复）。`OnAnimatorMove` 死亡时直接 return。PVP（`disableStateMachine`）在 `Die()` 内直接 return，死亡/重生归服务器（`ApplyDeathNetwork`/`ApplyRespawnNetwork`，其死亡动画用 `deadAnimationName`=Dead_B 固定后倒）。主控死自动接管下一存活角色；全灭 → 动态挂 `GameOverUI`（此时 `PlayerController.currentPlayerModel` 会在角色销毁后变空，`Update` 已加 null 守卫）。
- **`PlayerModel.cc` 编辑模式为 null**：编辑器向导/诊断脚本算脚底别用 `PlayerModel.cc`。
- **动画参数 hash**（`TPS_Movement.controller` / `Hunter_Parkour.controller` 共用，见 `PlayerModel` 常量）：`Speed`（0/0.33/0.66/1 → Idle/Walk/Jog/Sprint）、`VerticalSpeed`、`IsGrounded`、`IsSprinting`、`AimingX/AimingY`、`HoverClip`（Hunter 随机跳跃）。`LerpSpeedTo` 平滑逼近，`GetMoveSpeed` 把混合树 Speed 映射为世界速度。
- **Hunter 随机跳跃**：`randomJumpClips=true`（仅 Hunter）时 `PlayerHoverState.Enter` 用 `Random.Range(0,3)` 写 `HoverClip`，在 `Hunter_Parkour.controller` HoverJump 混合树随机播 1 个跑酷空中片段。**换跳跃 clip 前先隔离验证**（08-12 教训：换三段式跳跃后全员半身入地）。

## 命名与拼写

- 刻意保留拼写（**不是笔误**）：`stateMechaine`、`Destory()`、`updataAction`。新增代码用标准拼写。
- 输入生命周期：`PlayerController.Awake` new → `OnEnable/OnDisable` Enable/Disable → `OnDestroy` `Dispose()`。
- 人机三角队形 `GetFollowerTargetPosition(spacing=2.5, spread=35)`：主控为顶，索引 1 左后、索引 2 右后，倒三角。
