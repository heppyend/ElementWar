# FPS 暗夜猎人 · 开发记录与操作手册

> **适用范围**：仅 `Assets/Scenes/New Scene.unity`（学习/实验沙盒场景）。
> **目标**：用**项目原有角色状态机框架**（`StateMechaine`/`StateBase`/`MonoManager`）实现暗夜猎人的 **FPS 相关动作**。
>
> ## 🔴 当前状态（2026-08-10）
> - **KINEMATION 已彻底移除**（插件目录 `Assets/KINEMATION/` + `Rig_Hunter.asset` + `FPS_Hunter_Profile.asset` + 桥接/向导脚本全部删除，场景残留 Missing 组件 19 个 + 武器对象 3 个已清理）。
> - **结论**：KINEMATION 适合第一人称，在第三人称下接管 Animator 导致移动冲突 + 配置复杂，**放弃**。
> - **新方向**：复刻 **Game 场景已验证的 TPS 流程**（`PlayerController`/`PlayerModel`/状态机 + Animation Rigging IK 约束武器），参照卡拉彼丘二次元射击的成熟做法。
> - **待办**：① `FPSModel` 移动改回 Game 的 `OnAnimatorMove` + `animator.deltaPosition` 方案（恢复 root motion，位移走碰撞）；② 转向改 Game 的 `localMovement` 夹角转向；③ 瞄准改 Game 的瞬间朝向相机；④ 武器用 Animation Rigging 3 约束（右手 TwoBoneIK + 身体 MultiAim + 左手）对应双手。
>
> **分工（新）**：
> - **移动/跑酷** → 状态机 + Animator 混合树（CLazyRunner 动作包）——**移动逻辑复刻 Game 的 PlayerModel.OnAnimatorMove**
> - **武器/持枪** → **Animation Rigging IK 约束**（仿 Game 场景 3 约束），不用 KINEMATION
>
> 本文件同时是**操作日志**：所有代码编写、场景操作、设置项都记录在这里，方便回溯。历史 KINEMATION 记录保留在进度日志 §7，供参考。

---

## 1. 总体架构

```
┌────────────────────────────────────────────────────────────────┐
│                        输入层 (Input)                            │
│   MyInputSystem (New Input System)                              │
│   FPSController (单例)  轮询 Move/Sprint/Aim/Jump/Fire/Slide/Look│
└──────────────────────────────┬─────────────────────────────────┘
                               │ moveInput / worldMovement / isAiming ...
                               ▼
┌────────────────────────────────────────────────────────────────┐
│                        逻辑层 (Logic)                            │
│   FPSModel ──StateMechaine──▶ FPSStateBase / FPSXxxState        │
│   FPSAimState ──▶ FPSAimMode (HipFire / Shoulder / Ads)         │
└──────────────┬──────────────────────────────┬──────────────────┘
               │ 驱动混合树参数                  │ 瞄准/射击
               ▼                               ▼
┌────────────────────────────────────────────────────────────────┐
│                      表现层 (Presentation)                      │
│   ① Animator 混合树：Locomotion(Speed) / Air(VerticalSpeed)    │
│      → CLazyRunner 动画 (Idle/Walk/Jog/Dash/Slide/Jump/Land)    │
│   ② Cinemachine FreeLook：freeLookCamera(腰射) + aimingCamera   │
│      (肩射/开镜)                                                │
│   ③ Animation Rigging IK 约束：右手 TwoBoneIK + 身体 MultiAim    │
│      + 左手约束 → 枪对应双手（仿 Game 场景，待接入）              │
└────────────────────────────────────────────────────────────────┘
```

**核心原则**
- **代码控制状态 + Animator 播放动画**：状态机决定"做什么"（移动/冲刺/跳跃/滑铲/瞄准），Animator 混合树决定"怎么动"（丝滑衔接）。
- **混合树做衔接**：`Speed` 参数驱动 1D Locomotion 混合树（Idle→Walk→Jog→Sprint），`VerticalSpeed` 驱动空中升/降混合。状态切换只改参数目标值，由 `Lerp` 平滑过渡。
- **复用原有框架**：直接使用全局的 `StateMechaine`、`StateBase`、`MonoManager`、`SingleMonoBase<T>`、`IStateMachineOwner`、`MyInputSystem`。

---

## 2. 文件结构（全部为本次新建）

```
Assets/Scripts/FPS/
├── Core/
│   ├── FPSEnums.cs                # FPSState / FPSAimMode 枚举 + FPSAnimatorParams 参数名常量
│   ├── FPSModel.cs                # 宿主：CharacterController + Animator + 状态机 + 重力/落地 + 移动
│   └── FPSStateBase.cs            # 状态基类（重力 / 离地检测 / 瞄准监听 / 旋转辅助）
├── Controller/
│   └── FPSController.cs           # 单例：输入轮询 + 相机切换 + FOV + 瞄准目标
├── State/
│   ├── FPSIdleState.cs            # 待机：Speed→0
│   ├── FPSMoveState.cs            # 移动：Speed→Jog
│   ├── FPSSprintState.cs          # 冲刺：Speed→Sprint
│   ├── FPSAirState.cs             # 空中：升/降混合 + 空中控制 + 落地
│   ├── FPSSlideState.cs           # 滑铲：自控方向 + 速度衰减
│   └── FPSAimState.cs             # 瞄准：调度 AimMode
├── Aim/
│   ├── FPSAimModeBase.cs          # 瞄准模式抽象基类
│   ├── FPSHipFireMode.cs          # 腰射
│   ├── FPSShoulderMode.cs         # 肩射
│   └── FPSAdsMode.cs              # 开镜
├── Weapon/
│   ├── FPSWeaponBridge.cs         # KINEMATION 桥接层（空引用安全）
│   ├── FPSWeaponManager.cs        # 多武器切换管理器（显隐 + 换绑实体；数字键/滚轮）
│   └── FPSWeaponPoseAdjuster.cs   # 武器持枪姿态微调器（offset/rotation，可重复调）
└── Editor/
    ├── FPSAnimatorControllerBuilder.cs  # 菜单一键生成带混合树的 Animator Controller
    ├── FPSWeaponBindWizard.cs     # 菜单一键把多把枪绑定到 IK WeaponBone
    └── FPSWeaponPoseWizard.cs     # 菜单一键把枪归零到 IK WeaponBone + 朝向校正 + 挂姿态微调器
```

