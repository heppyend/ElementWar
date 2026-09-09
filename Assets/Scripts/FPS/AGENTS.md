# Assets/Scripts/FPS — Hunter/FPS 实验沙盒

> 此模块是 FPS 代码实验沙盒，不是 PVE/PVP 构建主路径。公开版不包含其实验场景和第三方表现资源；所有相机、Rig、Animator、武器和场景配置由使用者在合法取得资源后通过 Unity GUI 完成。

## 结构

- `Core/`：`FPSModel`、枚举和状态基类。
- `Controller/`：`FPSController` 输入轮询、FreeLook 轴接管、AimTarget。
- `State/`：Idle/Move/Sprint/Air/Slide/Aim。
- `Aim/`：HipFire、Shoulder、Ads 三种模式。
- `Editor/`：Animator/相机/场景辅助工具代码；Codex 不运行其写入性操作。
- `Weapon/` 当前为空，`FPSAimState` 的武器层仍是 TODO。

## 关键约束

- 本沙盒的 `FPSModel` 以代码驱动 CC 位移和手动相机轴为中心；不要把它与 PVE `PlayerModel` 的 Root Motion/Rig 约束混用。
- `FPSController` 同步两台 FreeLook 的 X 轴，避免 Priority 切换跳向；三种瞄准模式负责相机/FOV 策略。
- 自动补挂 Controller/Manager 仅是运行时兜底，不等于 Unity 引用已配置。出现槽位警告时，向用户交接 GUI 设置。
