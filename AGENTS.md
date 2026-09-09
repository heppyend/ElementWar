# AGENTS.md

## Codex 执行边界（优先级最高）

- Codex 仅修改代码文件；文档仅在用户明确要求时修改。
- 例外：目录创建、项目内文件/资源的搬运与整理由 Codex 直接执行，无需为此要求用户操作。移动 Unity 资源时必须同步移动对应 `.meta` 文件，先核对源/目标路径，不覆盖或删除未确认内容，并在完成后报告实际变更。
- 不得通过代码、Editor Wizard、AssetDatabase、序列化文件或其他间接手段修改 Scene、Prefab、模型、材质、Animator、Rig、组件引用、NavMesh、输入资产或任何 Unity GUI 配置。
- 需要组件拖拽、模型/Prefab 放置、Inspector 配置、场景搭建、烘焙或其他 GUI 操作时，说明用户操作步骤并等待用户确认，再继续代码工作。
- 这项边界覆盖本文中所有历史的“运行 Wizard 修改场景/Prefab”描述；那些描述仅用于解释现有代码和供用户手动操作。

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

**ElementWar** — a 3D third-person shooter (TPS) built in Unity 2022.3.62f3 using Universal Render Pipeline (URP). Core playable loop is complete: **three switchable characters**（荧 Lumine / 芙宁娜 Furina / 暗夜猎人 Hunter）、FPS 式代码驱动移动 + 跑酷动画、冲刺/滑铲/瞄准/射击、敌人寻路追击与攻击闭环、玩家/敌人血量、完整死亡流程、主菜单 UI。尚未完成：武器切换/弹药、HUD/准星/暂停菜单、音频、完整关卡。

> **开发者方向**：我的 Unity 开发方向是 **3C（角色/相机/控制手感） · 性能优化 · 网络 · 渲染**。
> **工程能力方向**：在上述方向上补充建设 **可观测性与数据驱动能力**：以性能预算和 Profiler 证据为依据，以网络延迟/丢包/带宽等指标驱动优化，并推进角色与技能配置解耦，服务后续 Lua、ECS 和多人规模扩展。

> ⚠️ **Hunter 暂不装备武器（2026-08-17 起）**：已移除 Hunter 身上的武器 + 全部 Animation Rigging IK 约束（只动 Hunter，荧/芙宁娜照常持枪）。瞄准状态/瞄准动画/双相机保留，开火因武器为空自然无动作。还原点已删（Git 接管，08-17）。

## Build & Test Commands

- **Open project**: Open the root folder (`E:\Unity product\ElementWar`) in Unity Hub and launch with Unity 2022.3.62f3.
- **Run tests**: `Window → General → Test Runner` in the Unity Editor. Uses `com.unity.test-framework@1.1.33`.
- **Build**: `File → Build Settings` → target Standalone Windows (current configured platform).

There is no CI/CD or headless build pipeline configured.

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `com.unity.inputsystem@1.14.2` | New Input System (source-generated wrapper at `Assets/Settings/InputSystem/MyInputSystem.cs`) |
| `com.unity.cinemachine@2.10.7` | Camera control (dual FreeLook cameras for normal/aiming, ImpulseSource for fire shake) |
| `com.unity.render-pipelines.universal@14.0.12` | URP rendering |
| `com.unity.animation.rigging@1.2.1` | Animation rigging (IK constraints for weapon aiming: `TwoBoneIKConstraint`, `MultiAimConstraint`) — ⚠️ 见下方 Hunter 例外 |
| `com.unity.ai.navigation@1.1.7` | NavMesh (NavMeshSurface baking, NavMeshAgent for enemy chase + AI companion follow) |
| `com.unity.probuilder@5.2.4` | In-editor level prototyping |

> 公开版不包含未确认再分发权的第三方模型、动画、音频、贴图、Prefab、场景或 Asset Store 包。以下历史资源路径仅存在于私有开发记录中，不是当前公开基线。

## Architecture

