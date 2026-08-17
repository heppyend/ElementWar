# ElementWar · 复习笔记

> 面向面试/复盘的一站式笔记 · Version 0.1 · 最后更新 2026-08-06
>
> 内容：① 项目介绍 → ② 项目概览 → ③ 开发流程 → ④ 技术点及实现 → ⑤ 面试可能问题 → ⑥ 当前 Bug

---

## 目录

- [一、项目介绍](#一项目介绍)
- [二、项目概览](#二项目概览)
- [三、开发流程](#三开发流程)
- [四、技术点及实现](#四技术点及实现)
- [五、面试可能问题](#五面试可能问题)
- [六、当前 Bug 清单](#六当前-bug-清单)

---

## 一、项目介绍

**ElementWar** 是一款基于 **Unity 2022.3.62f3 + URP** 开发的 **3D 第三人称射击游戏（TPS）**，当前版本 **0.1**。

**一句话定位**：以状态机为骨架、Root Motion 为移动根基、双角色可切换的 TPS 原型。

**当前可玩内容**：

| 模块 | 已实现 | 未实现 |
|------|--------|--------|
| 玩家 | WASD 移动 / 冲刺 / 跳跃 / 空中操控 / 瞄准 / 射击 / 双角色切换（荧、芙宁娜）/ 人机跟随 / 血量 / 死亡流程 | 自定义相机控制、死亡动画 |
| 敌人 | 丘丘人：寻路追击 / 受击 / 扣血 / 血条 / 死亡动画 / 销毁 / **攻击闭环** | 巡逻、Animation Event 攻击帧 |
| 武器 | 射击 / 子弹飞行 / 帧间碰撞 / 伤害 / 枪口火花 / 屏幕抖动 / **子弹+特效对象池** | 弹药、换弹、武器切换 |
| UI | 主菜单 / 提示菜单 / 退出确认 / **敌人+玩家血条** / **GAME OVER** / 文字发光 / 鼠标躲避 | HUD、准星、伤害数字、暂停菜单 |

**场景**：`GameStart.unity`（游戏开始界面 UI）→ 点「开始新游戏」→ `Game.unity`（游戏界面）。

**角色**：荧（Lumine，默认控制）+ 芙宁娜（Furina，可切换）；丘丘人为敌人；暗夜猎人 / 兰博基尼跑车仅为场景装饰模型，无逻辑。

---

## 二、项目概览

### 2.1 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 引擎 | Unity | 2022.3.62f3 |
| 渲染 | URP | 14.0.12 |
| 输入 | New Input System | 1.14.2 |
| 相机 | Cinemachine（双 FreeLook） | 2.10.7 |
| 动画 IK | Animation Rigging | 1.2.1 |
| 寻路 | AI Navigation（NavMesh） | 1.1.7 |
| 关卡 | ProBuilder | 5.2.4 |
| 文本 | TextMeshPro | 3.0.7 |
| 特效/风格 | EffectCore / YSA Toon / MMD4Mecanim | 插件 |

### 2.2 架构分层

```
输入层   MyInputSystem → PlayerController 轮询读取（Move/Sprint/Aim/Jump/Fire/1/2/3）
              │ moveInput / isAiming / isFire ...
逻辑层   StateMechaine → PlayerStateBase / EnemyStateBase
              │ 状态转换 + 行为（玩家 / 敌人 / 人机）
表现层   Animator(Root Motion) + CharacterController + Cinemachine + IK + UI + VFX
```

### 2.3 设计模式地图

| 模式 | 应用 | 位置 |
|------|------|------|
| 单例 | `SingleMonoBase<T>` 泛型单例 | `MonoManager`、`PlayerController`、`GameManager`、`UIManager`、UI 菜单、`EffectPool`（惰性创建） |
| 状态模式 | 玩家/敌人各自的状态类 | `PlayerStateBase` → 4 个玩家状态；`EnemyStateBase` → 4 个敌人状态 |
| 对象池 | ① 状态实例缓存（避免重复 new）② 子弹池 ③ 特效池 | ① `StateMechaine.stateDic` ② `PlayerWeapon.bulletPool` ③ `EffectPool`（按预制体分池） |
| 观察者/委托 | 集中式 Update 委托链 | `MonoManager.updataAction` |
| 标记接口 | 状态机宿主标识 | `IStateMachineOwner` |

### 2.4 关键类职责速查

| 类 | 职责 | 一句话 |
|----|------|--------|
| `PlayerController` | 输入轮询 + 移动方向计算 + 双相机/IK 管理 + 角色切换 + 死亡接管 + GAME OVER | 玩家「大脑」 |
| `PlayerModel` | CC 移动 + 状态切换 + 动画 + 地面检测 + 人机 NavMesh + 血量/受击/死亡 | 玩家「身体」 |
| `StateMechaine` | 泛型状态机（缓存/防重入/生命周期） | 状态调度器 |
| `MonoManager` | 集中式 Update 委托链 | 减少 MonoBehaviour 开销 |
| `EffectPool` | 通用特效对象池（按预制体分池，粒子播完回池） | 特效复用 |
| `EnemyBase` | 敌人基类：寻路/受击/血条/目标选择/攻击参数 | 敌人骨架 |
| `ZombieEnemy` | SwitchState 分发 | 丘丘人实现 |
| `PlayerWeapon` / `PlayerWeaponBullet` | 射击/子弹 + 子弹对象池 + 命中特效 | 武器系统 |
| `UIBase<T>` | UI 进出场动画 + 按钮控制 + 停用 Animator/GameObject | 菜单基类 |
| `EnemyHealthBarUI` | 血条 Billboard + 填充更新 | 面向相机的 World Space UI |
| `PlayerHealthBar` | 玩家血条（复用 HealthBar 预制体，仅主控显示） | 玩家 HUD |
| `GameOverUI` | 全灭时 GAME OVER + 任意键回主菜单 | 失败画面 |
| `HeadAimTarget` | 头部 IK 跟随鼠标 | 表现细节 |

---

## 三、开发流程

### 3.1 迭代时间线

```
2026-07-29  状态机初版 → 发现赋值/比较混淆 Bug + 重力语义错误 → 修复
2026-07-29  斜坡卡顿 → 地面检测重构：Raycast → SphereCast + 稳定性窗口 + 重力锁定（三轮迭代）
2026-08-01  文档化（Assets/Scripts/README.md + 根 README）
2026-08-01  批量修 Bug：HOVER_STABILITY_FRAMES(100→5)、子弹穿墙、InputSystem 泄漏、Animator null 防御
2026-08-03  芙宁娜模型适配（CC Center.y 不对齐下沉、缺 Animator Controller 无法移动）
2026-08-03+  双角色切换 + 人机跟随、丘丘人敌人（寻路/受击/血条/死亡）、UI 菜单、场景重构(GameStart/Game)
2026-08-06  全项目通读，重写 README，产出本笔记
2026-08-06  大修：攻击闭环+玩家血量、瞄准 LayerMask、空中控制、子弹/特效对象池、NavMesh 异常、死亡流程、血条、GAME OVER、UI 菜单三连 bug（遮挡/动画覆写/受力失衡）
2026-08-06  新增 EffectPool 特效对象池，枪口火花/命中特效/受击特效三处接入
2026-08-06  同步 README + PROJECT_NOTES 到当前代码状态
```

### 3.2 最有价值的开发经历：斜坡稳定性三连修（Bug #3~#7）

这是整个项目技术含量最高的一段，面试可直接讲：

**问题现象**：角色走斜坡/冲刺时抖动、卡顿、状态在 Move↔Hover 间来回横跳。

**根因分析**（三层）：
1. `CharacterController.isGrounded` 在斜面上会**瞬时 flicker**（某一帧 true 下一帧 false），直接触发悬空切换
2. 原 `IsHover()` 用**单根 Raycast** 从 `transform.position` 向下射，斜面上覆盖率低、易漏检误判
3. 稳定性窗口期内**重力仍在累积**，verticalSpeed 在 -2f 与越来越负之间振荡 → 抖动加剧

**解决方案**（三管齐下）：

| 层 | 措施 | 代码要点 |
|----|------|---------|
| 检测 | Raycast → **SphereCast** | 从 CC 真实底部 `pos.y + cc.center.y - height*0.5 + skinWidth` 出发，半径 `cc.radius*0.6` |
| 状态 | 稳定性窗口 | `ungroundedFrameCount` 连续 ≥ `HOVER_STABILITY_FRAMES(5)` 帧才切 Hover |
| 重力 | 窗口期锁速 | 窗口期内 `verticalSpeed = -2f`，不累积；窗口外才 `+= gravity*dt` |

**关键教训**：
- 起飞（`IsHover()` 距离检测，带 fallHeight 阈值防地面小颠簸）与落地（`cc.isGrounded` 接触检测）**不对称是有意设计**——`cc.Move()` 的碰撞判定比 SphereCast 更可靠
- 主动跳跃要**跳过稳定性延迟**：`ungroundedFrameCount = HOVER_STABILITY_FRAMES` 直接进入重力计算
- `HOVER_STABILITY_FRAMES` 曾因调试被改成 100（≈1.67s@60fps 重力冻结），导致走平台边缘半天不下落——**常量值与注释不符就是 bug**，要有回档代码的习惯

### 3.3 其他修复速记

| Bug | 根因 | 修复 |
|-----|------|------|
| 逻辑 Bug | `IsBeControl()` 中 `=` 误用为赋值而非 `==` | 改比较 |
| 重力语义 | 着地时 `verticalSpeed = gravity*dt`（-0.24/帧）语义不当 | 改常量 `-2f` 贴地 |
| 子弹穿墙 | 只检测 Enemy Tag，碰墙/地板不销毁；`GetComponent` 无 null 防御 | 命中任何碰撞体即销毁 + null 检查 |
| 输入泄漏 | `MyInputSystem` 未 `Dispose()` | `OnDestroy(){ input?.Dispose(); }` |
| 动画崩溃 | `OnAnimatorMove` 直接访问 Animator | 加 `if(animator==null) return` |
| 模型下沉/不动 | 芙宁娜 CC Center.y 不对齐 + 缺 Animator Controller | 校准 CC + 挂 Animator |

### 3.4 开发流程总结（可答"你怎么开发这个项目的"）

1. **先搭框架**：单例基类 → MonoManager → 泛型状态机 → 状态基类
2. **再填玩家**：输入 → 移动（Root Motion + 重力）→ 状态流转
3. **打磨手感**：解决斜坡卡顿（本次最有价值）、跳跃动量保持
4. **扩展玩法**：瞄准/射击/武器 → 敌人（寻路/受击）→ UI 菜单
5. **文档沉淀**：每轮修复写 Bug 编年史，方便回档和复盘

---

## 四、技术点及实现

### 4.1 泛型状态机 `StateMechaine`

**为什么**：TPS 有大量角色状态（Idle/Move/Hover/Aiming），用 switch/if 会越来越乱。

**实现**：
```csharp
public class StateMechaine {
    Dictionary<Type, StateBase> stateDic;  // 状态缓存池
    StateBase currentState;
    IStateMachineOwner owner;

    public void EnterState<T>() where T : StateBase, new() {
        if (currentState != null && currentState.GetType() == typeof(T)) return; // 防重入
        currentState?.Exit();          // 退出旧状态（注销 MonoManager）
        currentState = LoadState<T>(); // 取缓存或创建
        currentState.Enter();          // 进入新状态（注册 MonoManager）
    }
    StateBase LoadState<T>() {
        if (!stateDic.TryGetValue(typeof(T), out var state)) {
            state = new T();           // 首次 new
            state.Init(owner);         // Init 只调一次
            stateDic.Add(typeof(T), state);
        }
        return state;
    }
}
```

**设计要点**：
- `Dictionary<Type, StateBase>` **缓存状态实例**，避免每次切换都 new → 减少 GC
- `Init(owner)` 首次创建时调一次；`Enter()/Exit()` 每次切换调用 → 生命周期清晰
- **防重入**：同类型跳过，防止死循环
- `Stop()` 统一 Exit + Destory + 清字典

### 4.2 集中式 Update（MonoManager）

**为什么**：每个状态都写一个 MonoBehaviour 会有 N 个 Update 循环，性能浪费。

**实现**：`MonoManager` 唯一 MonoBehaviour，持有 `Action updataAction` 多播委托。
- 状态 `Enter()` 时 `AddUpdateAction(Update)`；`Exit()` 时 `RemoveUpdateAction(Update)`
- 一次状态切换 = 一次 Add + 一次 Remove，委托链整洁

**面试点**：委托链缺点——**+= 重复注册会多次调用**（若 Enter 重复而未 Exit），但状态机防重入避免了这点；移除用 `-=` 即便没注册过也不会报错。

### 4.3 泛型单例 `SingleMonoBase<T>`

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

**要点**：`where T : SingleMonoBase<T>` 的**自引用泛型约束**，保证强类型 `XXX.INSTANCE`；访问方式 `PlayerController.INSTANCE.moveInput`，避免 `FindObjectOfType`。

**可改进**：目前重复实例只 LogError 不销毁（DontDestroyOnLoad 场景切换会炸单例）。实际 UI 菜单继承 `UIBase<T>` 多个 UI 单例共存——因为它们是不同类型各自 `INSTANCE`，互不冲突。

### 4.4 Root Motion + 手动重力（移动核心）

```
OnAnimatorMove()（Animator 回调）:
  地面: playerDeltaMovement = animator.deltaPosition   ← 水平由动画驱动
  悬空: playerDeltaMovement = averageDeltaMovement*dt  ← 用跳跃前 3 帧平均速度
  所有: playerDeltaMovement.y = verticalSpeed*dt       ← 垂直手动叠加
        cc.Move(playerDeltaMovement)
```

- **为什么用 Root Motion**：角色的位移与动画同步，省去手动调速度对齐，动作自然
- **为什么垂直手动**：Root Motion 的 y 不受控，重力需要自己算
- **跳跃初速度**：`v = Mathf.Sqrt(-2 * g * h)`，g=-15、h=1.5 → v≈6.7m/s
- **3 帧滑动窗口**：`speedCache[3]` 存 animator.velocity 取平均 → 起跳保持惯性，不会瞬间静止

### 4.5 地面检测（SphereCast + 稳定性窗口）

**CC 底部计算**（不能简单用 transform.position）：
```csharp
float ccBottom = transform.position.y + cc.center.y - cc.height*0.5f + cc.skinWidth;
```
**SphereCast 代替 Raycast**：斜面覆盖率高。**稳定性窗口**：连续 5 帧离地才切 Hover，窗口期重力锁定 -2f。

### 4.6 双角色切换 + 人机跟随

- `GameManager.playerModels[]` 存所有角色；`PlayerController.currentPlayerModel` 指向当前控制者
- 数字键 1/2/3 → `SwitchPlayerModel(index)`（越界判断）
- 切换：旧角色 `Exit()`（启用 NavMeshAgent + 回 Idle → 变人机），新角色 `Enter()`（禁用 NavMeshAgent），相机 Follow/LookAt 重置
- **人机 AI**：`IsBeControl()` 判断是否玩家控制；非控制角色按距离用 NavMeshAgent 跟随，接近则待机

### 4.7 双相机切换（Cinemachine）

- 两架 `CinemachineFreeLook`：`freeLookCamera`（正常，Priority 100）、`aimingCamera`（瞄准，Priority 0）
- EnterAim：同步角度 → IK 切换 → Priority 交换（0/100）
- ExitAim：反向同步 → IK 恢复 → Priority 换回
- **同步角度的意义**：切换时视角不跳变
- **为什么双相机**：正常 TPS 视角（人物居中偏侧）与肩后瞄准视角（镜头上肩、更靠前）需要不同的 orbit 参数，单相机做不出自然切换

### 4.8 IK 约束（Animation Rigging）

| 约束 | 类型 | 时机 |
|------|------|------|
| 右手持枪 `TwoBoneIKConstraint` | 正常 | 权重 1 |
| 右手瞄准 `MultiAimConstraint` | 瞄准 | 权重 1（TwoBone=0） |
| 身体朝向 `MultiAimConstraint` | 瞄准 | 权重 1 |
| 头部 IK `HeadAimTarget`（MultiAimConstraint） | 常驻 | 跟随鼠标 |

头部 IK 实现：默认位置 = 相机位置 + restOffset；鼠标 delta 映射到相机 right/up 轴，前后**仅允许向后**（`Clamp(..., -maxBackward, 0)`），松手 Lerp 回中。

### 4.9 高速子弹防穿透（帧间 Raycast）

**为什么不用 OnCollisionEnter**：Rigidbody 在高速下可能一帧内穿越碰撞体（隧穿），OnCollisionEnter 检测不到。

```csharp
void CheckCollision() {
    Vector3 dir = transform.position - prevPosition;   // 本帧位移
    float distance = dir.magnitude;
    if (Physics.Raycast(prevPosition, dir.normalized, out hit, distance)) {
        if (hit.collider.CompareTag("Enemy")) {
            EnemyBase enemy = hit.collider.GetComponent<EnemyBase>();
            enemy?.Hurt(this, 1);
        }
        Destroy(gameObject);   // 命中任何物体即销毁（防穿墙）
    }
}
```
每帧先记录 `prevPosition`，下一帧从旧位置向新位置打射线，模拟整段飞行路径。

### 4.10 敌人系统

**EnemyBase**（抽象基类，实现 `IStateMachineOwner`）：
- 组件：Animator + NavMeshAgent + BoxCollider（Tag=Enemy, Layer 7）
- `FIndAttackTarget()`：遍历 `GameManager.playerModels[]` 找最近
- `Hurt(bullet, multiplier)`：受击动画 → 减速（0.5×, 0.5s 恢复）→ 喷血/滴血特效 → `currentHealth -= bullet.damage` → 血条更新 / 死亡
- 死亡：禁 NavMeshAgent + BoxCollider + 销毁血条 → Dead 状态播动画 → `Clear()` 销毁
- **血条**：`EnemyHealthBarUI` 在 World Space Canvas 下，`LookRotation(-dir)` 每帧面向相机（Billboard），受击后显示 6s

**ZombieEnemy**：`SwitchState` 分发到 Idle/Move/Attack/Dead 四个状态。

### 4.11 UI 系统

**UIBase<T>**（继承 SingleMonoBase，泛型单例）：
```csharp
Enter():  SetActive(true) → 播 FadeIn → 等动画播完 → ResumeButtons()
Exit(action): DisableButtons() → 播 FadeOut → 等 0.05s → action?.Invoke()
```
- `AnimatorCullingMode.AlwaysAnimate` 保证隐藏时动画也能播
- `DisableButtons()/ResumeButtons()` 抽象，子类实现防误触

**菜单流转**：
```
MainMenuUI(12按钮)
 ├─ 开始新游戏 → SceneManager.LoadScene("Game")
 ├─ 退出 → ExitMenuUI（确认后 Application.Quit）
 └─ 其余(在线/读取/角色/设置...) → TipMenuUI（占位提示）→ 确定 → 回主菜单
```

**ExcludeMouse**：鼠标进入 `avoidRadius` 内，按 (1 - 距离/半径) 比例施加排斥力，另加回到原位的回复力 → 按钮"躲着鼠标走"。

**TMPGlowControl**：`IPointerEnterHandler/IPointerExitHandler`，进入时 `_GlowPower` 淡入→定格→淡出，并开 `UNDERLAY_ON` 阴影（⚠️ 有 bug，见第六节）。

### 4.12 输入系统

- New Input System `.inputactions` → 自动生成 `MyInputSystem.cs`（**不手改生成文件，改 .inputactions**）
- `PlayerController.Update()` 轮询：`ReadValue<Vector2>()` / `IsPressed()` / `triggered`
- `moveInput` 已 `.normalized` 防斜向加速
- 生命周期：Awake new → OnEnable Enable → OnDisable Disable → OnDestroy Dispose
- `Look` 已定义未使用（相机由 Cinemachine 管）；`First/Second/Third` 做角色切换

---

## 五、面试可能问题

### Q1：介绍一下你的项目（30 秒电梯演讲）
> 一款 Unity URP 的 3D TPS，v0.1。用**泛型状态机**管理玩家/敌人状态，**Root Motion + 手动重力**做移动，**双 Cinemachine 相机 + Animation Rigging IK** 做瞄准，**帧间 Raycast** 解决高速子弹穿墙，**对象池（子弹 + 特效）**控制 GC，实现了**双角色切换（含人机跟随、死亡接管）、丘丘人敌人（寻路/受击/攻击/血条/死亡）、玩家血量/血条、GAME OVER 流程**和完整 UI 菜单。当前在做武器系统重构和音效。

### Q2：为什么用状态机？说说它的实现
> 角色行为有大量互斥状态，if/switch 会失控。我用泛型 `StateMechaine`：`Dictionary<Type, StateBase>` 缓存状态实例（首次 new + Init，之后复用，减 GC）；`EnterState<T>()` 先 Exit 旧、再 Enter 新，并做防重入；`Init` 只调一次、`Enter/Exit` 每次切换。玩家和敌人共用一套框架。

### Q3：为什么用集中式 MonoManager 而不是每个状态一个 MonoBehaviour？
> N 个状态 = N 个 Update 循环。我把状态的 Update 注册成 `Action` 委托，MonoManager 一个 Update 统一驱动。缺点：委托有微小间接开销；`+=` 重复注册会多次调用——但状态机防重入保证了不会重复 Enter。

### Q4：Root Motion 和程序移动怎么选？你的移动怎么做的？
> 水平用 Root Motion（`animator.deltaPosition`），动画与位移天然同步；垂直手动算重力（`verticalSpeed += gravity*dt`）再叠加到 y 上，最后 `cc.Move()`。悬空时没有水平动画位移，就缓存跳跃前 3 帧 `animator.velocity` 取平均保持惯性。

### Q5：怎么解决斜坡上角色抖动/卡顿？
> 三层方案：① `cc.isGrounded` 在斜坡会瞬时 flicker，改用**稳定性窗口**——连续 N 帧离地才切 Hover；② 窗口期**重力锁定 -2f**，防止垂直速度振荡；③ 地面检测从单根 Raycast 换成 **SphereCast**（从 CC 真实底部出发），斜面覆盖率高。还注意了起飞/落地不对称是有意的：起飞用距离检测防颠簸，落地用 `cc.isGrounded` 因为接触检测更准。

### Q6：高速子弹为什么会穿墙？怎么解决的？
> Rigidbody 默认是**离散检测**，子弹速度快到一帧位移超过碰撞体厚度就会隧穿。解决：每帧记录上一帧位置，从旧位置向新位置做**帧间 Raycast**（`Physics.Raycast(prevPos, dir, out hit, distance)`），等于把整段飞行路径做射线检测。这是高速弹道（射线枪）的标准做法。

### Q7：怎么实现瞄准？双相机为什么这么设计？
> 两架 Cinemachine FreeLook，用 **Priority** 切换（正常 100/瞄准 0，进入交换）。切相机前**同步 X/Y 轴角度**避免视角跳变。IK 上，瞄准时开 `MultiAimConstraint`（右手+身体跟 AimTarget）关 `TwoBoneIKConstraint`，退出反向。瞄准目标是屏幕中心射线的命中点。

### Q8：人机（非控制角色跟随）怎么做的？
> `GameManager.playerModels[]` 存角色，`PlayerController.currentPlayerModel` 标当前控制。`IsBeControl()` 判断。非控制角色不读玩家输入，按与当前角色的距离切 Idle/Move，用 NavMeshAgent 跟随。切换时旧角色 `Exit()` 开 NavMeshAgent 转人机，新角色 `Enter()` 关 NavMeshAgent。

### Q9：敌人受击/死亡流程？
> 子弹命中 `Enemy` Tag → `EnemyBase.Hurt()`：受击动画 + 减速 + 喷血特效 → `currentHealth -= damage` → 血条更新（显示 6s）。血量为 0 → `SwitchState(Dead)` + 禁用 NavMeshAgent/BoxCollider + 销毁血条 → 播死亡动画 → 播完 `Clear()` 销毁。血条是 World Space UI，每帧 `LookRotation` 面向相机。

### Q10：单例模式怎么实现的？有什么坑？
> `SingleMonoBase<T>` 自引用泛型约束，静态 `INSTANCE`。坑：跨场景会残留（需 `DontDestroyOnLoad` 或销毁逻辑）；重复实例目前只 LogError；Awake/OnDestroy 时 `INSTANCE=null` 要注意访问时机（`PlayerModel.Awake` 依赖 `PlayerController.INSTANCE`，有执行顺序风险）。

### Q11：URP 和内置管线区别？
> URP 是 SRP（可编程渲染管线）之一，基于物理的光照、单 Pass 前向渲染、SRP Batcher 合批、对移动端友好。本项目用了 URP + YSA Toon 卡通渲染 + URP 的 3 档质量配置（Performant/Balanced/HighFidelity）。

### Q12：项目中你遇到过最难调的 bug？
> 斜坡抖动。现象是状态机在 Move↔Hover 间横跳+视觉抖动，排查后是 `cc.isGrounded` 斜面 flicker + 重力窗口期振荡 + 单射线检测不可靠三重叠加，最终用 SphereCast + 稳定性窗口 + 重力锁定三管齐下解决。这让我理解了"一个现象背后往往是多层根因"。

### Q13：New Input System 和旧 Input 的区别？
> 事件驱动 + 轮询都支持；.inputactions 可视化配置；自动生成 C# 包装类；支持多设备（键鼠/手柄/触屏/XR）同一 Action 多绑定。本项目用轮询方式在 Update 里读值。

### Q14：对象池是什么？项目里怎么落地的？
> 频繁 `Instantiate/Destroy` 会触发 GC 卡顿。对象池预创建对象队列，用后回池复用。本项目有三层：① 状态机缓存状态实例（`StateMechaine.stateDic`）；② 子弹 `Queue` 池（`PlayerWeapon.bulletPool`，`GetBullet/RecycleBullet`）；③ 特效池（`EffectPool`，按预制体分池，粒子播完自动回池）。关键细节：**特效复用前必须 `Stop/Clear/Play`**，否则从池里再拿出来不会重新播。

### Q15：如果让你继续做，下一步做什么？
> ① 武器系统抽象（WeaponBase + 切换 + 换弹）；② 玩家 HUD 完善（弹药/准星/伤害数字/暂停菜单）；③ 敌人攻击改用 **Animation Event** 判定帧（当前是动画播完一次性结算）；④ 音效系统；⑤ 参数配置化（ScriptableObject）。

### Q16：讲讲你排查过的一个"现象简单但根因隐蔽"的 bug？
> 主菜单**退出按钮躲避鼠标只垂直不水平**，脚本逻辑明明是全向的。逐层排查：先排除按钮被遮挡/禁用（引用、布局、onClick 都正常）→ 再看 `ExcludeMouse` 距离算法（改用屏幕像素距离）→ 最后定位到 `FadeIn.anim`：它动画化了退出按钮的 `m_AnchoredPosition.x` 但**没动画 y**，而 MainMenu 的 Animator **停在 FadeIn 状态**，每帧覆写 x → 水平位移被锁死。修复是 FadeIn 播完后 `animator.enabled=false` 释放位置。**教训：Animator 停在某状态会持续覆写该状态 clip 绑定的属性，会与代码控制的同属性冲突**；顺带还踩了"受力累积导致 UI 位移飞出边界"和"ScreenPointToLocalPointInRectangle 在 Screen Space Camera 下换算异常"两个坑。

---

## 五·补充：特效对象池设计

- **为什么特效要池化**：射击/受击高频触发，每次 `Instantiate`/`Destroy` 产生 GC 压力；粒子对象与子弹不同，是"播完即死"的视觉对象
- **`EffectPool`**（全局单例，未挂载自动创建）：
  - 按预制体分池 `Dictionary<GameObject, Queue<GameObject>>`
  - `GetEffect(prefab,pos,rot)`：复用（重置位置 + `SetActive(true)` + 显式 `ps.Stop/Clear` + `ps.Play()`）或实例化
  - 回池：轮询 `!ps.IsAlive()` 或 `!activeSelf`（stopAction=Disable）判定播完，10s 超时兜底强制停用
- **三处接入**：枪口火花、子弹命中特效（`impactPrefab`）、敌人受击喷血/滴血
- **面试可讲**：对象池的「分池键是预制体引用」「粒子复用前必须 Reset」「用生命周期判定替代固定时间销毁」

## 六、当前 Bug 清单

> ✅ = 已于 **2026-08-06** 修复；其余为待办/特性。

| # | 状态 | 位置 | Bug | 修复/建议 |
|---|------|------|-----|-----------|
| 1 | ✅ | `ZombieIdle/Move/AttackState` | **Attack 状态永不触发**：Idle↔Move 死循环，Attack 空壳 | 完整攻击闭环：进范围→播 Attack 动画→动画播完对玩家 `TakeDamage(attackDamage)`→记录 `lastAttackTime` 进入冷却→Idle 重判 |
| 2 | ✅ | `Game.unity` PlayerController | **`aimLayerMask=119` 排除 Enemy(7) 层** | mask → **247**（119\|128，含 Enemy 层，仍排除 Player 层），准星可锁敌 |
| 3 | ✅ | `Lumine FBX.prefab` | **荧 `fallHeight=0`** | 改回 **0.2**（与芙宁娜/默认一致） |
| 4 | ✅ | `EnemyBase.Hurt()` | **硬编码 `GetComponent<BoxCollider>()`** | Awake 缓存 `bodyCollider`，死亡时 null 防御 |
| 5 | ✅ | `PlayerHoverState` | **悬空不可转向/移动** | 新增 `airControlSpeed=3`，Update 里 `cc.Move(worldMovement * airControlSpeed * dt)`（叠加在跳跃惯性上） |
| 6 | ✅ | `TMPGlowControl` | **阴影被立即关闭**（Update 每帧 DisableUnderlay） | 移除 Update；Enter 开阴影+发光，Exit 关阴影+归零 |
| 7 | ✅ | `UIBase._Exit` | **FadeOut 不等动画播完** | 改为等待 `IsAnimationBreak()`（2s 超时保护） |
| 8 | ✅ | `PlayerWeapon` / `PlayerWeaponBullet` | **子弹无对象池** | `Queue` 池化：`GetBullet/RecycleBullet`、`ResetBullet/ReturnToPool`、OnEnable/OnDisable 管理生命周期 |
| 9 | ✅ | `PlayerModel.Awake` | **依赖 `PlayerController.INSTANCE`** | angularSpeed 赋值移到 Start |
| 10 | 🟢 | `PlayerController` | **职责过重**：输入+相机+IK+移动+角色切换集中 | 待办（技术债，拆 InputHandler/CameraManager/IKManager） |
| 11 | ✅ | `ZombieAttackState` | 空壳 | 已重写（见 #1） |
| 12 | 🟢 | 主菜单 | 在线/读取/角色等按钮均弹占位提示 | 特性（未实现功能） |
| 13 | ✅ | `EditorBuildSettings` | 残留 `SampleScene` 引用 | 已清理，仅保留 Game + GameStart |
| 14 | 🟢 | 全局 | **无音效 / 无弹药换弹 / 无玩家 HUD** | 待办（路线图阶段三） |
| 15 | ✅ | `PlayerMoveState` / `EnemyBase.chaseTarget` | **`SetDestination` 报 "active agent / placed on NavMesh"**（NavMeshAgent 刚 `enabled=true` 的当帧就调 SetDestination，或角色不在烘焙网格上） | `SetDestination` 前加 `navMeshAgent != null && isOnNavMesh` 校验 |
| 16 | ✅ | `PlayerModel.Exit()` | **`NavMeshAgent` 启用报 "not close enough to the NavMesh"**（角色切下时不在烘焙区） | `enabled=true` 前 `NavMesh.SamplePosition(..., 5f)` 校正位置到最近网格点 |
| 17 | ✅ | `EnemyBase` | **敌人只锁定随从不攻击主控**：`FIndAttackTarget` 只在 Start 调一次、不排除死亡玩家 → 随从被反复打死，`isDead` 后无法切换/跟随 | 排除 `isDead` + `attackTargetRefreshInterval=0.5s` 定期刷新，始终打最近的存活角色 |
| 18 | ✅ | `PlayerController` | **射击/受击均无镜头抖动**：Impulse 无监听者 | `Start()` 为两架 FreeLook 虚拟相机动态 `AddComponent<CinemachineImpulseListener>()`。⚠️ 曾误挂 Main Camera 报 "CinemachineExtension requires a virtual camera"——ImpulseListener 必须挂虚拟相机，不是 Brain |
| 19 | ✅ | `PlayerHealthBar`（重做） | **玩家血条**（初版代码构建屏幕血条，更新异常） | 改用**复用敌人 `HealthBar.prefab` + `EnemyHealthBarUI`**（世界空间 BillBoard 跟随头顶），`PlayerModel.healthBarPrefab` 写入两玩家预制体；仅主控显示，`TakeDamage` 更新 |
| 20 | ✅ | `PlayerController` / `GameOverUI`（新增） | **死亡流程不完整** | 随从死→`isDead` 拦截切换；主控死→`OnPlayerDied` 自动接管下一位存活角色（数组顺序）；全灭→`GameOverUI` 弹 GAME OVER，任意键（New Input System 读 `Keyboard`/`Mouse`）返回 GameStart |
| 21 | ✅ | `GameOverUI` / `MainMenuUI` | **GAME OVER 返回主菜单后鼠标不见**：`Cursor.lockState` 跨场景保留，Game 中 Locked 后主菜单无解锁点 | ①`GameOverUI` 跳转前 `Cursor.lockState=None; visible=true`；②`MainMenuUI.Start()` 强制解锁（双保险） |
| 22 | ✅ | `UIBase._Exit` | **主菜单按钮不高亮/点不动**：TipMenu/ExitMenu 打开后 `_Exit` 只播 FadeOut 不 `SetActive(false)` → 透明面板残留激活，raycastTarget 挡住下层主菜单按钮射线 | FadeOut 完成并执行 action 后加 `gameObject.SetActive(false)`。教训：菜单切换必须显式停用退出的菜单，不能只靠淡出动画 |
| 23 | ✅ | `UIBase` / `ExcludeMouse` / `FadeIn.anim` | **退出按钮躲避鼠标只垂直不水平**：FadeIn.anim 动画化 Exit 按钮 `m_AnchoredPosition.x`（未动画 y），MainMenu Animator 停在 FadeIn 状态每帧覆写 x → 水平位移被锁死，ExcludeMouse 只能垂直躲 | ①`UIBase._Enter` FadeIn 播完后 `animator.enabled=false`（Enter/Exit 播放动画前重新启用）；②`ExcludeMouse` 延迟到位置连续 6 帧稳定后捕获回家位置（避免动画中间值）；③鼠标用 `Mouse.current.position.ReadValue()`。教训：Animator 停在某状态会**持续覆写**该状态 clip 绑定的属性，会与代码控制的同属性冲突 |
| 24 | ✅ | `ExcludeMouse` | **躲避会跑出界面且不回位**：旧实现「受力累积」，躲避 `avoidForce×dt`（≈6.4px/帧）与回位 `returnForce×dt`（≈0.03px/帧）数量级失衡——靠近被推飞（无上界）、移开回不来 | 改为「期望位置 + 插值」：`desiredPos = original ± avoidDir * maxAvoidDistance * strength`（有界 ≤120px），`Lerp(..., moveSpeed*dt)` 平滑跟随，鼠标移开期望位置回原位。教训：UI 位移用「位置插值」而非「速度累加」，天然有界稳定 |
| 25 | ✅ | `ExcludeMouse` | **躲避仍异常（一出场就飞/乱躲/鼠标远也躲）**：怀疑 `ScreenPointToLocalPointInRectangle` 在 Screen Space Camera 菜单下距离换算异常 | 改用 **`RectTransformUtility.WorldToScreenPoint(相机, 按钮中心)` 直接算屏幕像素距离**，绕开局部坐标换算；期望位置插值 + **硬边界 clamp**（任何情况不偏离原位超 `maxAvoidDistance`）；捕获回家位置延后到入场动画结束 + 最短延迟 0.8s；新增 **Inspector 运行时诊断字段**（diagDistance/diagBtnCenter/diagMouse/diagOriginal） |

### 本次修复引入的新能力（2026-08-06）

- **玩家生命值**：`PlayerModel.maxHealth=100`、`TakeDamage(int)`、`shakeOnHit`（受击相机震动）、`Die()`（停状态机/CC/NavMeshAgent）
- **玩家血条**：复用敌人 `HealthBar.prefab` + `EnemyHealthBarUI`（世界空间 BillBoard 跟随头顶），`PlayerModel.healthBarPrefab`/`healthBarHeight` 已写入两玩家预制体；仅主控显示，`TakeDamage` 更新，角色销毁自动清理
- **敌人攻击**：丘丘人 `attackDamage=10`、`attackCooldown=1.5s`；攻击动画播完结算伤害；**定期刷新目标**（打最近存活角色）
- **完整死亡流程**：随从死 → `isDead` 拦截切换（不可访问）；主控死 → `PlayerController.OnPlayerDied` 自动接管下一位存活角色；全灭 → `GameOverUI` GAME OVER + 任意键回 GameStart
- **角色切换防死**：`SwitchPlayerModel` 跳过 `isDead` 角色（带 LogWarning 提示），已死角色不做 `Exit()`（防重新启用移动）
- ✅ **死亡画面**：全灭 → `GameOverUI` GAME OVER + 任意键回 GameStart
- ⚠️ 玩家 Animator 仅有 Move/Aiming/Idle/Hover，**无 Hit/Dead clip** → 受击/死亡动画名默认留空（跳过动画，相机震动 + 血条兜底）；补齐动画后填名即可
- ⚠️ 无复活流程（不在本次范围）

---

## 附：面试准备自检清单

- [ ] 能徒手画出项目架构分层图
- [ ] 能讲清状态机三要素（缓存/防重入/生命周期）并手写核心代码
- [ ] 能解释 Root Motion + 手动重力的分工
- [ ] 能讲斜坡卡顿的根因链和三层解决方案
- [ ] 能解释帧间 Raycast 防子弹穿墙的原理
- [ ] 能讲双相机 Priority 切换 + 角度同步
- [ ] 能列出至少 5 个设计模式在本项目的落地
- [ ] 能说出项目当前 3 个最重要的 Bug 及修法