> 脚本全部在 `namespace FPS` 下；`StateMechaine` 等全局类直接使用。

---

## 3. 状态机设计

```
                ┌─────────────────────────────┐
                │      FPSAimState (瞄准)      │
                │  HipFire / Shoulder / Ads   │
                └──────▲──────────────▲──────┘
                       │ isAiming/isFire        │
   Idle ◀──▶ Move ◀──▶ Sprint                    空中也可进入瞄准
     │        │         │
     │ jump   │ jump    │ jump
     ▼        ▼         ▼
        FPSAirState (跳跃/下落)
           │ cc.isGrounded 落地 → 按输入回 Idle/Move/Sprint
           ▼
   Idle / Move / Sprint
     │ slide (C)
     ▼
   FPSSlideState (滑铲)
```

| 状态 | 职责 | Animator 表现 |
|------|------|--------------|
| `FPSIdleState` | Speed→0，监听移动/跳跃/滑铲/瞄准 | Locomotion 树 Idle 段 |
| `FPSMoveState` | Speed→Jog，监听冲刺/跳跃/滑铲/瞄准 | Locomotion 树 Walk/Jog 段 |
| `FPSSprintState` | Speed→Sprint，监听松开冲刺/跳跃/滑铲/瞄准 | Locomotion 树 Sprint 段 |
| `FPSAirState` | 空中水平控制 + 垂直速度混合 + 落地 | Air 树（Jump_Up ↔ Jump_Down 按 VerticalSpeed 混合） |
| `FPSSlideState` | 滑铲方向自控 + 速度衰减 | Slide 动画 |
| `FPSAimState` | 调度肩射/开镜/腰射模式 + 开火 | Aim 混合（暂无武器动画，先复用 Locomotion 低速段占位） |

**瞄准模式（框架就绪，后续补枪械动画即生效）**
- `FPSHipFireMode`（腰射）：不按右键仅开火，自由相机，武器在腰间。
- `FPSShoulderMode`（肩射）：按右键，aimingCamera 过肩视角，FOV 微缩。
- `FPSAdsMode`（开镜）：开镜键（默认 `V`，Inspector 可改），aimingCamera 拉近 FOV。

---

## 4. Animator 参数（混合树驱动）

代码统一通过 `FPSAnimatorParams` 引用，避免散落字符串：

| 参数 | 类型 | 用途 |
|------|------|------|
| `Speed` | float | Locomotion 混合树（0=Idle / 0.33=Walk / 0.66=Jog / 1.0=Sprint） |
| `VerticalSpeed` | float | Air 混合树（正=上升 Jump_Up，负=下降 Jump_Down） |
| `IsGrounded` | bool | 是否落地（控制 Air→Locomotion 过渡） |
| `IsSprinting` | bool | 是否冲刺（供后续脚步/音效/FOV 逻辑） |
| `IsAiming` | bool | 是否瞄准（Aim 状态过渡条件） |
| `AimMode` | float | 瞄准模式（0 腰射 / 1 肩射 / 2 开镜） |
| `AimingX`/`AimingY` | float | 瞄准时横向/纵向混合（后续瞄准混合树用） |

> Animator Controller 由 **菜单 `Tools/FPS/生成暗夜猎人 Animator Controller`** 一键生成（见 §6）。
> 生成器从 CLazyRunner FBX 自动匹配片段，找不到的会在 Console 报 Warning 并留空，可手动补拖。

---

## 5. 输入（复用 MyInputSystem，无需改动）

| 输入 | 绑定 | FPSController 读取 |
|------|------|-------------------|
| Move | WASD/摇杆 | `moveInput` |
| Look | 鼠标 delta | `lookDelta`（喂给 KINEMATION） |
| IsSprint | Shift | `isSprint` |
| IsAiming | 鼠标右键 | `isAiming` |
| IsJumping | Space | `isJumping` |
| IsSlide | C | `isSlide` |
| Fire | 鼠标左键 | `isFire` |
| Ads（开镜） | `V`（Inspector 可改 `adsKeyCode`） | `isAds` |

---

## 6. 操作步骤（用户手动执行，按顺序）

> 以下步骤需在 Unity 编辑器操作，脚本已就绪。

### 6.1 清理 New Scene
1. 打开 `Assets/Scenes/New Scene.unity`。
2. 删除旧 AnimancerController 系统的对象：`Player`、`CameraRig`、`LookAt`。
3. 保留：`Environment`、`Plane`、`Cube`(测试障碍)、`Directional Light`、`Main Camera`。
4. ⚠️ 地面/障碍物建议放 **Environment 层（第 6 层）**（若 `FPSModel.groundLayerMask` 只勾 Environment；若用默认 `~0` 可随意）。