> 📂 **模块文档索引**（子目录 `AGENTS.md`，读该模块代码时自动加载，改对应目录必读）：
> - [`Assets/Scripts/Player/AGENTS.md`](Assets/Scripts/Player/AGENTS.md) — 玩家系统：状态流转 / FPS 式移动 / 人机 / Hunter 无武器
> - [`Assets/Scripts/Enemy/AGENTS.md`](Assets/Scripts/Enemy/AGENTS.md) — 敌人系统：追击 / 攻击闭环 / 受击 / 死亡
> - [`Assets/Scripts/Editor/AGENTS.md`](Assets/Scripts/Editor/AGENTS.md) — 编辑器向导：Tools/玩家 + Tools/场景 铁律
> - [`Assets/Scripts/FPS/AGENTS.md`](Assets/Scripts/FPS/AGENTS.md) — FPS 原型框架（New Scene 沙盒）
> - [`Assets/Scripts/Network/AGENTS.md`](Assets/Scripts/Network/AGENTS.md) — PVP 网络同步：UDP 权威框架 / 简化移动 / 服务器 Server/（.NET）
> - 模型导入与 MMD/PMX 适配资料不随公开版提供；需要相关内容时必须在独立私有工作区按原许可证处理。

> 模块级细节见上；以下是全局架构摘要：

### Player System

```
PlayerController (Singleton)
  → reads New Input System (WASD, sprint, aim, jump, fire, slide, 1/2/3)
  → computes localMovement / worldMovement from camera-relative input
  → manages dual-camera swap (normal FreeLook ↔ aiming FreeLook)
  → manages IK constraint weights for aiming
  → SwitchPlayerModel(index) — 多角色切换（0 荧 / 1 芙宁娜 / 2 暗夜猎人）
  → OnPlayerDied() — 死亡接管：随从拦截 / 主控自动切换下一位 / 全灭 GAME OVER

PlayerModel : MonoBehaviour, IStateMachineOwner
  → owns CharacterController + Animator + StateMachine
  → holds PlayerWeapon reference + health + death flow
  → holds IK constraints (rightHandConstraint, rightHandAimConstraint, bodyAimConstraint)
  → dispatches SwitchState(PlayerState enum): Idle/Move/Hover/Aiming/Slide/Sprint
  → useFPSMovement=true：LateUpdate 用 cc.Move 代码驱动位移（FPS 式移动）
  → 人机模式（非主控）：NavMeshAgent 跟随，位移归代理独占，三角队形 GetFollowerTargetPosition()
  → Hunter 特例：⚠️ 2026-08-17 起 Hunter **不再装备武器**，武器/IK 约束全部移除，仅保留移动/瞄准（见下）
```

### Enemy System

```
EnemyBase (abstract) : MonoBehaviour, IStateMachineOwner
  → owns Animator + StateMachine
  → declares abstract SwitchState(EnemyState)
  → PlayStateAnimation() helper for CrossFadeInFixedTime
  → chaseTarget()（NavMeshAgent，isOnNavMesh 校验）/ FIndAttackTarget()（每 0.5s 刷新，打最近存活角色）
  → Hurt()（受击动画 + 减速 + 喷血/滴血特效（绿色，2026-08-19 换色）+ 扣血 + 血条）/ 死亡流程

EnemyStateBase : StateBase
  → caches enemyModel reference from Init()

ZombieEnemy : EnemyBase  (concrete implementation)
  → SwitchState dispatches to ZombieIdle/Move/Attack/Dead states

Enemy States: Idle → Move → Attack → Dead（攻击闭环已实现：进范围→播 Attack 动画→结算伤害→冷却→重判）
```

### Weapon System

