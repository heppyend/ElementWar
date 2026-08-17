# Assets/Scripts/Editor — 编辑器向导（Tools/玩家 + Tools/场景）

> ⚠️ 本目录是**改场景/预制体的必经之路**——`Game.unity` 是二进制场景，无法文本编辑。改场景一律走这些向导（幂等可重跑）。

## 菜单速查

| 菜单 | 工具类 | 作用 |
|------|--------|------|
| `Tools/玩家/修复 NavMesh 排除标记并校正地面（重烘焙）` | `FixNavMeshAndGroundWizard` | ⭐ NavMesh 出问题用它：移除误加 ignoreFromBuild + 抬升地面 + 清空重建 + 角色吸附（写 `_Diagnostics_NavMeshFix.log`） |
| `Tools/玩家/诊断 NavMesh 层配置（写日志文件）` | `NavMeshLayerDiagWizard` | 查 surface 设置 / 层6 物体 / 被排除物体 / 角色接地（写 `_Diagnostics_NavMeshScene.log`） |
| `Tools/场景/解开全部预制体（Unpack）` | `UnpackPrefabsInSceneWizard` | 场景全部 prefab 实例脱离 prefab 关联（08-17 起 Game 场景已全 Unpack） |
| `Tools/场景/把选中物体落到地面（吸附到表面）` | `SnapToFloorWizard` | 批量把选中物体底面吸附到下方表面（换地板后 cube 悬空用；需地面有 Collider） |
| `Tools/场景/给选中物体添加 NavMeshObstacle` / `移除...` | `NavMeshObstacleWizard` | 墙/柱子阻挡寻路（carving 动态避障，不用重烘焙） |
| `Tools/场景/把选中物体烘焙为可行走并重烘焙` | `BakeWalkableWizard` | 斜坡/楼梯/平台设为可行走面并重烘焙 |
| `Tools/配置 Running Slide 动画 (Humanoid)` | `SetupRunningSlide` | 滑铲动画配置 |
| 自动（`AssetPostprocessor`） | `RedWolfRoseFBXFixer` | FBX 导入后把内嵌材质自动换 `URP/Lit` + 重链贴图（红狼_玫瑰用） |

## 铁律

- **`Game.unity` 二进制、不可手改**：改场景（换控制器/修约束/重烘焙/移动物体）一律写向导执行。
- **08-17 起场景已全 Unpack**：场景对象已非 prefab 实例，可 `SetParent`/自由编辑、**无需** `RecordPrefabInstancePropertyModifications`。「prefab 实例内不能 SetParent」的限制仅剩 prefab 资产编辑时适用。
- **按 prefab 路径识别对象的旧工具已删**（08-17 清理），新工具按场景对象/名称操作。
- **诊断/修复脚本写 `_Diagnostics_*.log`（项目根）**：由 Claude 直接读文件定位，不让用户复制 Console。Editor.log 在 `%LOCALAPPDATA%\Unity\Editor\Editor.log`。
- **`PlayerModel.cc` 编辑模式为 null**：向导算角色脚底用场景 Transform/地面射线，别用 `PlayerModel.cc`。
- **NavMesh 事故复盘（08-17）**：旧「标记可能走上天」工具用启发式（顶部离地>2.5m 或浮空>2m）把 1000×1000 地面误判浮空 → 加 `NavMeshModifier(ignoreFromBuild)` → **烘焙 0 三角面** → 所有代理 not close enough。⚠️ **绝不要用启发式给地面加 ignoreFromBuild**；地面出问题跑 `FixNavMeshAndGroundWizard`。

## 写新向导的约定

- `UnityEditor` 命名空间，`[MenuItem("Tools/玩家/...")]` 或 `Tools/场景/...` 菜单路径。
- **幂等可重跑**；关键操作写 `_Diagnostics_*.log`。
- 场景改动后 `EditorSceneManager.MarkSceneDirty(scene)` + `SaveScene(scene)` / `SaveOpenScenes()`。
- 一次性工具用完即删（参照 08-17 清理习惯：Hunter 接入/还原、武器 IK 修复等向导已删）。