### 6.2 暗夜猎人模型就位
1. 确认 `Assets/Resource/Models/暗夜猎人_by_卡拉彼丘_.../暗夜猎人.fbx` 的 **Rig → Animation Type = Humanoid**，`Configure` 检查骨骼映射完整（MMD 模型骨名与 Humanoid 自动映射通常可自动匹配，若缺请手动补 Humanoid 骨骼）。
2. 将 暗夜猎人.fbx 拖入场景，作为**玩家根对象**（命名 `Hunter`）。若要套预制体，也可做成 Prefab。
3. 根对象添加组件：`CharacterController`、`Animator`（勾选 **Apply Root Motion**，因代码走 OnAnimatorMove 手动移动，勾不勾皆可，代码会兜底）。
4. 调整 `CharacterController` 中心/高度/半径与模型贴合（MMD 模型普遍中心偏下，需在 **Center.y 抬到腰/胯高度**）。

### 6.3 一键生成 Animator Controller
1. 菜单栏 → **Tools → FPS → 生成暗夜猎人 Animator Controller**。
2. 生成到 `Assets/Resource/Animations/FPS/FPS_Hunter.controller`。
3. 把它拖到根对象 `Animator → Controller`。
4. 若 Console 有 Warning 提到缺失片段，在生成器日志提示的槽位手动补拖对应 CLazyRunner FBX 动画。

### 6.4 挂载脚本
在根对象 `Hunter` 上添加：
- `FPSModel`（Inspector 可调重力/跳跃/滑铲/速度参数）
- `FPSController`（Inspector 拖入两架相机 + `AimTarget`）
- `FPSWeaponBridge`（自动查找 KINEMATION 组件，找不到则安静降级）

### 6.5 相机
1. 场景中创建/复用两架 CinemachineFreeLook（参照 Game 场景配置）：
   - `freeLookCamera`（腰射，Priority 100）
   - `aimingCamera`（肩射/开镜，Priority 0）
2. 两架相机的 `Follow` / `LookAt` 都指向 `Hunter`。
3. 新建空物体 `AimTarget`，拖到 `FPSController.AimTarget`。

### 6.6 KINEMATION 接入（射击/武器动画）
> KINEMATION 是纯代码框架，需在编辑器中做资产与组件配置。缺失时**不影响**移动/跑酷/瞄准框架运行（桥接层空引用安全）。
1. **Rig 资产**：用 KINEMATION 的 Rig 工具（`KAnimationCore`）为暗夜猎人创建 `KRig` 资产，映射 Weapon/Spine/Right Hand/Left Hand 等骨骼。参考包自带 `Assets/KINEMATION/FPSAnimationFramework/Assets/Rig_FPSAnimationFramework.asset`。
2. **输入配置**：根对象挂 `UserInputController`，引用包自带 `InputConfig_FPSAnimationFramework.asset`。
3. **播放管线**：根对象挂 `FPSPlayablesController`（配置 `upperBodyMask`）+ `FPSBoneController` + `FPSAnimator`。
4. **模型**：模型子物体挂 `KRigComponent`（引用 Rig 资产）。
5. **相机**：若要用 KINEMATION 镜头抖动/FOV，可在相机挂 `FPSCameraController`（`cameraBone` 指向角色头部相机骨）。
6. **Profile**：创建 `FPSAnimatorProfile`（`Assets/Create/KINEMATION/.../Animator Profile`），加入 `SwayLayer`/`Recoil`/`AdsLayer`/`AttachHandLayer` 等层设置。之后用 `FPSWeaponBridge.LinkProfile()` 在运行时挂接。
7. 武器模型可挂 `FPSAnimatorEntity`（含 `animatorProfile` + `defaultAimPoint`）。

> 💡 §6.6 的 **Profile 创建改为一键脚本**（2026-08-10）：`Tools/FPS/武器层一键装配 (IK骨骼+链+Profile)`，见 §6.7。手动右键 Create 路线已废弃（`WeaponLayerSettings` 是抽象类，且 Rig 缺 IK 骨骼时各层全空）。

### 6.7 武器层一键装配（阶段 7 第 5 步）
> 前置：脚本 `Assets/Scripts/FPS/Editor/FPSWeaponSetupWizard.cs` 已就绪。

1. 打开 `New Scene.unity`，等编译完成（无报错）。
2. 菜单栏 → **Tools → FPS → 武器层一键装配 (IK骨骼+链+Profile)**。
3. 确认 Console 输出 `完成 ✅ ... 骨骼链 x6 + Profile=Assets/FPS_Hunter_Profile.asset 已装配 9 层`（或看有无 `找不到骨骼` 报错）。
4. 校验（任选）：
   - Hierarchy 展开 Hunter 骨架：头骨下出现 `IK WeaponBone`（其下 `IK RightHand/LeftHand/RightElbow/LeftElbow`），骨架根下出现 `WeaponBone`/`WeaponBoneAdditive`/`IK 脚膝`，双手腕下出现 `IK WeaponBoneRight/Left`（都带 `KVirtualElement`）。
   - Project 中选中 `Assets/FPS_Hunter_Profile.asset` → Inspector 显示 9 个层（View/Ads/Sway/IkMotion/Additive/Look/Turn/Ik/AttachHand）。
   - Hunter 根对象 → `FPSAnimator` 组件的 Profile 槽已自动填入 `FPS_Hunter_Profile`。
5. Play 运行：移动/瞄准照旧，Console 不应有 KINEMATION 报错（武器层已激活，但**暂无武器模型**，持枪姿态需下一阶段挂 `FPSAnimatorEntity` 后调）。

> 可重跑：脚本幂等，重复执行只重建 Profile，不会重复创建骨骼/链。
> ⚠️ 脚本会改动 Hunter 骨架（新增 IK 空物体），先手动 Ctrl+S 保存场景再跑，便于回退。

