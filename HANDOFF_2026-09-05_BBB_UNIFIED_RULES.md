# ElementWar 交接：BBB 统一规则桥接（2026-09-05）

## 1. 继续任务前必须先读的结论

当前目标不是把 PVE、PVP 合并成同一个移动实现，而是建立唯一的玩法规则来源，并让两端保留各自必要的适配层：

```text
GameRules.v1.json（唯一数值规则来源，含版本与哈希）
  -> BBB 统一角色状态/规则运行时
     -> PVE Adapter：CharacterController、PVE AI、PVE 武器及表现
     -> PVP Adapter：PvPMotor、输入帧、服务器权威模拟、快照/预测
```

- BBB 的职责：统一角色状态语义与运行时规则入口；动画、混合器、Avatar Mask、装备等仍是表现配置。
- PVE/PVP 的职责：各自实现运动和网络边界。PVP 服务器仍是权威，不能把客户端 BBB 变成服务器的真相来源。
- `Assets/Scenes/TextScene/Single Framework.unity` 仅是 BBB 隔离验证场，不是 PVE/PVP 迁移完成的证据。

## 2. 当前真实基线

必须在真实项目根目录工作：

```text
E:\Unity product\ElementWar
```

不要在下面的 Codex worktree 修改或提交；它仍落后主线：

```text
C:\Users\wxm\.codex\worktrees\0886\ElementWar
```

2026-09-05 审计时，真实根目录：

- `HEAD == origin/master == 8cbb766195313c6adfeb38f788eaa3a979a3e796`
- 最新提交：`8cbb766 feat: 完成 BBB Ming 隔离测试配置`
- 前一提交：`7cec174 feat: 接入共享规则与 BBB Ming 测试资源`
- Unity 版本：`2022.3.62f3c1`
- 共享规则：`Assets/StreamingAssets/Rules/GameRules.v1.json`
- 规则标识：`elementwar-baseline-2026-09-04`
- 当时规则 SHA-256：`a557c446e4339d37a1f358bbba52be237a362414e3f34d3c2d08058b424491d0`

本轮尚未提交的工作区状态（交接文档生成时）：

```text
M  Assets/BBBWork/BBBNexus/Character/BBBCharacterController.cs
?? Assets/BBBWork/BBBNexus/Character/Rules.meta
?? Assets/BBBWork/BBBNexus/Character/Rules/BBBRuleProfileAdapter.cs
?? Assets/BBBWork/BBBNexus/Character/Rules/BBBRuleProfileAdapter.cs.meta
?? HANDOFF_2026-09-05_BBB_UNIFIED_RULES.md
```

`Rules.meta` 与脚本 `.meta` 是 Unity 在识别新脚本目录后自动生成的，提交本轮代码时应一并保留。

## 3. 已完成、已提交并验收的内容

### BBB Ming 隔离场

用户已在 `Single Framework.unity` 手动配置并验收：

- `BBB_Ming` 的 Animator、Animancer、输入源、动画门面、IK/Audio 引用已连通。
- 删除了该隔离场中确定废弃的 Hunter 与旧相机对象。
- CameraRig 已完成跟随和视角配置。
- 胶囊体已按角色脚底校正，解决初始悬空/腾空问题。
- 已验收：待机、行走、跑步、相机跟随、瞄准移动、Shift + 瞄准移动。

已知但不阻塞隔离主链路的日志：

1. `PlayerBrainSO 中未配置上半身状态`：目前 Upper Body 状态表尚未配置。
2. `Run` 动画事件 `SwitchSocket` 没有 receiver：是资源动画事件遗留，当前无装备 Socket 处理器。

此前阻塞错误 `ClipTransition.Clip is null` 已通过 `PlayerMoveStartState` 的方向起步动画回退逻辑修复并随 `8cbb766` 提交。

### PVP 单一规则来源基础

