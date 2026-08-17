# ElementWar 项目文档（面试复盘版）

> **项目定位**：基于 **Unity 2022.3.62f3 + URP** 开发的一款 **3D 第三人称射击游戏（TPS）**。
>
> **项目状态**：V0.1，核心玩法闭环已跑通。本文档面向面试复盘，按「项目介绍 → 开发流程 → 技术点及实现 → 项目难点」四个板块详细展开，覆盖玩家逻辑、敌人逻辑、玩家模型、敌人模型、动画特效、场景搭建、UI 界面等全部内容。
>
> 最后更新：2026-08-17
>
> ⚠️ **2026-08-17 变更**：暗夜猎人（Hunter）**暂不装备武器**，其武器 + 全部 Animation Rigging IK 约束已移除（只动 Hunter，荧/芙宁娜照常持枪）；瞄准状态/动画保留。文内 Hunter 武器/握枪 IK 相关描述（如 `HunterHandIK`、`FixHunterAimAxisWizard` 等）按**历史经验**理解，其通用教训仍适用于荧/芙宁娜。还原点：`_RestorePoint_20260817_GoodState/STATE.md`（当前基线还原点）。

---

## 目录

- [一、项目介绍](#一项目介绍)
- [二、项目开发流程](#二项目开发流程)
- [三、项目技术点及实现](#三项目技术点及实现)
- [四、项目难点与开发困难](#四项目难点与开发困难)
- [五、面试常见问题精选](#五面试常见问题精选)

---

# 一、项目介绍

## 1.1 一句话定位

**ElementWar 是一款以「泛型状态机」为骨架、以「Root Motion / 代码驱动位移」为移动根基、支持多角色切换与人机跟随、带完整敌人与 UI 闭环的 TPS 原型游戏。**

它不是我拿现成模板改的 Demo，而是从空项目起步、以「先框架、后玩法、再打磨」的顺序逐步搭建的完整可玩原型——从输入、状态机、移动、瞄准、射击，到敌人 AI、伤害血量、血条 HUD、GAME OVER 流程，再到主菜单界面，一条完整的「开局 → 战斗 → 失败/胜利 → 结算返回」链路全部走通。

## 1.2 开发环境与技术栈

| 类别 | 技术 | 版本 | 作用 |
|------|------|------|------|
| 引擎 | Unity | 2022.3.62f3 | 游戏引擎 |
| 渲染 | URP（Universal Render Pipeline） | 14.0.12 | 渲染管线，配合 YSA Toon 卡通渲染 |
| 输入 | New Input System | 1.14.2 | 键鼠/手柄/触屏统一输入，`.inputactions` 可视化配置 |
| 相机 | Cinemachine | 2.10.7 | 双 FreeLook 相机（正常/瞄准）+ 镜头冲击抖动 |
| 动画 IK | Animation Rigging | 1.2.1 | 持枪手部 TwoBoneIK、瞄准身体/手部 MultiAim、头部跟随 |
| 寻路 | AI Navigation（NavMesh） | 1.1.7 | 敌人追击 + 人机随从跟随 |
| 关卡 | ProBuilder | 5.2.4 | 编辑器内白模关卡搭建 |
| 文本 | TextMeshPro | 3.0.7 | 主菜单文字、发光/阴影特效 |
| 特效 | EffectCore（插件） | — | 枪口火花、子弹命中、受击喷血/滴血粒子 |
| 模型 | MMD4Mecanim（插件） | — | PMX 模型（荧/芙宁娜）转 FBX 导入 |
| 风格 | YSA Toon（插件） | — | URP 下的卡通渲染 Shader |

## 1.3 可玩内容与完成度

| 模块 | 已实现 | 完成度 |
|------|--------|--------|
| 玩家系统 | 移动 / 冲刺 / 跳跃（Hunter 三选一随机）/ 空中操控 / 滑铲 / 瞄准 / 射击 / 三角色切换（荧·芙宁娜·暗夜猎人）/ 人机跟随 / 血量 / 受击 / 死亡流程 | ≈90% |
| 敌人系统 | 丘丘人：寻路追击 / 目标刷新 / 攻击闭环 / 受击减速 / 喷血特效 / 血条 / 死亡销毁 | ≈65% |
| 武器系统 | 射击 / 子弹飞行 / 帧间碰撞 / 伤害 / 命中特效 / **子弹+特效双对象池** / 镜头抖动 | ≈55% |
| UI/HUD | 主菜单（12 按钮）/ 提示菜单 / 退出确认 / 敌人+玩家世界空间血条 / GAME OVER / 文字发光 / 按钮躲避鼠标 | ≈65% |
| 音频 | 未开始 | 0% |

## 1.4 场景构成

| 场景 | 用途 | 关键内容 |
|------|------|---------|
| `Assets/Scenes/GameStart.unity` | 主菜单 | MainMenuUI（12 个按钮）、TipMenuUI（占位提示）、ExitMenuUI（退出确认）、ExcludeMouse（按钮躲避鼠标）、TMPGlowControl（文字发光）、EventSystem |
| `Assets/Scenes/Game.unity` | 战斗关卡 | 三位玩家角色（荧/芙宁娜/暗夜猎人）、丘丘人敌人、敌人+玩家复用的 HealthBar 血条、NavMesh 烘焙、AimTarget、gallardo 跑车等装饰模型 |

进入流程：主菜单点「开始新游戏」→ 加载 `Game` 战斗场景 → Play 运行。

## 1.5 操作方式

| 按键 | 功能 |
|------|------|
| WASD | 移动 |
| Left Shift | 冲刺 |
| 鼠标右键（按住） | 瞄准 |
| 鼠标左键 | 开火（自动进入瞄准视角） |
| Space | 跳跃 |
| C | 滑铲（跑动/冲刺中按下） |
| `1` / `2` / `3` | 切换角色（荧 / 芙宁娜 / 暗夜猎人） |
| 鼠标移动 | 视角旋转（Cinemachine 处理）+ 角色头部跟随 |

---

# 二、项目开发流程

本节按「**为什么做 → 怎么做 → 遇到什么 → 结果如何**」的叙事方式，讲述整个项目从零到一的完整开发过程。整体开发顺序是：

```
搭框架（单例/状态机/集中式 Update）
  → 填玩家（输入 → 移动 → 状态流转）
  → 磨手感（斜坡稳定性、跳跃动量）
  → 扩玩法（瞄准/射击/武器）
  → 做敌人（寻路/受击/攻击/死亡）
  → 搭 UI（主菜单/血条/失败流程）
  → 加系统（双角色切换/人机跟随）
  → 重构与整合（FPS 式移动、暗夜猎人接入）
```

## 2.1 第一阶段：先搭框架（地基工程）

做任何角色游戏之前，我先把最底层、最通用的东西立起来，避免后面每个功能都从零造轮子。

**① 泛型单例 `SingleMonoBase<T>`**

项目中大量管理器（输入、相机、UI、全局数据）本质都是「全局唯一、随处可访问」，所以我用 C# 泛型 + 自引用约束做了一个单例基类：

```csharp
public class SingleMonoBase<T> : MonoBehaviour where T : SingleMonoBase<T> {
    public static T INSTANCE;
    protected virtual void Awake() {
        if (INSTANCE != null) Debug.LogError(name + "不符合单例模式");
        INSTANCE = (T)this;
    }
    protected virtual void OnDestroy() { INSTANCE = null; }
}
```

`where T : SingleMonoBase<T>` 的**自引用泛型约束**保证任何子类都能通过强类型 `XXX.INSTANCE` 访问，且**杜绝了 `FindObjectOfType` 这种每次搜索的昂贵查找**。项目里 `PlayerController`、`GameManager`、`MonoManager`、`UIManager`、所有 UI 菜单、特效对象池全部继承自它。

**② 泛型状态机 `StateMechaine`**

TPS 角色行为天然有大量**互斥状态**（待机、移动、悬空、瞄准、冲刺、滑铲），如果用 switch/if 堆，状态一多代码就会失控。我实现了一个非 MonoBehaviour 的泛型状态机：

- `Dictionary<Type, StateBase>` **缓存状态实例**：首次进入某状态时 `new` 一次并调用 `Init(owner)`，之后永久复用，避免频繁 `new` 带来的 GC；
- `EnterState<T>()` 依次执行「退出旧状态 → 载入（或创建）新状态 → 进入新状态」；
- **防重入**：如果当前已经是目标状态则直接 return，防止同状态反复 Exit/Enter 造成死循环；
- `Stop()` 统一退出当前状态、销毁所有缓存状态并清空字典（死亡、销毁时调用）。

**③ 集中式 Update `MonoManager`**

如果每个状态都是一个 MonoBehaviour，那么 10 个状态就有 10 个 `Update()` 循环。我让 `MonoManager` 成为唯一的 Update 宿主，内部持有一个 `Action` 多播委托：

- 状态 `Enter()` 时 `AddUpdateAction(Update)`，`Exit()` 时 `RemoveUpdateAction(Update)`；
- 每帧 `MonoManager.Update()` 统一 `Invoke()` 一遍委托链。

这样所有状态的 Update 集中在一处，便于调试和性能分析，也减少了 MonoBehaviour 数量。

**设计要点**：这三个基础件是后面一切系统的土壤——玩家、敌人各自持有自己的状态机实例，但**共用同一套状态机框架**；所有管理器通过单例访问；所有状态通过 MonoManager 驱动。

## 2.2 第二阶段：玩家系统开发（玩家逻辑 + 玩家模型）

玩家是游戏的灵魂，我把它拆成「大脑」和「身体」两个部分，职责分离：

### 2.2.1 玩家逻辑（`PlayerController` —— 玩家的大脑）

`PlayerController` 继承单例基类，负责一切**读与算**：

- **输入轮询**：在 `Update()` 里逐帧读取 New Input System 的 `moveInput`（WASD 归一化）、`isSprint`、`isAiming`、`isJumping`、`isFire`、`isSlide`；
- **移动方向计算**：把二维输入转换成世界空间方向，关键点是**相机相对方向**——用相机前向投影到水平面再归一化，乘以输入 y，加上相机右向乘以输入 x，得到 `worldMovement`；再用模型 `InverseTransformVector` 转成本地方向 `localMovement`，供角色旋转使用；
- **相机与 IK 管理**：进入/退出瞄准时同步双相机角度、交换优先级、切换 IK 约束权重（详见技术点部分）；
- **角色切换**：数字键 1/2/3 调用 `SwitchPlayerModel(index)`，跳过已死亡角色；
- **死亡接管**：主控死亡时自动把控制权交给下一位存活角色，全部死亡则挂载 GAME OVER UI。

### 2.2.2 玩家模型（`PlayerModel` —— 玩家的身体）

`PlayerModel` 是一个纯 `MonoBehaviour`（实现 `IStateMachineOwner` 标记接口），持有 `CharacterController`、`Animator`、状态机、`PlayerWeapon`、IK 约束、重力/跳跃参数，负责一切**动与播**：

- **移动**：`OnAnimatorMove()`（或 FPS 式下的 `LateUpdate`）中通过 `cc.Move()` 驱动位移；
- **状态切换**：`SwitchState(PlayerState)` 将枚举分发到具体状态类；
- **动画播放**：`PlayStateAnimation()` 用 `CrossFadeInFixedTime` 播放指定 clip；
- **地面检测**：`IsHover()` 用 SphereCast 从 CC 真实底部检测离地距离；
- **血量/死亡**：`maxHealth=100`、`TakeDamage()`、`Die()`。

### 2.2.3 玩家状态机（六个状态）

玩家状态从最初的 4 个（Idle/Move/Hover/Aiming）演进到 6 个（新增 Slide 滑铲、Sprint 冲刺）：

```
Idle ←→ Move ←→ Sprint          （Move 按住 Shift 进 Sprint，松开回 Move）
Idle/Move/Sprint → Hover         （跳跃 或 跌落超过 fallHeight 阈值）
Hover → Idle                     （落地）
任意地面状态 → Aiming            （右键按住 或 开火中）
Aiming → Idle                    （松开右键且未开火）
Idle/Move/Sprint → Slide         （移动中按 C，滑铲）
Slide → Move / Idle / Hover / Aiming
```

**基类 `PlayerStateBase` 承担三件跨状态的事情**：

1. **重力计算（含斜坡稳定性缓冲）**：不在地面时递增 `ungroundedFrameCount`，稳定性窗口内垂直速度锁定 `-2f`，超过窗口才真正累积重力，这是解决斜坡抖动的一层关键（详见难点章节）；
2. **瞄准状态全局监听**：只要 `isAiming || isFire` 为真，任何地面状态都会切到 Aiming——这意味着瞄准可以从待机、移动、悬空、滑铲任意状态进入，而无需在每种状态里都写一遍瞄准判断；
3. **跳跃触发 `SwitchToHover()`**：用公式 `v = √(2·g·h)`（g=-15, h=1.5 → v≈6.7m/s）计算跳跃初速度，并主动跳过稳定性延迟。

**各状态职责**：

| 状态 | 核心职责 |
|------|---------|
| `PlayerIdleState` | 待机；检测移动输入 → Move（按住冲刺直接进 Sprint）、检测跳跃 → Hover；人机分支判断是否离跟随目标过远 |
| `PlayerMoveState` | 播放 Move 动画；FPS 式下 `LerpSpeedTo(JOG_BLEND)` 平滑逼近慢跑速度并写 `horizontalVelocity`；按 C → Slide、按 Shift → Sprint；处理角色转向 |
| `PlayerSprintState` | Speed 拉满走 Locomotion 树 Dash 段；可冲刺跳跃 / 冲刺滑铲；松开冲刺或停止输入回 Move |
| `PlayerHoverState` | 空中水平控制（写 `horizontalVelocity` 插值到 `airControlSpeed`）；落地 `cc.isGrounded` 回 Idle；人机用 NavMeshAgent 是否在网格上判定落地 |
| `PlayerAimingState` | 进入时 `EnterAim()`（相机/IK 切换）；每帧屏幕中心射线更新 `AimTarget`；开火调用 `weapon.Fire()` + 相机抖动；`AimingX/Y` 驱动瞄准走射混合树；退出时 `ExitAim()` |
| `PlayerSlideState` | 播放 RunningSlide 动画；滑铲方向朝主视角、速度按时间衰减（冲刺时更强）；强制贴地；可被跳跃中断、可切瞄准 |

### 2.2.4 移动系统的两次演进

**初版（Root Motion 驱动）**：水平位移来自 `animator.deltaPosition`（Root Motion），垂直位移手动叠加 `verticalSpeed * dt`，最后统一 `cc.Move()`。好处是位移与动画天然同步、动作自然；缺点是依赖动画数据质量，且 Root Motion 偶尔会绕过 CharacterController 造成穿模。

**当前版（FPS 式代码驱动，`useFPSMovement=true`）**：位移完全由 `PlayerModel.LateUpdate()` 里的 `cc.Move(horizontalVelocity * dt + verticalSpeed * dt)` 驱动。各状态每帧写入 `horizontalVelocity`，`Speed` 混合树参数（0 待机 / 0.33 走 / 0.66 慢跑 / 1.0 冲刺）由 `LerpSpeedTo` 平滑逼近，`GetMoveSpeed(blend)` 把混合值映射回世界速度。瞄准走射保留 X Bot 2D 混合树（`AimingX/AimingY`）。**⚠️ 关键细节：`applyRootMotion` 仍保持 `true`**——Animation Rigging 约束依赖它才求值（Unity 已知问题，`applyRootMotion=false` 时约束失效），根运动由 `OnAnimatorMove` 拦截丢弃（FPS 分支直接 return），位移仍完全由代码驱动，不穿模。

> **为什么用 `LateUpdate` 而非 `Update`？** 状态的 Update 经 MonoManager 集中式调度通常早于本帧，先写值、`LateUpdate` 再位移，避免「先移动后写值」导致位移被吞。

## 2.3 第三阶段：手感打磨（最有技术含量的一段）

移动跑通后，我花大力气打磨手感，遇到并解决了一个教科书级的疑难问题——**斜坡上角色抖动、卡顿、状态机在 Move↔Hover 间横跳**。

排查发现是**三层根因叠加**：

1. `CharacterController.isGrounded` 在斜面上会**瞬时 flicker**（这一帧 true、下一帧 false），直接触发悬空切换；
2. 原地面检测用**单根 Raycast** 从 `transform.position` 向下射，斜面上覆盖率低、易误判；
3. 稳定性窗口期内**重力仍在累积**，垂直速度在 `-2f` 和越来越负之间振荡，抖动加剧。

解决方案三管齐下（详见难点章节）：**SphereCast 代替 Raycast + 稳定性窗口 + 窗口期重力锁定**。这让我深刻理解了「一个现象背后往往是多层根因」，也是面试中最有讲头的一段经历。

## 2.4 第四阶段：瞄准、射击与武器系统

**瞄准**采用双 Cinemachine FreeLook 相机 + Animation Rigging IK：

- 平时用正常 TPS 视角（Priority=100），按住右键切到肩后瞄准视角（Priority=100）；切相机前先**同步两架相机的 X/Y 轴角度**，保证视角不跳变；
- 瞄准时 IK 从 `TwoBoneIKConstraint`（腰射持枪）切换到 `MultiAimConstraint`（右手+身体朝向 `AimTarget`），退出时反向恢复；
- `AimTarget` 是屏幕中心射线（`ViewportPointToRay(0.5,0.5)`）的命中点，射线 LayerMask 包含 Enemy 层（可锁敌）但排除 Player 层；
- 另有一个 `HeadAimTarget` 专门驱动头部 IK：鼠标移动时头部跟随偏移（可左右/上下/向后，禁止向前），松手自动回中——纯表现细节，但让角色「活」了很多。

**射击**由 `PlayerWeapon` 负责：射速 0.15s/发（≈6.67 发/秒），从对象池取子弹 + 从特效池取枪口火花，命中任何碰撞体播命中特效并回池。镜头抖动用 `CinemachineImpulseSource.GenerateImpulse()`——这里踩过一个坑：`CinemachineImpulseListener` 必须挂虚拟相机而不是 Main Camera（详见难点）。

**子弹**是 Rigidbody 高速飞行（`flyPower=30`），碰撞不用 `OnCollisionEnter`（高速会隧穿），而是每帧从**上一帧位置**向**当前位置**做 Raycast，把整段飞行路径都做一次检测（帧间碰撞检测）。

## 2.5 第五阶段：敌人系统开发（敌人逻辑 + 敌人模型）

### 2.5.1 敌人基类（`EnemyBase` —— 敌人骨架）

`EnemyBase` 是抽象类，实现 `IStateMachineOwner`，持有 `Animator`、`NavMeshAgent`、`BoxCollider`，提供四组能力：

- **寻路追击**：`chaseTarget()` → `SetDestination()`（先校验 `isOnNavMesh`）；
- **目标选择**：`FIndAttackTarget()` 遍历 `GameManager.playerModels[]` 找**最近的存活玩家**，每 0.5s 定期刷新——避免锁定已死的尸体、始终攻击最近的威胁；
- **受击 `Hurt()`**：播受击动画 → 移动动画减速（0.5 倍，0.5s 恢复）→ 喷血 + 滴血特效（走特效池）→ 扣血 → 更新/销毁血条；
- **攻击判定**：`IsAttackTargetInAttackRange()` 判断目标是否进入 `minAttackDistance=1` 的范围内。

### 2.5.2 敌人实现（`ZombieEnemy` —— 丘丘人）

`ZombieEnemy` 是 `EnemyBase` 的具象实现，`SwitchState` 把枚举分发到四个敌人状态：

```
Idle → Move（目标不在攻击范围）→ chaseTarget() 追击
   ↑      │
   │      └── 进入攻击范围 → Attack（播攻击动画 → 动画播完结算伤害 → 进入 1.5s 冷却）→ Idle 重判
   └──────（Dead 由受击致死触发）
```

**攻击闭环**是敌人系统最重要的补全：最初 `ZombieAttackState` 是空壳，敌人只会在 Idle↔Move 之间循环追着玩家跑但永远打不到人。我补全了完整逻辑——进入攻击范围 → 播 Attack 动画 → **动画播放完毕**（`IsAnimationBreak`）才结算伤害 → 记录 `lastAttackTime` 进入 `attackCooldown=1.5s` 冷却 → 切回 Idle 重判距离与冷却。

### 2.5.3 敌人模型

敌人预制体结构：根节点挂 `ZombieEnemy` + `NavMeshAgent` + `BoxCollider`，Tag=Enemy、Layer 7=Enemy，内部嵌套 `丘丘人.fbx` 模型与 Animator。血条是 World Space UI，`EnemyHealthBarUI` 每帧 `LookRotation(-dir)` 面向相机（Billboard），受击后显示 6s。

**死亡流程**：血量归零 → 禁用 NavMeshAgent + 禁用碰撞体（null 防御）+ 销毁血条 → `SwitchState(Dead)` 播死亡动画 → 动画播完 `Clear()` 销毁。这套流程里埋了不少边界情况的坑（详见难点）。

## 2.6 第六阶段：UI 系统开发

### 2.6.1 UI 基类（`UIBase<T>`）

所有菜单继承 `UIBase<T>`（本身也是泛型单例），统一进出场动画与按钮控制：

- `Enter()`：激活 → 播 FadeIn → 等动画播完 → `ResumeButtons()` → **停用 Animator**（释放位置控制权）；
- `Exit(action)`：禁用按钮 → 播 FadeOut → 等动画播完（2s 超时保护）→ 执行回调（显示目标菜单）→ **`SetActive(false)`**（防止透明面板挡住下层菜单的鼠标射线）。

### 2.6.2 菜单体系

主菜单 12 个按钮：「开始新游戏」→ `LoadScene("Game")`；「退出」→ 退出确认弹窗；其余（在线/继续/读取/角色/设置等）→ 占位提示弹窗。菜单流转全部经过 `Exit(回调)` → 目标 `Enter()` 的异步衔接，保证淡出淡入顺序正确。

### 2.6.3 两个亮点交互

- **`ExcludeMouse` 按钮躲避鼠标**：鼠标靠近时按钮沿着「远离鼠标」方向偏移、移开后自动回位，全向且有界（最大偏移 120px，绝不飞出界面）。实现要点是用**屏幕像素距离**（`WorldToScreenPoint`）而非局部坐标换算，用「期望位置 + Lerp 插值」而非「受力累积」——后者会数值失衡导致按钮被推飞（详见难点）；
- **`TMPGlowControl` 文字发光**：鼠标悬停时文字材质 `_GlowPower` 淡入→定格→淡出，并开启 `UNDERLAY_ON` 阴影，移开归零关阴影。

### 2.6.4 世界空间血条

玩家血条**复用敌人 `HealthBar.prefab` + `EnemyHealthBarUI` 逻辑**（Billboard 面向相机 + `UpdateHealthBar(ratio)` 更新填充），由 `PlayerHealthBar` 在 `Awake` 动态挂载，**仅当前主控角色显示**——切换角色后自动切换显示，角色销毁自动清理。这样一套血条逻辑同时服务敌人和玩家，避免重复造轮子。

### 2.6.5 GAME OVER 流程

全部角色死亡时，`PlayerController` 动态给自身挂载 `GameOverUI`：**纯代码构建**一个 Screen Space Overlay Canvas（半透明遮罩 + GAME OVER 大字 + 提示文字），用 New Input System 监听任意键，**跳转主菜单前先解锁光标**（`Cursor.lockState` 是跨场景静态状态，Game 中锁定后不手动解锁，主菜单鼠标会不可见）。

## 2.7 第七阶段：双角色切换与人机跟随

初版是单角色，后来扩展成「三角色可切换 + 非控制角色自动跟随」的人机系统：

- `GameManager.playerModels[]` 数组持有全部角色，`PlayerController.currentPlayerModel` 指向当前主控；
- 按 `1/2/3` 切换：旧角色 `Exit()` → 启用 NavMeshAgent 转为随从；新角色 `Enter()` → 禁用 NavMeshAgent、相机 Follow/LookAt 重置；
- **人机 AI**：非控制角色不进玩家输入逻辑，`PlayerStateBase.IsBeControl()` 判断控制权。随从在 Idle/Move 之间按**与主控的距离**切换，超过 `stoppingDistance=2` 则用 NavMeshAgent 跟随；
- **三角队形**：`GetFollowerTargetPosition()` 计算随从专属站位——主控最前（顶点），随从按数组索引在主控后方两侧对称展开（底边），索引 1 → 左后、索引 2 → 右后，形成倒三角/楔形队列，避免随从挤成一团。

## 2.8 第八阶段：FPS 式移动重构 + 暗夜猎人接入

### 2.8.1 移动方案重构（TPS_Movement.controller）

后来我把 New Scene 沙盒里验证过的 FPS 式移动（代码驱动位移 + CLazyRunner 跑酷系混合树）迁移回 Game 主场景，为全部角色生成统一控制器 `TPS_Movement.controller`：普通移动走 `Speed` 一维混合树（待机/走/慢跑/冲刺），瞄准走射保留 `AimingX/AimingY` 2D 混合树。配套写了编辑器工具 `Tools/玩家/生成 TPS 移动控制器`、`应用/还原 TPS 移动`，批量切换三个角色预制体；旧 Root Motion 方案用 `useFPSMovement=false` 随时可回退。

### 2.8.2 暗夜猎人一键接入（AddHunterToGameWizard）

把 New Scene 的暗夜猎人（FPS 沙盒角色）接入 Game 场景作为第三位玩家，我写了一个一键编辑器向导：

1. 以芙宁娜预制体为模板克隆，替换模型为暗夜猎人 FBX；
2. **用 `Avatar.humanDescription` 解析 Humanoid 骨骼**，把模板的 2 个 TwoBoneIK + 2 个 MultiAim 约束重接到猎人骨骼；
3. 武器重挂到猎人右手骨（保持相对姿态）；
4. 用模型全部渲染器包围盒计算并写入 CharacterController / NavMeshAgent 尺寸；
5. 场景内实例化、注册到 `GameManager.playerModels` 索引 2（数字键 3 切换）、把 `AimTarget` 挂到 MultiAim 源。

因为 `Player.controller` 的动画都是 Humanoid 片段，**可自动重定向**到猎人 Avatar，所以动作状态零改动。这个向导体现了对 Unity 编辑器 API（`AssetDatabase`、`PrefabUtility`、`EditorSceneManager`）和骨骼重定向机制的掌握。

### 2.8.3 Hunter 专属打磨（2026-08-14 ~ 08-16）

暗夜猎人由 **MMD 模型**转制（Humanoid），接入并换跑酷动画后暴露出一串只有它才有的问题，逐个解决：

1. **约束不生效根因**：Animation Rigging 会把约束 GameObject 绑定到 Animator 骨骼流，Hunter 的 `Rigs` 容器在角色根下、不在模型骨骼树内 → Job 创建失败、永不驱动。修复：把 `Rigs` 移到模型根骨 `174.!Root` 下（详见难点 4.10）；
2. **applyRootMotion 必须为 true**：一度为了代码驱动把 applyRootMotion 设为 false，结果 **Animation Rigging 约束根本不求值**（Unity 已知问题）。改回 true，根运动由 `OnAnimatorMove` 丢弃、位移仍走代码——约束恢复 + 不穿模两全其美；
3. **Hunter 专属跑酷控制器 `Hunter_Parkour.controller`**：移动换 CLazyRunner 跑酷包、跳跃三选一随机（`PlayerModel.randomJumpClips` + `HoverClip` 混合树），瞄准走射保留 X Bot——只改 Hunter，不动三角色共享的 `TPS_Movement.controller`；
4. **手部握枪改内置 OnAnimatorIK**：TwoBoneIK 对 Hunter 的 MMD 骨骼链不生效（Job 有效但 tip 贴不上 target），改 `HunterHandIK`（自动复用原 TwoBoneIK 的 target，`OnAnimatorIK` 驱动双手握枪，TwoBoneIK 权重归 0）后正常；
5. **枪口朝上根因**：MultiAim 的 `aimAxis` 枚举 `Y=2`，而 Hunter 枪管在手腕局部≈+Z → 瞄准时被甩向上。按枪管方向数据驱动重算 `aimAxis` + `offset` 修复（详见难点 4.11）；
6. **NavMesh 三件套**：针对「走上天/走不过来」重写了 `RebakeNavMeshWizard`——清旧数据重建 + 诊断 + 备份还原（详见难点 4.12）。

## 2.9 动画与特效体系

### 动画

- **玩家动画**：CLazyRunner 跑酷动作包（走/慢跑/冲刺/跳跃/落地）+ X Bot 瞄准走射 2D 混合树 + Running Slide 滑铲。三角色共用 `TPS_Movement.controller`，用 `Speed`/`VerticalSpeed`/`IsGrounded`/`IsSprinting`/`AimingX`/`AimingY` 参数驱动；**Hunter 单独用 `Hunter_Parkour.controller`**（移动换跑酷包、跳跃三选一随机，规避改共享控制器波及其他角色）；
- **敌人动画**：丘丘人自带 Idle/Move/Attack/Dead/Hit 动画，状态播完用 `normalizedTime >= 1 && !IsInTransition` 判定（`IsAnimationBreak`）；
- **IK 动画分层**：腰射持枪（TwoBoneIK）、瞄准（MultiAim 右手+身体）、头部跟随（MultiAim 独立目标）三个层次的约束，按状态切换权重。（**Hunter 例外**：TwoBoneIK 对 MMD 骨骼不生效，手部改用内置 OnAnimatorIK。）

### 特效

三处特效统一走**特效对象池**：枪口火花、子弹命中特效、敌人受击喷血/滴血。`EffectPool` 单例按预制体分池（`Dictionary<预制体, Queue<实例>>`），复用前显式 `Stop/Clear/Play`，播完（`IsAlive()` 判定 + stopAction=Disable + 10s 超时兜底）自动回池——消除高频射击/受击时 `Instantiate/Destroy` 的 GC 压力。

## 2.10 场景搭建

用 ProBuilder 白模搭建战斗场地（地面、斜坡、平台），`Game.unity` 内放置：三位玩家角色、丘丘人敌人、双 FreeLook 相机、AimTarget、WorldSpace Canvas（血条容器）、NavMesh 烘焙。重点细节：

- **NavMesh 烘焙**是敌人追击和人机跟随的前提，角色切换、敌人寻路都依赖「是否在烘焙网格上」的判断；
- **`EditorBuildSettings` 清理**：删掉残留的 SampleScene，只保留 Game + GameStart，保证打包/运行从主菜单正确进入；
- 装饰模型（gallardo 跑车）仅做场景氛围，不挂任何逻辑；早期场景里残留的「裸暗夜猎人 FBX 装饰」（半埋地下、骨架 stripped）与正式 `Hunter.prefab` 角色是**两份不同对象**，曾导致半身入地误判，已用清理向导删除。

---

# 三、项目技术点及实现

本节用**文字为主、代码为辅**的方式，把项目里值得在面试中展开讲的技术点逐个讲透，讲清「为什么这么设计、底层原理是什么、有没有坑」。

## 3.1 分层视角：整个项目的架构直觉（先看这一节）

> 这是整份技术文档的「总纲」。项目里几乎所有系统都能套进下面这张分层图——后面每个技术点，都是这张图里某一层的展开。面试时先讲这张图，再逐层深入，比零散罗列技术点更有说服力。

### 五层流水线

```
┌─────────────────────────────────────────────────────────┐
│ ① 输入层   PlayerController.Update()                      │
│   读 New Input System → moveInput / isSprint / isAiming / │
│   isFire / isSlide（每帧轮询）                             │
└───────────────────────┬─────────────────────────────────┘
                        ▼
┌─────────────────────────────────────────────────────────┐
│ ② 状态层   当前 State.Update()（经 MonoManager 驱动）      │
│   根据输入决定行为：写 horizontalVelocity / verticalSpeed / │
│   speedBlend / 是否切换状态                               │
└───────────────────────┬─────────────────────────────────┘
                        ▼
┌─────────────────────────────────────────────────────────┐
│ ③ 位移层   PlayerModel.LateUpdate() / OnAnimatorMove()    │
│   唯一的位移发生地：cc.Move(...)                           │
│   （人机例外：位移权交给 NavMeshAgent）                    │
└───────────────────────┬─────────────────────────────────┘
                        │ （与 ③ 并行，不冲突）
┌─────────────────────────────────────────────────────────┐
│ ④ 表现层   Animator（混合树 / 动画片段）                   │
│   只决定「看起来在做什么」，不产生位移                      │
└───────────────────────┬─────────────────────────────────┘
                        │ （最后叠加）
┌─────────────────────────────────────────────────────────┐
│ ⑤ 修正层   Animation Rigging IK                          │
│   手 / 身体 / 头实时对准枪、AimTarget、鼠标                │
└─────────────────────────────────────────────────────────┘
```

**每一层只干一件事**：

| 层 | 位置 | 职责 | 一句话 |
|----|------|------|--------|
| ① 输入层 | `PlayerController.Update()` | 读输入 | 玩家按了什么 |
| ② 状态层 | 各 `PlayerState` / `EnemyState` | 算行为 | 当前该干嘛、参数是多少 |
| ③ 位移层 | `PlayerModel.LateUpdate()` | 移动 | 真正的位移只在这里发生 |
| ④ 表现层 | `Animator` + 动画资产 | 表演 | 看起来在做什么 |
| ⑤ 修正层 | `Animation Rigging` IK | 对位 | 手/头/身体对准动态目标 |

### 动作包、Root Motion 与 IK 的分工（最容易混淆的一组）

| 层 | 对应概念 | 管什么 |
|----|---------|--------|
| 数据层 | **跑酷动作包**（CLazyRunner 等动画资产） | 「动作长什么样」——走/跑/跳/滑铲的骨骼姿态，是录好的数据 |
| 位移层 | **Root Motion / 代码驱动位移** | 「位移多少」——从动画提取 `deltaPosition`，或代码算 `horizontalVelocity` |
| 修正层 | **IK（Animation Rigging）** | 「手/身体/头对准哪」——动画数据不知道枪、目标、鼠标在哪，IK 实时把骨骼拽过去 |

一句话：**动作包管「大动作」，Root Motion 管「走多远」，IK 管「精细对位」。** 三者不冲突，是叠在一起的三层。IK 不是另一种动画、不取代动作包，而是动画播完之后叠加的「运行时修正」——位移状态的分层实现在 3.5 展开，IK 在 3.8 展开。

### 正/反向运动学（FK vs IK）

- **正向运动学（FK）**：从骨骼算末端。动画文件录好了每个关节的旋转曲线，播放时骨头照曲线动，手落在哪完全由动画数据决定，改不了。
- **反向运动学（IK）**：从末端反推关节。先定「手要落在哪」（目标位置），再反算肩、肘、腕该转多少才能把手送到那儿。

IK 的价值：录好的动画是死的，它不知道角色手里有枪、枪口对着谁、鼠标在看哪。IK 在动画求值完成后，实时把特定骨骼拽到这些「动画不知道」的目标上。

## 3.2 泛型状态机 `StateMechaine`

**为什么用状态机？** TPS 的角色行为是典型的「互斥状态集合」：同一时刻只能处于一个行为（待机/移动/悬空/瞄准…），而且状态切换有明确的触发条件。用 if/switch 堆，状态一旦增多，判断逻辑会互相缠绕、很难维护。状态机把「每个状态的行为」封装成独立类，把「状态切换」收敛到一处，符合开闭原则——新增状态只需加一个类 + 一条分发，不改动已有状态。

**实现要点**：

1. **状态缓存**：`Dictionary<Type, StateBase>`，首次进入 `new T()` + `Init(owner)`，之后复用。这既是**对象池**思想（避免重复 new 的 GC），也保证 `Init` 只执行一次、`Enter/Exit` 每次切换执行——生命周期语义清晰；
2. **防重入**：`currentState.GetType() == typeof(T)` 直接 return。这个细节在状态机与 MonoManager 委托链配合时尤其重要（见 3.3 集中式 Update）；
3. **生命周期**：`Init → Enter/Update/Exit（循环）→ Destory`，玩家和敌人共用同一套框架，只是 `SwitchState` 分发到各自的子状态类；
4. **`IStateMachineOwner` 标记接口**：状态宿主（PlayerModel/EnemyBase）实现它，`Init` 时把宿主传给状态，状态通过强转访问宿主属性（`playerModel`、`enemyModel`）。

## 3.3 集中式 Update（MonoManager）

**为什么不用「每个状态一个 MonoBehaviour」？** N 个状态就有 N 个 `Update()` 循环，除了 MonoBehaviour 数量膨胀，每帧还要多次进入引擎回调。MonoManager 把状态的 Update 注册成 `Action` 委托，自己作为唯一宿主每帧 `Invoke()` 一遍。

**委托链的注意事项**：`+=` 重复注册会导致同一次 Update 被调用多次。但因为状态机有**防重入**，不会出现「同一状态被重复 Enter 而不 Exit」的情况，所以委托链始终干净；`-=` 移除未注册的委托也不会报错，安全性好。代价是委托调用有微小的间接开销，但对状态数量级可以忽略。

## 3.4 泛型单例 `SingleMonoBase<T>`

**自引用泛型约束** `where T : SingleMonoBase<T>` 是核心——它让子类继承时直接得到强类型的静态 `INSTANCE`，`PlayerController.INSTANCE.moveInput` 这种访问清晰、可读、无查找成本。

**已知的坑**（我在项目里亲历并处理）：

- 单例在场景切换时会残留或重复，需要 `DontDestroyOnLoad` 或销毁逻辑；当前实现重复实例只 LogError 不销毁；
- `Awake/OnDestroy` 里 `INSTANCE` 的赋值/置空时机要注意——曾有代码在 `Awake` 依赖 `PlayerController.INSTANCE`，而另一脚本的 `Awake` 还没跑，导致拿到 null。解决方式是**把依赖实例的赋值延迟到 `Start`**（所有 `Awake` 已执行完毕）。

## 3.5 移动系统：Root Motion 与程序位移的取舍

**Root Motion 方案**：水平位移来自 `animator.deltaPosition`，垂直手动叠加。好处是位移和动画天然同步——动画播多快就走多快，不用手动调速度对齐；坏处是 Root Motion 数据质量决定手感上限，且可能绕过 CharacterController 造成穿模。

**FPS 式代码驱动（当前方案）**：位移完全由 `LateUpdate` 的 `cc.Move()` 驱动，状态每帧写入 `horizontalVelocity`。`Speed` 混合树参数由 `LerpSpeedTo` 平滑逼近目标值（0/0.33/0.66/1.0 对应待机/走/慢跑/冲刺），`GetMoveSpeed(blend)` 把混合值映射回世界速度。**关键细节：`applyRootMotion` 保持 `true`**——Animation Rigging 约束依赖它求值（Unity 已知问题），根运动由 `OnAnimatorMove` 拦截丢弃、位移仍完全由代码控制。这样**速度完全可控**，动画只是「表现层」，移动逻辑与动画解耦，也从根本上杜绝了 Root Motion 穿模。

**为什么保留两套？** 保留 `useFPSMovement` 开关是为了随时回退对比，开发期方便 A/B 验证手感。面试可以讲这个「参数化方案切换」的思路。

#### 位移状态的分层实现（用分层图读代码）

在「FPS 式代码驱动」下，每个状态只负责「算」，真正的位移在 `PlayerModel.LateUpdate()` 里统一执行，动画只负责「演」：

| 状态 | ② 状态层写入什么 | ③ 位移层结果 | ④ 表现层播什么 |
|------|----------------|-------------|---------------|
| Idle | `LerpSpeedTo(0)`，`horizontalVelocity=0` | 不动 | Locomotion 树 Idle 段 |
| Move | `LerpSpeedTo(0.66)`，`horizontalVelocity = worldMovement * GetMoveSpeed(speedBlend)` | 朝相机输入方向匀速前进 | Jog 慢跑段 |
| Sprint | `LerpSpeedTo(1.0)`，同样写 `horizontalVelocity` | 跑得更快（speedBlend→1 时约 8m/s） | Dash 冲刺段 |
| Hover | 水平向 `worldMovement*airControlSpeed` 插值（无输入则保留起跳动量并轻微衰减）；垂直由基类累积重力 | 空中抛物线：水平可控 + 垂直下落 | Air 混合树（VerticalSpeed 驱动升降） |
| Slide | `horizontalVelocity = slideDirection * speed`（speed 按剩余时间从 7 衰减到 1.5，冲刺更猛）；`verticalSpeed` 锁 `-2f` | 朝主视角滑行、越来越慢，强制贴地 | RunningSlide（仅当姿态，位移由状态自控） |
| Aiming | `horizontalVelocity = (aimingY*forward + aimingX*right).normalized * aimMoveSpeed` | 瞄准慢速走射 | AimingX/AimingY 2D 混合树 |

四个值得展开的面试点：

1. **为什么位移用 `LateUpdate` 而不是 `Update`？** 状态层经 MonoManager 集中调度通常早于本帧，`LateUpdate` 保证「状态先写值 → 本帧再位移」，否则会「先移动后写值」，位移被吞掉。
2. **为什么水平代码驱动、垂直手动重力？** 水平速度完全可控（想多快就多快，与动画数据无关）；垂直走物理公式（重力累积 + 跳跃初速度 `√(2gh)≈6.7m/s`），二者相加得到最终 `cc.Move`。
3. **位移与动画解耦，是新旧方案的本质区别。** 旧方案位移「来自」动画 `deltaPosition`（表现层兼任位移层，速度不可控、会穿模）；新方案位移「全由代码」，动画只是播着配合，换任何动作包只需对得上 `Speed` 参数。
4. **人机不走第 ③ 层。** 随从的 `horizontalVelocity` 被强制写 `Vector3.zero`，位移权完全交给 NavMeshAgent——`cc.Move` 与 NavMeshAgent 是两套位移系统，同时用会抢位置导致随从原地打转，所以必须二选一（主控归代码、随从归寻路）。

## 3.6 地面检测：SphereCast + 稳定性窗口 + 重力锁定

这是本项目技术含量最高的一段，三层设计环环相扣：

**① 用 SphereCast 替代 Raycast 计算 CC 真实底部**：

```csharp
float ccBottom = transform.position.y + cc.center.y - cc.height * 0.5f + cc.skinWidth;
```

CC 的几何中心 ≠ transform 原点，必须把 `cc.center`（中心偏移）、`cc.height`、`cc.skinWidth`（外壳厚度）都算进去，从真实脚底出发做球形投射，半径取 `cc.radius * 0.6`。球体在斜面上的覆盖率远高于单根射线，不易漏检。

**② 稳定性窗口**：`ungroundedFrameCount` 连续超过 `HOVER_STABILITY_FRAMES=5` 帧才真正切 Hover。因为 `cc.isGrounded` 在斜面上会瞬时 flicker，直接切换会导致状态机横跳。

**③ 窗口期重力锁定**：窗口内 `verticalSpeed` 恒为 `-2f`（贴地小值），不累积重力。如果不锁，垂直速度会在 `-2f` 和越来越负之间振荡，抖动反而更剧烈。

**不对称是有意设计**：起飞用 `IsHover()` 距离检测（带 fallHeight 阈值防地面小颠簸误触发），落地用 `cc.isGrounded` 接触检测（`cc.Move()` 的碰撞判定比 SphereCast 更可靠）。这两个方向的目的不同，不能图省事对称处理。

## 3.7 双相机瞄准系统（Cinemachine）

**为什么双相机而不是单相机切参数？** 正常 TPS 视角（角色居中偏侧、镜头远）和肩后瞄准视角（镜头上肩、更近、更低）需要的 orbit 参数差异很大，单相机很难做自然的无缝切换。两架 `CinemachineFreeLook` 各配一套 orbit，用 **Priority 交换** 决定谁生效：

- 正常：freeLook Priority=100，aiming Priority=0；
- 瞄准：交换为 0 / 100。

**切相机前必须同步角度**：`aimingCamera.m_XAxis.Value = freeLookCamera.m_XAxis.Value`（X/Y 都同步），否则切换瞬间视角会跳变。退出瞄准时反向同步（把瞄准后的角度带回正常相机），保证体验连续。

**镜头抖动**：瞄准相机挂 `CinemachineImpulseSource`，开火/受击时 `GenerateImpulse()`，由两架虚拟相机上的 `CinemachineImpulseListener` 接收。关键点：**ImpulseListener 是 CinemachineExtension，必须挂虚拟相机**，挂 Main Camera（只有 CinemachineBrain）会报错——这是实测踩出来的坑。

## 3.8 IK 约束（Animation Rigging）

项目里有三组 IK，各司其职、按状态切换权重：

| 约束 | 类型 | 作用 | 激活时机 |
|------|------|------|---------|
| 右手腰射 | `TwoBoneIKConstraint` | 手自然握枪 | 正常状态（weight=1） |
| 右手瞄准 | `MultiAimConstraint` | 右手朝向 AimTarget | 瞄准（weight=1） |
| 身体瞄准 | `MultiAimConstraint` | 上半身朝向 AimTarget | 瞄准（weight=1） |
| 头部 | `MultiAimConstraint` | 头部跟随鼠标 | 常驻 |

两个 MultiAim 都 Reference 同一个 `AimTarget`。`EnterAim()/ExitAim()` 里交换权重（瞄准约束 1/腰射 0，退出反向）。

**头部 IK（HeadAimTarget）**：静止位置 = 相机位置 + restOffset；鼠标 delta 映射到相机 right/up 轴做偏移（水平/垂直方向带 Clamp），前后方向**只允许向后**（`Clamp(-maxBackward, 0)`）——避免头转向相机前方穿模；松手用 `Lerp` 回中。这个细节让角色「有眼神」，是小成本高收益的表现层投入。

> **Hunter 例外（2026-08-16，08-17 起已随无武器化移除）**：当时 TwoBoneIK 对 Hunter 的 MMD 骨骼链不生效，改用 Unity 内置 OnAnimatorIK（`HunterHandIK`）。**现 Hunter 已无武器**，此实现仅作历史；通用教训仍适用于荧/芙宁娜武器 IK：① 改 MultiAim 数据必须 `ref var d = ref constraint.data`（`var d = data` 是复制 struct、改副本无效）；② `aimAxis` 是 `[NotKeyable]`，改后需 `rigBuilder.Clear(); Build();` 重建才生效。详见难点 4.11。

#### 动画、Root Motion 与 IK 的分工（分层视角）

对照 3.1 的总纲，这三者是叠在一起的三层，不冲突：

| 层 | 对应概念 | 管什么 | 例子 |
|----|---------|--------|------|
| 数据层 | **跑酷动作包**（CLazyRunner） | 动作长什么样（正向运动学 FK） | 走/跑/跳/滑铲的骨骼姿态 |
| 位移层 | **Root Motion / 代码位移** | 位移多少 | 从动画提取 `deltaPosition`，或代码算 `horizontalVelocity` |
| 修正层 | **IK（Animation Rigging）** | 手/身体/头对准哪 | 上面三个约束 |

关键认知：

- **动作包管「大动作」，Root Motion 管「走多远」，IK 管「精细对位」**，三者层层叠加、互不干扰；
- IK **不是**另一种动画、**不取代**动作包——它是动画求值完成后叠加的「运行时修正」。动作包的手是甩动的，但持枪时手必须稳稳握住枪握把，于是 `TwoBoneIKConstraint` 把肩→肘→手这条链实时拽到枪的位置（IK 从「手要落到哪」反推关节角）；瞄准时 `MultiAimConstraint` 把右手/上半身实时转向 `AimTarget`（动画数据不知道目标在哪）；
- **为什么这里用 IK 而不是专门录瞄准动画？** 因为目标（准星、敌人、鼠标）是**运行时才存在**的，动画数据没法预先录；用 IK 让角色去适配任意方向的动态目标，代码零成本且任意角度都成立。

## 3.9 高速子弹防穿透（帧间 Raycast）

**为什么不用 `OnCollisionEnter`？** Rigidbody 默认是**离散碰撞检测**，当子弹速度快到一帧内位移超过碰撞体厚度，子弹会直接「隧穿」过去，`OnCollisionEnter` 根本检测不到。

**解决**：每帧记录上一帧位置 `prevPosition`，下一帧从旧位置向新位置发射一条 Raycast，把**整段飞行路径**都纳入检测（模拟扫掠体）。命中任何碰撞体 → 播命中特效 → 回池；命中 Enemy Tag 且组件非空 → `enemy.Hurt()`。还做了**生成时重叠检测**（`OverlapSphere`）——如果子弹出生在敌人体内立即命中，避免「贴脸打不中」。

> 这是高速弹道（射线枪/狙击）的行业标准做法，也是面试高频题「高速物体怎么防穿墙」的答案。

## 3.10 对象池体系（三层）

项目里有**三层对象池**，是「对象池思想」的完整落地：

1. **状态实例缓存**：状态机 `Dictionary<Type, StateBase>`，避免状态反复 new；
2. **子弹池**：`PlayerWeapon` 的 `Queue<PlayerWeaponBullet>`，`GetBullet/RecycleBullet` 管理取出/回收，`OnEnable/OnDisable` 管理生命周期（启用时启动超时协程、停用时停止协程并清空速度）；
3. **特效池**：`EffectPool` 全局单例，**按预制体分池**（`Dictionary<预制体, Queue>`）。

**特效池的关键细节**：① 分池键是**预制体引用**而不是字符串，天然类型安全；② 复用前必须显式 `ps.Stop(true, StopEmittingAndClear) + ps.Play()`，否则从池里拿出来的粒子不会重新播（这是对象池最常见的坑）；③ 用 `ParticleSystem.IsAlive()` + `stopAction=Disable` + 10s 超时兜底判断「播完」，替代固定时间销毁——生命周期更准、也不怕卡死。

## 3.11 敌人 AI 设计（NavMesh + 状态机）

**目标选择**：`FIndAttackTarget()` 遍历 `GameManager.playerModels[]`，排除 `isDead` 玩家，取距离最近者；每 `0.5s` 定期刷新。这个「定期刷新」解决了两个真实 bug：只在 Start 调一次会锁定死目标；不排除死亡玩家会一直追尸体。

**追击**：`chaseTarget()` → `SetDestination()`。前置校验 `navMeshAgent.isOnNavMesh`——NavMeshAgent 刚 `enabled=true` 的当帧还没落到网格上，直接 `SetDestination` 会抛「active agent / placed on NavMesh」；角色不在烘焙区时还要先 `NavMesh.SamplePosition` 校正位置。

**攻击闭环**：进入攻击范围 → Attack 状态播动画 → `IsAnimationBreak`（`normalizedTime>=1 && !IsInTransition`）判定动画播完 → 结算伤害 → 记录 `lastAttackTime` 进入冷却 → 回 Idle 重判。用**状态机 + 冷却 + 动画完成判定**替代「每帧无脑攻击」，敌人行为有节奏、有间隔。

**血条**：World Space UI 的 Billboard 实现（每帧 `LookRotation(-dir)` 面向相机），`UpdateHealthBar(ratio)` 用 `Image.fillAmount` 更新——纯 uGUI 即可实现，无需第三方插件。

## 3.12 双角色切换与人机跟随

核心是**控制权的状态转移**，而不是两套逻辑：

- `IsBeControl()` 判断「我是不是主控」；是 → 走玩家输入逻辑；否 → 走人机分支；
- **切换**：旧角色 `Exit()`（开 NavMeshAgent + 回 Idle 转人机）、新角色 `Enter()`（关 NavMeshAgent）、相机 Follow/LookAt 重置；
- **随从跟随**：`PlayerMoveState` 的人机分支按与跟随目标（主控周围的三角队形站位点）的距离决定 Idle 还是 Move，用 NavMeshAgent 寻路；
- **死人不打架**：死亡角色 `isDead` 拦截切换，`SwitchPlayerModel` 跳过已死亡角色，主控死亡自动接管下一位存活者——保证系统在角色死亡时依然自洽。

## 3.13 完整死亡流程

```
子弹命中敌人 → 敌人.Hurt() → 扣血 → 血量归零 → 禁 NavMeshAgent/碰撞体/销毁血条
   → 播 Dead 动画 → 播完 Clear() 销毁

玩家被攻击 → TakeDamage() → 扣血 → 相机震动 + 血条更新
   → 血量归零 → Die() → 停状态机/CC/NavMeshAgent
   → 随从死亡：拦截切换（不可访问）
   → 主控死亡：自动接管下一位存活角色
   → 全部死亡：动态挂载 GameOverUI → 任意键返回主菜单
```

这个流程把「玩家/敌人/随从/主控/游戏结束」五种死亡情形全部覆盖，并处理了光标跨场景残留、角色销毁清理血条等边界。

## 3.14 UI 系统设计

`UIBase<T>` 的几个设计决定值得展开：

1. **进出场必须等动画播完**：用 `IsAnimationBreak()`（`normalizedTime>=1 && !IsInTransition`）+ 2s 超时保护轮询，防止动画缺失时死等；
2. **退出必须 `SetActive(false)`**：只淡出不停用，透明面板的 raycastTarget 会永久挡住下层菜单的鼠标射线（主菜单按钮点不动的根因）；
3. **播完动画要停用 Animator**：Animator 停在 FadeIn 状态会**每帧覆写该 clip 绑定的属性**（比如 `m_AnchoredPosition.x`），与代码控制的同属性冲突（退出按钮只能垂直躲避的根因）；
4. **按钮防误触**：进出场动画期间 `DisableButtons()`，播完 `ResumeButtons()`。

## 3.15 编辑器工具（提升开发效率）

- ⚠️ **08-17 工具清理**：Tools/玩家 菜单已精简，一次性修复/诊断/还原工具（`AddHunterToGameWizard` / `ApplyHunterParkourWizard` / `RemoveHunterWeaponWizard` / `FixHunterAimWizard` / `RestoreWeaponsLikeFurinaWizard` / `RefineWeaponIKTargetsWizard` / 武器 IK 诊断等）已随项目稳定删除；
- `FixNavMeshAndGroundWizard`：修 NavMesh（移除误加 ignoreFromBuild + 抬升地面 + 清空重建 + 角色吸附，写日志）；
- `NavMeshLayerDiagWizard`：诊断 NavMesh 层配置（写日志文件）；
- `UnpackPrefabsInSceneWizard` / `SnapToFloorWizard` / `NavMeshObstacleWizard` / `BakeWalkableWizard`：场景工具（解开预制体 / 落地吸附 / 墙体阻挡寻路 / 斜坡楼梯可行走）。

这些工具体现的是「**把重复手工操作脚本化**」的工程意识——用编辑器 API 把预制体、场景、动画控制器这些资源在编辑器里自动化组装。

---

# 四、项目难点与开发困难

这一节是面试的「故事库」。我按「现象 → 排查 → 根因 → 解决 → 教训」的结构记录每个坑，方便复盘和讲述。

## 4.1 斜坡稳定性（三轮迭代，本项目最大难点）

**现象**：角色走斜坡/冲刺时抖动、卡顿，状态机在 Move↔Hover 间来回横跳。

**排查**：逐层拆。第一轮发现 `cc.isGrounded` 在斜面瞬时 flicker，一离地就触发悬空切换——这是「状态层」问题；第二轮发现即使加了稳定性窗口，窗口内**重力仍在累积**，垂直速度在 `-2f` 和越来越负之间振荡，冲刺/陡坡时抖动更剧烈——这是「重力层」问题；第三轮发现原检测用单根 Raycast，斜面上覆盖率低、容易漏检——这是「检测层」问题。

**解决**（三管齐下）：

| 层 | 措施 | 要点 |
|----|------|------|
| 检测 | Raycast → **SphereCast** | 从 CC 真实底部出发，半径 `cc.radius*0.6` |
| 状态 | **稳定性窗口** | 连续 5 帧离地才切 Hover |
| 重力 | **窗口期锁速** | 窗口内 `verticalSpeed = -2f`，不累积 |

**教训**：
- 「一个现象背后往往是多层根因」，修一层不够，要按数据流把整条链都验证一遍；
- 主动跳跃要**跳过稳定性延迟**（直接置 `ungroundedFrameCount = 5`），否则起跳会被窗口吃掉；
- `HOVER_STABILITY_FRAMES` 曾因调试被改成 100（≈1.67s@60fps 重力冻结），导致走平台边缘半天不下落——**常量值与注释不符就是 bug**，要有回档代码的习惯。

## 4.2 高速子弹穿墙

**现象**：子弹穿过薄墙或高速时打不到目标。

**根因**：Rigidbody 离散碰撞检测在高速下一帧位移超过碰撞体厚度就会隧穿，`OnCollisionEnter` 检测不到。

**解决**：帧间 Raycast——每帧从上一帧位置向当前位置扫一条射线，等效把整段飞行路径纳入检测；命中任何碰撞体即销毁/回池（防穿墙），并补了生成时重叠检测（贴脸必中）。

**教训**：物理引擎的碰撞检测是「离散采样」，高速运动必须做「扫掠」或「连续碰撞检测」，「帧间 Raycast」是最常用的低成本方案。

## 4.3 NavMesh 的一串坑

**问题一：`SetDestination` 报「active agent / placed on NavMesh」**
NavMeshAgent 刚 `enabled=true` 的当帧还没落到网格上就调 `SetDestination` 会抛错。解决：调用前校验 `isOnNavMesh`。

**问题二：`NavMeshAgent` 启用报「not close enough to the NavMesh」**
角色切下时若不在烘焙区，`enabled=true` 会报错。解决：启用前 `NavMesh.SamplePosition(..., 5f)` 把位置校正到最近网格点。

**问题三：随从切换后原地不动**
人机模式下如果既让 NavMeshAgent 驱动位移、又让 `OnAnimatorMove`/`LateUpdate` 的 `cc.Move()` 改位置，两个系统会**抢位置**，寻路被干扰导致原地打转。解决：人机分支明确「位移归 NavMeshAgent，角色只播动画」，代码驱动位移只对主控生效。

**教训**：NavMeshAgent 与 CharacterController 是两套移动系统，**必须明确「谁拥有位移权」**，不能混用。

## 4.4 UI 菜单三连 bug（现象简单、根因隐蔽）

**Bug A：主菜单按钮不高亮/点不动**
打开过 TipMenu/ExitMenu 返回后，主菜单按钮被挡住。根因：退出菜单只播了 FadeOut 没 `SetActive(false)`，透明面板的 raycastTarget 一直挡着下层按钮的鼠标射线。解决：`UIBase._Exit` 在动画播完并执行回调后停用自身。

**Bug B：退出按钮躲避鼠标只垂直不水平**
`ExcludeMouse` 逻辑明明是全向的，却只上下躲。排查到最后定位到 `FadeIn.anim`：它动画化了退出按钮的 `m_AnchoredPosition.x`（没动画 y），而 MainMenu 的 Animator **停在 FadeIn 状态**，每帧覆写 x → 水平位移被锁死。解决：FadeIn 播完后 `animator.enabled=false` 释放位置控制权。

**教训**：Animator 停在某状态会**持续覆写该状态 clip 绑定的属性**，会与代码控制的同属性冲突——这是「动画系统与脚本系统抢属性」的典型坑。

**Bug C：躲避会跑出界面且不回位**
旧实现是「受力累积」：躲避 `avoidForce×dt`（≈6.4px/帧只增不减）与回位 `returnForce×dt`（≈0.03px/帧）数量级严重失衡——靠近被推飞、移开回不来。解决：改成**有界位置式插值**——期望位置 = 原位 ± `maxAvoidDistance`（≤120px，绝不飞出界面），`Lerp` 平滑跟随，鼠标移开自动回位。

**教训**：UI 位移用「位置插值」而非「速度累加」，天然有界稳定；数值设计要检查两个方向的量级是否平衡。

**Bug D：躲避仍异常（一出场就飞/乱躲/鼠标远也躲）**
继续追查发现 `ScreenPointToLocalPointInRectangle` 在 Screen Space Camera 下距离换算异常。解决：改用 `RectTransformUtility.WorldToScreenPoint` 直接算**屏幕像素距离**，加硬边界 clamp，并把「回家位置」捕获延迟到入场动画结束 + 0.8s（避免捕获动画中间值），还加了 Inspector 运行时诊断字段辅助排障。

**教训**：复杂的 UI 数学换算问题，优先用**屏幕像素**这种「直读」数据绕开中间转换层；加运行时诊断字段是高效的排障手段。

## 4.5 模型适配问题

**问题一：芙宁娜模型下沉/无法移动**
运行时模型下沉（CC Center.y 与模型不对齐），且缺 Animator Controller 无法移动。解决：校准 CharacterController 的 center/height，补挂 Animator Controller。

**问题二：暗夜猎人重定向**
芙宁娜模板的 IK 约束骨骼引用与猎人 Humanoid 骨骼不匹配，直接换模型约束会失效。解决：用 `Avatar.humanDescription` 解析猎人骨骼映射，把模板的 IK 约束全部重接到猎人骨骼（右键/左键 TwoBoneIK + 身体/右手 MultiAim），武器重挂到右手骨保持相对姿态，碰撞体按模型包围盒适配。

**问题三：荧 fallHeight=0**
荧预制体 `fallHeight=0`，一离地就判悬空，走路都触发 Hover。解决：改回 0.2（与默认一致）。

## 4.6 相机抖动不生效

**现象**：开火/受击都不抖屏，Impulse 没反应。

**根因**：`CinemachineImpulseSource` 发出的冲击波需要 `CinemachineImpulseListener` 接收，而场景里**没有任何监听者**。补监听时又踩坑：`CinemachineImpulseListener` 是 CinemachineExtension，**必须挂虚拟相机**，挂 Main Camera 会报「CinemachineExtension requires a virtual camera」。

**解决**：`PlayerController.Start()` 为两架 FreeLook 虚拟相机动态补挂 ImpulseListener（两个都挂，正常/瞄准任一视角生效）。

## 4.7 输入系统的坑

**问题一：`MyInputSystem` 未 Dispose，退出 Play 报泄漏**
New Input System 生成的包装类持有原生资源，`OnDestroy` 必须 `Dispose()`。解决：`OnDestroy(){ input?.Dispose(); }`。

**问题二：`IsBeControl()` 赋值/比较混淆**
曾经把 `==` 误写为 `=`，导致控制权判断恒为真/假，逻辑错乱。这类 bug 编译不报错、运行时才暴露，靠认真 review 和调试发现。

**问题三：GAME OVER 返回主菜单鼠标不可见**
`Cursor.lockState` 是**跨场景的静态状态**，Game 中锁定后主菜单没有解锁点。解决：`GameOverUI` 跳转前解锁 + `MainMenuUI.Start()` 强制解锁（双保险）。

## 4.8 敌人 AI 与死亡流程的补全

**问题一：Attack 状态永不触发**
敌人只会在 Idle↔Move 循环追玩家，`ZombieAttackState` 是空壳。解决：补全攻击闭环（进范围 → 攻击动画 → 动画播完结算伤害 → 冷却）。

**问题二：只锁定随从不攻击主控**
`FIndAttackTarget` 只在 Start 调一次、不排除死亡玩家，导致敌人只打最近的随从，随从被打死后目标失效。解决：排除 `isDead` + 每 0.5s 定期刷新，始终打最近的存活角色。

**问题三：`Hurt()` 硬编码 `GetComponent<BoxCollider>()`**
每次受击都查找碰撞体，且敌人可能没有 BoxCollider。解决：`Awake` 缓存 `bodyCollider` + null 防御。

**问题四：受击/死亡动画缺失**
玩家 Animator 没有 Hit/Dead clip，直接播会刷警告。解决：动画名留空则跳过（相机震动 + 血条兜底），补齐动画后填名即可——**设计上预留了扩展位**。

## 4.9 瞄准 LayerMask 与射线的坑

**问题：准星无法锁敌**
`aimLayerMask=119` 排除了 Enemy(7) 层，屏幕中心射线打不到敌人，`AimTarget` 只能停在远处。解决：mask → 247（包含 Enemy 层，仍排除 Player 层，避免瞄准到自己人）。

**教训**：LayerMask 的默认值/手动值必须与项目自定义 Layer（本例 Layer 7=Enemy）一致，配置与代码要联动校验。

## 4.10 Hunter 约束不生效的根因（Rigs 骨骼树 + applyRootMotion）

**现象**：Hunter 的 TwoBoneIK/MultiAim 数据全部正确（IsValid=True、骨骼引用都对、权重 1），但 Play 时拖 IK target 手臂纹丝不动；同结构的荧/芙宁娜却正常。

**排查**：静态检查（IsValid、IsChildOf、Bind 不抛异常）全部通过，不足以发现问题——`BindStreamTransform` 对不在骨骼流内的 Transform 返回无效 handle 但不报错。最终用**运行时探针**确认 Job 没在驱动骨骼，并在控制台抓到硬证据：`Could not resolve '...' because it is not a child Transform in the Animator hierarchy`。

**根因一（Rigs 不在骨骼树）**：RigBuilder 创建约束 Job 时用 `animator.BindStreamProperty` 把**约束 GameObject 本身**绑定到 Animator 骨骼流，要求约束组件在骨骼流内。Hunter 的 `Rigs` 容器挂在角色根下、不在 `暗夜猎人` 模型骨骼树内 → Job 创建失败 → 约束永不驱动。修复：把 `Rigs` 移到模型根骨 `174.!Root` 下。

**根因二（applyRootMotion=false 导致约束不求值）**：换 FPS 代码驱动时把 applyRootMotion 设为 false，**Animation Rigging 约束根本不求值**（Unity 已知问题，discussion 824687）。修复：applyRootMotion 恒为 true，根运动由 `OnAnimatorMove` 拦截丢弃、位移仍由代码驱动——约束恢复 + 不穿模两全其美。

**教训**：
- 约束不生效先查「Rigs 是否在 Animator 骨骼流内」，再查「applyRootMotion 是否开启」；
- **静态校验通过 ≠ 运行时生效**，必须用运行时探针（把 target 推离手骨看手是否跟随）确认真实驱动。

## 4.11 Hunter 瞄准枪口朝上（MultiAim aimAxis 枚举陷阱）

**现象**：只有 Hunter 右键瞄准时**枪口朝上**（探针枪口方向 y≈+0.8，子弹仍打向 AimTarget）；荧/芙宁娜正常。

**根因**：Hunter 武器刚性挂在右手骨下、**枪管在手腕局部空间≈+Z**；而 MultiAim 的 `m_AimAxis` 枚举默认 `Y=2`（+Y 指向目标），枪管（+Z）与 +Y 垂直被甩向上。荧/芙宁娜枪管在手腕局部≈+Y，恰好与 aimAxis=Y 一致，所以只有 Hunter 坏。

**解决**：数据驱动修复向导按 `barrelLocal = constrained.InverseTransformPoint(muzzle)` 算枪管局部方向 → `aimAxis = NearestAxis(barrelLocal)` → `offset = FromToRotation(barrelLocal, axisVec).eulerAngles`（offset 是**后置旋转**、右乘局部空间，不是 `FromToRotation(axisVec, barrelLocal)`）。

**踩坑**（都是历次「向导看似成功却无效」的共性根因）：
- MultiAim `m_AimAxis` 枚举是 `X=0, X_NEG=1, Y=2, Y_NEG=3, Z=4, Z_NEG=5`，**Y=2 不是 Z**（读 prefab YAML 时极易看错）；
- 改 `RigConstraint.data` 必须 `ref var d = ref constraint.data`——`var d = data` 是**复制 struct**，改副本不生效；
- `aimAxis` 是 `[NotKeyable]`（Job 创建时烘焙），改后必须 `rigBuilder.Clear(); Build();` 重建才生效（`offset` 是 `[SyncSceneToStream]` 每帧同步）。

## 4.12 NavMesh 反复「走上天 / 走不过来」

**现象**：随从偶尔走不过来、敌人与随从「走上天」（`isOnNavMesh=false`），NavMesh 数据还会莫名丢失，项目无 git、只能靠文件备份手动回滚。

**排查**：写了**只读诊断向导**——检查每个 NavMeshSurface 的设置（collect/size/center/layerMask），列出烘焙层上所有网格/碰撞体并标出「高」（顶部离地 >2.5m）与「浮空」（整体离地 >2m）的疑似走上天物体，再对每个玩家/敌人做 `NavMesh.SamplePosition` 探针（找不到面 = 走不过来）。

**解决**：重写 `RebakeNavMeshWizard` 增强工具——先自动备份 `Game.unity` 到 `_Backup_NavMesh_*` → `NavMesh.RemoveAllNavMeshData()` + 每个 NavMeshSurface `RemoveData()` 清空 → 重新 `BuildNavMesh` → 保存；对疑似走上天物体加 `NavMeshModifier(ignoreFromBuild=true)` 标记不可行走再烘焙；提供「还原备份」菜单。⚠️ API 修正：`NavMeshData` 在 2022.3 只有 `sourceBounds`，没有 `positions/triangles`。

**教训**：
- 改场景（尤其**二进制场景**）必须**先备份**，恢复点思维要贯穿始终；
- 烘焙层上「高/浮空」物体是走上天的常见来源，用启发式标记不可行走是低成本防御；
- `SetDestination` 前校验 `isOnNavMesh`（代理刚启用当帧还没落到网格）。

---

# 五、面试常见问题精选

> 这一节把上面所有内容浓缩成面试中最常被问的问题和可直接背的答案提纲。详细展开见各章节。

**Q1：30 秒介绍你的项目？**
> 一款 Unity URP 的 3D TPS 原型。用**泛型状态机**管理玩家/敌人状态，**代码驱动位移（FPS 式）**做移动，**双 Cinemachine 相机 + Animation Rigging IK** 做瞄准，**帧间 Raycast** 解决高速子弹穿墙，**三层对象池**（状态/子弹/特效）控制 GC，实现了三角色切换（含人机跟随、死亡接管）、丘丘人敌人（寻路/受击/攻击/血条/死亡）、玩家血量/血条、GAME OVER 流程和完整 UI 菜单。

**Q2：为什么用状态机？怎么实现的？**
> **满分版**：因为角色行为是典型的**互斥状态集合**——待机/移动/冲刺/悬空/瞄准/滑铲，同一时刻只能处于一种，且切换有明确的触发条件。用 if/switch 堆，状态一多判断就互相缠绕、没法维护。
> 我的实现是**泛型状态机 `StateMechaine`**：① **状态缓存**——`Dictionary<Type, StateBase>`，首次进入 new 一次 + `Init`，之后永久复用（既是对象池思想减 GC，又保证 `Init` 只执行一次、`Enter/Exit` 每次切换，生命周期清晰）；② **防重入**——当前已是目标状态直接 return，防止同状态反复 Exit/Enter 死循环；③ **集中驱动**——状态 `Enter` 时把 Update 注册到 MonoManager 委托链、`Exit` 注销；④ **玩家敌人共用**——玩家 6 态、敌人 4 态走同一套框架，`SwitchState` 只做分发。加新状态只需加一个类 + 一条分发，符合开闭原则。
>
> **⏱ 精简版**：角色行为是互斥状态集合，switch 会失控。泛型状态机：Dictionary 缓存实例（首次 new + Init、之后复用减 GC），EnterState 先 Exit 旧再 Enter 新、防重入；Init 一次、Enter/Exit 每次切换；Update 注册到 MonoManager 集中驱动。玩家敌人共用一套。

**Q3：怎么解决斜坡上角色抖动？**（高频题）
> **满分版**：这是**三层根因叠加**，我三管齐下：① `cc.isGrounded` 在斜面会**瞬时 flicker**（某一帧 true、下一帧 false）→ 加**稳定性窗口**，连续 5 帧离地才切 Hover；② 窗口期**重力仍在累积** → 窗口期锁定 `verticalSpeed = -2f`，防垂直速度在 `-2f` 和越来越负之间振荡；③ 原单根 Raycast 斜面覆盖低 → 改 **SphereCast**，从 CC 真实底部（`pos.y + cc.center.y - height/2 + skinWidth`）出发，半径 `cc.radius * 0.6`。
> 两个细节：起飞用 `IsHover()` 距离检测（带 fallHeight 阈值防地面小颠簸）、落地用 `cc.isGrounded` 接触检测——**不对称是有意设计**，接触检测在落地瞬间更可靠；主动跳跃要**跳过稳定性延迟**（直接置 `ungroundedFrameCount = 5`），否则起跳被窗口吃掉。
>
> **⏱ 精简版**：三层：① isGrounded 斜面瞬时 flicker → 连续 5 帧离地才切 Hover；② 窗口期重力锁定 -2f 防振荡；③ Raycast 换 SphereCast（CC 真实底部出发）提升斜面覆盖率。起飞距离检测、落地接触检测不对称是有意的。

**Q4：高速子弹为什么会穿墙？怎么解决？**
> **满分版**：Rigidbody 默认是**离散碰撞检测**，子弹速度快到一帧位移超过碰撞体厚度就会「隧穿」，`OnCollisionEnter` 根本检测不到。解决：**帧间 Raycast**——每帧记录上一帧位置 `prevPosition`，下一帧从旧位置向新位置打一条射线（`Physics.Raycast(prevPos, dir, out hit, distance)`），**把整段飞行路径都纳入检测**。命中任何碰撞体播命中特效并回池（防穿墙）；命中 Enemy Tag 调 `Hurt()`。另补**生成时重叠检测**（`OverlapSphere`）：子弹出生在敌人体内立即命中，避免「贴脸打不中」。这是高速弹道（射线枪/狙击）的标准做法。
>
> **⏱ 精简版**：离散碰撞检测在高速下会隧穿。每帧从上一帧位置向当前位置做 Raycast，等效扫掠整段飞行路径，命中任何物体即回池。另加出生重叠检测保证贴脸命中。

**Q5：怎么实现瞄准？双相机为什么这么设计？**
> **满分版**：两架 Cinemachine FreeLook——正常视角（Priority=100）和肩后瞄准视角（Priority=0），进入瞄准时**交换 Priority** 决定谁生效。**为什么双相机**：正常 TPS 视角（角色居中偏侧、镜头远）和瞄准视角（镜头上肩、更近、更低）需要的 **orbit 参数差异大**，单相机做不出自然切换。
> 关键细节：① 切相机前**同步两架的 X/Y 轴角度**，否则视角跳变；② 瞄准时 IK 从 `TwoBoneIK`（腰射持枪）切到 `MultiAim`（右手+身体朝向 AimTarget），退出反向恢复；③ `AimTarget` 是屏幕中心 `ViewportPointToRay(0.5,0.5)` 的命中点，LayerMask 含 Enemy 层可锁敌、排除 Player 层；④ 镜头抖动用 `CinemachineImpulseSource` + 虚拟相机上的 `ImpulseListener`；⑤ **Hunter 例外**——TwoBoneIK 对 MMD 骨骼链不生效，Hunter 手部改用内置 `OnAnimatorIK`（`HunterHandIK`）；MultiAim 的 `aimAxis` 必须按枪管方向配置，否则枪口朝上（见难点 4.11）。
>
> **⏱ 精简版**：双 FreeLook 用 Priority 交换切换正常/瞄准视角，切前同步 X/Y 角度防跳变——单相机调不出两种差异明显的 orbit。IK 上瞄准切 MultiAim、退出恢复 TwoBoneIK；AimTarget 是屏幕中心射线的命中点。（Hunter 例外：MMD 骨骼 TwoBoneIK 不生效，手部用内置 OnAnimatorIK。）

**Q6：对象池在项目里怎么落地的？**
> **满分版**：项目里有**三层对象池**：① **状态实例缓存**——状态机 `Dictionary<Type, StateBase>`，避免状态反复 new；② **子弹池**——`PlayerWeapon` 的 `Queue<PlayerWeaponBullet>`，`GetBullet/RecycleBullet` 管理，`OnEnable/OnDisable` 管生命周期（启用时启动超时协程、停用时停止协程并清空速度）；③ **特效池**——`EffectPool` 全局单例，按预制体分池（`Dictionary<预制体, Queue>`）。
> 两个关键细节：**特效复用前必须显式 `Stop(true, StopEmittingAndClear) + Play()`**，否则从池里拿出来的粒子不会重新播（对象池最常见的坑）；回池判定用 `ParticleSystem.IsAlive()` + `stopAction=Disable` + 10s 超时兜底，而非固定时间销毁——生命周期更准、不怕卡死。
>
> **⏱ 精简版**：三层：状态机缓存状态实例；子弹 Queue 池（OnEnable/OnDisable 管生命周期）；特效池按预制体分池。关键：特效复用前必须 Stop/Clear/Play，否则不重新播；用 IsAlive() 判定播完回池而非固定时间销毁。

**Q7：人机（随从）怎么实现的？**
> **满分版**：`GameManager.playerModels[]` 持有所有角色，`PlayerController.currentPlayerModel` 标记主控，`IsBeControl()` 判断控制权。**切换**：旧角色 `Exit()`（开 NavMeshAgent 转随从 + 回 Idle）、新角色 `Enter()`（关 NavMeshAgent）、相机 Follow/LookAt 重置。**随从 AI**：非主控不进玩家输入逻辑，按与主控的距离切 Idle/Move，超过 `stoppingDistance` 用 NavMeshAgent `SetDestination` 跟随，站位走**三角队形**（`GetFollowerTargetPosition` 按数组索引在主控后方两侧对称展开）。
> 两个关键边界：**位移权**——随从的 `horizontalVelocity` 强制写 0，位移完全交 NavMeshAgent，`cc.Move` 与 NavMeshAgent 是两套位移系统，同时用会抢位置导致随从原地打转；**死亡处理**——`isDead` 拦截切换，主控死亡自动接管下一位存活角色，全部死亡出 GAME OVER。
>
> **⏱ 精简版**：playerModels[] 存角色，IsBeControl() 判断主控。切换 = 旧角色开 NavMeshAgent 转随从、新角色关 NavMeshAgent、相机重置；随从按距离切 Idle/Move 用 NavMeshAgent 跟随，位移权完全交寻路（否则与 cc.Move 抢位置）。

**Q8：讲一个你印象最深、最难的 bug？**（高频题）
> **满分版**（以斜坡抖动为例，按「现象 → 排查 → 根因 → 解决 → 教训」讲）：
> **现象**：角色走斜坡/冲刺时抖动、卡顿，状态机在 Move↔Hover 来回横跳。
> **排查**：逐层拆。① 状态层——`cc.isGrounded` 在斜面瞬时 flicker，一离地就触发悬空切换；② 重力层——即使加了窗口，窗口内重力仍在累积，垂直速度在 `-2f` 和越来越负之间振荡；③ 检测层——原单根 Raycast 在斜面覆盖低、易误判。
> **解决**：三管齐下——稳定性窗口（连续 5 帧离地才切 Hover）+ 窗口期重力锁定 `-2f` + Raycast 改 SphereCast（从 CC 真实底部出发）。
> **教训**：「一个现象背后往往是多层根因」，修一层不够，要按数据流把整条链都验证一遍。
>
> **⏱ 精简版**：选「斜坡抖动」：现象是斜坡上 Move↔Hover 横跳；排查出三层根因（isGrounded 瞬时 flicker、窗口期重力振荡、单射线漏检）；解决是稳定性窗口 + 重力锁定 + SphereCast 三管齐下。教训：现象简单、根因往往多层，按数据流逐层验证。

**Q9：如果让你继续做，下一步做什么？**
> ① 武器系统抽象（WeaponBase + 切换 + 换弹）；② 玩家 HUD（弹药/准星/伤害数字/暂停菜单）；③ 敌人攻击改用 **Animation Event** 判定帧（现在是动画播完一次性结算）；④ 音效系统；⑤ 参数配置化（ScriptableObject）。

**Q10：角色动作模块是怎么实现的？（满分回答模板）**

> **一句话总纲**：我的角色动作模块拆成三层来看——**状态机是骨架，动画是表现层，IK 是修正层**。
>
> **第一层，状态机骨架。** 角色行为是典型的互斥状态集合——待机、移动、冲刺、悬空、瞄准、滑铲，同一时刻只能处于一种，用 if/else 堆起来会失控。所以我写了一个**泛型状态机框架**，玩家和敌人共用：用 `Dictionary` 缓存状态实例，首次 new + Init、之后复用，减少 GC；`EnterState` 先退出旧状态再进入新状态，并做**防重入**；`Init` 只调一次、`Enter/Exit` 每次切换，生命周期清晰。所有状态的 Update 统一注册到 `MonoManager` 的委托链，集中驱动，而不是每个状态一个 MonoBehaviour。
>
> **第二层，动画表现。** 状态 `Enter` 时通过 `CrossFadeInFixedTime` 播对应动画。移动动画用跑酷动作包，**Locomotion 混合树**用一维 Speed 参数控制（0 待机 / 0.33 走 / 0.66 慢跑 / 1 冲刺），状态里用 `LerpSpeedTo` 每帧平滑逼近目标值，所以走→跑→冲刺是自然过渡；瞄准走射单独用 AimingX/AimingY 的 **2D 混合树**，实现八方向走射。
>
> 这里有个关键设计——**动画和位移解耦**。初版用 Root Motion，位移直接取自动画的 `deltaPosition`，速度不可控、还容易穿模。后来改成代码驱动：关掉 `applyRootMotion`，位移全在 `LateUpdate` 的 `cc.Move`，**动画只负责「演」，不负责「动」**。好处是速度完全可控，换任何动作包只需对得上参数。
>
> **第三层，IK 修正。** 因为是持枪角色，必须解决手、身体和动画不匹配的问题——**动画是录好的，它不知道枪在哪、准星在哪、鼠标在哪**。所以用 Animation Rigging 加了三组约束：腰射时 `TwoBoneIK` 把右手固定到枪握把；瞄准时切换 `MultiAim`，让右手和上半身实时朝向屏幕中心的 AimTarget；另有一个头部 IK 跟随鼠标转动、松手回中，让角色「有眼神」。
>
> **边界与复用**：同一套状态机框架玩家、敌人共用；**人机随从只播动画、位移交给 NavMeshAgent**，避免两套位移系统抢位置。三个角色共用同一个动画控制器，换角色只换 Avatar 自动重定向，零成本扩展。
>
> **不足与反思**：敌人攻击判定现在是动画播完一次性结算，更规范的做法是走 **Animation Event** 在指定帧触发伤害；动作数据来自动作包、偏僵硬，想再提升手感可以换 AI 动捕或高质量动捕数据。

> **⏱ 30 秒精简版（怕超时用）**：动作模块 = 状态机骨架 + 动画表现层 + IK 修正层。骨架是玩家敌人共用的泛型状态机（状态缓存、防重入、集中 Update）；表现层用动作包的混合树，Speed 参数平滑过渡，并且**位移与动画解耦**——位移走代码、动画只管演；修正层因为持枪，用 TwoBoneIK 固定手、MultiAim 让手和上身朝准星、头部 IK 跟鼠标。人机随从只播动画、位移交 NavMeshAgent。三个角色共用一套控制器，换模型只换 Avatar。

**Q11：项目分了哪几个模块？（模块划分）**

> **核心认知**：模块划分是「**横切业务**」，和「输入→状态→位移→表现→IK」五层流水线（**纵切职责**）是两种正交视角——一个角色跑动，会同时穿过状态机、移动、动画三个模块。面试先说清你用哪个视角，就赢了一半。
>
> **按职责划分，项目 ≈ 9 个功能模块 + 3 个支撑模块**：
>
> | # | 功能模块 | 核心文件 | 一句话职责 |
> |---|---------|---------|-----------|
> | 1 | 输入模块 | `PlayerController` + `MyInputSystem` | 读 New Input System、算相机相对移动方向 |
> | 2 | 状态机模块 | `StateMechaine` + `StateBase` + 各 `State` | 管理角色互斥状态流转（玩家 6 态 / 敌人 4 态） |
> | 3 | 移动模块 | `PlayerModel` + `PlayerStateBase` | 唯一的位移发生地：`cc.Move`、重力、地面检测、空中控制 |
> | 4 | 动画与 IK 模块 | `Animator` + `TPS_Movement.controller` + Rigging 约束 + `HeadAimTarget` | 播片/混合树过渡 + 手/身体/头对准动态目标 |
> | 5 | 相机模块 | `PlayerController` + 双 FreeLook + Impulse | 正常/瞄准视角切换、开火抖动 |
> | 6 | 战斗模块 | `PlayerWeapon` + `PlayerWeaponBullet` + `PlayerModel` 血量 | 射击、子弹、帧间碰撞、伤害、血量、死亡 |
> | 7 | 敌人 AI 模块 | `EnemyBase` + `ZombieEnemy` + 敌人状态 | 寻路、目标选择、攻击闭环、受击、死亡销毁 |
> | 8 | 人机与切换模块 | `GameManager.playerModels` + `SwitchPlayerModel` | 三角色切换、随从跟随、死亡接管 |
> | 9 | UI 模块 | `UIBase` + 各菜单 + 血条 + GameOverUI + 交互特效 | 主菜单、HUD 血条、GAME OVER、按钮躲避/文字发光 |
>
> | # | 支撑模块 | 核心文件 | 职责 |
> |---|---------|---------|------|
> | 1 | 框架基类 | `SingleMonoBase` / `StateBase` / `IStateMachineOwner` / `PlayerStateBase` | 泛型单例、状态生命周期、控制权判断 |
> | 2 | 管理器 | `MonoManager` / `GameManager` / `UIManager` | 集中式 Update、全局角色列表、WorldSpace Canvas |
> | 3 | 工具与对象池 | `EffectPool` / `HeadAimTarget` / 子弹池 | 特效复用、头部跟随、全局资源管理 |

**Q12：为什么这么分？（划分原则）**

> 划分原则就两条：**一是职责单一**——移动只管位移、动画只管演、输入只管读，互不掺和，每个模块只回答一个「做什么」；**二是低耦合**——模块之间不直接互相引用，通过单例和 Manager 通信。比如：状态机不 new 对象（泛型状态机自己缓存）；敌人找目标只读 `GameManager.playerModels` 数组，不依赖具体玩家类；UI 只认 `UIManager` 的 Canvas，不碰战斗逻辑。
>
> 追问一句「这么分有什么好处」，答**为了扩展性**：敌人加 Boss，只需继承 `EnemyBase` 加一个状态分发，不动玩家模块；换动画，只需换混合树、对得上 Speed 参数，不动移动模块；加武器，只需实现 `PlayerWeapon` 的射击接口。模块分得清楚，以后的改动是「**加法**」而不是「改法」。

**Q13：每个模块分别怎么实现？（满分版 + 精简版）**

> **满分版（逐模块一句实现要点）**：
>
> 1. **输入模块**：New Input System 定义 Action，`PlayerController.Update()` 每帧轮询读值，`moveInput` 归一化后用「相机前向投影 × 输入 y + 相机右向 × 输入 x」算出世界移动方向，再 `InverseTransformVector` 转模型本地方向供转向用。
> 2. **状态机模块**：泛型 `StateMechaine`，`Dictionary<Type, StateBase>` 缓存状态实例（首次 new + Init、之后复用减 GC），`EnterState<T>` 先退出旧状态再进入新状态并防重入；玩家、敌人共用一套。
> 3. **移动模块**：状态层每帧写 `horizontalVelocity` 和 `verticalSpeed`，`PlayerModel.LateUpdate()` 统一 `cc.Move`；地面检测用 SphereCast 从 CC 真实底部出发 + 连续 5 帧离地的稳定性窗口 + 窗口期重力锁定 `-2f`，三管齐下防斜坡抖动。
> 4. **动画与 IK 模块**：状态 `Enter` 时 `CrossFadeInFixedTime` 播对应动画；Speed 参数（0/0.33/0.66/1）驱动 Locomotion 混合树平滑过渡，`LerpSpeedTo` 每帧逼近；持枪/瞄准/头部三组 Rigging 约束（TwoBoneIK + MultiAim ×2）实时对准动态目标。
> 5. **相机模块**：两架 Cinemachine FreeLook 用 Priority 交换切换正常/瞄准视角，切换前同步 X/Y 轴角度防视角跳变；开火/受击用 `CinemachineImpulseSource.GenerateImpulse()` 抖屏。
> 6. **战斗模块**：`PlayerWeapon.Fire()` 射速限制 + 从子弹对象池取子弹 → 子弹 Rigidbody 飞行 + 帧间 Raycast 防穿透 → 命中敌人调 `Hurt()` 扣血 → 血量归零走死亡流程（禁移动/寻路 + 播死动画 + 销毁）。
> 7. **敌人 AI 模块**：`FIndAttackTarget` 每 0.5s 找最近的存活玩家 → NavMeshAgent `SetDestination` 追击（先校验 `isOnNavMesh`）→ 进攻击范围播攻击动画 → 动画播完（`IsAnimationBreak`）结算伤害 → 进入 1.5s 冷却 → 回 Idle 重判距离。
> 8. **人机与切换模块**：`GameManager.playerModels[]` 持有所有角色，`IsBeControl()` 判断主控；切换 = 旧角色开 NavMeshAgent 转随从、新角色关 NavMeshAgent、相机重置；随从按与主控的距离切 Idle/Move，位移完全交 NavMeshAgent（避免与 `cc.Move` 抢位置）。
> 9. **UI 模块**：`UIBase<T>` 统一进出场动画（等动画播完 + 播完停用 Animator 释放位置 + 退场 `SetActive(false)` 防遮挡）；血条是 World Space 复用一套 BillBoard（每帧 `LookRotation(-dir)` 面向相机）；GAME OVER 纯代码构建 Screen Space Overlay Canvas。
> 10. **框架基类**：`SingleMonoBase<T>` 自引用泛型约束 `where T : SingleMonoBase<T>` 提供强类型单例 `INSTANCE`；`StateBase` 定义 Init/Enter/Exit/Update/Destory 生命周期。
> 11. **管理器**：`MonoManager` 把状态的 Update 注册成 `Action` 委托链，一个 Update 统一驱动所有状态；`GameManager` 持有 `playerModels[]`；`UIManager` 持有 WorldSpace Canvas。
> 12. **工具与对象池**：`EffectPool` 按预制体分池（`Dictionary<预制体, Queue>`），复用前显式 `Stop/Clear/Play`、粒子播完自动回池；子弹池用 Queue + `OnEnable/OnDisable` 管理生命周期。

> **⏱ 30 秒精简版（怕超时用）**：九个功能模块——输入（轮询算方向）、状态机（泛型缓存+防重入）、移动（LateUpdate 统一 cc.Move + 分层防抖）、动画 IK（混合树平滑 + 三组约束对位）、相机（Priority 切换 + 同步角度）、战斗（对象池子弹 + 帧间 Raycast）、敌人 AI（NavMesh 追击 + 冷却攻击闭环）、人机（NavMeshAgent 跟随 + 死亡接管）、UI（UIBase 进出场 + WorldSpace 血条）。三个支撑模块——框架基类（泛型单例 + 状态生命周期）、管理器（MonoManager 集中 Update + GameManager）、工具对象池（特效池 + 子弹池）。

**Q14：敌人 AI 模块是怎么设计的？**

> **满分版**：敌人是「`EnemyBase` 抽象基类 + `ZombieEnemy` 具象 + 4 个状态」的三层结构：
> - **目标选择**：`FIndAttackTarget` 遍历 `GameManager.playerModels[]` 找**最近的存活玩家**，每 0.5s 定期刷新——避免锁定已死的尸体、始终攻击最近的威胁；
> - **追击**：NavMeshAgent `SetDestination`，先校验 `isOnNavMesh`（刚启用当帧没落到网格会抛错）；
> - **攻击闭环**：进入 `minAttackDistance` 范围 → Attack 状态播攻击动画 → `IsAnimationBreak` 判定动画播完 → 对目标 `TakeDamage(10)` → 记录 `lastAttackTime` 进入 1.5s 冷却 → 回 Idle 重判距离与冷却；
> - **受击 `Hurt()`**：播 Hit 动画 → 移动动画减速 0.5×（0.5s 恢复）→ 喷血/滴血特效（走特效池）→ 扣血 → 更新血条；
> - **死亡**：血量为 0 → 禁 NavMeshAgent + 禁碰撞体（null 防御）+ 销毁血条 → Dead 状态播动画 → 播完 `Clear()` 销毁。
> 扩展性：`EnemyBase` 是抽象类，加 Boss 只需继承 + 换状态分发，不动玩家模块。
>
> **⏱ 精简版**：EnemyBase 基类 + 状态机：每 0.5s 刷新找最近存活玩家 → NavMeshAgent 追击 → 进范围播攻击动画 → 动画播完结算伤害 → 1.5s 冷却。受击播 Hit + 减速 + 喷血特效 + 扣血；死亡禁寻路/碰撞 + 播 Dead + 销毁。加新敌人只继承基类换状态分发。

**Q15：Animation Rigging 约束不生效时，你是怎么排查的？（MMD 模型三连坑）**

> **满分版**（很能体现「程序员的调试方法论」，按现象→排查→根因→解决讲）：
> **现象**：新接入的 Hunter（MMD 模型转 Humanoid）IK 数据全对（IsValid=true、引用都对、权重 1），但 Play 拖 target 手臂不动；同结构的荧/芙宁娜正常。
> **排查**：静态校验（IsValid/IsChildOf/Bind 不抛异常）全部通过——这是第一个陷阱，**静态校验通过 ≠ 运行时生效**。改用运行时探针（把 target 推离手骨 0.3m 看手跟不跟），最终在控制台抓到硬证据 `Could not resolve ... not a child Transform in the Animator hierarchy`。
> **根因**：Animation Rigging 的 Job 会把**约束 GameObject 本身**绑定到 Animator 骨骼流（`animator.BindStreamProperty`），约束组件必须在骨骼流内；Hunter 的 `Rigs` 容器在角色根下、不在模型骨骼树内 → Job 创建失败 → 永不驱动。修复：把 `Rigs` 移入模型根骨。
> **第二个根因**：换 FPS 代码驱动时把 `applyRootMotion=false`，Animation Rigging **根本不求值**（Unity 已知问题）。修复：applyRootMotion 恒为 true，根运动由 `OnAnimatorMove` 丢弃、位移仍走代码。
> **第三个坑（瞄准）**：MMD 武器枪管在手腕局部≈+Z，而 MultiAim 的 `aimAxis` 枚举 Y=2（+Y 指向目标）→ 枪口被甩向上。按 `InverseTransformPoint(muzzle)` 算枪管局部方向，`aimAxis = NearestAxis(...)`、`offset = FromToRotation(barrelLocal, axisVec)` 数据驱动修复。改 `RigConstraint.data` 必须 `ref var d = ref constraint.data`（`var d = data` 复制 struct 改副本无效）。
>
> **⏱ 精简版**：约束不生效先查两件事——① `Rigs` 容器是否在 Animator 骨骼树内（不在则 Job 永不驱动）；② `applyRootMotion` 是否为 true（false 时约束根本不求值）。静态校验通过不等于运行时生效，必须用运行时探针确认真实驱动。MMD 枪口朝上是 `aimAxis` 枚举陷阱（枪管≈+Z 但默认 aimAxis=Y），按枪管方向重算即可。

---

## 附：项目文件速查

| 类别 | 文件 |
|------|------|
| 项目文档 | `README.md`（总览）、`PROJECT_NOTES.md`（复习笔记）、`CLAUDE.md`（AI 辅助开发指南）、`FPS_HUNTER_DEV.md`（Hunter 开发日志）、本文件（面试复盘） |
| 核心脚本 | `Assets/Scripts/`（Base / Utils / Manager / Player / Enemy / UI / FPS / Diagnostics） |
| 编辑器向导 | `Assets/Scripts/Editor/`（Tools/玩家 菜单：Hunter 四件套 + NavMesh 三件套 + 角色接入/还原） |
| 输入配置 | `Assets/Settings/InputSystem/MyInputSystem.inputactions` |
| 主场景 | `Assets/Scenes/Game.unity`、`Assets/Scenes/GameStart.unity`、`Assets/Scenes/New Scene.unity`（FPS 沙盒） |
| 角色预制体 | `Assets/Resource/Prefabs/`（Lumine FBX / Pilot Furina / Hunter / 丘丘人 / HealthBar） |
| 动画控制器 | `Assets/Resource/Animations/Player/`（`TPS_Movement.controller` 共用 / `Hunter_Parkour.controller` Hunter 专属） |