### 6.8 多枪绑定与切换（阶段 8）
> 架构：**所有武器共享骨架的 `IK WeaponBone` 锚点**（决定"手握中心"，武器层动画只驱动它）；每把枪是 IK WeaponBone 的子物体，用各自本地偏移对齐握把 → 切枪只需**显隐 + 换绑实体**（`FPSWeaponManager`），天然支持多枪。输入：**数字键 1/2/3**（复用 First/Second/Third）+ **鼠标滚轮**循环。
>
> 脚本已就绪：`FPSWeaponManager.cs`（运行时）+ `FPSWeaponBindWizard.cs`（编辑器绑定向导）。

1. 打开 `New Scene.unity`，等编译完成（无报错）。
2. 菜单栏 → **Tools → FPS → 绑定武器到猎人 (AR03+SMG01+手枪)**。
3. 确认 Console 输出 `完成 ✅ 绑定 3 把枪到 Hunter，已挂 FPSWeaponManager`（并逐把打印 `已绑定 Weapon_XXX`）。
4. 校验：Hierarchy 展开骨架 `IK WeaponBone` 下出现 `Weapon_AR03`（可见）/`Weapon_SMG01`/`Weapon_HG01`（隐藏）；Hunter 根对象多了一个 `FPSWeaponManager` 组件，weapons 列表 3 项。
5. **调姿态（关键，每把枪）**：选中可见武器（如 Weapon_AR03），在 Inspector 调其 `Transform` 直到：枪口朝前（若朝向反了，`Rotation` 的 Y 加 180）、握把/重心落在 `IK WeaponBone` 原点（可连带调 IK WeaponBone 的 local transform 整体平移/旋转）。再调子物体 `AimPoint` 到枪口/机瞄位置。
6. **Play 验证**：按 **1/2/3** 或滚轮切枪，应看到枪即时换、双手始终贴合握把（KINEMATION 换 Profile 带 blend 平滑过渡）。Console 不应有报错。

> ⚠️ **常见坑**：
> - **枪在头里（腰射）**：删除 PoseSampler 后腰射无层定位武器 → 用 `Tools/FPS/当前武器摆到胸前` 一键移到胸前，再微调 `FPSWeaponPoseAdjuster.offset`。
> - **KRigComponent 报空引用**：bind 向导必须「复用 + ImportRig 刷新层级」；不要在 Hierarchy 手动删武器（会残留空引用），要用菜单重跑。

> ⚠️ 每次只能调当前显示的枪：切到下一把继续调。AimPoint 若不在枪口，右键开镜会打偏，需调准。
> 可重跑：绑定向导幂等（同名武器先删后建，FPSWeaponManager 列表覆盖）。

### 6.9 武器姿态对齐（阶段 8，调持枪姿态）
> 场景：绑定向导把枪塞进 IK WeaponBone 后，枪口可能朝后/偏移，双手抓不到。本向导把枪**根归零到 IK WeaponBone 原点 + 朝向校正（+Z 前向）+ 挂 FPSWeaponPoseAdjuster**（可手调 offset/rotation，每次切枪自动应用，改一次 Save 即永久）。
>
> 脚本已就绪：`FPSWeaponPoseWizard.cs`（编辑器）+ `FPSWeaponPoseAdjuster.cs`（运行时微调器）。

1. 打开 `New Scene.unity`，等编译完成。
2. 菜单栏 → **Tools → FPS → 姿态对齐 (每把枪枪口朝前+握把居中)**。
3. 确认 Console 输出 `完成 ✅ 对齐 N 把枪`，并逐把打印 `✅ Weapon_XXX：根归零 + 朝向校正完成`（可能附带 `几何中心 = ...`）。
4. 选中当前显示的武器（如 Weapon_AR03），检查 `FPSWeaponPoseAdjuster` 组件：
   - 枪应**枪口朝前（+Z）**、几何中心落在 IK WeaponBone 原点；
   - 若仍不正，微调 `offset`（本地平移）/ `rotation`（本地旋转）字段直到双手握枪、枪口朝前。
5. 把子物体 `AimPoint` 拖到枪口/机瞄位置（若向导默认 +Z 0.6 不在枪口，手动拖）。
6. **Play 验证**：按 **1/2/3** 或滚轮切枪，双手贴合握把、切换平滑；右键开镜准星对准 AimPoint。改姿态字段 → 退出 Play 自动保留（编辑器改的即场景数据）。

> ⚠️ 每把枪的 offset/rotation 是独立的，切枪后各自微调。
> 朝向判定是启发式：若某把枪仍朝后，直接改该枪 `rotation.y = 180`。

---

## 7. 进度日志