已提交的 `GameRules.v1.json`、DTO、校验器和哈希链路，已由 PVP 客户端与 .NET UDP 服务器读取；握手严格校验 `rulesetId + schemaVersion + SHA-256`。服务器不得对规则不一致静默降级。

服务器命令：

```powershell
dotnet run --project Server/ElementWarServer.csproj --no-restore -- --self-test
```

本轮执行结果：

```text
[Rules] loaded ruleset=elementwar-baseline-2026-09-04 hash=a557c446e4339d37a1f358bbba52be237a362414e3f34d3c2d08058b424491d0
[SelfTest] 12/12 passed
```

注意：这只证明服务器规则、战斗与版本校验基线，不能替代 Unity Play Mode 验收。

## 4. 本轮未提交代码：BBB 规则桥接

### 变更文件

- `Assets/BBBWork/BBBNexus/Character/BBBCharacterController.cs`
- `Assets/BBBWork/BBBNexus/Character/Rules/BBBRuleProfileAdapter.cs`

### 行为

`BBBCharacterController.Awake()` 会在创建 `RuntimeData`、`MotionDriver` 和状态系统前：

1. 复制 `PlayerSO` 为 `HideFlags.DontSave` 的运行时实例。
2. 仅复制会被玩法规则覆盖的三个数值模块：`CoreSO`、`JumpSO`、`AimingSO`。
3. 从 `GameRulesRuntimeLoader` 读取 `GameRules.v1.json` 并默认应用 `pve` profile。
4. 输出一次日志：

   ```text
   [Rules] BBB 已应用 elementwar-baseline-2026-09-04/pve，hash=...
   ```

这不会写回 Inspector、PlayerSO 资产、Ming 配置资产、动画资源或场景。

### 当前已映射的明确字段

| GameRules 字段 | BBB 运行时字段 |
| --- | --- |
| `life.maxHealth` | `CoreSO.MaxHealth` |
| `movement.gravity` | `CoreSO.Gravity` |
| `movement.walkSpeed` | `CoreSO.WalkSpeed` |
| `movement.jogSpeed` | `CoreSO.JogSpeed` |
| `movement.sprintSpeed` | `CoreSO.SprintSpeed` |
| `movement.aimMoveSpeed` | `AimingSO.AimWalkSpeed`、`AimJogSpeed`、`AimSprintSpeed` |
| `movement.jumpVelocity` | `JumpSO.JumpForce`、`JumpForceWalk`、`JumpForceSprint` |

`aimMoveSpeed` 当前在共享规则中只有一个值，因此 BBB 的三档瞄准移动统一取该值；混合器只决定动画表现，不改变规则数值。

### 明确没有映射的字段

这些字段暂不应伪造映射，必须在对应适配器存在后再接入：

- `movement.rotationSpeedDeg`：BBB 当前使用的是 `RotationSmoothTime`，二者不是同一物理量。
- slide 四项：BBB 尚未确认存在与 PVE 滑铲一一等价的运行时模块。
- 武器全部字段：BBB 装备/武器模块尚未成为当前 PVE/PVP 共用武器适配器。
- spawn、PVP 命中胶囊和重生：继续由 PVP 服务器权威实现。

### 为 PVP 预留的入口

`BBBCharacterController.TryApplyRulesBeforeBoot(...)` 可由未来 PVP 适配器在 `Start` 之前传入经读取与校验的 `document.pvp_1v1`。

- 若 PVP 适配器在 BBB `Awake` 前已注入 PVP profile，`ApplyDefaultPveRules()` 会检测 `AppliedRuleProfile` 并不再覆盖它。
- 若 PVP 适配器在 BBB `Awake` 后、所有 `Start` 前注入，也可覆盖默认 PVE profile；已创建的 `RuntimeData.CurrentHealth` 会同步更新。
- BBB 一旦 `_booted`，该入口拒绝更换规则，避免半运行态的规则突变。

## 5. 现在必须由用户完成的 Unity 验证

这是 GUI/Play Mode 边界，Codex 不修改 Scene、Prefab、Inspector、输入资产或资源配置。

