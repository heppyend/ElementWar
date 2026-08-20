# Assets/Scripts/Network — PVP 网络同步

> 2026-08-19 新建。PVP 自定义 UDP 权威框架（参考 CalabiYau 架构，非直接移植）。服务器是独立 .NET UDP 进程（`Server/`，不在 Assets 内）。

## 架构

```
Unity 客户端 (Assets/Scripts/Network/)          .NET 服务器 (Server/)
  NetClient（连接/上报输入/收快照/可靠事件）  UDP → UdpGameServer（30Hz Tick + 路由）
  PvPMotor（本地预测，简化移动）              GameWorld（权威模拟：移动/命中/死亡重生/计分）
  RemoteAvatar（远端插值）                    SnapshotBuilder/ClientReplicator/ReliableEventLedger
  PVPHealthUI（血条/计分/对局结束）           Messages.cs（两端 DTO 对齐）
```

## 关键约定

- **简化移动模型**（C# 服务器跑不了 CharacterController）：服务器 `GameWorld.SimulatePlayer` 与客户端 `PvPMotor` 数学一致（walk2.2/jog5/sprint8/aim2.5、gravity-15、jump6.7、slide0.8s/初速7/末速1.5/冲刺加成2）。改任一端常量必须同步另一端。PVP 无墙碰撞（掩体纯视觉）。
- **滑铲是服务器计时状态**：客户端 isSlide 只在一个 30Hz 帧为 true（NetClient 锁存 0.12s 发完即清），服务器 `GameWorld` 收到后启动 `SlideTicksRemaining` 计时（方向锁 input.WorldMove 或当前朝向、速度衰减、强制贴地），不依赖后续输入；跳跃可中断。MoveState=5 供远端播 RunningSlide。
- **角色动画统一 `TPS_Movement.controller`（混合版，PVE/PVP 共用）**：Locomotion 混合树 **Idle/Walk/Jog 段用旧 X Bot 动画**（`@Idle` / `@Run forward`，Speed 阈值 0/.33/.66，fileID -203655887218126122）、**Sprint 段用跑酷 `Mvm_Dash`**（Speed 1）、**Air 混合树用旧 `@Hover`**、滑铲跑酷 `Esc_Slide_Loop`、瞄准 X Bot strafe。两套动画都 Humanoid 可跨骨骼 retarget。⚠️ PVP 曾用独立 `TPS_Movement_PVP.controller`（08-19 已删，统一回 TPS_Movement）；Hunter 仍用 `Hunter_Parkour.controller`。
- **⚠️ 远端瞄准混合（08-20 修复 bot 平移）**：`RemoteAvatar` 必须设 `AimingX/AimingY`（从插值位移推字符局部移动方向）+ `IsSprinting`——否则远端（尤其 bot）进 Aiming 状态时 `AimingBlend` 树（靠 AimingX/Y）停在 0/0 → 静态瞄准姿势 + 容器平移 =「平移/滑行」。PvPMotor 本地有相机相对输入所以不受影响。
- **开火手感复制 PVE**：NetClient.SendFire 客户端限速 0.15s（与 PVE bulletInterval 一致）+ 调 `weapon.Fire(_aimPoint)` 走完整 PVE 视觉（Rigidbody 弹道 + 枪口火花 EffectPool + Fired 事件触发 WeaponAudio 枪声），伤害由服务器权威判定、视觉子弹对玩家无效。服务器 `FireCooldownSeconds` 已对齐 0.15f（否则视觉 6.67 发/秒只有 1/5 结算）。开火/受击相机震动走 `PVPCameraRig.ShakeCamera()`（ImpulseSource+ImpulseListener 挂瞄准 FreeLook）。
- **两端 DTO 严格一致**：服务器 `Server/Messages.cs`（JsonPropertyName camelCase）↔ 客户端 `Transport/NetMessages.cs`（public camelCase 字段）。改协议两端一起改。
- **输入 tick 对齐**：`ServerWelcome.serverTick` 给客户端对齐 `inputTick` 基线（否则被判"太旧"拒绝）。服务器 latest-input 语义 + 6 tick key-up 丢失窗口。
- **玩家只控 1 角色**：PVP 屏蔽 AI 队友。PlayerModel 置 `disableStateMachine=true`，位移由 PvPMotor 驱动 transform（无 CharacterController 位移）；NavMeshAgent 禁用。
- **命中在服务器**：hitscan（射线 vs 历史帧胶囊，回退≤0.35s 延迟补偿）。客户端子弹纯视觉（枪口火花+音效）。
- **可靠事件**：死亡/重生/击杀/对局结束走 `ReliableEventLedger`（eventId + ACK + 去重 + lifeStateVersion）。快照永远全量（不搞 delta）。

