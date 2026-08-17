# Assets/Scripts/FPS — FPS 原型框架（New Scene 沙盒）

> 暗夜猎人 FPS 玩法原型框架，跑在 `Assets/Scenes/New Scene.unity`（实验场）。命名空间 `FPS`。
> **Game 场景的 `useFPSMovement` 移动方案就是从这套迁过去的**。说「继续 Hunter FPS 开发」时从这里继续。
> ⚠️ KINEMATION 已彻底移除（接管 Animator Playable Graph 与移动冲突，放弃）；武器系统未接入（`FPS/Weapon/` 为空占位）。

## 目录结构

| 目录/文件 | 职责 |
|-----------|------|
| `Core/FPSModel.cs` | 宿主：CC + Animator + StateMechaine；LateUpdate 统一 `cc.Move`；IsHover / FaceDirection / FaceCameraYaw |
| `Core/FPSStateBase.cs` | 状态基类（缓存 FPSModel/FPSController 引用） |
| `Core/FPSEnums.cs` | `FPSState` 枚举 + 动画参数 hash |
| `Controller/FPSController.cs` | 单例：输入轮询 + 双相机接管 + 瞄准射线（`UpdateAimTarget`） |
| `State/` | 状态实现：`FPSIdleState` / `FPSMoveState` / `FPSSprintState` / `FPSAirState` / `FPSSlideState` / `FPSAimState` |
| `Aim/` | 瞄准模式：`FPSAimModeBase`（抽象，`Enter/Exit/Update` 模式接口）→ `FPSHipFireMode`（腰射）/ `FPSShoulderMode`（肩射，FOV 55）/ `FPSAdsMode`（开镜，默认 V 键，FOV 40） |
| `Editor/` | `FPSAnimatorControllerBuilder`（生成控制器）/ `FPSCameraFixWizard`（修相机）/ `FPSSceneCleanup`（场景清理） |
| `Utils/FPSRuntimeDiag.cs` | 运行时诊断 |
| `Weapon/` | ⚠️ 空占位，武器系统未接入 |

## 与 Game 玩家系统的差异（最重要）

| 项 | FPS 框架（本目录） | Game 玩家（`Assets/Scripts/Player/`） |
|----|-------------------|--------------------------------------|
| `applyRootMotion` | **false**（关闭，纯代码驱动，防 root motion 绕过 CC 穿模/相机抖） | **true**（恒开——Animation Rigging 约束依赖它求值；根运动被 `OnAnimatorMove` 拦截丢弃） |
| 移动 | LateUpdate 手动 `cc.Move`（同思路） | LateUpdate 手动 `cc.Move` |
| 相机 | `FPSController` **手动接管** FreeLook 轴 | Cinemachine 自动读轴 |
| 瞄准 | 三模式抽象（HipFire/Shoulder/Ads）+ FOV 变化 | 单 Aiming 状态 + 双相机 Priority 切换 |

## 本模块关键点

- **FPSController 相机接管**：两架 FreeLook 的 `m_XAxis.m_InputAxisName=""` + `m_InputAxisValue=0`（`DisableAutoAxis`）禁用自动读轴；鼠标 `lookDelta` 同时加到两架 X 轴（防切换 Priority 时朝向突变被拽转）；Y 轴固定 `fixedLookY=0.6`（防上下抖）。**改相机先动轴，别依赖 Cinemachine 默认输入**。
- **FPSModel 移动**：状态每帧写 `horizontalVelocity`/`verticalSpeed`，`LateUpdate` 里 `FaceDirection`（`applyRootMotion=false` 后 Animator 不自动转向，手动兜底）+ `cc.Move`。
- **自动补挂兜底**：`MonoManager.INSTANCE`/`FPSController.INSTANCE` 缺失时 `FPSModel.Start` 自动创建（但相机槽位需 Inspector 手动拖入，会有 LogWarning）。
- **瞄准参数**：`Speed`（0/0.33/0.66/1）+ `AimingX/AimingY`；瞄准状态内由 `FPSAimState` 调度三种 Aim 模式，模式类直接调 `controller.SetAimCamera/SetAimFov`。

## 已知状态

- 移动/跑酷/滑铲/瞄准（腰射/肩射/开镜）已跑通。
- 阶段 7 武器层暂停：KINEMATION 被弃后武器未接入，`FPS/Weapon/` 为空——补武器动画/武器系统后，`FPSAimState` 的开火入口直接生效。