```
PlayerWeapon : MonoBehaviour
  → Fire(targetPos) — rate-limited by bulletInterval (0.15s ≈ 6.67 发/秒)；成功发射广播 Fired 事件
  → 子弹走 Queue 对象池 + 枪口火花走 EffectPool（不再 Instantiate/Destroy）
  → 08-20：开火附「子弹轨迹（短命 LineRenderer 曳光）+ 击中墙体弹孔（程序化贴花，角色身上不贴）」——`SpawnTracerAndHole` 沿枪口射线定终点，纯视觉

WeaponAudio : MonoBehaviour（可替换音效组件，挂在 PlayerWeapon 同物体）
  → 订阅 PlayerWeapon.Fired 播枪声；fireClips 数组在 Inspector 里自由替换/增删即换枪声
  → 公开版不附带音频文件或枪械 Prefab；合法取得资源后由使用者在 Unity Inspector 中配置

PlayerWeaponBullet : MonoBehaviour
  → Rigidbody-based projectile (flyPower=30) + 帧间 Raycast 防穿透
  → 命中播特效回池；命中 Enemy Tag 调用 Hurt()（damage=10）
  → lifetime 自动销毁（回池）
```

> ⚠️ Hunter 不再装备武器（08-17 起），武器/握枪 IK 全部移除；上述握枪方案仅作历史记录。荧/芙宁娜武器 IK 的通用教训见「Critical Implementation Details」。

### State Machine

```
StateMachine (generic, not a MonoBehaviour)
  → caches state instances (Type → StateBase), created once then reused
  → EnterState<T>() handles exit-old/enter-new/anti-reentry
  → Stop() calls Exit on current + Destory on all cached states

IStateMachineOwner — marker interface for state machine hosts
```

### Core Base Classes

| Class | Role |
|-------|------|
| `StateBase` | Abstract: `Init(owner)`, `Enter()`, `Exit()`, `Update()`, `Destory()` |
| `PlayerStateBase : StateBase` | Shared gravity logic (runs via MonoManager), aiming input check, `SwitchToHover()` for jump |
| `EnemyStateBase : StateBase` | Caches `enemyModel` from owner cast |
| `SingleMonoBase<T>` | Singleton MonoBehaviour: `INSTANCE` static access, duplicate-detection in Awake |
| `MonoManager : SingleMonoBase<MonoManager>` | Centralized Update — states register callbacks via `AddUpdateAction`/`RemoveUpdateAction` |

### Key Patterns

- **Singleton via `SingleMonoBase<T>`**: `PlayerController.INSTANCE`, `MonoManager.INSTANCE` — accessed directly rather than via `FindObjectOfType`.
- **Centralized Update via `MonoManager`**: State `Update()` methods are registered as delegate callbacks so only one MonoBehaviour Update loop runs.
- **FPS 式代码驱动移动（当前，`useFPSMovement=true`）**：`applyRootMotion` **保持 `true`**（Animation Rigging 依赖它求值），根运动由 `OnAnimatorMove` 拦截丢弃（FPS 分支直接 return）；位移由 `PlayerModel.LateUpdate` 的 `cc.Move(horizontalVelocity + verticalSpeed)` 驱动。`Speed` 混合树 0/.33/.66/1 映射 0/walk/jog/sprint，`LerpSpeedTo` 平滑逼近。**注意：位移用 LateUpdate 而非 Update**——确保状态类（经 MonoManager 通常早于本帧）先写 `horizontalVelocity` 再 Move，避免"先移动后写值"位移被吞。
- **Locomotion 混合树为混合版（2026-08-19 起，PVE/PVP 共用 `TPS_Movement.controller`）**：Idle/Walk/Jog 段（Speed 0/.33/.66）用旧 X Bot 动画（`@Idle`/`@Run forward`，fileID `-203655887218126122`），Sprint 段（Speed 1）用跑酷 `Mvm_Dash`；Air 混合树用旧 `@Hover`；滑铲跑酷 `Esc_Slide_Loop`；瞄准 X Bot strafe。两套动画均 Humanoid 可跨骨骼 retarget。Hunter 仍用 `Hunter_Parkour.controller`。
- **旧 Root Motion + Manual Gravity（`useFPSMovement=false` 回退）**：Horizontal movement comes from Animator's `deltaPosition`; vertical is manually computed and overlaid in `OnAnimatorMove()`. During Hover, a 3-frame rolling average of pre-jump `animator.velocity` is used for horizontal momentum.
- **人机位移归 NavMeshAgent 独占**：非主控角色在 `LateUpdate` / `OnAnimatorMove` 都不调 `cc.Move`（否则 CC 每帧改 transform 与 NavMeshAgent 抢位置 → 随从原地不动）；`PlayerStateBase` 重力用 `if (IsBeControl())` 包裹（不能 return，人机 else 分支在 base 之后执行）。
- **State caching**: State instances are created once and reused; `Init()` is called on first creation, `Enter()`/`Exit()` on each transition.
- **Input is polled** in `PlayerController.Update()`, not event-driven. States read `playerController.moveInput` etc. each frame.
- **场景操作边界**：当前主 PVE 场景是 `PVEGame.unity`。场景/Prefab/资源变更必须由用户在 Unity GUI 执行；Codex 只可修改相关 C#，并提供操作步骤、等待确认。

