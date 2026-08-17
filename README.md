# ElementWar

> **3D 第三人称射击游戏 (TPS) · Unity 2022.3.62f3 · Universal Render Pipeline**
>
> Version **0.1** — 三角色（荧/芙宁娜/暗夜猎人）/状态机/移动/瞄准射击/敌人/伤害/血条/UI 已可用，关卡与完整玩法待完善。

---

## 项目状态

```
玩家系统      ██████████░ 90%  三角色切换 / FPS 式移动 / 跑酷动画 / 冲刺 / 滑铲 / 跳跃 / 瞄准 / 射击 / 人机跟随 / 血量 / 死亡流程已可用
敌人系统      ███████░░░░ 65%  寻路追击 / 受击 / 血条 / 死亡 / 攻击闭环已实现，攻击动画/巡逻待完善
武器系统      ██████░░░░░ 55%  射击 / 碰撞 / 伤害 / 子弹对象池 / 特效对象池已实现，弹药 / 切换待开发
UI/HUD       ███████░░░░ 65%  主菜单 / 敌人+玩家血条 / GAME OVER / 特效文字已实现，HUD / 准星 / 暂停菜单待开发
音频          ░░░░░░░░░░  0%  未开始
```

---

## 暗夜猎人 Hunter（第三位玩家）

> 暗夜猎人最初在 `Assets/Scenes/New Scene.unity`（沙盒）做 FPS 玩法原型，2026-08-12 起**正式并入 Game 场景作为第三位可切换玩家**（数字键 `3`）。完整开发记录见 **[FPS_HUNTER_DEV.md](FPS_HUNTER_DEV.md)**。

**演化历程**：
- ✅ **FPS 原型（New Scene，08-10 前）**：`Assets/Scripts/FPS/` 框架（Idle/Move/Sprint/Air/Slide/Aim）+ CLazyRunner 混合树跑通；**KINEMATION 已彻底移除**（第一人称框架在第三人称下接管 Animator Playable Graph 与移动冲突，放弃），方向改为复刻 Game 场景已验证的 TPS 流程
- ✅ **接入 Game（08-12）**：`Tools/玩家/把暗夜猎人加入 Game 玩家（一键）` 以 Furina 玩家预制体为模板生成 `Hunter.prefab`，**动作状态零改动**沿用 Game 状态机（Humanoid 动画自动重定向），注册为第三位玩家
- ✅ **移动统一 FPS 式（08-12）**：全部角色 `useFPSMovement=true` + 统一 `TPS_Movement.controller`（CLazyRunner 跑酷移动 + X Bot 瞄准走射）
- ✅ **Hunter 专属跑酷控制器（08-16）**：`Hunter_Parkour.controller`——移动换 CLazyRunner 跑酷包、**跳跃三选一随机**（`randomJumpClips`）、瞄准走射保留 X Bot
- ✅ **手部握枪改内置 OnAnimatorIK（08-16）**：Animation Rigging 的 TwoBoneIK 对 Hunter 的 MMD 骨骼链不生效（详见已知问题 #30），改用 `HunterHandIK` 组件（复用原 TwoBoneIK target，`OnAnimatorIK` 驱动双手握枪）
- ✅ **枪口朝上已修复（08-16）**：`Tools/玩家/修复瞄准轴` 按枪管方向重算 `aimAxis` + `offset`（MultiAim 枚举陷阱，详见已知问题 #29）
- ✅ **无武器化（08-17）**：移除 Hunter 武器 + 全部 Animation Rigging IK 约束（只动 Hunter），**瞄准状态/动画保留**；还原点已删（Git 接管）

---

## 快速开始

1. 用 **Unity Hub** 打开本目录，使用 **Unity 2022.3.62f3** 版本
2. 打开 `Assets/Scenes/GameStart.unity`（主菜单）或直接进入 `Assets/Scenes/Game.unity`（战斗场景）
3. 主菜单点「开始新游戏」→ 加载 `Game` 场景；点击 Play 进入运行模式
4. 操作方式：

| 按键 | 功能 |
|------|------|
| WASD | 移动 |
| Left Shift | 冲刺 |
| 鼠标右键（按住） | 瞄准 |
| 鼠标左键 | 开火（自动进入瞄准视角） |
| Space | 跳跃 |
| C | 滑铲（跑动/冲刺中按下） |
| `1` / `2` / `3` | 切换角色（荧 / 芙宁娜 / 暗夜猎人） |
| 鼠标移动 | 视角旋转（Cinemachine 自动处理）+ 头部跟随 |
| 主菜单按钮 | 悬停发光 / 鼠标靠近自动躲避 |

> ⚠️ 主菜单中「在线 / 继续 / 读取 / 角色 / 服装 / 设置 / 成就 / 作者 / 语言 / 语音」按钮目前均弹出「提示菜单」（占位）；仅「开始新游戏」「退出」可用。

---

## 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 引擎 | Unity | 2022.3.62f3 |
| 渲染 | URP | 14.0.12 |
| 输入 | New Input System | 1.14.2 |
| 相机 | Cinemachine | 2.10.7 |
| 动画 IK | Animation Rigging | 1.2.1 |
| 寻路 | AI Navigation (NavMesh) | 1.1.7 |
| 关卡 | ProBuilder | 5.2.4 |
| 文本 | TextMeshPro | 3.0.7 |
| 渲染风格 | YSA Toon（卡通渲染） | 插件 |
| 特效 | EffectCore（粒子特效） | 插件 |
| 动作动画 | CLazyRunnerActionAnimPack（跑酷动作包） | 插件 |
| 模型导入 | MMD4Mecanim（PMX→FBX） | 插件 |

---

## 架构总览

### 核心设计模式

```
┌──────────────────────────────────────────────────────────────┐
│                      设计模式应用                              │
│                                                               │
│  SingleMonoBase<T> ─── 泛型单例（MonoManager, UIManager,      │
│                         PlayerController, GameManager）       │
│                                                               │
│  StateBase ─── 状态模式（Idle/Move/Hover/Aiming）              │
│      └── PlayerStateBase（重力 + 瞄准监听 + 跳跃 + 人机判断）   │
│      └── EnemyStateBase（敌人状态基类 + 动画完成判断）          │
│                                                               │
│  StateMechaine ─── 泛型状态机（Type → StateBase 缓存池）       │
│                                                               │
│  MonoManager ─── 集中式 Update（Action 委托链代替多 MB Update） │
│                                                               │
│  UIBase<T> ─── UI 基类（FadeIn/FadeOut + 按钮禁用/恢复）       │
│                                                               │
│  IStateMachineOwner ─── 标记接口（PlayerModel / EnemyBase）    │
└──────────────────────────────────────────────────────────────┘
```

### 系统分层

