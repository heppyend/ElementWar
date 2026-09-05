# 诺诺 + P90 / TextScene 隔离适配交接（2026-09-05）

## 下一任务

仅在 `Assets/Scenes/TextScene/Single Framework.unity` 中完成 **诺诺 + P90** 的 BBB-Nexus 隔离适配与 Play Mode 验收。

不要重构 PVE/PVP，不要将诺诺接入旧 `PlayerModel` / `PlayerController` 状态机，不要把其他新角色提前接入。

## 已固定的资源基线

### 诺诺

- 已确认 Unity 预览上色成功的 PMX 转换流程：Blender MMD Tools -> 标准材质节点 -> FBX。
- 正式批量输出：
  `Assets/BBBWork/NewCharacterAssets/Unclassified import/诺诺/Converted/诺诺_BBB.fbx`
- 结构离线验证：1 个网格 `诺诺_mesh`、1 个骨架 `诺诺_arm`、31,593 顶点。
- 旧测试输出 `Nono_BBB.fbx`、`Nono_BBB_MaterialTest.fbx`、`Nono_BBB_AutoTest.fbx` 均保留；不要删除或覆盖，后续由用户决定清理。

### P90

- 模型与已上色材质目录：
  `Assets/BBBWork/NewWeaponAssets/Firearms/FN P90 & Custom/FN P90 & Custom/`
- 武器 Prefab：`P90_Nono_Weapon.prefab`。
  - 根节点 scale 为 `(1,1,1)`。
  - 根节点已挂 `P90Behaviour`。
  - `LeftHandGoal` 与 `Muzzle` 已由用户在 Inspector 绑定。
  - `MuzzleFlash` 当前可为空。
- 武器配置资产：`Configs/P90_Nono_Config.asset`。
  用户已将 `P90_Nono_Weapon.prefab` 拖入其 `Prefab` 字段；数值、投射物、开火/换弹动作尚未验收。
- 代码资产：
  - `Assets/BBBWork/BBBNexus/Item/Data/Weapons/P90SO.cs`：继承 `AKSO`，复用已知自动武器数据形状。
  - `Assets/BBBWork/BBBNexus/Item/Logic/Weapons/P90Behaviour.cs`：继承 `AK46Behaviour`，复用已知自动武器运行逻辑。

### PMX 批量转换技能

- 位置：`C:\Users\wxm\.codex\skills\unity-pmx-character-conversion\`
- 功能：默认保留 PMX 贴图映射、过滤 MMD 物理辅助网格，并验证每个 FBX 为 1 网格 + 1 骨架。
- 全部新角色已批量转换并离线结构验证；用户已确认 Unity 内均成功上色。

## 已确认与未确认

| 项目 | 状态 | 证据边界 |
|---|---|---|
| 新角色 FBX/贴图导入 | 已确认 | 用户已确认 Unity 预览全部上色成功；每个批量 FBX 离线结构验证通过。 |
| Ming BBB 基线 | 部分已确认 | Play Mode 已确认待机、移动、跑步与瞄准组合；**未确认跳跃/落地**。 |
| P90 材质与结构 | 部分已确认 | 用户已完成材质；Prefab 的挂点绑定已完成。未做诺诺持枪实机验收。 |
| 诺诺 BBB 接入 | 未开始 | 尚未完成 Humanoid、诺诺 Prefab、BBB 依赖/配置引用、装备配置绑定与 Play Mode。 |
| Unity 编译/Console | 未独立验收 | `RootMotionExtractor.cs` 的 CS0414 已移除；仍需以 Unity Console 0 error 为准。 |

## 不可跨越的边界

- Unity 的 Rig、材质、Prefab、Scene、Animator、Inspector 组件引用均由用户在 GUI 执行；Codex 只修改 C#、做只读审计，并给出单步操作。
- 目录创建和项目内资源搬运可由 Codex 执行，但不得覆盖/删除未确认资源，并必须连同 `.meta` 一起移动。
- 当前 Git 工作区有用户资源搬运及未提交 Unity Scene 状态。不要执行 `git reset`、`git checkout --`、清理 untracked 文件或批量删除测试资源。
- 当前阶段不触碰 PVE/PVP 共享规则重构；`BBBCharacterController.cs` 中已有未验证的运行时规则适配改动，和本次诺诺验收无关，除非编译错误才定位它。

## 推荐最小闭环（严格按序）

1. 在 Unity 选择 `诺诺_BBB.fbx`，人工确认材质、面数和朝向后设为 Humanoid；Apply。
2. 从该 FBX 建立诺诺专用 Prefab，不改 Ming，不接旧 PVE 控制器。
3. 在 TextScene 复制 Ming 的 BBB 依赖形状到诺诺，并逐项替换为诺诺自己的模型/Animator/配置；保留 P90 的独立 Prefab 与 `P90_Nono_Config`。
4. 将 `P90_Nono_Config` 绑定为诺诺的默认装备槽 1；先不猜测弹药、后坐力、投射物和动画数值。
5. 进入 Play Mode，只验收：生成无报错、待机、移动、跑步、瞄准、持 P90、开火入口与左手/枪口挂点。每一项分开记录。
6. 只有上述通过后，才补 P90 数值、火花/投射物、换弹和其他角色适配。

## 本次验收标准

完成不等于“模型已显示”。必须同时有：

1. Unity Console 无 error；
2. TextScene 中仅诺诺 + P90 的隔离组合可进入 Play Mode；
3. 待机、移动、跑步、瞄准和持枪均无空引用/模型错位；
4. P90 左手目标、枪口 Transform 引用有效；
5. 不把 Ming 跳跃/落地、PVE/PVP 或其他角色当作本任务已验收内容。