### State Transitions

```
                    Aiming (right mouse held OR firing)
               ┌──── from any state ────→ Aiming ──→ Idle (release) ────┐
               │                                                         │
  Idle ←→ Move ←→ Sprint (FPS 式移动；Move 按住 Shift 进 Sprint)         │
  Move/Sprint → Slide (按 C，播完回 Move)                                │
  Idle/Move/Sprint → Hover (jump pressed OR fallHeight exceeded)         │
  Hover → Idle (cc.isGrounded)                                           │
```

Aiming is checked in `PlayerStateBase.Update()` every frame — when `isAiming` or `isFire` is true, any grounded state transitions to `PlayerAimingState`. When both are released, it transitions back to Idle.

> **Hunter 随机跳跃**：`PlayerModel.randomJumpClips=true` 时，`PlayerHoverState.Enter` 用 `Random.Range(0,3)` 写 `HoverClip` 参数，在 `Hunter_Parkour.controller` 的 HoverJump 混合树（`Jmp_Base_B_Root` / `Jmp_Move_Left` / `Jmp_BackAir_Keep`）中随机播 1 个。

### Aiming & Camera System

- **Dual CinemachineFreeLook cameras**: `freeLookCamera` (normal) and `aimingCamera` (aim-down-sights). Priority swap on enter/exit aim.
- **EnterAim()**: syncs aiming camera rotation from normal camera, sets IK constraint weights (right hand → MultiAimConstraint, body → MultiAimConstraint), swaps camera priority.
- **ExitAim()**: reverse — syncs normal camera from aiming camera, restores TwoBoneIKConstraint, swaps priority.
- **Hunter 例外（2026-08-17 起）**: Hunter 无武器，`HunterHandIK`（OnAnimatorIK 握枪）与全部武器 IK 约束已移除；瞄准仅保留相机 + X Bot 瞄准动画 + `PlayerModel.EnterAim/ExitAim`（已加 null 保护）。
- **Aim target**: Screen-center raycast (`Camera.main.ViewportPointToRay(0.5, 0.5, 0)`) against `aimLayerMask`, updates `AimTarget.position`.
- **Fire shake**: `CinemachineImpulseSource` on the aiming camera, triggered by `ShakeCamera()`. ImpulseListener 必须挂**虚拟相机**（挂 Main Camera 报 "requires a virtual camera"）。
- **Aiming movement**: Animator blend tree parameters `AimingX`/`AimingY` lerp from `moveInput` for strafe/forward-back animation blending while aiming.

## File Locations