| 日期 | 内容 |
|------|------|
| 2026-08-08 | 规划：确认用原有状态机框架做移动/跑酷，KINEMATION 做射击/武器动画。建立本文档。 |
| 2026-08-08 | 核心框架（FPSEnums/FPSModel/FPSController/FPSStateBase）已编写。 |
| 2026-08-08 | 状态类（Idle/Move/Sprint/Air/Slide/Aim）已编写，Locomotion/Air 混合树由 Speed/VerticalSpeed 驱动。 |
| 2026-08-08 | 瞄准模式框架（HipFire/Shoulder/Ads）已编写。 |
| 2026-08-08 | KINEMATION 桥接层 FPSWeaponBridge 已编写（空引用安全降级）。 |
| 2026-08-08 | Animator Controller 一键生成器（Tools/FPS/生成暗夜猎人 Animator Controller）已编写。 |
| 2026-08-08 | 修复编译错误 ①：`AnimatorConditionMode.IfFalse` 不存在 → 改为 `IfNot`；②：BlendTree 非 ScriptableObject，不能 `CreateInstance<T>` → 改用 `new BlendTree()`。顺带修：`sm` 声明位置、FindClip 路径漏包前缀、混合树显式阈值（Locomotion 0/0.33/0.66/1，Air -1/+1）。 |
| 2026-08-08 | 加固运行时：FPSAirState 空中保动量（起跳继承水平速度，无输入时空气阻力衰减）；FPSModel 缺 FPSController 时自动补挂（防 NRE）；FPSAimState 瞄准时 Speed 收敛到慢走段。 |
| 2026-08-08 | 场景搭建：清理 New Scene（删 Player/CameraRig/LookAt）；暗夜猎人 Avatar 映射全绿；一键生成 FPS_Hunter.controller（Idle/Walk/Jog/Sprint/JumpUp/JumpDown/Slide 全部匹配成功）；挂载 FPSModel+FPSController+FPSWeaponBridge；两架 FreeLook 相机 + AimTarget 就位，Follow/LookAt 指向 Hunter。 |
| 2026-08-08 | 运行调试：角色 WASD/冲刺/跳跃/滑铲已跑通。修复 ①：Hunter 的 Animator 被旧 Anbi.controller 占用 → 改为 FPS_Hunter.controller；②：Main Camera 缺 CinemachineBrain → 补挂；③：FreeLook 相机视角在头顶 → 调 Orbits(Top 2.4/3、Middle 1.8/2、Bottom 0.3/1.25)；④：Cinemachine 新版无 Input Axis Name、轴映射错乱 → FPSController 手动接管 m_XAxis/m_YAxis，用鼠标 Look 驱动 + xSensitivity/ySensitivity 灵敏度。 |
| 2026-08-08 | 相机手感定案：上下扫视调参易出"特写/跳档"，最终**固定 Y 轴视角（fixedLookY=0.6，正常第三人称），仅 X 轴左右旋转响应鼠标**，灵敏度 xSensitivity=0.15。绕开 FreeLook 上下扫视问题，先跑顺核心玩法，上下扫视留作后续优化。 |
| 2026-08-08 | 阶段7 KINEMATION 接入开始：创建 KRig 骨骼资产 `Assets/Rig_Hunter.asset`（右键 Hunter → Auto Rig Mapping）。清理重复创建的 `Assets/Scripts/FPS/Controller/Rig_Hunter.asset`（内容相同，放错位置）。 |
| 2026-08-08 | KRig 配置完成：`Rig_Hunter.asset` 的 Rig Component ← Hunter（挂 KRigComponent，total bones 181）、Animator ← FPS_Hunter.controller、Import Rig 导入骨骼，警告消失。 |
| 2026-08-08 | Input Config 配置完成：复用 KINEMATION 自带 `InputConfig_FPSAnimationFramework.asset`（含 MouseInput/MoveInput/IsAiming/AimingWeight 等属性），已填入 `Rig_Hunter.asset`。 |
| 2026-08-08 | KRigComponent 编辑器槽空掉是 KINEMATION 已知小坑（_rigComponent 不序列化），不影响运行（KRigComponent 自持骨骼快照 + Rig 资产存引用表）。 |
| 2026-08-08 | KINEMATION 核心组件挂载完成：Hunter 上已有 KRigComponent + UserInputController(填 InputConfig) + FPSPlayablesController + FPSBoneController + FPSAnimator（后两者 Profile/UpperBodyMask 待填）。 |
| 2026-08-08 | 上半身 AvatarMask 完成：创建 `FPS_Hunter_UpperBody`（Humanoid 模式，勾选头/双臂/肩/躯干，双腿不勾），已填入 FPSPlayablesController.UpperBodyMask。 |
| 2026-08-08 | ⏸️ **阶段7 暂停于第 5 步**：创建 `FPSAnimatorProfile`（右键 Create → KINEMATION → FPS Animation Framework → Animator Profile，命名 FPS_Hunter_Profile），并在其 Settings 列表添加 WeaponLayer + AttachHandLayer + AdsLayer（每个层需填 Rig Asset=Rig_Hunter.asset + 骨骼链）。 |
| 2026-08-10 | 🔍 **排查前置**：确认 Rig_Hunter.asset **没有** KINEMATION 需要的 IK 辅助骨骼（WeaponBone / IK WeaponBone / IK 手肘膝脚）与命名链（PelvisChain / SpineRootChain / RightHandChain / LeftHandChain / RightFootChain / LeftFootChain）——这是阶段 7 第 5 步的真正前置（官方 FPSAnimatorProfileWizard 会因此解析不到骨骼，直接手动建 Profile 各层也会全空）。 |
| 2026-08-10 | ✅ **编写一键装配脚本** `Assets/Scripts/FPS/Editor/FPSWeaponSetupWizard.cs`（菜单 **Tools/FPS/武器层一键装配 (IK骨骼+链+Profile)**）。替代原「手动右键 Create Profile」路线，一次完成：① 在暗夜猎人骨骼下创建 KINEMATION IK 辅助骨骼（含 KVirtualElement 跟随真实骨骼，肘/膝用真实肘膝骨做提示点）；② `ImportRig` 重建 Rig_Hunter.asset 索引；③ 补齐 6 条命名链；④ 创建 `Assets/FPS_Hunter_Profile.asset`（10 层：PoseSampler→View→Ads→Sway→IkMotion→Additive→Look→Turn→Ik→AttachHand，`AdsLayerSettings` 即「WeaponLayer+AdsLayer」的具体实现——`WeaponLayerSettings` 是抽象类，无法单独实例化）；⑤ 自动挂到 Hunter 的 FPSAnimator。全部**幂等**可重跑。 |
| 2026-08-10 | ✅ **一键装配脚本运行成功**：`完成 ✅ 角色=Hunter 骨骼链 x6 + Profile=Assets/FPS_Hunter_Profile.asset 已装配 10 层`，无骨骼缺失警告。阶段 7 第 5 步完成。 |
| 2026-08-10 | ✅ **多枪切换架构定案**：所有武器共享骨架 `IK WeaponBone` 锚点（手握中心，武器层只驱动它），每把枪是其子物体用本地偏移对齐握把 → 切枪=显隐+换绑实体，天然支持多枪。 |
| 2026-08-10 | ✅ **代码完成**：`FPSWeaponBridge` 加 `LinkEntity(weaponGO)`（绑实体含 defaultAimPoint）；`FPSController` 暴露 `switchWeaponNumber`（数字 1/2/3 复用 First/Second/Third）；新建 `FPSWeaponManager`（武器列表/切枪/滚轮循环/空引用安全）；新建 `FPSWeaponBindWizard`（编辑器一键绑定 AR03+SMG01+手枪 到 IK WeaponBone + AimPoint + Entity，幂等）。 |
| 2026-08-10 | ⏸️ **阶段8 待用户操作**：运行 `Tools/FPS/绑定武器到猎人` → 调每把枪本地变换（枪口朝前/握把对齐 IK WeaponBone）+ AimPoint → Play 用 1/2/3 / 滚轮验证切枪与持枪姿态。完成后再记录。 |
| 2026-08-10 | ✅ **绑定向导运行成功**：`完成 ✅ 绑定 3 把枪到 Hunter，已挂 FPSWeaponManager`（AR03+SMG01+HG01 挂 IK WeaponBone 下）。 |
| 2026-08-10 | ✅ **新增姿态对齐工具**：`FPSWeaponPoseWizard`（菜单 Tools/FPS/姿态对齐：把枪根归零到 IK WeaponBone + 几何中心补偿 + 朝向校正 + 挂 `FPSWeaponPoseAdjuster`）+ `FPSWeaponPoseAdjuster`（offset/rotation 可重复微调，每次切枪自动应用）。目标：枪口朝前 + 双手握把 + AimPoint 对准准星。 |
| 2026-08-10 | ⏸️ **待用户操作**：运行 `Tools/FPS/姿态对齐` → 微调每把枪 offset/rotation（若朝向反了 rotation.y=180）+ 拖 AimPoint 到枪口 → Play 用 1/2/3 / 滚轮验证切枪与持枪姿态。完成后再记录。 |
| 2026-08-10 | 🐛 **修复运行时报错 ①（PoseSampler NRE）**：`PoseSamplerJob.Initialize` 直接取 `poseToSample.clip`（需持枪姿势动画资产），我们没有该资产 → 去掉 PoseSamplerLayer（profile 10 层 → 9 层：View/Ads/Sway/IkMotion/Additive/Look/Turn/Ik/AttachHand）。装配脚本已改，需重跑「武器层一键装配」重建 profile。 |
| 2026-08-10 | 🐛 **修复运行时报错 ②（瞄准模式 NRE）**：`FPSAimState.Update()` 切换瞄准模式时新模式实例只 `Enter()` 未 `Init(model, controller)` → `FPSHipFireMode.controller` 为 null。已在切换处补 `modeInstance.Init(model, controller)`。 |
| 2026-08-10 | 🐛 **修复运行时报错 ③（KRigComponent.hierarchy 空引用）**：bind 向导「先删旧枪再建新枪」却没刷新骨架层级——武器对象是 IK WeaponBone 子物体，会被扫进 `KRigComponent.hierarchy`，旧枪 Destroy 后序列化层级残留空引用，运行时 `KRigComponent.Initialize()` 遍历即崩（连带 FPSBoneController.Update/LateUpdate/Dispose 一串 NRE）。**修复：bind 向导改为复用已有武器（保留姿态微调）+ 完成后 `ImportRig`（内部 RefreshHierarchy）重建序列化层级**。 |
| 2026-08-10 | 🐛 **修复姿态对齐 offset 不同步**：`FPSWeaponPoseWizard` 把几何中心补偿写进 `localPosition`，但 `FPSWeaponPoseAdjuster.offset` 仍为 0，运行时 `Start()` 把枪重置回零位（头里），向导工作被冲掉。**修复：对齐后把最终 localPosition/rotation 写回 adjuster.offset/rotation**。 |
| 2026-08-10 | ✅ **新增便捷菜单**：`Tools/FPS/当前武器摆到胸前 (可再微调 offset)`——一键把当前显示武器从头部原点移到胸前（offset 起始值 `(0.05, -0.35, 0.45)`，可再微调）。解决"枪卡在头里"。 |
| 2026-08-10 | 📌 **架构要点**：删除 PoseSamplerLayer 后，**腰射(不开镜)时没有武器层会定位武器**——枪停在 IK WeaponBone 原点(头)。因此持枪位由 `FPSWeaponPoseAdjuster.offset` 决定：姿态对齐把它摆到头原点，再手动微调到胸前；开镜时 AdsLayer 会把枪拉到瞄准点。 |
| 2026-08-10 | 🐛 **修复相机上下严重抖动（右键/开镜点击时）**：**根因 = 两架 FreeLook 相机的 Orbits 差异巨大**——`freeLookCamera`(Y=0.6 处)高≈1.92/半径 2.2，`aimingCamera`(Y=0.6 处)高**≈2.9**/半径 2.75。每次瞄准切 Priority 相机瞬间上跳近 1 米+拉远，松开跳回，快速点击=剧烈抖。**修复：菜单 `Tools/FPS/相机轨道对齐 (消除瞄准抖动)`**——把 aimingCamera 的 m_Orbits(数组)同步为 freeLookCamera + 同步初始 Lens.FOV + 同步 m_YAxis.Value，切换只剩瞄准 FOV 收放(预期)，无位置跳变。 |
| 2026-08-10 | 🔄 **武器方案方向变更（用户反馈）**：KINEMATION 姿态微调(offset/rotation)太复杂不直观，改用 **Animation Rigging 约束方案（仿 Game 场景：3 个约束把枪对应双手，移动/奔跑/跳跃自然对上）**。待相机修复验证后实施。 |
| 2026-08-10 | 🔍 **新症状（相机抖仍存在 + 人物卡动画 + 平移）→ 根因判断 = KINEMATION 抢占 Animator**：`FPSPlayablesController.InitializeController()` 用 `_animator.playableGraph` + `AnimationPlayableOutput.Create(graph, "FPSAnimatorGraph", _animator)` **直接接管 Animator 输出**，原移动混合树被包住，初始化/权重异常时停帧 →「卡在某帧动作但人在平移」（`OnAnimatorMove` 用代码 `horizontalVelocity` 移动，不依赖 `animator.deltaPosition`）+ 相机连带上下抖。**诊断工具：`Tools/FPS/切换武器层启用 (验证动画冲突)`**——一键禁用 KINEMATION 组件 + 隐藏武器，Play 验证移动是否恢复正常。 |
| 2026-08-10 | 📌 **结论**：若禁用武器层后移动/相机恢复 → 确认 KINEMATION 冲突 → 实施约束武器方案（移除 KINEMATION 组件，Animator 恢复完整控制，武器用 Animation Rigging 约束挂手）。 |
| 2026-08-10 | 🐛 **修复禁用武器层后点击瞄准 NRE**：`FPSWeaponBridge.SetAimMode()` → `UserInputController.SetValue()` → `GetPropertyIndex()` NRE。根因：禁用 KINEMATION(`FPSAnimator.enabled=false`)后 `UserInputController.Initialize()` 不跑，`_inputPropertyMap` 为 null，但 bridge 仍调用 `inputController`（非 null 未初始化）。**修复：`FPSWeaponBridge.IsReady` 收紧为 `fpsAnimator.enabled && inputController != null && playablesController != null`，所有访问 KINEMATION 的方法都过 `IsReady` 静默降级**。禁用武器层后瞄准状态也安全。 |
| 2026-08-10 | 🔍 **确认：瞄准卡帧+平移+上下抖是设计框架问题（非 KINEMATION）**——禁用武器层后移动/奔跑/跳跃/滑铲全正常，但进入瞄准仍抖。 |
| 2026-08-10 | 🐛 **修复瞄准抖动根因（三处）**：① **相机 X 轴不同步**：`FPSController.Update` 原只把 lookDelta 加到 active 相机，切换 Priority 时另一架 X 轴停在旧值 → 朝向突变。改：**两架相机 X 轴同时加增量**（永远同步），Y 固定只对 active。② **瞄准移动方向突变**：`FPSAimState` 原用 `controller.worldMovement`（基于 `Camera.main`，而 Camera.main 在切换 Priority 时 Cinemachine 会 blend 过渡 → 方向突变 → 平移跳变）。改：瞄准移动用**角色自身前向/右向**（`transform.forward/right`），不用相机方向。③ **角色被拽转**：`FaceCameraYaw` 转速 ×2→×1（温和回正）。 |
| 2026-08-10 | 🐛 **修复"点按左右键执行移动动画+向前位移+卡前12帧"**：根因 = `FPSAimState` 里 `LerpSpeedTo(WALK_BLEND)`（Speed→0.33 走路段）——把 Locomotion 混合树切到走路段但瞄准状态无动画推进 → 卡在走路前 12 帧 + 位移。**改：瞄准时无输入则 Speed→Idle(0) 彻底站定（horizontalVelocity=0）；有输入才极慢微移（LerpSpeedTo WALK_BLEND）**。点击左右键不再触发走路动画/位移/抖动。 |
| 2026-08-10 | 🔍 **运行时诊断（FPSRuntimeDiag）确诊**：瞄准时 `hVel=(0,0,0)`、`speedBlend=0`、`move=(0,0)`（代码无位移），但 `ccPos` 仍斜向变化 + 动画推进 + `applyRM=True` → **位移来自 Animator Root Motion，绕过 CharacterController 直接写 transform → 穿模 + 位移不受控 + 相机跟着抖**。`OnAnimatorMove` 只 `cc.Move(horizontalVelocity)`，未消费 `animator.deltaPosition`，且似乎未被调用 → root motion 直接作用 transform。 |
| 2026-08-10 | 🐛 **根治移动/穿模/抖动**：`FPSModel.Awake` 强制 `animator.applyRootMotion = false`（动画不再直接写 transform，位移全走代码）；移动从 `OnAnimatorMove` 改到 `Update` 直接 `cc.Move(horizontalVelocity + verticalSpeed)`——**位移完全走 CharacterController 碰撞，杜绝 root motion 绕过 CC 导致的穿模/位移不受控/相机抖**。这是 TPS 标准做法（动画只表现，移动由代码控制）。 |
| 2026-08-10 | 🔴 **彻底放弃 KINEMATION，全部删除**：删除 `Assets/KINEMATION/` 插件目录、`Rig_Hunter.asset`、`FPS_Hunter_Profile.asset`、以及引用它的脚本（FPSWeaponBridge/FPSWeaponManager/FPSWeaponPoseAdjuster/FPSWeaponSetupWizard/FPSWeaponBindWizard/FPSWeaponPoseWizard/FPSDebugTools）。清理 FPSAimState/FPSController/FPSAimModeBase 里 KINEMATION 引用。 |
| 2026-08-10 | ✅ **场景清理完成**：`Tools/FPS/清理场景残留引用` 移除 **19 个 Missing 组件**（Hunter 上的 KRigComponent/UserInputController/FPSAnimator/FPSBoneController/FPSPlayablesController/FPSWeaponBridge）+ **3 个武器对象**（Weapon_*）。场景已无 KINEMATION 残留。 |
| 2026-08-10 | 📌 **决策依据**：KINEMATION 为第一人称设计（镜头=眼睛），在第三人称下①接管 Animator Playable Graph 导致移动冲突②配置复杂（Rig/Profile/10 层动画 Job）③调姿态不直观。**Game 场景的 Animation Rigging IK 约束（Unity 内置）已验证、直观、零成本，是第三人称持枪正解**。参照卡拉彼丘二次元射击 = 移动状态机做深 + IK 武器，无黑科技。 |
| 2026-08-12 | ✅ **暗夜猎人接入 Game 场景**（一键向导 `AddHunterToGameWizard`，`Tools/玩家/把暗夜猎人加入 Game 玩家`）：以 Furina 玩家预制体为模板生成 `Hunter.prefab`——替换模型为 暗夜猎人.fbx、TwoBoneIK/MultiAim 约束重接到猎人 Humanoid 骨骼、武器重挂右手、CC/NavMeshAgent 按模型包围盒适配；打开 Game 场景实例化并注册到 `GameManager.playerModels` 索引 2（数字键 3 切换），MultiAim 源指向场景 AimTarget。**动作状态零代码改动**（沿用 Game 状态机 + `Player.controller`，Humanoid 动画自动重定向）。FPS 沙盒（New Scene）不受影响。可逆：`Tools/玩家/从 Game 移除暗夜猎人`。 |
| 2026-08-12 | ✅ **Game 全部玩家移动切换为 FPS 式**：`PlayerModel.useFPSMovement=true` + 统一 `TPS_Movement.controller`（`Tools/玩家/生成 TPS 移动控制器` 生成）——普通移动用 CLazyRunner 跑酷系（Speed Locomotion 树 + VerticalSpeed Air 树 + RunningSlide），瞄准走射保留 X Bot（AimingX/AimingY 2D 树）；新增 `PlayerSprintState`（冲刺=独立状态走 Dash 段）、代码驱动位移（`PlayerModel.LateUpdate` 的 `cc.Move`）。射击/人机保留。`Tools/玩家/应用/还原 TPS 移动` 批量切 3 个角色预制体。旧 root motion 方案 `useFPSMovement=false` 可回退。 |
| 2026-08-12 | ✅ **跳跃改为完整前跳序列**（用户反馈悬浮循环不满意）：TPS_Movement.controller 的 Hover 拆为 `HoverStart`（Jmp_FrontAir_A_Start 起跳）→ `Hover`（Jmp_FrontAir_A_Keep 空中循环）→ `Land`（Land_Base_Wait 落地缓冲）；`PlayerHoverState` 内三段切换（起跳 0.2s → 空中 → 落地 0.4s 回 Idle），落地不再硬切。 |
| 2026-08-12 | 🔙 **完整前跳已回退**（用户实测 Play 后所有角色半身入地、动画异常）：控制器/代码/GUID 引用静态核对全对，根因未定（疑为 FrontAir/Land clip 姿态带高度偏移，applyRootMotion=false 下与 CC 错位）。已把 `TPSMovementControllerBuilder`（Hover 恢复 Air 混合树 Jump_Up↔Jump_Down）与 `PlayerHoverState`（恢复原逻辑）回退到可用版本，需重跑「生成 TPS 移动控制器」菜单重建控制器。教训：换跳跃 clip 前先在隔离环境验证不使模型离地/入地。 |
| 2026-08-12 | 🐛 **半身入地根因 = Game 场景暗夜猎人裸 FBX 装饰残留**（`1217568050`，MMD 转换残留，骨架 stripped，localPosition.y=-1.138 半埋地下），与正式 Hunter.prefab 是两份不同对象。新增 `CleanupGameSceneWizard`（Tools/玩家/清理 Game 场景残留资产）删除后恢复正常。⚠️ 教训：场景里暗夜猎人有「裸 FBX 装饰」和「Hunter.prefab 角色」两份，别混淆。 |
| 2026-08-12 | ✅ **新增武器绑定向导** `BindWeaponToHunterWizard`（Tools/玩家/把突击步枪绑定到 Hunter 右手）：Assault_Rifle_03.prefab 挂 Hunter 右手骨 + 自动用枪模包围盒算枪口建 Bullet Spawn Point + 复用现有 Bullet_BlazingRed 子弹/火花 + 填 PlayerModel.weapon（修掉 Hunter 无武器问题）。 |
| 2026-08-12 | 🔍 **RigSync 异常**（清理后仍报 `TransformStreamHandle cannot be resolved`，SkinnedMeshRenderer 可见时触发，非致命）：静态核对 3 玩家约束全部有效，未定位缺陷；疑为运行时某约束引用了动画流外 Transform。待办：运行时诊断脚本精确定位。 |

---

## 8. 常见问题

- **移动没反应**：确认根对象有 `CharacterController` + `Animator`，且 `FPSModel` 的 `groundLayerMask` 覆盖了脚下地面层。
- **跳跃卡半空 / 落地不触发**：同 `PlayerModel` 经验，落地用 `cc.isGrounded`，起飞用 `IsHover()` 距离检测，`HOVER_STABILITY_FRAMES` 稳定性窗口防斜坡抖动。
- **瞄准没有动画**：目前无武器瞄准片段，`FPSAimState` 复用 Locomotion 低速段占位；补枪械动画后拖入 Aim 混合树即可，代码无需改。
- **KINEMATION 报错**：若未配置 KINEMATION 组件，`FPSWeaponBridge` 全部空引用保护，不会中断其他系统；报错通常是 KINEMATION 自身组件未按 §6.6 装配。