## 运行

1. **启动服务器**：`cd Server && dotnet run`（默认 0.0.0.0:7777，30Hz）
2. **Unity 主菜单** → 在线 → 填服务器 IP（同机 `127.0.0.1`）→ 选角色（荧/芙宁娜）→ 连接
3. 加载 `PVPGame` 场景 → `NetworkLauncher` 自动连服务器。**单人即可玩**（见「训练模式」）；双人：开 2 个客户端实例（编辑器 + build）。
4. 场景搭建：`Tools/玩家/搭建 PVP 场景（PVPGame）`（幂等）。

## 训练模式（单人可玩，2026-08-20）

> 单人连服务器不再空场：**服务器自动补位，总战斗人数恒为 2**。
> ⚠️ **08-20 起默认关闭**（`GameWorldSettings.EnableBots=false`，`--bots` 开启）：单人纯测试时不补 bot，避免被 bot 秒杀干扰；要训练模式 `cd Server && dotnet run -- --bots`。

- **补位规则**（`GameWorld.EnsureCombatantCount`）：人类 1 人 → 补 1 个 bot；第 2 个人类加入 → 移除 bot（纯 1v1）；人类走剩 1 人 → 重补。满员判断按 `HumanCount`（`welcome_full` 只在 ≥2 人类时触发）。
- **bot 是服务器侧纯逻辑**：无网络端点，`ServerPlayer.IsBot=true`，id 用**独立负数**（-1/-2…），人类 id 恒 1、2…（不占序号，计分板/出生点不撞）。bot 不生成客户端连接，但出现在快照/可靠事件里 → 客户端 RemoteAvatar 自动渲染（复用现有远端路径，无需新组件）。
- **bot AI**（`GameWorld.SimulateBot` / `ResolveBotFire`）：追击（距离>14m 冲刺 8m/s）→ 侧移走位（6~14m，随机方向 0.5~0.83s 换向）→ 后撤（<6m）；面向目标；瞄准带误差（距离×0.06+0.8m，防激光枪）+ 大致朝向校验；射程 35m、冷却 0.15s 与人类一致；**复用同一 `ResolveShot` 射线命中**（人类/bot 一套弹道）。走完整生命流程：受击/死亡/重生/计分/对局结束（复用 `Die`/`Respawn`/`MatchEnd`，分数同样 5 分制）。
- **调参**：`GameWorldSettings` 的 `BotChaseRange`/`BotBackRange`/`BotAimErrorFactor`/`BotAimErrorBase`/`BotDecisionTicksMin`/`BotDecisionTicksMax`。
- **协议**：`PlayerSnapshotMessage` 新增 `isBot`（两端 DTO 已同步），客户端 `NetClient.BotIds` 维护 bot 集合，`PVPHealthUI` 计分板显示「Bot」+ 标题「训练模式（机器人）」。
- 冒烟测试（临时脚本，未入库）：起服务器后跑 `python <temp>/bot_smoke_test.py`（UDP 模拟客户端，验证生成/移动/开火/死亡/重生/补位全链路，12 项断言）。

## 08-20 修复（PVP 训练模式实测反馈）