| What | Where |
|------|-------|
| Core scripts | `Assets/Scripts/` |
| Base classes | `Assets/Scripts/Base/` |
| Utils | `Assets/Scripts/Utils/` |
| Player system | `Assets/Scripts/Player/` |
| Player states | `Assets/Scripts/Player/State/` |
| Enemy system | `Assets/Scripts/Enemy/` |
| Enemy states | `Assets/Scripts/Enemy/State/` |
| FPS 原型框架（New Scene） | `Assets/Scripts/FPS/`（Core/Controller/State/Aim/Editor） |
| 运行时诊断工具 | `Assets/Scripts/Diagnostics/`（GroundDiag / FollowerDiag / **DebugInfoWindow** 左上角 FPS 信息窗，F3 开关） |
| 编辑器向导（Tools/玩家） | `Assets/Scripts/Editor/`（一键生成/修复，场景二进制改动必经之路） |
| Input actions asset | `Assets/Settings/InputSystem/MyInputSystem.inputactions` |
| Auto-generated input C# | `Assets/Settings/InputSystem/MyInputSystem.cs` (do not hand-edit) |
| URP settings | `Assets/Settings/` (3 quality tiers: Performant, Balanced, HighFidelity) |
| 角色、动画、音频和场景资源 | 公开版不随仓库分发；由使用者从合法来源自行取得 |
| 运行时代码 | `Assets/Scripts/` |
| 输入与 URP 配置 | `Assets/Settings/` |
| 主工程配置 | `ProjectSettings/` |

## Editor Tools（Tools/玩家 菜单）

> 下列向导用于说明已有项目能力，须由用户在 Unity GUI 手动执行；Codex 不执行它们，也不以代码替代其场景/资源改动。

| 菜单 | 作用 |
|------|------|
| `修复 NavMesh 排除标记并校正地面（重烘焙）` | ⭐ NavMesh 出问题用它：移除误加的 ignoreFromBuild + 抬升地面 + 清空重建 + 角色吸附（写 `_Diagnostics_NavMeshFix.log`） |
| `诊断 NavMesh 层配置（写日志文件）` | 查 surface 设置 / 层6 物体 / 被排除物体 / 角色接地（写 `_Diagnostics_NavMeshScene.log`） |
| `解开全部预制体（Unpack）`（Tools/场景） | 场景全部 prefab 实例脱离 prefab（自由编辑，改场景不碰 prefab） |
| `把选中物体落到地面（吸附到表面）`（Tools/场景） | 批量把选中物体底面吸附到下方表面（换地板后 cube 悬空用；需地面有 Collider） |
| `给选中物体添加/移除 NavMeshObstacle`（Tools/场景） | 墙/柱子阻挡寻路（carving 动态避障，不用重烘焙） |
| `把选中物体烘焙为可行走并重烘焙`（Tools/场景） | 斜坡/楼梯/平台设为可行走面并重烘焙 |

> ⚠️ **公开版边界**：Tools/玩家 菜单中的场景、Prefab、模型和资源写入操作只描述已有代码能力，不代表公开仓库包含对应内容；任何 Unity GUI 资源配置都需要使用者在合法取得资源后自行完成。

## Code Conventions

| Category | Style |
|----------|-------|
| Classes | PascalCase |
| Fields (private/public) | camelCase |
| Methods | PascalCase |
| Interfaces | `I` prefix (`IStateMachineOwner`) |
| Enums | PascalCase (`PlayerState.Idle`, `EnemyState.Move`) |
| Comments | Chinese (中文) |

> ⚠️ 历史拼写状态：状态机类已于 2026-08-29 统一为标准拼写 `StateMachine`；`Destory()`、`updataAction` 仍暂时保留。新代码用标准拼写（见各子目录 AGENTS.md）。

## Critical Implementation Details