```
┌─────────────────────────────────────────────────────────┐
│                    输入层 (Input)                         │
│   MyInputSystem (New Input System 封装)                  │
│   PlayerController 轮询读取 Move/Sprint/Aim/Jump/Fire/1/2│
└───────────────────────┬─────────────────────────────────┘
                        │ moveInput / isAiming / isFire ...
                        ▼
┌─────────────────────────────────────────────────────────┐
│                    逻辑层 (Logic)                         │
│   StateMachine → PlayerStateBase / EnemyStateBase        │
│   状态转换 + 行为逻辑（玩家 / 敌人 / 人机）               │
│   GameManager 持有 PlayerModel[]（多角色）                │
└───────────────────────┬─────────────────────────────────┘
                        │ verticalSpeed / moveInput ...
                        ▼
┌─────────────────────────────────────────────────────────┐
│                    表现层 (Presentation)                  │
│   Animator (Root Motion) + CharacterController           │
│   Cinemachine FreeLook (双相机)                          │
│   Animation Rigging (IK 约束 + 头部 IK)                  │
│   UI (uGUI + TextMeshPro + WorldSpace 血条)              │
│   VFX (EffectCore 粒子)                                  │
└─────────────────────────────────────────────────────────┘
```

### 玩家状态流转

```
Idle ←→ Move ←→ Sprint （FPS 式移动；Move 按住 Shift 进 Sprint，松开回 Move）
Idle/Move/Sprint → Hover （跳跃 或 跌落超过 fallHeight 阈值）
Move/Sprint → Slide （跑动/冲刺中按 C；播完回 Move）
Hover → Idle （落地）
任意地面状态 → Aiming （右键按住 或 开火中）
Aiming → Idle （松开右键且未开火）
```

> **FPS 式移动（2026-08-12 起，已验证可用）**：全部角色 `PlayerModel.useFPSMovement=true`，普通移动改用 New Scene 验证过的代码驱动方案——`Speed/VerticalSpeed` 混合树（CLazyRunner 跑酷系片段）+ `PlayerModel.LateUpdate` 的 `cc.Move` 位移；瞄准走射保留 X Bot（`AimingX/AimingY` 2D 混合树）。控制器统一为 `TPS_Movement.controller`（菜单 `Tools/玩家/生成 TPS 移动控制器` 生成，`Tools/玩家/应用/还原 TPS 移动` 批量切 3 个角色预制体）；旧 root motion 方案可随时 `useFPSMovement=false` 回退。**注意：FPS 式下 `applyRootMotion` 保持 `true`**——Animation Rigging 约束依赖它求值，根运动位移由 `OnAnimatorMove` 拦截丢弃，位移仍由 `cc.Move` 驱动（详见已知问题 #27）。

> **Hunter 专属跑酷（2026-08-16 起）**：Hunter 使用独立控制器 `Hunter_Parkour.controller`（`Tools/玩家/给 Hunter 应用跑酷动作包`），移动换 CLazyRunner 跑酷包、跳跃在 3 个跑酷空中片段中**随机三选一**（`PlayerModel.randomJumpClips` + `HoverClip` 参数，仅 Hunter 启用），瞄准走射仍走 X Bot 树。

> **人机模式**：非当前控制角色（`IsBeControl() == false`）不走玩家输入逻辑，而是在 Idle/Move 之间按**与当前控制角色的距离**切换，用 NavMeshAgent 自动跟随/待机（见 `PlayerIdleState` / `PlayerMoveState` 的 else 分支）。

### 敌人状态流转（丘丘人）

```
Idle → Move（目标不在攻击范围）→ chaseTarget() 追击
   ↑      │
   │      └── 进入攻击范围 → Attack（播攻击动画 → 动画播完对玩家造成伤害 → 进入冷却）→ Idle
   └──────────────────────────（Idle 重判距离与冷却；Dead 由受击死亡触发）
```

> 目标选择：`FIndAttackTarget()` 排除已死亡玩家，每 `attackTargetRefreshInterval=0.5s` 定期刷新，始终追击**最近的存活角色**。

---

## 场景结构

| 场景 | 用途 | 关键内容 |
|------|------|---------|
| `Assets/Scenes/GameStart.unity` | 主菜单 | MainMenuUI（12 按钮）、TipMenuUI（提示）、ExitMenuUI（退出确认）、ExcludeMouse、TMPGlowControl、EventSystem |
| `Assets/Scenes/Game.unity` | 战斗关卡 | 三角色（荧 Lumine / 芙宁娜 Furina / 暗夜猎人 Hunter）、丘丘人敌人、HealthBar（敌人+玩家复用）、gallardo 跑车（装饰）、NavMesh 烘焙（NavMeshSurface）、AimTarget。⚠️ 08-17 起全部 prefab 实例已 **Unpack**（`Tools/场景/解开全部预制体`），场景对象脱离 prefab 可自由编辑，改场景不回写 prefab |
| `Assets/Scenes/New Scene.unity` | FPS 沙盒 | 暗夜猎人 FPS 原型（`Assets/Scripts/FPS/` 框架），移动/跑酷/滑铲/瞄准验证用；已并入 Game 后保留作实验场 |

> `EditorBuildSettings` 已清理残留 `SampleScene`，仅保留 Game + GameStart。

---

## 目录结构

