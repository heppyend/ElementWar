# ElementWar — 脚本系统技术文档

> **Unity 2022.3.62f3 · URP · 3D TPS（第三人称射击）· 早期开发阶段**
>
> 最后更新：2026-08-05

---

## 目录

- [1. 项目概览](#1-项目概览)
- [2. 目录结构](#2-目录结构)
- [3. 架构总览](#3-架构总览)
- [4. 核心系统详解](#4-核心系统详解)
  - [4.1 输入系统](#41-输入系统)
  - [4.2 状态机框架](#42-状态机框架)
  - [4.3 MonoManager 集中式 Update](#43-monomanager-集中式-update)
  - [4.4 玩家系统](#44-玩家系统)
  - [4.5 敌人系统](#45-敌人系统)
  - [4.6 武器系统](#46-武器系统)
  - [4.7 瞄准与摄像机系统](#47-瞄准与摄像机系统)
  - [4.8 动画与物理移动](#48-动画与物理移动)
- [5. 状态转换图](#5-状态转换图)
- [6. 数据流图](#6-数据流图)
- [7. 已修复 Bug 编年史](#7-已修复-bug-编年史)
- [8. 已知问题与 TODO](#8-已知问题与-todo)
- [9. 命名规范与编码约定](#9-命名规范与编码约定)
- [10. 关键实现细节（踩坑记录）](#10-关键实现细节踩坑记录)
- [11. 动画资源与教程参考](#11-动画资源与教程参考)

---

## 1. 项目概览

| 项目 | 详情 |
|------|------|
| 类型 | 3D 第三人称射击 (TPS) |
| 引擎 | Unity 2022.3.62f3 |
| 渲染管线 | Universal Render Pipeline (URP) 14.0.12 |
| 输入系统 | New Input System 1.14.2 |
| 摄像机 | Cinemachine 2.10.7（双 FreeLook 相机） |
| 动画 | Animator + Animation Rigging 1.2.1（IK 约束） |
| 关卡编辑 | ProBuilder 5.2.4 |
| 渲染风格 | YSA Toon（卡通渲染插件） |
| 特效 | EffectCore（血溅/火花/烟雾粒子特效） |
| 角色模型 | MMD4Mecanim 导入的 PMX 模型（Lumine） |

#### 场景说明

| 场景 | 用途 |
|------|------|
| `Assets/Scenes/GameStart.unity` | 主菜单场景 |
| `Assets/Scenes/Game.unity` | 主游戏场景（含 Player、战斗系统，含 NavMesh 烘焙） |
| `Assets/Scenes/New Scene.unity` | **学习/实验场景**：空白沙盒，用于学习与验证单项技术（物理、动画、UI、着色器等），不属于正式游戏流程，改动自由 |

### 已完成功能

- ✅ 玩家移动（WASD + 手柄，相机相对方向）
- ✅ 行走/冲刺双速混合（Animator BlendTree）
- ✅ 角色旋转朝向移动方向
- ✅ 跳跃/重力/跌落检测（含斜坡稳定性处理）
- ✅ Root Motion 驱动 + 手动垂直速度叠加
- ✅ 瞄准系统（右键瞄准，IK 约束切换，双相机切换）
- ✅ 头部 IK 跟随（HeadAimTarget 鼠标驱动，摇头/点头，自动回中）
- ✅ 射击系统（左键开火，射速限制，枪口火花 VFX）
- ✅ 子弹系统（Rigidbody 飞行 + 帧间射线碰撞检测）
- ✅ 摄像机抖动（CinemachineImpulseSource）
- ✅ 敌人基础框架（状态机 + NavMesh 寻路）
- ✅ 敌人受击反馈（受击动画 + 喷血/滴血特效）
- ✅ 敌人目标选择（找最近 PlayerModel）

### 尚未实现

- ❌ 敌人 AI 行为逻辑（状态机有框架但状态逻辑为空壳）
- ❌ 生命值/伤害系统
- ❌ 武器切换/弹药系统
- ❌ UI/HUD 系统
- ❌ 音效系统
- ❌ 空中水平移动控制
- ❌ 鼠标/手柄瞄准旋转（Look 输入已定义但未使用）

---

## 2. 目录结构

```
Assets/Scripts/
├── Base/                         # 基类 / 基础设施
│   ├── SingleMonoBase.cs         # 泛型单例 MonoBehaviour 基类
│   ├── StateBase.cs              # 状态抽象基类（Init/Enter/Exit/Update/Destory）
│   ├── PlayerStateBase.cs        # 玩家状态基类（重力计算、跳跃触发、瞄准监听）
│   ├── EnemyStateBase.cs         # 敌人状态基类（缓存 enemyModel 引用）
│   └── EnemyBase.cs              # 敌人基类（状态机、寻路、受击、目标选择）
│
├── Utils/                        # 工具 / 框架
│   ├── StateMachine.cs           # 泛型状态机（状态缓存、Enter/Exit、防重入）
│   └── HeadAimTarget.cs          # 头部瞄准目标（鼠标跟随，驱动头部 IK）
│
├── Manager/                      # 全局管理器
│   ├── MonoManager.cs            # 全局 Update 管理器（Action 委托链）
│   └── GameManager.cs            # 游戏管理器（持有所有 PlayerModel 引用）
│
├── Player/                       # 玩家系统
│   ├── PlayerController.cs       # 玩家控制器（输入读取、移动方向、相机管理、IK 切换）
│   ├── PlayerModel.cs            # 角色模型（CC 移动、状态切换、动画播放、地面检测）
│   ├── PlayerWeapon.cs           # 玩家武器（射速控制、子弹实例化、火花特效）
│   ├── PlayerWeaponBullet.cs     # 子弹（Rigidbody 飞行、帧间碰撞检测）
│   └── State/                    # 玩家子状态
│       ├── PlayerIdleState.cs    # 待机 → 移动 / 跳跃
│       ├── PlayerMoveState.cs    # 移动 → 待机 / 跳跃（含动画混合、旋转）
│       ├── PlayerHoverState.cs   # 悬空 → 落地 → 待机
│       └── PlayerAimingState.cs  # 瞄准（射线检测、开火、IK、移动混合）
│
└── Enemy/                        # 敌人系统
    ├── ZombieEnemy.cs            # 僵尸敌人（SwitchState 分发）
    └── State/                    # 敌人子状态
        ├── ZombieIdleState.cs    # 待机 → 移动
        ├── ZombieMoveState.cs    # 移动/追击 → 待机
        ├── ZombieAttackState.cs  # 攻击（仅播放动画）
        └── ZombieDeadState.cs    # 死亡（仅播放动画）
```

### 其他重要目录

| 目录 | 说明 |
|------|------|
| `Assets/Settings/InputSystem/` | New Input System 配置（`.inputactions` + 自动生成 `.cs`） |
| `Assets/Settings/` | URP 质量配置（Performant / Balanced / HighFidelity 三档） |
| `Assets/Resources/Models/Lumine/` | 玩家角色模型（PMX → FBX 导入） |
| `Assets/Resources/Animations/Player/` | 玩家动画资源 |
| `Assets/Plugins/YSA Toon/` | 卡通渲染 Shader 插件 |
| `Assets/Plugins/EffectCore/` | 粒子特效插件（血液、火花、烟雾等） |
| `Assets/Plugins/MMD4Mecanim/` | MMD 模型导入工具 |
| `Assets/Scenes/` | 场景目录（`GameStart` 主菜单 / `Game` 主游戏 / `New Scene` 学习沙盒） |

---

## 3. 架构总览

```
┌──────────────────────────────────────────────────────────────────┐
│                     GameManager (单例)                             │
│                     · playerModels[]                               │
│                     · 敌人寻找攻击目标时从这里获取玩家列表              │
└────────────────────────────┬─────────────────────────────────────┘
                             │ 持有引用
                             ▼
┌──────────────────────────────────────────────────────────────────┐
│                    PlayerController (单例)                         │
│  · 读取 New Input System（Move / Sprint / Aim / Jump / Fire）     │
│  · 计算 localMovement / worldMovement（相机相对方向）               │
│  · 管理双相机切换（freeLookCamera ↔ aimingCamera）                  │
│  · 管理 IK 约束权重（TwoBoneIK ↔ MultiAimConstraint）              │
│  · 持有 currentPlayerModel 引用                                    │
└────────────────────────────┬─────────────────────────────────────┘
                             │ currentPlayerModel
                             ▼
┌──────────────────────────────────────────────────────────────────┐
│               PlayerModel : IStateMachineOwner                    │
│  · CharacterController (Unity 内置碰撞体)                          │
│  · Animator (Root Motion 驱动)                                    │
│  · 重力/跳跃参数 (gravity=-15, jumpHeight=1.5, fallHeight=0.2)    │
│  · SwitchState(PlayerState) → StateMachine                       │
│  · OnAnimatorMove() → cc.Move()                                   │
│  · IsHover() — SphereCast 地面检测                                 │
│  · 持有 IK 约束引用（右手 TwoBoneIK、右手/身体 MultiAimConstraint）  │
│  · 持有 PlayerWeapon 引用                                          │
└──────────┬───────────────┬────────────────┬──────────────────────┘
           │ owns          │ owns           │ owns
           ▼               ▼                ▼
    ┌──────────┐   ┌──────────────┐   ┌──────────────┐
    │StateMachine│  │ PlayerWeapon │   │Animator + CC │
    │(泛型缓存)  │  │ · Fire()     │   │· Root Motion │
    └─────┬─────┘   │ · 子弹实例化 │   │· IK 约束     │
          │         └──────┬───────┘   └──────────────┘
          │                │
    ┌─────┴────────────────┴──────┬──────────────┬──────────────┐
    │                             │              │              │
    ▼                             ▼              ▼              ▼
PlayerIdleState            PlayerMoveState  PlayerHoverState  PlayerAimingState
(待机 → 移动/跳跃)         (移动/冲刺/旋转)  (悬空 → 落地)     (瞄准/射击/移动)
    │                             │              │              │
    └─────────────────────────────┴──────────────┴──────────────┘
                     全部继承自 PlayerStateBase
                     · 每帧重力计算（含斜坡稳定性缓冲）
                     · IsBeControl() 控制权判断
                     · SwitchToHover() 跳跃触发
                     · 瞄准状态全局监听（isAiming / isFire → 自动切瞄准）

═══════════════════════════════════════════════════════════════════

┌──────────────────────────────────────────────────────────────────┐
│                  EnemyBase (抽象) : IStateMachineOwner             │
│  · Animator + StateMachine + NavMeshAgent                        │
│  · 受击特效 (bloodSmashPrefab / bloodDrippingPrefab)              │
│  · FIndAttackTarget() — 找最近 PlayerModel                        │
│  · Hurt(bullet, multiplier) — 受击处理                             │
│  · chaseTarget() — 追击 / IsAttackTargetInAttackRange() — 距离判断│
└────────────────────────────┬─────────────────────────────────────┘
                             │ extends
                             ▼
┌──────────────────────────────────────────────────────────────────┐
│               ZombieEnemy (具体实现)                               │
│  · SwitchState(EnemyState) 分发到具体状态类                         │
└──────────┬───────────────┬────────────────┬──────────────────────┘
           │               │                │
    ┌──────▼──────┐ ┌──────▼──────┐ ┌───────▼───────┐ ┌───────────┐
    │ZombieIdleState│ │ZombieMoveSt│ │ZombieAttackSt│ │ZombieDeadSt│
    │ 待机→移动     │ │ 追击→待机   │ │ 攻击(空壳)    │ │ 死亡(空壳)  │
    └─────────────┘ └────────────┘ └──────────────┘ └───────────┘
                     全部继承自 EnemyStateBase
                     · 自动注册/注销 MonoManager Update
                     · 缓存 enemyModel 引用
```

### 框架层级关系

```
StateBase (抽象)
  ├── PlayerStateBase ─── 重力计算 + 瞄准监听 + 跳跃触发
  │     ├── PlayerIdleState
  │     ├── PlayerMoveState
  │     ├── PlayerHoverState
  │     └── PlayerAimingState
  │
  └── EnemyStateBase ─── 缓存 enemyModel
        ├── ZombieIdleState
        ├── ZombieMoveState
        ├── ZombieAttackState
        └── ZombieDeadState

SingleMonoBase<T> (泛型单例)
  ├── MonoManager ─── 集中式 Update 调度
  ├── PlayerController ─── 输入 + 相机 + IK
  └── GameManager ─── 全局引用持有

IStateMachineOwner (标记接口)
  ├── PlayerModel
  └── EnemyBase → ZombieEnemy

StateMechaine (非 MonoBehaviour 泛型状态机)
  · 状态缓存: Dictionary<Type, StateBase>
  · EnterState<T>() → Exit 旧 → Enter 新
  · 防重入: 同类型跳过
```

---

## 4. 核心系统详解

### 4.1 输入系统

使用 Unity **New Input System**（`MyInputSystem.inputactions`），在 `PlayerController.Update()` 中**轮询**读取，非事件驱动。

#### 输入映射表

| Action | 类型 | 键鼠绑定 | 手柄绑定 | 对应变量 |
|--------|------|---------|---------|---------|
| Move | Vector2 | WASD / 方向键 | 左摇杆 | `moveInput` |
| Look | Vector2 | 鼠标移动 | 右摇杆 | **（已定义但未使用）** |
| Fire | Button | 鼠标左键 | RT | `isFire` |
| IsSprint | Button | Left Shift | — | `isSprint` |
| IsAiming | Button | 鼠标右键 | — | `isAiming` |
| IsJumping | Button | Space | — | `isJumping` |

**输入流转**：
```
Input System → PlayerController.Update() 轮询
  → moveInput (Vector2) / isSprint / isAiming / isJumping / isFire (bool)
  → 各 State.Update() 中读取 → 驱动状态切换和行为
```

**注意事项**：
- 输入在 `PlayerController.OnEnable()/OnDisable()` 中整体启用/禁用
- `moveInput` 已经 `.normalized`，防止斜向移动过快
- `Look` 输入虽然已定义映射（鼠标、手柄右摇杆），但 `PlayerController` 中并未读取和处理，**摄像机旋转目前完全由 Cinemachine FreeLook 自行处理**

---

### 4.2 状态机框架

#### 核心设计

```
StateMechaine (泛型状态机)
  ├── owner: IStateMachineOwner           // 状态宿主
  ├── currentState: StateBase             // 当前激活状态
  └── stateDic: Dictionary<Type, StateBase> // 状态缓存池
```

**关键特性**：

1. **状态缓存**：首次进入某状态时 `new T()` 创建并调用 `Init(owner)`，之后保存在字典中复用，避免频繁 GC 分配
2. **防重入**：`currentState.GetType() == typeof(T)` 时直接 return，防止同一状态反复 Exit/Enter
3. **生命周期**：`EnterState<T>()` → `Exit()` 旧状态 → `Enter()` 新状态
4. **销毁清理**：`Stop()` 依次 `Exit()` 当前状态 + `Destory()` 所有缓存状态 + 清空字典

#### 状态基类

```csharp
public abstract class StateBase
{
    public abstract void Init(IStateMachineOwner owner);  // 首次创建时调用
    public abstract void Enter();   // 每次进入状态时调用（注册 MonoManager Update）
    public abstract void Exit();    // 每次退出状态时调用（注销 MonoManager Update）
    public abstract void Update();  // 每帧由 MonoManager 驱动
    public abstract void Destory(); // 状态机销毁时调用
}
```

**⚠️ 注意**：`Destory()` 是刻意保留的拼写（与项目代码中 "Destroy" 命名区分），不是拼写错误。

---

### 4.3 MonoManager 集中式 Update

为避免每个状态都是一个 MonoBehaviour 带来的性能开销，项目使用 `MonoManager` 单例集中调度所有状态的 Update：

```
MonoManager.Update()  (唯一的 MonoBehaviour Update 循环)
  └── updataAction?.Invoke()  // 多播委托
        ├── PlayerIdleState.Update()
        ├── PlayerMoveState.Update()
        ├── ...
        └── 任一 EnemyState.Update()
```

- 状态 `Enter()` 时 `MonoManager.INSTANCE.AddUpdateAction(Update)`
- 状态 `Exit()` 时 `MonoManager.INSTANCE.RemoveUpdateAction(Update)`
- 一次状态切换 = 一次 Add + 一次 Remove，保持委托链整洁

**优点**：减少 MonoBehaviour 开销，所有 Update 集中一处便于调试和性能分析
**代价**：委托调用有微小间接开销（但对于状态数量级可忽略）

---

### 4.4 玩家系统

#### PlayerController（输入 + 相机 + IK）

| 职责 | 说明 |
|------|------|
| 输入轮询 | 每帧从 MyInputSystem 读取 Move/Sprint/Aim/Jump/Fire |
| 移动方向计算 | 基于相机朝向将 `moveInput` 转为 `worldMovement`（世界空间）和 `localMovement`（模型本地空间） |
| 相机管理 | `EnterAim()`/`ExitAim()` 同步双相机角度并切换 Priority |
| IK 管理 | 瞄准时启用 MultiAimConstraint（手+身体），关闭 TwoBoneIKConstraint（右手） |
| 摄像机抖动 | `ShakeCamera()` 触发 CinemachineImpulseSource |
| 光标锁定 | `Start()` 中 `Cursor.lockState = CursorLockMode.Locked` |

**移动方向计算公式**：
```csharp
// 相机前方向量投影到水平面
cameraForwardProjection = (camera.forward.x, 0, camera.forward.z).normalized;
// 世界空间移动方向
worldMovement = cameraForwardProjection * moveInput.y + camera.right * moveInput.x;
// 本地空间（用于旋转计算）
localMovement = model.InverseTransformVector(worldMovement);
```

#### PlayerModel（移动 + 状态 + 动画）

| 组件 | 用途 |
|------|------|
| `CharacterController` | Unity 内置碰撞体，通过 `cc.Move()` 驱动 |
| `Animator` | Root Motion 水平移动 + 动画状态机 |
| `StateMechaine` | 状态机实例 |
| `PlayerWeapon` | 武器引用 |
| IK 约束 | `rightHandConstraint`（TwoBoneIK）、`rightHandAimConstraint`（MultiAim）、`bodyAimConstraint`（MultiAim） |

**核心参数**：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `gravity` | -15 | 重力加速度 |
| `jumpHeight` | 1.5 | 跳跃高度（用于计算初速度） |
| `fallHeight` | 0.2 | 离地触发 Hover 的阈值距离 |
| `HOVER_STABILITY_FRAMES` | 5 | 连续离地帧数阈值（≈0.083s@60fps） |

#### 各玩家状态职责

| 状态 | 进入动作 | Update 逻辑 | 退出条件 |
|------|---------|------------|---------|
| **Idle** | 播放 "Idle" 动画 | 检测移动输入 → Move；检测跳跃 → Hover | moveInput ≠ 0 / isJumping |
| **Move** | 播放 "Move" 动画 | Sprint 混合 (MoveBlend: 0→1)；旋转朝向移动方向；检测待机/跳跃 | moveInput = 0 / isJumping |
| **Hover** | 播放 "Hover" 动画 | 检测落地 | cc.isGrounded → Idle |
| **Aiming** | 播放 "Aiming" 动画；EnterAim()；更新瞄准目标 | 射线更新 `AimTarget.position`；检测开火 → Fire()；检测退出瞄准；混合 AimingX/Y 移动输入 | !isAiming && !isFire → Idle |

---

### 4.5 敌人系统

#### EnemyBase（抽象基类）

| 组件/功能 | 说明 |
|-----------|------|
| `NavMeshAgent` | Unity 寻路系统，`stoppingDistance = minAttackDistance` |
| `StateMechaine` | 玩家同款泛型状态机 |
| `attackTarget` | 当前攻击目标（最近的 PlayerModel） |
| `FIndAttackTarget()` | 遍历 `GameManager.INSTANCE.playerModels[]`，找距离最近的 |
| `Hurt(bullet, multiplier)` | 受击处理：播放 Hit 动画 + 喷血特效 + 滴血特效 |
| `chaseTarget()` | `navMeshAgent.SetDestination(attackTarget.position)` |
| `IsAttackTargetInAttackRange()` | 距离 < minAttackDistance（默认 1） |

#### Zombie 状态流转（当前实现）

```
Idle ──(目标不在攻击范围)──→ Move ──(追击中)──→ Idle（当进入攻击范围时）
                                                      ↓
                                              (应切 Attack，但当前未实现)
```

**⚠️ 当前状态问题**：
- `ZombieAttackState` 只播放 Attack 动画，没有任何实际攻击逻辑
- `ZombieDeadState` 只播放 Dead 动画，没有死亡条件触发
- `ZombieMoveState` 追击到攻击范围后切回 Idle 而不是 Attack
- 没有 Patrol/Wander 行为

---

### 4.6 武器系统

#### PlayerWeapon

```csharp
public class PlayerWeapon : MonoBehaviour
{
    Transform bulletSpawnPoint;          // 子弹生成点
    PlayerWeaponBullet bulletEffectPrefab; // 子弹预制体
    GameObject bulletSparkPrefab;        // 枪口火花预制体
    float bulletInterval = 0.15f;        // 射速限制（≈6.67发/秒）
    float lastFireTime;                  // 上次开火时间戳
}
```

**Fire(Vector3 targetPos)**：
1. 检查射速间隔（`Time.time - lastFireTime < bulletInterval` 则跳过）
2. 计算方向 `(targetPos - bulletSpawnPoint.position).normalized`
3. 实例化子弹 + 火花，设置朝向

#### PlayerWeaponBullet

```
生命周期：Instantiate → rb.velocity = forward * flyPower → CheckCollision() 每帧 → Destroy(lifetime≈10s)
```

**碰撞检测**（帧间 Raycast）：
```csharp
CheckCollision() {
    Vector3 dir = transform.position - prevPosition;  // 两帧间位移
    float distance = |dir|;
    Physics.Raycast(prevPosition, dir.normalized, out hit, distance);
    if (hit.collider.CompareTag("Enemy"))
        hit.collider.GetComponent<EnemyBase>().Hurt(this, 1);
}
```

**⚠️ 设计注意事项**：
- 子弹使用 Rigidbody 运动 + 手动帧间 Raycast 碰撞检测，而非 `OnCollisionEnter`（高速子弹可能穿透）
- 当前只检测 "Enemy" Tag 碰撞，不处理墙壁/地板碰撞
- `damage` 字段已定义但 `Hurt()` 中未使用（伤害系统未实现）

---

### 4.7 瞄准与摄像机系统

#### 双相机架构

```
正常状态 ───── freeLookCamera (Priority=100) ───── 第三人称自由视角
                aimingCamera (Priority=0)

瞄准状态 ───── freeLookCamera (Priority=0)
                aimingCamera (Priority=100) ───── 肩后瞄准视角
```

#### 相机切换流程

```
EnterAim():
  1. 同步 aimingCamera 角度 ← freeLookCamera 角度
  2. IK 切换: TwoBoneIKConstraint(weight=0), MultiAimConstraint(weight=1)
  3. 相机 Priority 交换: freeLook=0, aiming=100

ExitAim():
  1. 同步 freeLookCamera 角度 ← aimingCamera 角度（保持视角一致）
  2. IK 恢复: TwoBoneIKConstraint(weight=1), MultiAimConstraint(weight=0)
  3. 相机 Priority 交换: freeLook=100, aiming=0
```

#### IK 约束

| 约束 | 类型 | 用途 | 激活时机 |
|------|------|------|---------|
| `rightHandConstraint` | TwoBoneIKConstraint | 非瞄准时右手自然持枪位置 | 正常状态 |
| `rightHandAimConstraint` | MultiAimConstraint | 瞄准时右手跟随 AimTarget | 瞄准状态 |
| `bodyAimConstraint` | MultiAimConstraint | 瞄准时身体朝向目标 | 瞄准状态 |

两个约束都 Reference `AimTarget` Transform。头部的 `Head Aim Constraint`（MultiAimConstraint）Reference `Head Aim Target` Transform。

#### HeadAimTarget（头部跟随）

`HeadAimTarget` 独立于武器瞄准系统，专门驱动角色头部 IK，让头部自然地跟随鼠标/视角方向微调：

```
默认位置 = 相机位置 + restOffset（Inspector 可调，用于设定角色默认看向）
鼠标偏移 = 鼠标 delta → 相机右轴(水平) + 相机上轴(垂直) + 仅向后(-forward, 禁止向前)
最终位置 = 默认位置 + 鼠标偏移（偏移量受 maxHorizontal/maxVertical/maxBackward 限制）
```

- 移动鼠标 → 头部跟随偏移（摇头/点头）
- 松开鼠标 → Lerp 回中到默认朝向
- 前后方向只允许向后（Clamp 到 `[-maxBackward, 0]`），绝不会跑到相机前方

#### 瞄准目标更新

```csharp
UpdateAimingTarget() {
    // 屏幕中心射线
    Ray ray = Camera.main.ViewportPointToRay(0.5, 0.5, 0);
    if (Physics.Raycast(ray, out hit, maxRayDistance, aimLayerMask))
        AimTarget.position = hit.point;                // 命中点
    else
        AimTarget.position = ray.origin + ray.direction * maxRayDistance; // 远端点
}
```

**瞄准默认覆盖所有层 (`aimLayerMask = ~0`)**，可通过 Inspector 排除不需要的层。

#### 瞄准状态下的移动

`PlayerAimingState` 通过 BlendTree 参数 `AimingX` / `AimingY` 驱动瞄准时的八方向移动动画（前后左右 + 对角线混合），值从 `moveInput` 线性插值而来。

---

### 4.8 动画与物理移动

#### 移动架构

```
OnAnimatorMove()  (由 Animator 驱动调用)
  │
  ├── 地面状态 (Idle/Move/Aiming):
  │     horizontalMovement = animator.deltaPosition    // Root Motion 水平位移
  │     缓存 animator.velocity 到 3 帧滑动窗口          // 用于跳跃时的动量保持
  │
  ├── Hover 状态:
  │     horizontalMovement = averageDeltaMovement * dt  // 用跳跃前的平均速度
  │
  └── 所有状态:
        playerDeltaMovement.y = verticalSpeed * dt      // 手动叠加垂直速度
        cc.Move(playerDeltaMovement)                    // 统一提交移动
```

#### 三帧速度缓存

```csharp
// 缓存前 3 帧的 animator.velocity，用于 Hover 状态保持水平动量
UpdateAverageCachsSpeed(animator.velocity)  →  speedCache[3]
averageDeltaMovement = (v0 + v1 + v2) / 3
```

这样跳跃时玩家能保持冲刺/走路的惯性水平速度，而不会瞬间静止。

#### 跳跃初速度公式

```csharp
// 物理公式 v² = 2gh 中 v = √(2gh)，这里 g 取绝对值
verticalSpeed = Mathf.Sqrt(-2 * gravity * jumpHeight)
// gravity=-15, jumpHeight=1.5 → v ≈ 6.7 m/s
```

---

## 5. 状态转换图

### 玩家状态

```
                         ┌──────────────────────────────┐
                         │  PlayerStateBase.Update()     │
                         │  全局监听: isAiming || isFire  │
                         └──────────┬───────────────────┘
                                    │ 从任意地面状态 → Aiming
                                    ▼
┌──────────────────────────────────────────────────────────────────┐
│                                                                    │
│    ┌──────────┐  moveInput≠0   ┌──────────┐                      │
│    │   Idle   │ ────────────→  │   Move   │                      │
│    │  (待机)  │ ←──────────── │  (移动)   │                      │
│    └────┬─────┘  moveInput=0   └────┬─────┘                      │
│         │ isJumping                 │ isJumping                   │
│         │ 或 fallHeight 超阈值       │                             │
│         ▼                           ▼                             │
│    ┌────────────────────────────────────┐                         │
│    │            Hover (悬空)             │                        │
│    │  · 保持水平动量（3帧平均速度）       │                         │
│    │  · 重力累积下降                     │                         │
│    └────────────────┬───────────────────┘                         │
│                     │ cc.isGrounded                                │
│                     ▼                                              │
│                ┌──────┐                                           │
│                │ Idle │  (落地)                                    │
│                └──────┘                                           │
│                                                                   │
│    ┌──────────────┐  !isAiming && !isFire                         │
│    │   Aiming     │ ──────────────────────→  Idle                 │
│    │  (瞄准/射击)  │                                               │
│    └──────────────┘                                               │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

**⚠️ 重要**：Aiming 可以从任何地面状态（Idle/Move/Hover）进入，因为瞄准检测在 `PlayerStateBase.Update()` 基类中，每帧先于子类逻辑执行。

### 敌人状态（僵尸）

```
         ┌──────┐  不在攻击范围   ┌──────┐
         │ Idle │ ────────────→  │ Move │
         │(待机) │ ←──────────── │(追击) │
         └──┬───┘   进入攻击范围   └──┬───┘
            │                       │
            ▼                       ▼
        ┌────────┐            ┌──────────┐
        │ Attack │            │  Dead    │
        │ (攻击) │            │ (死亡)   │
        └────────┘            └──────────┘
```

**当前实际行为**：Move 追击到攻击范围后切回 Idle → Idle 检测不在范围又切 Move，形成 **Idle↔Move 循环**。Attack/Dead 状态没有实际的触发条件。

---

## 6. 数据流图

```
                    ┌──────────────────┐
                    │  New Input System │
                    └────────┬─────────┘
                             │ 轮询
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    PlayerController.Update()                  │
│                                                              │
│  moveInput ← Move.ReadValue<Vector2>().normalized            │
│  isSprint  ← IsSprint.IsPressed()                            │
│  isAiming  ← IsAiming.IsPressed()                            │
│  isJumping ← IsJumping.IsPressed()                           │
│  isFire    ← Fire.IsPressed()                                │
│                                                              │
│  worldMovement = cameraFwd * moveInput.y + cameraRight * x   │
│  localMovement = model.InverseTransformVector(worldMovement) │
└────────────────────────┬────────────────────────────────────┘
                         │ 全局可访问 (INSTANCE / 公共字段)
                         ▼
┌─────────────────────────────────────────────────────────────┐
│               PlayerStateBase.Update() (每帧)                 │
│                                                              │
│  1. 重力计算 (cc.isGrounded? + 稳定性窗口)                     │
│  2. 瞄准检测 (isAiming || isFire → SwitchState(Aiming))      │
└──────┬──────────────────────────────────────────────────────┘
       │
       ├─── PlayerIdleState.Update()
       │      ├── moveInput≠0 → SwitchState(Move)
       │      └── isJumping → SwitchToHover()
       │
       ├─── PlayerMoveState.Update()
       │      ├── isJumping → SwitchToHover()
       │      ├── moveInput=0 → SwitchState(Idle)
       │      ├── Sprint 混合: animator.SetFloat("MoveBlend", 0→1)
       │      └── 旋转: transform.Rotate(0, Atan2(localMovement), 0)
       │
       ├─── PlayerHoverState.Update()
       │      └── cc.isGrounded → SwitchState(Idle)
       │
       └─── PlayerAimingState.Update()
              ├── !isAiming && !isFire → SwitchState(Idle)
              ├── isFire → weapon.Fire(AimTarget.position) + ShakeCamera()
              ├── UpdateAimingTarget() (屏幕中心射线)
              └── AimingX/Y BlendTree 混合

                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 PlayerModel.OnAnimatorMove()                  │
│                                                              │
│  playerDeltaMovement = animator.deltaPosition (Root Motion)  │
│  或 averageDeltaMovement * dt (Hover 时)                     │
│  playerDeltaMovement.y = verticalSpeed * dt (手动垂直)       │
│  cc.Move(playerDeltaMovement)                                │
└─────────────────────────────────────────────────────────────┘
```

---

## 7. 已修复 Bug 编年史

### 第一轮：逻辑错误修复（2026-07-29）

| # | 严重程度 | 文件:行 | 问题 | 修复 |
|---|---------|---------|------|------|
| 1 | **🔴 关键** | `PlayerStateBase.cs:58` | `IsBeControl()` 中 `playerModel = playerController...` 误用赋值 `=` 而非比较 `==` | 改为 `==` |
| 2 | 🟡 次要 | `PlayerStateBase.cs:47` | 着地时 `verticalSpeed = gravity * dt`（约 -0.24/帧），语义不当 | 改为常量 `-2f` |

### 第二轮：地面检测重构 — 修复斜坡卡顿（2026-07-29）

**根因**：
- 原 `IsHover()` 用单根 `Physics.Raycast` 从 `transform.position` 向下检测，未从 CC 真实底部出发
- 斜坡上 `cc.isGrounded` 瞬时 flicker → 触发 IsHover → 单射线在斜面可靠性差 → 误判离地 → 状态跳变 → 卡顿

| # | 严重程度 | 文件:行 | 问题 | 修复 |
|---|---------|---------|------|------|
| 3 | **🔴 关键** | `PlayerModel.cs:94-111` | IsHover() 单根 Raycast 不靠谱 | 重写为 **SphereCast**：从 CC 底部 `(pos.y + center.y - height/2 + skinWidth)` 出发，半径 `cc.radius * 0.6` |
| 4 | **🔴 关键** | `PlayerStateBase.cs:39-55` | isGrounded flicker 立即触发状态切换 | 新增 `ungroundedFrameCount` + `HOVER_STABILITY_FRAMES = 3` |

### 第三轮：垂直振荡修复 — 解决陡坡+冲刺卡顿（2026-07-29）

**根因**：第二轮只阻止了状态切换，但窗口期内重力仍在累积。斜坡 flicker 时 `verticalSpeed` 在 -2f 和越来越负的值之间振荡 → 冲刺/陡坡时抖动更剧烈。

| # | 严重程度 | 文件:行 | 问题 | 修复 |
|---|---------|---------|------|------|
| 5 | **🔴 关键** | `PlayerStateBase.cs:43-47` | 窗口期仍在累积重力 | 窗口期锁定 `verticalSpeed = -2f` |
| 6 | 🟡 调优 | `PlayerModel.cs:40` | `HOVER_STABILITY_FRAMES = 3` 不够 | 提升至 **5 帧**（代码中注释说5帧，但实际当前值 = **100**） |
| 7 | 🟡 联动 | `PlayerStateBase.cs:82` | 主动跳跃需等稳定性窗口 | 跳跃时直接将 `ungroundedFrameCount` 设为阈值跳过延迟 |

### 第四轮：Bug 修复（2026-08-01）

#### 修复 #1 — HOVER_STABILITY_FRAMES 修正

**文件**: `PlayerModel.cs:51` | **严重程度**: 🔴 关键

**问题**：常量值为 100，注释明确说应为 5 帧。导致真实延迟 ≈1.67s@60fps。

<details>
<summary>📋 原始代码（点击展开，用于回档）</summary>

```csharp
// PlayerModel.cs:51 — 原始
public const int HOVER_STABILITY_FRAMES = 100;
```

</details>

**修复后**:
```csharp
public const int HOVER_STABILITY_FRAMES = 5;
```

---

#### 修复 #2 — 子弹穿墙修复

**文件**: `PlayerWeaponBullet.cs:41-56` | **严重程度**: 🔴 关键

**问题**：子弹只检测 Enemy Tag，碰到墙壁/地板不销毁，会穿墙。且 `GetComponent<EnemyBase>()` 无 null 防御。

<details>
<summary>📋 原始代码（点击展开，用于回档）</summary>

```csharp
// PlayerWeaponBullet.cs:41-56 — 原始 CheckCollision()
void CheckCollision()
{
    RaycastHit hit;
    Vector3 dir=transform.position-prevPosition;//子弹方向
    float distance=Vector3.Distance(transform.position,prevPosition);//两帧之间的子弹飞行距离

    //绘制线段检测碰撞
    if(Physics.Raycast(prevPosition,dir.normalized,out hit, distance))
    {
        //检测是否为敌人
        if (hit.collider.CompareTag("Enemy"))
        {
            EnemyBase enemy=hit.collider.GetComponent<EnemyBase>();
            enemy.Hurt(this, 1);
        }
    }
}
```

</details>

**修复后**:
```csharp
void CheckCollision()
{
    RaycastHit hit;
    Vector3 dir=transform.position-prevPosition;
    float distance=Vector3.Distance(transform.position,prevPosition);

    if(Physics.Raycast(prevPosition,dir.normalized,out hit, distance))
    {
        if (hit.collider.CompareTag("Enemy"))
        {
            EnemyBase enemy=hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.Hurt(this, 1);
        }
        //击中任何碰撞体后销毁子弹（防止穿墙）
        Destroy(gameObject);
    }
}
```

---

#### 修复 #3 — 落地检测：保持 `cc.isGrounded`（未改动逻辑）

**文件**: `PlayerHoverState.cs:19-24` | **严重程度**: 无（确认为非 Bug）

**问题**：曾尝试将落地检测改为 `!IsHover()`，导致落地后动画无法切回待机。回滚后确认：起飞用 `IsHover()`（距离检测防颠簸）与落地用 `cc.isGrounded`（接触检测）的不对称是**有意为之的正确设计**。仅更新了注释。

<details>
<summary>📋 原始代码（点击展开，用于回档）</summary>

```csharp
// PlayerHoverState.cs:19-24 — 原始 Update()
public override void Update()
{
    base.Update();
    #region 检测角色是否落在地面上
    if (playerModel.cc.isGrounded)
    {
        playerModel.SwitchState(PlayerState.Idle);
    }
    #endregion 
}
```

</details>

**当前（与原始逻辑一致，仅注释更详细）**:
```csharp
public override void Update()
{
    base.Update();
    #region 检测角色是否落在地面上
    // 落地用 cc.isGrounded（接触检测），起飞用 IsHover()（距离检测）
    // 二者不对称是有意为之：
    // - 起飞需要 fallHeight 阈值防止地面小颠簸误触发
    // - 落地需要 cc.Move() 的精确碰撞检测，SphereCast 在 CC 落地瞬间可能不可靠
    if (playerModel.cc.isGrounded)
    {
        playerModel.SwitchState(PlayerState.Idle);
    }
    #endregion
}
```

---

#### 修复 #4 — MyInputSystem 资源释放

**文件**: `PlayerController.cs` | **严重程度**: 🟡 中等

**问题**：`MyInputSystem` 在 `OnDestroy` 中未调用 `Dispose()`，退出 Play Mode 时报 Assert 泄漏警告。

<details>
<summary>📋 原始代码（点击展开，用于回档）</summary>

```csharp
// PlayerController.cs — 原始末尾（OnDisable 之后无 OnDestroy）
private void OnDisable()
{
    input.Disable();
}

} // class closing
```

</details>

**修复后**:
```csharp
private void OnDisable()
{
    input.Disable();
}

private void OnDestroy()
{
    input?.Dispose();
}
```

---

#### 修复 #5 — OnAnimatorMove Animator null 防御

**文件**: `PlayerModel.cs:152` | **严重程度**: 🟢 低

**问题**：`OnAnimatorMove()` 直接访问 `animator.deltaPosition`，若 Animator 被意外移除会 NRE。

<details>
<summary>📋 原始代码（点击展开，用于回档）</summary>

```csharp
// PlayerModel.cs:152 — 原始 OnAnimatorMove() 开头
private void OnAnimatorMove()
{
    Vector3 playerDeltaMovement = animator.deltaPosition;//获取动画控制器当前帧的位置信息
```

</details>

**修复后**:
```csharp
private void OnAnimatorMove()
{
    if (animator == null) return;
    Vector3 playerDeltaMovement = animator.deltaPosition;//获取动画控制器当前帧的位置信息
```

---

## 8. 已知问题与 TODO

### 🔴 高优先级

| # | 问题 | 位置 | 说明 | 建议方案 |
|---|------|------|------|---------|
| 1 | **无伤害/生命值系统** | 全局 | 子弹有 `damage=10` 字段但 `Hurt()` 未使用；无血量、死亡、复活逻辑 | 优先建立 Health/Damage 接口 |
| 2 | **敌人 AI 空壳** | `Enemy/State/` | Attack/Dead 状态只播动画，无实际逻辑；Move→Idle 循环不切 Attack | 实现完整行为树或状态逻辑 |

### 🟡 中优先级

| # | 问题 | 位置 | 说明 | 建议方案 |
|---|------|------|------|---------|
| 3 | **空中无法控制移动方向** | `PlayerHoverState.cs` | 跳跃后 `Update()` 只检测落地，完全不处理 `moveInput` | 在 Hover 中读取 moveInput 施加水平力或速度 |
| 4 | **Look 输入未使用** | `PlayerController.cs` | InputSystem 定义了 Look Action（鼠标/右摇杆），但代码中未读取 | 接入 Cinemachine Input Provider 或手动控制相机 |
| 5 | ~~落地检测不对称~~ **（已确认非 Bug）** | `PlayerHoverState.cs:20` | 落地用 `cc.isGrounded`（接触检测），起飞用 `IsHover()`（距离检测）—— 不对称是有意为之 | 保持现状 |
| 6 | **无武器切换/弹药系统** | 全局 | 只有一把武器，无换弹、弹药管理等 | 抽象 WeaponBase，设计武器管理器 |

### 🟢 低优先级 / 功能缺失

| # | 类别 | 说明 |
|---|------|------|
| 7 | UI/HUD | 无任何 UI：血量、弹药、准星、伤害数字、菜单 |
| 8 | 音效 | 无 Audio System：射击、脚步、受击、环境音 |
| 9 | 动画事件 | 无 Animation Event 驱动攻击判定帧、脚步音效等 |
| 10 | 对象池 | 子弹实例化用 `Instantiate`/`Destroy`，大量射击时有 GC 压力 |
| 11 | 多玩家 | `GameManager.playerModels[]` 是数组但当前只有单玩家 |
| 12 | 敌人类型 | 仅 ZombieEnemy 一种，EnemyBase 框架支持扩展但缺少具体实现 |

---

## 9. 命名规范与编码约定

| 类别 | 规范 | 示例 |
|------|------|------|
| 类名 | PascalCase | `PlayerController`, `PlayerWeaponBullet` |
| 接口 | `I` 前缀 + PascalCase | `IStateMachineOwner` |
| 枚举类型 | PascalCase | `PlayerState`, `EnemyState` |
| 枚举值 | PascalCase | `PlayerState.Idle`, `EnemyState.Move` |
| 公共字段 | camelCase | `moveInput`, `verticalSpeed` |
| 私有字段 | camelCase | `stateMechaine`, `speedCache` |
| 方法 | PascalCase | `SwitchState()`, `PlayStateAnimation()` |
| 常量 | UPPER_SNAKE_CASE | `HOVER_STABILITY_FRAMES`, `CACHE_SIZE` |
| 注释语言 | 中文（中文） | `/// <summary>玩家控制器</summary>` |
| Inspector 字段 | `[Tooltip("中文说明")]` | `[Tooltip("子弹生成的位置")]` |
| `[HideInInspector]` | 用于运行时赋值的 public 字段 | `[HideInInspector] public Vector2 moveInput;` |

### 代码风格约定

- **单例模式**：通过 `SingleMonoBase<T>` 继承实现，访问用 `INSTANCE` 静态属性
- **状态注册**：状态在 `Enter()` 中注册 MonoManager Update，`Exit()` 中注销
- **输入访问**：状态通过 `PlayerController.INSTANCE` 单例直接访问输入字段
- **字段访问**：偏好 `public` 字段 + `[HideInInspector]`，而非 `[SerializeField] private`（快速原型风格）
- **拼写注意**：项目中使用 `StateMechaine`（Machine 拼写变体）、`Destory`（非标准拼写）、`updataAction`（update 变体），这些是刻意保留的命名，新增代码建议用标准拼写

---

## 10. 关键实现细节（踩坑记录）

### 10.1 斜坡稳定性

**问题**：`CharacterController.isGrounded` 在斜坡上不可靠，会瞬时 flicker（某一帧 true，下一帧 false），导致状态机在 Move ↔ Hover 间来回切换。

**解决方案（三层防护）**：
1. **SphereCast 替代 Raycast**：`IsHover()` 用球体投射，在斜面上的覆盖率远超单根射线
2. **稳定性窗口**：`ungroundedFrameCount` 计数 + `HOVER_STABILITY_FRAMES` 阈值，只有连续 N 帧离地才真正切换
3. **重力锁定**：稳定窗口期内 `verticalSpeed` 锁定为 -2f，防止重力累积导致垂直振荡

**⚠️ 当前值问题**：`HOVER_STABILITY_FRAMES` 实际值为 100（代码注释说 5），这意味着在地面上有 100 帧（约 1.67s@60fps）的重力冻结窗口。如果玩家走下一个高台边缘，将会有 1.67 秒的延迟才能真正进入下落状态。这很可能是之前调试时改大后忘记改回。

### 10.2 CC 底部坐标计算

```csharp
float ccBottom = transform.position.y + cc.center.y - cc.height * 0.5f + cc.skinWidth;
```

必须考虑 `cc.center`（CC 中心偏移）、`cc.height`、`cc.skinWidth`（碰撞体外壳厚度），不能简单用 `transform.position` 代替。

### 10.3 Root Motion + 手动重力

- 水平：完全依赖 Animator 的 `deltaPosition`（Root Motion）
- 垂直：手动计算 `verticalSpeed` 叠加到 `deltaPosition.y`
- Hover 时：用跳跃前 3 帧 `animator.velocity` 的平均值替代 Root Motion（因为悬空动画没有水平位移）

### 10.4 IK 权重时机

`EnterAim()`/`ExitAim()` 在 `PlayerAimingState.Enter()`/`Exit()` 中调用。但基类 `PlayerStateBase.Update()` 中如果检测到 `isAiming || isFire`，会在 `Enter()` 之前就切换到 Aiming 状态。此时 `currentPlayerModel` 可能为 null（如果 Start 还没跑完），`ExitAim()` 在 `Start()` 中被调用。需要确认运行时 `currentPlayerModel` 的初始化时机。

### 10.5 子弹碰撞检测

子弹不使用 `OnCollisionEnter`（高速物体可能穿透），而是在 `Update()` 中手动做帧间 Raycast：记录上一帧位置，每帧计算位移差，从 `prevPosition` 向当前 `position` 发射射线。这是高速子弹碰撞检测的标准做法。

### 10.6 InputSystem 生命周期

`MyInputSystem` 在 `PlayerController.Awake()` 中 `new`，在 `OnEnable()` 中 `Enable()`，在 `OnDisable()` 中 `Disable()`，但**没有**在 `OnDestroy()` 中调用 `Dispose()`——这可能导致退出 Play Mode 时出现 "This will cause a leak" 的 Assert 警告。

---

## 11. 动画资源与教程参考

> **背景**：当前角色动画以 Mixamo 免费资源为主。Mixamo 数据量大但质量一般（批量自动生成，动作工整、无个性），即使配合状态机 + 混合树 + IK 分层，角色手感仍偏"僵硬"。
>
> **结论**：僵硬问题不在状态机/Animator 架构，而在**动画数据本身的质量**与**重定向到 Lumine 的匹配度**。优先从这两处下手，而非重构代码。

### 11.1 动画资源网站

#### 付费高质量

| 网站 | 地址 | 说明 |
|------|------|------|
| **FAB** | `fab.com` | Unity+Unreal 合并官方商店，搜 `third person shooter` / `mocap`，质量远超 Mixamo |
| **Unity Asset Store** | `assetstore.unity.com` | 搜 `TPS animation` / `shooter animation pack` |
| **Cubebrush** | `cubebrush.co` | 独立开发者动作包，有免费档 |
| **Gumroad** | `gumroad.com` | 搜 `mocap animation fbx`，独立动画师出售动捕重定向包 |

#### AI 视频转动捕（治"僵硬"的推荐路径）

用真实参考视频（持枪走位、举枪瞄准、掩体移动等）生成专业级动捕，再重定向到 Lumine：

| 网站 | 地址 | 说明 |
|------|------|------|
| **DeepMotion Animate3D** | `deepmotion.com` | 免费 AI 视频动捕，导出 FBX 带 root motion |
| **Move.ai** | `move.ai` | 手机视频生成专业级动捕（拍摄质量越高越好） |
| **Plask** | `plask.ai` | 浏览器端 AI 动捕 |
| **Rokoko Video** | `rokoko.com/products/rokoko-video` | Rokoko 免费 AI 动捕 |

> **提示**：Unreal Marketplace 已并入 FAB，直接 `fab.com` 即可；FAB 每月有免费资产，FBX 可直接导入 Unity。

### 11.2 僵硬问题排查步骤

1. **先验证重定向问题**：把同一批动画套到 Unity 自带标准 Humanoid 模型上跑一遍。如果明显变好，说明是 Lumine 骨骼（Mihoyo 骨骼比例与标准 Humanoid 差异大）的重定向问题而非数据问题。
2. **再换数据源头**：用 AI 动捕（DeepMotion/Move.ai）或 FAB 高质量包替换 Mixamo 数据。
3. **最后考虑动画后处理层**：Animancer / Final IK / Motion Matching（见下）。

Lumine 重定向修复手段：
- Blender + **Auto-Rig Pro** 将 Lumine 重新绑定到标准 Humanoid 再导出
- 或手动细调 Unity Humanoid Avatar 的 Mapping（腿、肩、腰最容易歪）
- 移动/射击类动作保留 **root motion**，可消除大量脚滑

### 11.3 状态机 / 动画系统教程

| 名称 | 地址 | 说明 |
|------|------|------|
| **Infallible Code** | `infalliblecode.com` + YouTube 同名 | State Machine 系列，State pattern 在 Unity 的落地 |
| **Code Monkey** | `codemonkey.com` + YouTube | 现代 Unity 角色控制器/状态机 |
| **Boss Room** | `github.com/Unity-Technologies/BossRoom` | 官方联机战斗示例（状态机 + 战斗） |
| **Game Kit 3** | `github.com/Unity-Technologies/Gamekit3D` | 官方 3D 角色战斗示例 |
| **Unity 官方演讲** | `learn.unity.com` 搜 *Game Architecture with Scriptable Objects* | 状态/行为的组织方式 |
| **Animancer** | `kybernetik.com.au/animancer` | 替代 Animator 的高级动画系统（付费），混合与 root motion 控制粒度大 |
| **Final IK** | `root-motion.com` | 高级 IK 方案（付费），持枪手部贴合更真实 |
| **Motion Matching** | `github.com/Unity-Technologies/motion-matching` | 运动匹配（需要较新 Unity 版本，2022.3 可用第三方方案） |

---

## 附录：Unity Package 依赖清单

| Package | Version | 用途 |
|---------|---------|------|
| `com.unity.inputsystem` | 1.14.2 | New Input System |
| `com.unity.cinemachine` | 2.10.7 | 摄像机管理 |
| `com.unity.animation.rigging` | 1.2.1 | IK 动画约束 |
| `com.unity.render-pipelines.universal` | 14.0.12 | URP 渲染管线 |
| `com.unity.ai.navigation` | 1.1.7 | NavMesh 寻路 |
| `com.unity.probuilder` | 5.2.4 | 关卡原型制作 |
| `com.unity.textmeshpro` | 3.0.7 | 文本渲染（待接入 UI） |
| `com.unity.test-framework` | 1.1.33 | 单元测试 |