- **Slope flicker mitigation**: `cc.isGrounded` is unreliable on slopes. The system uses `HOVER_STABILITY_FRAMES` (currently **5** ≈ 0.083s@60fps) as a buffer — gravity is locked at `-2f` within the window. Only after the window expires does real gravity accumulate and the state transitions to Hover. 历史详见 `Assets/Scripts/Player/AGENTS.md`（斜坡三层防护由来）。
- **`IsHover()`** uses `Physics.SphereCast` from the actual CharacterController bottom position (accounting for `cc.center`, `cc.height`, `cc.skinWidth`, and `cc.radius`), NOT a simple `transform.position` raycast. 起飞用 `IsHover()`（距离检测）+ 落地用 `cc.isGrounded`（接触检测）的不对称是**有意设计**。
- **`MyInputSystem.cs`** is auto-generated — edit the `.inputactions` asset, not the C# file.
- **`OnAnimatorMove()`**: FPS 式移动（`useFPSMovement=true`）时**直接 return 丢弃根运动**（但 `applyRootMotion` 保持 true——Animation Rigging 约束依赖它求值）；旧方案下平均后 3 帧 `animator.velocity`，Hover 时用它维持惯性，并每帧叠加手动 `verticalSpeed`。
- **⚠️ `applyRootMotion` 恒为 `true`（两套移动方案都是）**：Animation Rigging 约束（TwoBoneIK/MultiAim）在 `applyRootMotion=false` 时**根本不求值**（Unity 已知问题）。FPS 代码驱动时根运动由 `OnAnimatorMove` 拦截丢弃，位移仍由 `LateUpdate` 的 `cc.Move` 驱动，不穿模。
- **Aiming input is checked in `PlayerStateBase.Update()`**, not in individual states — this means aiming can be entered from Idle, Move, or Hover. The aiming check runs before state-specific logic because it's in the base class.
- **IK constraint toggling**: `EnterAim()`/`ExitAim()` on PlayerController swap between `TwoBoneIKConstraint` (hip-fire right hand) and `MultiAimConstraint` (aim-down-sights right hand + body). Both constraints reference the `AimTarget` transform.
- **Hunter 例外（2026-08-17 起已移除武器）**: Hunter 不再装备武器/IK 约束（只动 Hunter，荧/芙宁娜照常持枪）。`PlayerModel.EnterAim/ExitAim` 已加 null 保护，Hunter 瞄准不 NRE；开火因 `weapon==null` 自然无动作。还原点已删（Git 接管，Hunter 无武器化为当前基线）。
  - ⚠️ Hunter 保留：移动/跑酷/瞄准状态/瞄准动画/双瞄准相机（`PlayerAimingState` + `Hunter_Parkour.controller`）。**只删了武器组件与 IK 约束**。
  - 以下武器 IK 通用教训仍适用于荧/芙宁娜：改 MultiAim 数据必须 `ref var d = ref constraint.data`（`var d = data` 复制 struct 改副本无效）；`aimAxis` 是 `[NotKeyable]`，改后需 `rigBuilder.Clear(); Build();` 重建；`offset` 是**后置旋转**（右乘局部空间），按枪管方向算 `offset = FromToRotation(barrelLocal, axisVec).eulerAngles`。
- **人机位移归 NavMeshAgent 独占**：非主控角色不调 `cc.Move`（否则与 NavMeshAgent 抢位置 → 随从原地不动）；`PlayerStateBase.Update()` 重力逻辑用 `if (IsBeControl())` 包裹但**不能 return**（人机 else 分支在 base 之后执行）。随从三角形队形 `GetFollowerTargetPosition(spacing=2.5, spread=35)`。
- **场景与资源修改**：Codex 不执行任何场景、Prefab 或资源写入。用户如需变更，在 Unity GUI 中完成；PVE 主场景为 `PVEGame.unity`，现有对象已脱离 Prefab 实例关联，修改场景不会同步回 Prefab 资产。
- **Bullet**: Rigidbody 飞行（`flyPower=30`）+ 帧间 Raycast 防穿透 + Queue 对象池；命中播特效回池、命中 `Enemy` Tag 调用 `Hurt()`（damage=10）。枪口火花/命中特效/敌人受击特效统一走 `EffectPool`（按预制体分池，粒子播完自动回池）。
- **NavMesh 出问题（走上天/走不过来/not close enough）**：⚠️ 先查是否 `NavMeshModifier(ignoreFromBuild)` 误标了地面——08-17 事故：旧「标记可能走上天」工具用「顶部离地>2.5m 或浮空>2m」启发式，把 1000×1000 的 Ground 也判成浮空排除 → **0 三角面 → 所有代理 not close enough**。修复跑 `Tools/玩家/修复 NavMesh 排除标记并校正地面（重烘焙）`（移除 ignoreFromBuild + 抬地面 + 清空重建 + 角色吸附，写日志）；排查跑 `Tools/玩家/诊断 NavMesh 层配置（写日志文件）`。旧 `RebakeNavMeshWizard` 已删（其「标记走上天」启发式误伤地面）。注意：层 6 = Environment；`PlayerModel.cc` 运行时才赋值、**编辑模式为 null**（向导别用它算脚底）。