```
ElementWar/
├── README.md                          # 本文件
├── PROJECT_NOTES.md                   # 复习笔记（介绍/流程/技术点/面试题/Bug）★
├── FPS_HUNTER_DEV.md                  # 暗夜猎人 FPS 开发日志（New Scene 原型 → 并入 Game）★
├── ElementWar项目文档.md                # 面试复盘版项目文档
├── CLAUDE.md                          # Claude Code AI 辅助开发指南
├── Assets/
│   ├── Scripts/
│   │   ├── README.md                  # 脚本系统详细技术文档 ★
│   │   ├── Base/                      # 基类
│   │   │   ├── SingleMonoBase.cs      #   泛型单例
│   │   │   ├── StateBase.cs           #   状态抽象
│   │   │   ├── PlayerStateBase.cs     #   玩家状态基类（重力+瞄准+跳跃+人机）
│   │   │   ├── EnemyStateBase.cs      #   敌人状态基类
│   │   │   ├── EnemyBase.cs           #   敌人基类（寻路+受击+血条+目标）
│   │   │   └── UIBase.cs              #   UI 基类（FadeIn/FadeOut + 按钮控制）
│   │   ├── Utils/
│   │   │   ├── StateMachine.cs        #   泛型状态机
│   │   │   ├── HeadAimTarget.cs       #   头部 IK 跟随目标
│   │   │   └── EffectPool.cs          #   通用特效对象池（按预制体分池）
│   │   ├── Manager/
│   │   │   ├── MonoManager.cs         #   集中式 Update 管理器
│   │   │   ├── GameManager.cs         #   全局管理器（PlayerModel[]）
│   │   │   └── UIManager.cs           #   UI 管理器（WorldSpaceCanvas）
│   │   ├── Player/
│   │   │   ├── PlayerController.cs    #   输入+相机+IK+角色切换+死亡接管
│   │   │   ├── PlayerModel.cs         #   角色模型+移动+状态+人机+血量/死亡
│   │   │   ├── PlayerWeapon.cs        #   武器（射击+火花+子弹对象池，荧/芙宁娜用）
│   │   │   ├── PlayerWeaponBullet.cs  #   子弹（飞行+碰撞+命中特效）
│   │   │   └── State/                 #   玩家状态实现（Idle/Move/Hover/Aiming/Slide/Sprint）
│   │   ├── Enemy/
│   │   │   ├── ZombieEnemy.cs         #   丘丘人敌人（SwitchState 分发）
│   │   │   └── State/                 #   敌人状态实现
│   │   ├── FPS/                       #   暗夜猎人 FPS 原型框架（New Scene 沙盒用）★
│   │   │   ├── Core/                  #   FPSModel / FPSStateBase / FPSEnums
│   │   │   ├── Controller/            #   FPSController（相机接管 + 输入）
│   │   │   ├── State/                 #   Idle/Move/Sprint/Air/Slide/Aim 状态
│   │   │   ├── Aim/                   #   HipFire / Shoulder / Ads 瞄准模式
│   │   │   └── Editor/                #   控制器生成 / 场景清理 / 相机修复向导
│   │   ├── Diagnostics/               #   运行时诊断工具 ★
│   │   │   ├── GroundDiag.cs          #   角色接地/Animator controller 检查
│   │   │   └── FollowerDiag.cs        #   人机跟随诊断
│   │   │   ├── Editor/                    #   编辑器向导（Tools/玩家 + Tools/场景）★
│   │   │   ├── FixNavMeshAndGroundWizard.cs       #   ⭐ 修 NavMesh（移除 ignoreFromBuild + 抬地面 + 重烘焙 + 角色吸附）★
│   │   │   ├── NavMeshLayerDiagWizard.cs          #   诊断 NavMesh 层配置（写日志文件）★
│   │   │   ├── UnpackPrefabsInSceneWizard.cs     #   解开场景全部预制体（脱离 prefab，自由编辑）★
│   │   │   ├── SnapToFloorWizard.cs               #   批量把选中物体落到地面（吸附到表面）★
│   │   │   ├── NavMeshObstacleWizard.cs           #   给选中物体添加/移除 NavMeshObstacle（阻挡寻路）★
│   │   │   ├── BakeWalkableWizard.cs              #   把选中物体烘焙为可行走（斜坡/楼梯）并重烘焙★
│   │   │   ├── RedWolfRoseFBXFixer.cs             #   FBX 模型导入修复
│   │   │   └── SetupRunningSlide.cs               #   滑铲动画配置
│   │   └── UI/
│   │       ├── MainMenuUI.cs          #   主菜单
│   │       ├── TipMenuUI.cs           #   提示菜单
│   │       ├── ExitMenuUI.cs          #   退出确认菜单
│   │       ├── EnemyHealthBarUI.cs    #   血条（Billboard，敌人+玩家复用）
│   │       ├── PlayerHealthBar.cs     #   玩家血条（复用 HealthBar 预制体）
│   │       ├── GameOverUI.cs          #   游戏结束（GAME OVER + 任意键回主菜单）
│   │       ├── ExcludeMouse.cs        #   按钮躲避鼠标（有界位置插值）
│   │       └── TMPGlowControl.cs      #   文字发光/阴影
│   ├── Scenes/
│   │   ├── Game.unity                 # 战斗场景（含 NavMesh）
│   │   ├── GameStart.unity            # 主菜单场景
│   │   └── New Scene.unity            # FPS 沙盒（暗夜猎人原型实验场）
│   ├── Settings/
│   │   ├── InputSystem/               # 输入配置（MyInputSystem.inputactions + 生成 .cs）
│   │   └── URP-*.asset                # 3 档画质配置
│   ├── Resource/
│   │   ├── Models/                    # Lumine/Furina/丘丘人/暗夜猎人/gallardo 等模型
│   │   ├── Animations/Player/         # TPS_Movement.controller / Hunter_Parkour.controller
│   │   ├── Prefabs/                   # 角色预制体（Lumine/Furina/Hunter.prefab）、敌人/血条/UI 按钮
│   │   └── Effects/                   # EffectCore 粒子特效（子弹/血液）
│   └── Plugins/
│       ├── YSA Toon/                  # 卡通渲染 Shader
│       ├── EffectCore/                # 粒子特效
│       └── MMD4Mecanim/               # MMD 模型导入工具
├── Packages/
│   └── manifest.json                  # Unity Package 依赖清单（含 com.unity.ai.navigation 1.1.7）
└── ProjectSettings/                   # Unity 项目设置
```

---

## 核心系统详解

### 🎮 输入系统

- 基于 Unity **New Input System**，轮询模式（`PlayerController.Update()` 每帧读取）
- 支持键鼠 + 手柄 + 触屏 + XR 四种绑定方案
- Action 一览：`Move`(WASD/方向键/左摇杆)、`Look`(鼠标/右摇杆，**已定义未使用**)、`Fire`(左键)、`IsSprint`(Shift)、`IsAiming`(右键)、`IsJumping`(空格)、`First/Second/Third`(数字 1/2/3 → 切换角色)
- ⚠️ `Look` 输入已映射但代码未读取，相机旋转完全由 Cinemachine FreeLook 处理；鼠标 delta 另用于头部 IK（`HeadAimTarget`）

### 🧠 状态机框架

- **泛型状态机** `StateMechaine`：`Dictionary<Type, StateBase>` 缓存状态实例，避免重复 new / GC
- **防重入**：同类型状态跳过 Exit/Enter 循环
- **生命周期**：`Init (首次) → Enter/Exit (每次切换) → Update (每帧) → Destory (销毁)`
- **集中式 Update**：状态 `Enter()` 注册 / `Exit()` 注销到 `MonoManager` 委托链

### 🏃 玩家移动

