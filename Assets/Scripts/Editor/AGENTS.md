# Assets/Scripts/Editor — Unity 编辑器工具代码

> 本目录可以修改 Editor C# 的实现，但 Codex **不得运行**任何会改写 Scene、Prefab、资源或 Project Settings 的菜单命令。需要用户操作时，给出菜单路径、前置选择、预期日志和验证步骤，等待确认。

## 现有工具分组

- 场景与 NavMesh：`FixNavMeshAndGroundWizard`、`NavMeshLayerDiagWizard`、`BakeWalkableWizard`、`NavMeshObstacleWizard`、`SnapToFloorWizard`、`UnpackPrefabsInSceneWizard`。
- PVP/动画/音频：`BuildPVPSceneWizard`、`AnimatorDiagnosticWizard`、`SetupRunningSlide`、`WeaponAudioWizard`。
- 表现与导入：`CJKFontWizard`、血效三个 Wizard。

## 代码约束

- 菜单操作必须幂等、退出 Play Mode 后才允许执行、明确 Undo/保存/日志语义，并避免靠名称模糊匹配误伤对象。
- 当前 PVE 主场景是 `PVEGame.unity`，不是历史记录中的 `Game.unity`。
- 修改涉及 PVE/PVP 场景时，工具只可作为用户手动执行的实现；Codex 不可用它替代 GUI 操作。
- 诊断工具可写报告；报告不得被当作配置事实源，仍以用户确认的 Editor 状态为准。