1. 在 Unity 打开 `Assets/Scenes/TextScene/Single Framework.unity`。
2. 点击 Play。
3. Console 应出现一次 `[Rules] BBB 已应用 elementwar-baseline-2026-09-04/pve，hash=...`。
4. 不应出现新的编译错误或新的红色规则桥接异常。
5. 复测：待机、走、跑、瞄准走、Shift + 瞄准走、相机跟随。
6. 将 Console 的新增日志和行为结果发给下一位 agent。

如果出现 `PlayerSO 缺少 Core、JumpAndLanding 或 Aiming 模块`，不要改代码或资产；先截取 BBB_Ming Inspector 和 Console，确认是哪一个引用为空。

## 6. 后续推荐顺序

### Phase A：收口本轮桥接

仅在用户完成上面的 Play Mode 验收后：

1. 审查 `git diff` 与所有 `.meta`。
2. 再跑服务器 `--self-test`。
3. 提交并 push 本轮桥接（只有用户明确要求时执行）。

### Phase B：PVP BBB 适配器（不能改服务器权威）

先设计再编码：

1. 在 PVP 客户端为未来 BBB 角色创建一个代码适配器，读取与 `NetClient` 相同的已验证规则文档。
2. 在 BBB Boot 前调用 `TryApplyRulesBeforeBoot(document.pvp_1v1, ...)`。
3. 保留 `PvPMotor` 与 `GameWorld.SimulatePlayer` 的同构数学；服务器不依赖 Unity BBB，也不依赖客户端状态机。
4. 先补纯 C# 映射/边界测试，再做两客户端 Unity 验收和篡改规则拒绝验收。

### Phase C：PVE 迁移（分段，不直接替换场景）

旧 PVE `PlayerModel`/`PlayerController`/`PlayerWeapon` 仍保留大量 Inspector 数值，是当前最大的规则偏移来源。迁移顺序：

1. 为旧 PVE 写 Adapter，从 `GameRules.pve` 读取，而非删代码或改 Inspector。
2. 先迁移生命、重力、走/跑/冲刺、跳跃等可一一对应字段。
3. 单独设计 rotation、slide、武器和动画表现的映射，不能把不同量纲硬塞进 BBB。
4. 用一个角色和隔离场验证后，再由用户执行 PVE Scene/Prefab GUI 接入。

不要直接把 `GameRules` 塞进旧 `PlayerModel/PlayerWeapon` 当作最终架构；那会继续保留 BBB 与旧 PVE 两套角色栈。临时 Adapter 只能是迁移步骤。

## 7. 强制边界与常见坑

- 仅改代码。不得通过代码、Editor Wizard、AssetDatabase、序列化或其他间接方式改 Scene、Prefab、模型、Animator、Rig、输入资产、组件引用或 NavMesh。
- 每次改动前先在真实项目根目录执行 `git status --short`；保留所有用户未提交的资源变更。
- `MyInputSystem.cs` 为生成文件，不能手改。
- PVP 是 UDP 服务器权威：客户端只能预测和表现，规则不一致必须拒绝连接。
- 不要在 PVE/PVP 尚未迁移前宣称“BBB 已统一 PVE/PVP”；当前仅完成隔离场验证与第一段运行时规则桥接。
- 任何 Unity Play Mode 结果必须和服务器命令结果分开陈述，不能互相替代。

## 8. 建议新聊天的第一条指令

```text
先阅读 HANDOFF_2026-09-05_BBB_UNIFIED_RULES.md 与根目录 AGENTS.md。
不要修改任何 Scene、Prefab、Inspector、动画、模型、输入资产或其他 Unity 资源。
先核对真实根目录 E:\Unity product\ElementWar 的 git status、HEAD/origin/master 和本轮未提交桥接 diff；
然后根据用户提供的 Single Framework Play Mode 日志，先做诊断与验收，再决定是否提交，或开始 PVP BBB Adapter 的设计。
```