- **FPS 式代码驱动（当前，`useFPSMovement=true`）**：`applyRootMotion` **保持 `true`**（Animation Rigging 约束依赖它求值），但根运动被 `OnAnimatorMove` 拦截丢弃（`useFPSMovement` 时直接 return）；位移由 `PlayerModel.LateUpdate` 的 `cc.Move(horizontalVelocity + verticalSpeed)` 驱动。`Speed` 混合树参数（0 Idle / 0.33 Walk / 0.66 Jog / 1.0 Dash）由 `LerpSpeedTo` 平滑逼近，`horizontalVelocity = worldMovement * GetMoveSpeed(speedBlend)`；`VerticalSpeed` 驱动空中升/降（`Hover` 为 Air 混合树）。动画片段来自 CLazyRunner 跑酷动作包（统一 `TPS_Movement.controller`）。
- **Hunter 专属跑酷（`Hunter_Parkour.controller`）**：Hunter 移动动画换 CLazyRunner 跑酷包（Idle/Walk/Jog/Dash + RunningSlide + Aim 不变），跳跃走 `HoverJump` 混合树——`HoverClip` 参数（0/1/2）在 3 个跑酷空中片段间**随机三选一**（`PlayerModel.randomJumpClips`，仅 Hunter 启用）
- **旧 root motion 方案（`useFPSMovement=false` 回退）**：水平移动 Animator Root Motion 驱动（`animator.deltaPosition`），`MoveBlend` 0（走）↔ 1（冲刺）Lerp 平滑
- **垂直移动**：手动重力计算叠加到 y 分量
- **相机相对方向**：`worldMovement = cameraForward(投影) * input.y + camera.right * input.x`
- **跳跃动量保持**：3 帧滑动窗口缓存 `animator.velocity`，悬空时用平均速度维持惯性
- **地面检测**：`IsHover()` 用 **SphereCast**（CC 真实底部 + 半径 `cc.radius*0.6`），配合 `HOVER_STABILITY_FRAMES=5` 稳定性窗口 + 窗口期重力锁定 `-2f`

### 🔀 多角色切换与人机系统

- **角色列表**：`GameManager.playerModels[]` 挂接所有角色预制体（荧 Lumine / 芙宁娜 Furina / 暗夜猎人 Hunter）
- **切换**：按 `1/2/3` → `PlayerController.SwitchPlayerModel(index)`：
  - 旧角色 `Exit()`：位置校正到最近 NavMesh 点后启用 NavMeshAgent + `SwitchState(Idle)`（转为**人机跟随**）
  - 新角色 `Enter()`：禁用 NavMeshAgent + 相机 Follow/LookAt 重置
- **人机 AI**：非控制角色 Idle/Move 状态按**与当前控制角色的距离**切换，超过 `stoppingDistance=2` 则用 NavMeshAgent 跟随，接近则待机
- **三角队形**：`PlayerModel.GetFollowerTargetPosition(spacing=2.5, spread=35)` 让随从呈**倒三角**——主控在最前，随从在主控后方两侧对称展开（索引 1 → 左后、索引 2 → 右后），人机跟随目标是各自偏移点而非主控本体
- **⚠️ 人机位移归 NavMeshAgent 独占**：`PlayerModel.LateUpdate` / `OnAnimatorMove` 在主控时才 `cc.Move`，人机不调用——否则 CharacterController 每帧改 transform 与 NavMeshAgent 抢位置，寻路被干扰导致随从原地不动（动画照播）

### 🎯 瞄准与射击

- **双 Cinemachine FreeLook**：`freeLookCamera`(Priority 100) ↔ `aimingCamera`(Priority 0)，EnterAim/ExitAim 双向同步角度并交换 Priority
- **IK 约束**：瞄准时 `MultiAimConstraint`（右手+身体）权重=1、`TwoBoneIKConstraint`=0；退出时反向恢复
- **Hunter 手部例外（已随无武器化移除，08-17）**：Hunter 不再持枪，`HunterHandIK`（OnAnimatorIK 握枪）与武器 IK 约束已全部移除；瞄准仅剩相机 + `PlayerAimingState` + X Bot 瞄准动画（`PlayerModel.EnterAim/ExitAim` 已加 null 保护，不 NRE）
- **头部 IK**：`HeadAimTarget` 驱动角色头部跟随鼠标（可左/右/上/后偏移，禁止向前），松手自动回中
- **瞄准目标**：屏幕中心 `ViewportPointToRay(0.5, 0.5)` 射线，命中即更新 `AimTarget.position`（mask=247，含 Enemy 层可锁敌，仍排除 Player 层）
- **射击**：`PlayerWeapon.Fire()` 射速 0.15s/发（≈6.67 发/秒），子弹走对象池 + 枪口火花走特效池；`CinemachineImpulseListener` 挂两架虚拟相机 → 射击/受击均屏幕抖动
- **子弹**：Rigidbody 飞行（`flyPower=30`）+ **帧间 Raycast 防穿透**；命中任何碰撞体播命中特效并回池，命中 `Enemy` Tag 且组件非空时调用 `Hurt()`

### 👹 敌人系统（丘丘人）

- **预制体**：`丘丘人.prefab`（根挂 `ZombieEnemy` + `NavMeshAgent` + `BoxCollider`，Tag=`Enemy`，Layer 7=Enemy，嵌套 `丘丘人.fbx` 模型）
- **寻路追击**：`chaseTarget()` → `NavMeshAgent.SetDestination()`（先 `isOnNavMesh` 校验）
- **目标选择**：`FIndAttackTarget()` 遍历 `GameManager.playerModels[]` 找最近玩家，排除已死亡；每 `attackTargetRefreshInterval=0.5s` 定期刷新，始终打**最近的存活角色**
- **攻击闭环**：进入攻击范围 → `ZombieAttackState` 播 Attack 动画 → 动画播完对 `attackTarget.TakeDamage(attackDamage=10)` → 记录 `lastAttackTime` 进入 `attackCooldown=1.5s` 冷却 → Idle 重判距离
- **受击 `Hurt()`**：受击动画 → 减速移动（0.5×，0.5s 恢复）→ 喷血/滴血特效（走 EffectPool）→ **扣血 `bullet.damage=10`**
- **血条**：受击后显示 6s，Billboard 面向相机，血量归零 → `SwitchState(Dead)`
- **死亡**：禁用 NavMeshAgent + BoxCollider（null 防御），销毁血条 → `ZombieDeadState` 播放 Dead 动画 → 动画播完 `Clear()` 销毁

### 🖥️ UI 系统

- **`UIBase<T>`**：抽象 UI 基类（泛型单例），`Enter()` 播 FadeIn、`Exit(action)` 播 FadeOut 后回调；进入动画期间 `DisableButtons()` 防误触，播完 `ResumeButtons()` 并停用 Animator（释放位置）；退出播完回调后 `SetActive(false)`（防止透明面板遮挡下层菜单）
- **`MainMenuUI`**：12 个按钮；「开始新游戏」→ `SceneManager.LoadScene("Game")`，「退出」→ `ExitMenuUI`，其余按钮 → `TipMenuUI` 占位
- **`TipMenuUI` / `ExitMenuUI`**：提示/退出确认弹窗，FadeIn/Out 切换
- **`ExcludeMouse`**：按钮躲避鼠标——**有界位置式插值**（期望位置 = 原位 ± `maxAvoidDistance`，`Lerp` 平滑跟随，鼠标移开自动回位），用 `WorldToScreenPoint` 算屏幕像素距离
- **`GameOverUI`**：全部角色死亡时动态挂载，GAME OVER 大字 + 任意键返回 GameStart（跳转前解锁光标）
- **`PlayerHealthBar`**：玩家血条，复用敌人 `HealthBar.prefab` + `EnemyHealthBarUI`，仅主控显示
- **`TMPGlowControl`**：鼠标悬停时文字发光（`_GlowPower` 淡入淡出）+ 阴影
- **`EnemyHealthBarUI`**：血条逻辑，每帧 `LookRotation(-dir)` 面向相机，`UpdateHealthBar(ratio)` 更新填充