## PVP 网络同步（2026-08-19 新增）

- **架构**：自定义 UDP 权威框架（参考 CalabiYau，非移植）。服务器是独立 .NET UDP 进程（`Server/`，`dotnet run`），客户端只上报输入和开火意图，服务器 **60Hz** 权威模拟位置/血量/命中/死亡重生/计分，按客户端全量快照，客户端本地预测 + 误差校正 + 远端插值。
- **简化移动**：C# 服务器跑不了 CharacterController → PVP 用 `PvPMotor`（客户端）与 `GameWorld.SimulatePlayer`（服务器）同一简化数学（无墙碰撞，掩体纯视觉）。PVE 的 CharacterController/状态机完全不动。
- **PVP 屏蔽 AI 队友**：每客户端只控 1 角色（荧/芙宁娜），`PlayerModel.disableStateMachine=true` + NavMeshAgent 禁用，位移由 PvPMotor 驱动 transform。
- **入口**：主菜单「在线」→ PVPLobbyUI（IP/角色选择）→ 加载 `PVPGame` 场景（`Tools/玩家/搭建 PVP 场景` 一键搭）。双人测试：服务器 `dotnet run` + 2 个 Unity 客户端实例连 127.0.0.1:7777。
- 细节、协议、两端 DTO 对齐见 `Assets/Scripts/Network/AGENTS.md` 与 `Server/`。

## Known Issues / TODO

- [x] **已实现**：敌人 AI（寻路追击/攻击闭环/受击/血条/死亡）、子弹碰撞伤害、空中移动控制、玩家/敌人血量系统、完整死亡流程（随从拦截/主控接管/全灭 GAME OVER）、主菜单 UI、玩家/敌人血条
- [x] **Hunter 无武器化（2026-08-17）**：移除武器 + 全部 IK 约束（只动 Hunter），瞄准状态/动画保留；还原点已删（Git 接管）
- [ ] 玩家 Animator 无 "Hit"/"Dead" clip——受击/死亡动画名默认留空，跳过动画靠相机震动 + 血条归零兜底，死亡有 LogWarning
- [ ] 无复活流程
- [ ] `PlayerModel.Update()` 为空——`OnAnimatorMove` 依赖 Animator，Animator 缺失时无兜底
- [ ] 落地检测维持现状（起飞 `IsHover()` + 落地 `cc.isGrounded` 不对称是**有意设计**，暂不改成 `!IsHover()`）
- [ ] 无武器切换/弹药/换弹系统
- [ ] 无 HUD 弹药/准星/伤害数字/暂停菜单
- [ ] 无音频系统
- [ ] 主菜单「在线/继续/读取/角色/设置」等按钮为占位（弹提示菜单），仅「开始新游戏」「退出」可用
- [ ] `PlayerController` 职责过重（输入+相机+IK+角色切换+死亡接管），技术债待拆分为 InputHandler/CameraManager/IKManager
- [x] ~~Hunter 出生点位置、右手武器姿态微调（`AimOffsetTuner`）~~ 已随无武器化移除（08-17）；再装备武器时按还原点恢复
- [ ] `Random.Range` 跳跃三选一只在 `PlayerHoverState.Enter` 生效；换跳跃 clip 前需隔离验证是否让模型离地/入地（2026-08-12 教训）