- **瞄准右肩镜头**（`PVPCameraRig`）：瞄准相机**不再与普通视角共用 YAxis**。`_aim.m_YAxis.Value = _aiming ? AimShoulderY(0.5 中段轨道) + _aimPitch : _yAxis`，进入瞄准归零 `_aimPitch`、瞄准时仅做 ±俯仰微调。否则前置镜头抬到人物上方时，瞄准镜头也跟着到上方 → 不是右肩镜头。XAxis（水平）仍与普通视角共享。瞄准轨道距离 **1.2→1.8**（1.2 贴进角色头部 = "拉很近/穿模"）。
- **⚠️ PVP 瞄准 IK（08-20 修，枪不跟准心）**：PVP 预制体里 `MultiAimConstraint` 源对象是空的（`m_Item0.transform: {fileID: 0}`），且 PVP 无 PlayerController/状态机 → 没人更新 AimTarget、没人切 IK 权重 → 移动时枪掉下来。修：`NetClient.WireLocalAimTarget()`（生成后）补一个运行时 `AimTarget_PVP` 子物体接进所有 MultiAim 源（`ref var d = ref c.data; d.sourceObjects.SetTransform(0, t)`，之后 `rig.Clear()+Build()` 重建），并每帧 `_localAimTarget.position = _aimPoint` 跟准心；`PvPMotor` 按 `isAiming||isFire` 切换权重（瞄准 MultiAim=1/髋部 TwoBoneIK=0，松开恢复）。⚠️ 预制体 MultiAim 默认 `m_Weight:1`（激活），接线后必须重置为 0（髋部持枪），否则不瞄准枪也被拽着。
- **左上角信息窗**（`Assets/Scripts/Diagnostics/DebugInfoWindow.cs`）：运行时自建 TMP 覆盖层，显示 FPS/帧耗时/分辨率/场景/网络状态，F3 开关，`DontDestroyOnLoad` 常驻；`NetworkLauncher` 自动确保存在（PVP）。
- **中文 UI 乱码（口口口）**：TMP 默认字体 LiberationSans 无 CJK 字形。`Assets/Scripts/Editor/CJKFontWizard.cs`（Tools/玩家/烘焙中文默认字体）从 `Assets/Resource/字体/印品抹茶体.ttf` 烘焙 Dynamic 字体并设为 TMP 默认，再扫描场景/预制体替换旧默认字体（Additive 不打断当前场景）。⚠️ **需在编辑器里手动运行一次**（batch 因编辑器已打开项目而无法执行）。

## 场景/目录

| 文件 | 职责 |
|------|------|
| `Transport/UdpSocket.cs` | UDP 封装（发送/非阻塞接收） |
| `Transport/NetMessages.cs` | 两端共用 DTO（Msg 常量 + 各消息类） |
| `Client/NetClient.cs` | 客户端中枢：握手/上报输入/收快照/预测校正/远端管理/可靠事件/开火 |
| `Client/PvPMotor.cs` | 本地预测简化移动 + Animator 驱动（数学与服务器一致） |
| `Client/RemoteAvatar.cs` | 远端玩家插值表现 |
| `Client/PVPCameraFollow.cs` | PVP 轨道相机（鼠标视角，屏幕中心=瞄准；旧简化相机，已被 PVPCameraRig 替代） |
| `Client/PVPHealthUI.cs` | PVP HUD（血条/计分/死亡重生/对局结束，运行时自建） |
| `GameMode/NetLobbyConfig.cs` | 跨场景传参（ip/port/characterId/playerName） |
| `GameMode/NetworkLauncher.cs` | 场景入口：读配置 → 配置 NetClient → Connect |
| `GameMode/PlayerSpawner.cs` | 角色预制体 + 出生点按 playerId 分配 |
| `PVPUI/PVPLobbyUI.cs` | 主菜单「在线」弹出的大厅（IP/角色/连接） |

## 服务器（Server/，.NET，参考 CalabiYau 移植）

- `Program.cs` 组合根（--port/--tickrate）；`UdpGameServer.cs` 传输+30Hz Tick+路由+快照+可靠事件重发；
- `GameWorld.cs` 纯权威模拟（零 IO 可单测）；`SnapshotBuilder.cs`/`ClientReplicator.cs`（2 人全量复制）；
- `ReliableEventLedger.cs` 可靠事件；`Messages.cs` DTO；`Vec3.cs` 自研向量。
- 数值：HP100 伤害25 冷却0.75s 重生3s 射程35m 命中半径1.2m 瞄准容差——均来自 `GameWorldSettings`。