### 🎯 暗夜猎人（Hunter）专题

> Hunter 由 MMD 模型（暗夜猎人.fbx，Humanoid）转制，加入 Game 后踩坑最多。核心结论汇总：

- ⚠️ **无武器化（08-17）**：Hunter 不再装备武器，武器 + 全部 Animation Rigging IK 约束（TwoBoneIK ×2、MultiAim ×2、RigBuilder、Rigs）已移除（**只动 Hunter**，荧/芙宁娜照常持枪）。**保留**：移动/跑酷、瞄准状态机（`PlayerAimingState`）、双瞄准相机、`Hunter_Parkour.controller`（X Bot 瞄准树 + HoverClip 随机跳跃 + IK Pass）。开火因 `weapon==null` 自然无动作；`PlayerModel.EnterAim/ExitAim` 已加 null 保护。一键清理工具：`Tools/玩家/移除 Hunter 武器与 IK 约束`；还原点已删（Git 接管）
- **以下武器 IK 教训对荧/芙宁娜仍然有效**（Hunter 的坑就是从它们身上踩出来的）：
  - **`applyRootMotion` 恒为 `true`（两套移动方案都是）**：Animation Rigging 约束依赖 root motion 才求值（Unity 已知问题，`applyRootMotion=false` 时约束失效）。FPS 代码驱动时根运动由 `OnAnimatorMove` 拦截丢弃，位移仍由 `LateUpdate` 的 `cc.Move` 驱动，不穿模
  - **枪口朝上根因是 `aimAxis` 枚举陷阱**：MultiAim 的 `m_AimAxis` 枚举 `X=0, X_NEG=1, Y=2, Y_NEG=3, Z=4, Z_NEG=5`；枪管在手腕局部空间方向决定选哪个轴，`offset = FromToRotation(barrelLocal, axisVec).eulerAngles`（offset 是**后置旋转**、右乘局部空间）
  - **改 MultiAim 数据必须用 `ref var d = ref constraint.data`**：`RigConstraint.data` 是 ref 返回属性，`var d = data` 会复制 struct，改副本无效（历次"向导看似成功却无效"的共性根因）
  - **`aimAxis` 是 `[NotKeyable]`**：job 创建时烘焙，运行期改完必须 `rigBuilder.Clear(); Build();` 重建才生效
  - **`Rigs` 容器必须在 Animator 骨骼树内**：约束不在骨骼流内则 Job 永不驱动（Hunter 曾踩过，现无武器用不到）
- **跑酷动画专属控制器**：`Hunter_Parkour.controller`（`Tools/玩家/给 Hunter 应用跑酷动作包`），避免改共享的 `TPS_Movement.controller` 波及荧/芙宁娜

---

## 特效对象池（EffectPool）

- 子弹对象池（`PlayerWeapon` Queue）与特效对象池（`EffectPool` 单例，按预制体分池）分离
- `EffectPool`：`Dictionary<预制体, Queue>`，`GetEffect(prefab,pos,rot)` 取出/实例化 → 播粒子 → `ParticleSystem.IsAlive()` 判断播完 → 自动停用回池；未挂载时自动创建单例
- 三处特效统一走池：**枪口火花**（`PlayerWeapon.Fire`）、**子弹命中特效**（`PlayerWeaponBullet.impactPrefab`，已配置为 `Bullet_SilverFlare_Small_Impact`）、**敌人受击喷血/滴血**（`EnemyBase.Hurt`）
- 消除射击/受击时每次 `Instantiate`/`Destroy` 的 GC 压力

## 已知问题与 Bug

> ✅ = 已修复；其余为待办/特性。

