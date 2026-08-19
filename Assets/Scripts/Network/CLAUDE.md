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
- **开火手感复制 PVE**：NetClient.SendFire 客户端限速 0.15s（与 PVE bulletInterval 一致）+ 调 `weapon.Fire(_aimPoint)` 走完整 PVE 视觉（Rigidbody 弹道 + 枪口火花 EffectPool + Fired 事件触发 WeaponAudio 枪声），伤害由服务器权威判定、视觉子弹对玩家无效。服务器 `FireCooldownSeconds` 已对齐 0.15f（否则视觉 6.67 发/秒只有 1/5 结算）。开火/受击相机震动走 `PVPCameraRig.ShakeCamera()`（ImpulseSource+ImpulseListener 挂瞄准 FreeLook）。
- **两端 DTO 严格一致**：服务器 `Server/Messages.cs`（JsonPropertyName camelCase）↔ 客户端 `Transport/NetMessages.cs`（public camelCase 字段）。改协议两端一起改。
- **输入 tick 对齐**：`ServerWelcome.serverTick` 给客户端对齐 `inputTick` 基线（否则被判"太旧"拒绝）。服务器 latest-input 语义 + 6 tick key-up 丢失窗口。
- **玩家只控 1 角色**：PVP 屏蔽 AI 队友。PlayerModel 置 `disableStateMachine=true`，位移由 PvPMotor 驱动 transform（无 CharacterController 位移）；NavMeshAgent 禁用。
- **命中在服务器**：hitscan（射线 vs 历史帧胶囊，回退≤0.35s 延迟补偿）。客户端子弹纯视觉（枪口火花+音效）。
- **可靠事件**：死亡/重生/击杀/对局结束走 `ReliableEventLedger`（eventId + ACK + 去重 + lifeStateVersion）。快照永远全量（不搞 delta）。

## 运行

1. **启动服务器**：`cd Server && dotnet run`（默认 0.0.0.0:7777，30Hz）
2. **Unity 主菜单** → 在线 → 填服务器 IP（同机 `127.0.0.1`）→ 选角色（荧/芙宁娜）→ 连接
3. 加载 `PVPGame` 场景 → `NetworkLauncher` 自动连服务器。双人：开 2 个客户端实例（编辑器 + build）。
4. 场景搭建：`Tools/玩家/搭建 PVP 场景（PVPGame）`（幂等）。

## 场景/目录

| 文件 | 职责 |
|------|------|
| `Transport/UdpSocket.cs` | UDP 封装（发送/非阻塞接收） |
| `Transport/NetMessages.cs` | 两端共用 DTO（Msg 常量 + 各消息类） |
| `Client/NetClient.cs` | 客户端中枢：握手/上报输入/收快照/预测校正/远端管理/可靠事件/开火 |
| `Client/PvPMotor.cs` | 本地预测简化移动 + Animator 驱动（数学与服务器一致） |
| `Client/RemoteAvatar.cs` | 远端玩家插值表现 |
| `Client/PVPCameraFollow.cs` | PVP 轨道相机（鼠标视角，屏幕中心=瞄准） |
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