| # | 类别 | 问题 | 状态 |
|---|------|------|------|
| 1 | 敌人 AI | **Attack 状态永不触发**：Idle↔Move 死循环，`ZombieAttackState` 空壳 | ✅ 已修复：完整攻击闭环（进范围→攻击→伤害→冷却） |
| 2 | 瞄准 | **`aimLayerMask=119` 排除 Enemy 层**，准星无法锁敌 | ✅ 已修复：mask → 247（含 Enemy 层，仍排除 Player 层） |
| 3 | 配置 | **荧(Lumine) `fallHeight=0`**，一离地即判悬空 | ✅ 已修复：改回 0.2 |
| 4 | 敌人 | **`Hurt()` 硬编码 `GetComponent<BoxCollider>()`** | ✅ 已修复：缓存 `bodyCollider` + null 防御 |
| 5 | 移动 | **Hover 无法空中转向/移动** | ✅ 已修复：新增 `airControlSpeed=3` 空中水平控制 |
| 6 | UI | **`TMPGlowControl` 阴影被每帧关闭** | ✅ 已修复：移除 Update 中的 `DisableUnderlay()` |
| 7 | UI | **`UIBase._Exit` 不等动画播完** | ✅ 已修复：等待动画播完（2s 超时保护） |
| 8 | 性能 | **子弹用 Instantiate/Destroy**，GC 压力 | ✅ 已修复：子弹对象池（Queue 复用） |
| 9 | 健壮性 | **`PlayerModel.Awake` 依赖 `PlayerController.INSTANCE`** | ✅ 已修复：angularSpeed 赋值移至 Start |
| 10 | 架构 | **`PlayerController` 职责过重** | 待办（技术债，建议拆 InputHandler/CameraManager/IKManager） |
| 11 | 弹药 | **无弹药/换弹/武器切换** | 待办（路线图阶段三） |
| 12 | 音效 | **无音频系统** | 待办（路线图阶段三） |
| 13 | 存档 | **主菜单按钮多为占位** | 特性（在线/读取/角色/设置等仅弹提示菜单） |
| 14 | 构建 | **`EditorBuildSettings` 残留 SampleScene** | ✅ 已修复：已清理，仅保留 Game + GameStart |
| 15 | 运行时 | **`SetDestination` 报 "active agent / placed on NavMesh"** | ✅ 已修复：`PlayerMoveState` 人机跟随与 `EnemyBase.chaseTarget()` 在 `SetDestination` 前加 `isOnNavMesh` 校验（NavMeshAgent 刚启用时下一帧才落到网格） |
| 16 | 运行时 | **`NavMeshAgent` 启用报 "not close enough to the NavMesh"**（角色在未烘焙区切换） | ✅ 已修复：`PlayerModel.Exit()` 启用代理前 `NavMesh.SamplePosition` 校正位置到最近网格点 |
| 17 | 敌人 | **只锁定随从不攻击主控**（`FIndAttackTarget` 不排除死亡 + 只在 Start 调一次） | ✅ 已修复：排除 `isDead` 玩家 + 每 `attackTargetRefreshInterval=0.5s` 定期刷新，始终攻击最近的存活角色 |
| 18 | 反馈 | **射击/受击均无镜头抖动**（Impulse 无监听者） | ✅ 已修复：`PlayerController.Start()` 为两架 FreeLook 虚拟相机动态补 `CinemachineImpulseListener`（⚠️ 必须挂虚拟相机，挂 Main Camera 会报 "CinemachineExtension requires a virtual camera"） |
| 19 | HUD | **玩家血条**（初版代码构建的屏幕血条更新异常） | ✅ 已重做：改为**复用敌人 `HealthBar.prefab` + `EnemyHealthBarUI` 逻辑**（世界空间 BillBoard，跟随头顶），仅主控显示；`healthBarPrefab` 已写入两玩家预制体 |
| 20 | 死亡 | **死亡流程不完整**：主控死了无后续，随从死无法切换 | ✅ 已完善：随从死→不可访问；主控死→自动切换视角到下一位存活角色（按 1/2/3/4 数组顺序）；**全部死亡→弹出 GAME OVER 大字 UI，按任意键返回 GameStart 主菜单** |
| 21 | 光标 | **GAME OVER 返回主菜单后鼠标不见了**（`Cursor.lockState` 是跨场景静态状态，Game 中锁定后无解锁点） | ✅ 已修复：`GameOverUI` 跳转前解锁 + `MainMenuUI.Start()` 强制解锁（双保险），主菜单鼠标可点击 |
| 22 | UI | **主菜单按钮不高亮/点不动**（打开过 TipMenu/ExitMenu 后返回） | ✅ 已修复：`UIBase._Exit` 在 FadeOut 播完并执行回调后 `gameObject.SetActive(false)`——退出菜单必须停用，否则透明面板的 raycastTarget 会永久挡住下层主菜单按钮的鼠标射线 |
| 23 | UI | **退出按钮躲避鼠标只上下垂直，不左右** | ✅ 已修复：`FadeIn.anim` 动画化 Exit 按钮的 `m_AnchoredPosition.x`（未动画 y），而 MainMenu Animator 停在 FadeIn 状态每帧覆写 x → 水平位移被锁死。① `UIBase._Enter` FadeIn 播完后 `animator.enabled=false` 释放位置（Enter/Exit 播放动画前重新启用）；② `ExcludeMouse` 延迟到位置稳定后捕获回家位置（避免捕获动画中间值）+ 改用 `Mouse.current` 读鼠标 |
| 24 | UI | **躲避会跑出界面且不回位** | ✅ 已修复：旧实现用「受力累积」——躲避 `avoidForce×dt`（≈6.4px/帧只增不减）与回位 `returnForce×dt`（≈0.03px/帧）数量级严重失衡 → 鼠标靠近被推飞、移开回不来。改为**有界位置式插值**：期望位置 = 原位 ± `maxAvoidDistance`（≤120px，绝不跑出界面），`Lerp` 平滑跟随，鼠标移开自动回位 |
| 25 | UI | **躲避仍异常（一出场就飞/乱躲/鼠标远也躲）** | ✅ 已修复：`ScreenPointToLocalPointInRectangle` 在 Screen Space Camera 下距离换算异常 → 改用 `WorldToScreenPoint` 直接算屏幕像素距离；加**硬边界 clamp**（任何情况不偏离原位超 `maxAvoidDistance`）；回家位置延后到入场动画结束 + 0.8s 延迟后捕获；新增 Inspector 运行时诊断字段（diagDistance 等） |
| 26 | Hunter | **约束全部正确但拖 target 手臂不动**（`Could not resolve ... not a child Transform in the Animator hierarchy`） | ✅ 已修复（08-14）：RigBuilder 用 `animator.BindStreamProperty` 把约束 GameObject 绑定到骨骼流，Hunter 的 `Rigs` 容器不在模型骨骼树内 → Job 永不驱动。`FixHunterRigsInBoneTreeWizard` 把 `Rigs` 移到模型根骨 `暗夜猎人/174.!Root` 下 |
| 27 | Hunter | **`applyRootMotion=false` → Animation Rigging 约束根本不求值**（换 FPS 代码驱动后手/枪纹丝不动） | ✅ 已修复（08-16）：`PlayerModel.Awake` 改回 `animator.applyRootMotion=true`（FPS 分支），`OnAnimatorMove` 对 useFPSMovement 提前 return 丢弃根运动 → 约束恢复求值 + 位移仍由 LateUpdate `cc.Move` 驱动。**applyRootMotion 恒为 true（两套方案都是）** |
| 28 | Hunter | **换跑酷动画后持枪别扭**：右手 target 挂固定点 Rigs、左手 target 挂错、重复枪 ×2、MultiAim 源空 | ✅ 已修复（08-16）：`FixHunterWeaponRigWizard`——去重复武器、右手 target 重挂上胸骨(Chest)、左手 target 挂武器根（保持世界位置）、MultiAim `sourceObjects[0]` → 场景 AimTarget |
| 29 | Hunter | **瞄准时枪口朝上**（子弹仍打向 AimTarget） | ✅ 已修复（08-16）：MultiAim `m_AimAxis` 枚举陷阱——`Y=2`（不是 Z），Hunter 枪管在手腕局部≈+Z 被甩向上。`FixHunterAimAxisWizard` 按 `barrelLocal = constrained.InverseTransformPoint(muzzle)` 重算 `aimAxis` + `offset = FromToRotation(barrelLocal, axisVec).eulerAngles`（offset 是后置旋转、右乘局部空间） |
| 30 | Hunter | **TwoBoneIK 对 MMD 骨骼不生效**（换跑酷后手和枪别扭，job 有效但 tip 不贴 target） | ✅ 已修复（08-16）：`HunterHandIK` 改用 **Unity 内置 OnAnimatorIK**（复用原 TwoBoneIK target），TwoBoneIK 权重归 0 防打架；`ApplyHunterHandIKWizard` 挂场景 Hunter 实例 |
| 31 | 移动 | **跳跃换完整前跳序列后所有角色半身入地、动画异常**（`HoverStart`→`Hover`→`Land` 三段） | ✅ 已回退（08-12）：控制器/代码/GUID 引用静态核对全对，根因未定（疑为这些 clip 姿态带高度偏移，applyRootMotion=false 下与 CC 错位）。Hover 恢复 Air 混合树（Jump_Up↔Jump_Down）。教训：换跳跃 clip 前先在隔离环境验证不使模型离地/入地 |
| 32 | 运行时 | **半身入地真正根因 = 场景玩家实例 Animator controller 为 null**（二进制场景 + LoadPrefabContents 操作后引用脱节） | ✅ 已修复（08-12）：`FixAnimatorControllerWizard` 从预制体/资产恢复 controller 并保存场景。教训：换动画片段若入地不只查代码/控制器，先查运行时 `Animator.runtimeAnimatorController` 是否 null（GroundDiag） |
| 33 | NavMesh | **NavMesh 数据丢失 / "走上天" / 随从走不过来**（isOnNavMesh=false） | ✅ 已修复（08-16）：`RebakeNavMeshWizard` 增强版——`NavMesh.RemoveAllNavMeshData()` + 每个 NavMeshSurface 清空后重建；新增**诊断**（标出高/浮空物体）与**标记不可行走**（`NavMeshModifier(ignoreFromBuild)`）与**备份还原**（`_Backup_NavMesh_*`） |
| 34 | Hunter | **Hunter 武器已移除（08-17）**：无武器化，**瞄准状态/动画保留**（用户明确），#26~#30 的 Hunter 武器 IK 修复随之退役 | ✅ 已执行：`RemoveHunterWeaponWizard`（`Tools/玩家/移除 Hunter 武器与 IK 约束`）清武器 + 约束 + Rigs，只动 Hunter；`PlayerModel.EnterAim/ExitAim` 加 null 保护；还原点已删（Git 接管） |
| 35 | NavMesh | **重烘焙救不回来 + 角色 not close enough**：旧「标记可能走上天」工具（启发式：顶部离地>2.5m 或浮空>2m）把 1000×1000 地面 Ground 也误判浮空 → 全部 5 个环境物体被加 `NavMeshModifier(ignoreFromBuild)` → **烘焙 0 三角面** → 所有代理无法创建 | ✅ 已修复（08-17）：删旧 `RebakeNavMeshWizard`；新增 `FixNavMeshAndGroundWizard`（移除 ignoreFromBuild + 抬升地面到角色脚底 + 清空重建 + 角色吸附到网格，写日志）修复，烘焙 803 三角面、6 角色落回地面；诊断用 `NavMeshLayerDiagWizard`（写日志） |

### 本次修复新增的玩家系统能力

- **玩家生命值**：`PlayerModel.maxHealth=100` + `TakeDamage(int)`，受击相机震动（`shakeOnHit`），血量归零死亡（停移动/寻路/状态机）
- **玩家血条**：复用敌人 `HealthBar.prefab` + `EnemyHealthBarUI`（世界空间 BillBoard 跟随头顶），`PlayerModel.healthBarPrefab` 已写入玩家预制体（Hunter 由 Furina 模板克隆继承）；仅主控显示，`TakeDamage` 实时更新
- **敌人攻击闭环**：丘丘人进入攻击范围 → 播 Attack 动画 → 动画播完对玩家造成 `attackDamage=10` 伤害 → 进入 `attackCooldown=1.5s` 冷却 → 重判距离；并**定期刷新攻击目标**（打最近的存活角色）
- **完整死亡流程**：随从死 → 不可访问；主控死 → 自动切换下一位存活角色（`PlayerController.OnPlayerDied`）；全部死亡 → `GameOverUI` 弹出 GAME OVER，任意键返回 `GameStart`
- ✅ 死亡画面：全部角色死亡 → `GameOverUI` 弹出 GAME OVER，任意键返回 GameStart（有死亡流程但无单角色死亡动画）
- ⚠️ 玩家 Animator 暂无 "Hit"/"Dead" clip，受击/死亡动画名默认留空（跳过动画，相机震动 + 血条归零兜底）；死亡会有 `LogWarning` 提示
- ⚠️ 无复活流程（不在本次范围，见路线图）

---

## 开发建议与改进路线图

### 🔴 阶段一：核心修复（✅ 大部分已完成）

1. ✅ **敌人攻击闭环**：进入攻击范围 → Attack 状态 → 动画播完结算伤害 → 冷却（2026-08-06 完成）
2. ✅ **瞄准 LayerMask**：mask → 247，含 Enemy 层可锁敌（2026-08-06 完成）
3. ⏳ **攻击判定改用 Animation Event**：当前是"动画播完一次性结算"，可优化为动画中特定帧触发伤害判定（动作游戏标准做法）

### 🟡 阶段二：体验完善

4. ✅ **空中移动控制**：`PlayerHoverState` 新增 `airControlSpeed=3`（2026-08-06 完成）
5. ⏳ **Look 输入接入**：为灵敏度/反转 Y/死区等自定义相机控制打基础
6. ✅ **子弹/特效对象池**：子弹 `Queue` 池 + `EffectPool` 特效池（2026-08-06 完成）
7. ✅ **落地检测**：维持现状——起飞 `IsHover()`（距离检测防颠簸）+ 落地 `cc.isGrounded`（接触检测）的不对称是**有意设计**

### 🟢 阶段三：系统扩展

8. **武器系统重构**：抽象 `WeaponBase`（fireRate/damage/ammo/reload）+ `WeaponManager` 切换
9. **HUD**：弹药、准星、伤害数字、暂停菜单（玩家/敌人血条已有）（uGUI 或 UI Toolkit）
10. **音效系统**：Audio Mixer 分组（SFX/BGM/UI/Voice）+ Animation Event 触发
11. **配置化**：把硬编码参数抽到 ScriptableObject（PlayerConfig/WeaponConfig/EnemyConfig）

---

## 技术债务清单

| 类别 | 问题 | 严重度 | 建议 |
|------|------|--------|------|
| 代码规范 | `StateMechaine`、`Destory()`、`updataAction` 等拼写不规范 | 低 | 新代码用标准拼写；旧代码批量重命名（注意 IDE 重构） |
| 内存管理 | `MyInputSystem` 已在 OnDestroy Dispose（✅已修复） | — | — |
| 性能 | 子弹/特效对象池（✅已修复） | — | 子弹 `Queue` 池 + `EffectPool` 特效池 |
| 架构 | `PlayerController` 职责过重（输入+相机+IK+角色切换+死亡接管） | 中 | 拆分 InputHandler/CameraManager/IKManager |
| 扩展性 | `GameManager.playerModels[]` 硬编码角色数组 | 低 | 支持动态注册/删除角色 |
| 可配置性 | 大量参数硬编码 | 低 | ScriptableObject 配置资产 |

---

## 编码风格

| 类别 | 规范 |
|------|------|
| 类/接口/枚举/方法 | PascalCase |
| 字段（公有/私有） | camelCase |
| 常量 | UPPER_SNAKE_CASE |
| 接口 | `I` 前缀 |
| 注释 | Chinese（中文） |
| Inspector 提示 | `[Tooltip("中文")]` |
| 单例访问 | `XXX.INSTANCE` |

> 详细规范见 `Assets/Scripts/README.md`

---

## 关键参考

- **复习笔记**：[PROJECT_NOTES.md](PROJECT_NOTES.md) — 项目介绍/开发流程/技术点/面试题/当前 Bug 一站式笔记
- **脚本系统详细文档**：[Assets/Scripts/README.md](Assets/Scripts/README.md) — 类详解、Bug 编年史、踩坑记录
- **AI 辅助开发**：[CLAUDE.md](CLAUDE.md) — Claude Code 项目上下文
- **输入配置**：[Assets/Settings/InputSystem/MyInputSystem.inputactions](Assets/Settings/InputSystem/MyInputSystem.inputactions)

---

## 开发日志

| 日期 | 内容 |
|------|------|
| 2026-08-17 | **Hunter 无武器化**：移除武器 + 全部 Animation Rigging IK 约束（TwoBoneIK ×2 / MultiAim ×2 / RigBuilder / Rigs），**只动 Hunter**（荧/芙宁娜照常持枪）；新增 `RemoveHunterWeaponWizard`（`Tools/玩家/移除 Hunter 武器与 IK 约束`）；删除 Hunter 武器专用脚本/工具 12 个（HunterHandIK、AimOffsetTuner、AimRigProbe、AimTuner、MuzzleDebugLogger + 7 个向导）；`PlayerModel.EnterAim/ExitAim` 加 null 保护；**瞄准状态/动画保留**；还原点已删（Git 接管） |
| 2026-08-16 | 修复 Hunter 瞄准枪口朝上：MultiAim `aimAxis` 枚举陷阱（枪管≈+Z 但 aimAxis=Y）+ `offset` 后置旋转语义；`FixHunterAimAxisWizard` 按枪管方向数据驱动修复；新增 `AimOffsetTuner` 运行时微调组件（手腕→武器间插调整节点，`gunPosition`/`gunAngle` 纯 Transform 驱动） |
| 2026-08-16 | Hunter 手部握枪改 **Unity 内置 OnAnimatorIK**（`HunterHandIK`）：Animation Rigging 的 TwoBoneIK 对 MMD 骨骼链不生效（job 有效但 tip 不贴 target），换内置 IK 后正常；`ApplyHunterHandIKWizard` 挂场景实例，TwoBoneIK 权重归 0 |
| 2026-08-16 | Hunter 武器 IK 修复（`FixHunterWeaponRigWizard`）：**applyRootMotion 恒 true**（Animation Rigging 约束依赖它求值，FPS 代码驱动时根运动由 OnAnimatorMove 拦截丢弃）、去重复武器、左右手 IK target 按芙宁娜标准重挂（右→Chest、左→武器根）、MultiAim sourceObjects 补场景 AimTarget |
| 2026-08-16 | Hunter 专属跑酷控制器 `Hunter_Parkour.controller`（`ApplyHunterParkourWizard`）：移动换 CLazyRunner 跑酷包、跳跃 3 片段随机三选一（`PlayerModel.randomJumpClips` + `HoverClip`）、瞄准走射保留 X Bot；只改 Hunter 不动共享控制器 |
| 2026-08-16 | NavMesh 增强工具 `RebakeNavMeshWizard`：清旧数据重建（`RemoveAllNavMeshData` + surface 清空）修复"走上天/走不过来"；新增 NavMesh 诊断（高/浮空物体标记）与备份还原（`_Backup_NavMesh_*`） |
| 2026-08-14 | 修复 Hunter 约束不生效根因：`Rigs` 容器不在 Animator 骨骼树内 → RigBuilder 绑定失败 → Job 永不驱动；`FixHunterRigsInBoneTreeWizard` 把 `Rigs` 移到模型根骨 `暗夜猎人/174.!Root` 下 |
| 2026-08-12 | Game 全部玩家移动切换为 FPS 式：`useFPSMovement=true` + 统一 `TPS_Movement.controller`（CLazyRunner 跑酷移动 + X Bot 瞄准走射），新增 Sprint 状态、代码驱动位移（`PlayerModel.LateUpdate` 的 `cc.Move`），射击/人机保留；`Tools/玩家/生成 TPS 移动控制器` + `应用/还原 TPS 移动` 批量切 3 个角色预制体 |
| 2026-08-12 | 新增编辑器向导 `AddHunterToGameWizard`：把 New Scene 的暗夜猎人一键接入 Game 玩家（生成 Hunter.prefab，动作状态保持 Game 方案，注册 playerModels 索引 2，数字键 3 切换，可逆移除菜单） |
| 2026-07-29 | 修复状态机逻辑 Bug（赋值/比较混淆）+ 重力语义错误 |
| 2026-07-29 | 重构地面检测：Raycast → SphereCast + 稳定性窗口 + 重力锁定，解决斜坡卡顿和垂直振荡 |
| 2026-08-01 | 完善项目文档：更新 `Assets/Scripts/README.md`，创建根目录 `README.md`，补充架构图和开发建议 |
| 2026-08-01 | Bug 修复：HOVER_STABILITY_FRAMES (100→5)、子弹穿墙、InputSystem 泄漏、Animator null 防御 |
| 2026-08-03 | Furina 模型适配：修复运行时模型下沉（CC Center.y 不对齐）及无法移动（缺少 Animator Controller） |
| 2026-08-03+ | 双角色切换 + 人机跟随系统（GameManager.playerModels[] + 1/2 键 + NavMeshAgent 跟随） |
| 2026-08-03+ | 敌人系统：丘丘人预制体 + ZombieEnemy 状态机 + NavMesh 寻路 + 受击特效 + 扣血 + 血条 |
| 2026-08-03+ | UI 系统：UIBase + 主菜单/提示/退出菜单 + ExcludeMouse + TMPGlowControl + UIManager |
| 2026-08-03+ | 场景重构：TestScene → Game/GameStart；新增 HeadAimTarget 头部 IK；HealthBar 预制体 |
| 2026-08-06 | 通读全项目，重写根 README（增：UI/双角色/血条/敌人逻辑；删：过时的"UI 未开始"描述），新增 PROJECT_NOTES.md 复习笔记 |
| 2026-08-06 | Bug 修复：敌人攻击闭环（Attack 状态 + 玩家血量/受击/死亡）、瞄准 LayerMask 修正、Lumine fallHeight、空中控制、子弹对象池、TMPGlowControl 阴影、UIBase 退场动画、脚本执行顺序、BuildSettings 清理 |
| 2026-08-06 | Bug 修复：NavMeshAgent 切换报 "not close enough"（位置校正）、`SetDestination` 报错（isOnNavMesh 校验）、敌人目标锁定/定期刷新（排除死亡）、相机抖动生效（补 CinemachineImpulseListener）、新增玩家血条 HUD（PlayerHealthBar） |
| 2026-08-06 | Bug 修复：ImpulseListener 改挂两架虚拟相机（修 "requires a virtual camera"）、完整死亡流程（随从拦截/主控自动接管/全灭 GAME OVER+任意键回主菜单）、玩家血条改用敌人 HealthBar 预制体逻辑 |
| 2026-08-06 | 新增通用特效对象池 `EffectPool`（按预制体分池 + 粒子播完自动回池），枪口火花/子弹命中特效/敌人受击特效三处统一接入 |
