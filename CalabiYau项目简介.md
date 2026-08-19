# CalabiYau 联机 TPS 项目简介

> 本文档用于在 **ElementWar** 项目中提供 **CalabiYau**（联机 Tank TPS 同步 Demo）的项目背景，方便后续两个项目联合开发时快速对齐上下文。
> 最后核对时间：2026-08-19。CalabiYau 自己的完整文档见其仓库内 `CLAUDE.md`、`README.md` 与 `docs/`。

## 一句话介绍

CalabiYau 是一个 **Unity 客户端 + .NET 8 C# UDP 服务器** 的第三人称联网射击同步 Demo。客户端只上报输入和开火意图；服务器以固定 30 Hz Tick 权威模拟位置、血量、死亡、重生和命中结果，再向每名客户端构建对应的 JSON 状态快照。客户端通过本地预测、服务端校正、远端快照插值、有限可靠结果事件、按客户端距离分级复制和射击延迟补偿，在弱网（100–200 ms 延迟）下保持可玩的同步体验。

项目定位是**实习作品集级联网 Demo**：代码优先清晰可讲，用 `UdpClient + JSON` 跑通核心链路，不刻意堆复杂框架，保留未来升级 MessagePack / LiteNetLib / KCP / 差分快照的空间。

## 与 ElementWar 的关系

| | CalabiYau | ElementWar |
|---|---|---|
| 定位 | 联网同步技术 Demo（可讲解、可验收） | 完整 3D TPS 游戏（玩法/角色/关卡） |
| 引擎 | Unity 2021.3.45f2c1（URP 12） | Unity 2022.3.62f3（URP 14） |
| 服务器 | .NET 8 控制台 UDP 服务器（无 Unity 依赖） | 无（当前纯单机） |
| 网络 | 服务端权威同步核心全部实现 | 尚未接入网络 |
| 角色 | Tank（轻量级玩家实体） | 荧 / 芙宁娜 / 暗夜猎人 |

联合开发的意义：**ElementWar 已有完整的 TPS 玩法与角色表现，CalabiYau 已有完整且可讲解的联机同步核心**。后续大概率是把 CalabiYau 的同步架构（服务端权威 Tick / 输入上报 / 快照 / 预测 / 插值 / 开火收据 / 可靠结果事件）移植或借鉴到 ElementWar，为其接入多人对战。

## 已实现能力（以 2026-08-19 代码为准）

- UDP + JSON 通信，`ClientHello` / `ServerWelcome` 连接与 playerId 分配。
- 客户端按网络 Tick（30 Hz）发送输入；服务器 30 Hz 权威模拟玩家位置、车身朝向、炮塔瞄准、血量和死亡状态。
- 服务器按客户端独立构建 `WorldSnapshot`（距离过滤：18 m 内或战斗中高频、45 m 内低频 5 Hz、45 m 外不复制）。
- 本地玩家客户端预测；收到服务器快照后按 `lastProcessedInputTick` 回滚并重放未确认输入。
- 本地预测误差支持死区、平滑修正和硬修正（阈值随估算 RTT 调整，输入活跃时延后修正避免拉扯）。
- 远端玩家快照缓冲与插值（插值延迟随快照间隔自适应）。
- 网络开火：`FireRequest` → `FireReceipt`（有限重发），服务器命中判定后广播 `FireEvent` / `HitEvent` / `HealthChangedEvent`。
- 服务端权威扣血、死亡、重生（可靠事件 `DeathEvent` / `RespawnEvent` / `KillEvent` / `MatchEndEvent`，带 `eventId` + ACK + 去重 + 单调 `lifeStateVersion` 防乱序）。
- 服务器射击延迟补偿：基于历史状态回退到开火时刻的目标位置做 hitscan 判定（最大回退 0.35 s）。
- Unity 运行时网络调试面板（`F3` 开关）：playerId / RTT / Tick / 快照缓冲 / 预测修正次数与误差 / 快照丢失率 / 复制范围 / 开火收据 / 可靠事件统计等。
- 无 Unity 依赖的服务器自动检查（`Server/Server.Tests` 控制台断言）。

## 代码架构（两端）

### .NET 服务器 `Server/Server/`（纯 .NET 8，无外部包）

```
Program.cs            组合根：启动参数 → 配置 → UdpGameServer
UdpGameServer.cs      传输层：UDP 收发、按 type 路由、固定 Tick 循环、发送与遥测
GameWorld.cs          纯权威世界：命令校验/缓存、移动模拟、射击、伤害、死亡重生、延迟补偿历史（不依赖 UDP/JSON/Unity）
SnapshotBuilder.cs    只读 Capture(world) → 复制候选
ClientReplicator.cs   每连接一个：实体范围、优先级、发送频率、独立快照序号
ClientRegistry.cs     endpoint↔playerId、最后收包时间、超时清理
ReliableEventLedger.cs 每连接一个：可靠事件重发 / ACK / 遥测
Messages.cs           服务端 JSON DTO（System.Text.Json，camelCase）
```

- 所有世界/注册表变更统一在一个 `stateLock` 下进行；锁内算好待发送内容，锁外发送。
- `GameWorld` 完全不依赖 Unity/网络，可在无服务器代码的情况下单独跑规则测试。

### Unity 客户端 `Assets/Code/`

```
UdpNetworkClient.cs    网络枢纽：套接字、按 tick 发输入、收包/路由、本地预测校正、远端生成与销毁、
                       开火有限重发、可靠事件 ACK/去重、RTT 与快照丢失估算、F3 调试面板
NetworkTankAvatar.cs   单网络玩家表现：远端快照缓冲+插值、服务器状态应用、受击闪红、死亡染色、HP 标签
TankController.cs      单机部件组合（TankInput / TankMotor / TankAim / TankWeapon）与控制模式开关
Player.cs / Target.cs  早期单机遗留脚本（已被网络链路取代，勿在联网行为中修改）
```

- 消息 DTO 在**两端各定义一份，字段必须保持一致**：服务器在 `Messages.cs`（`[JsonPropertyName]`），客户端在 `UdpNetworkClient.cs` 底部（小写字段 + `JsonUtility`）。

## 快速运行

```powershell
# 1. 启动服务器（监听 127.0.0.1:7777，30 Hz；默认开启距离过滤）
cd Server/Server
dotnet run

# 与早期"全员全量复制"基线对比
dotnet run -- --full-snapshots

# 2. 服务器自动检查（无需 xUnit，直接运行）
cd Server/Server.Tests
dotnet run
```

Unity 端：用 Unity 2021.3.45f2c1 打开仓库根目录，进入 `Assets/Scenes/SampleScene.unity`，先启动服务器再 Play；`F3` 开关注调试面板。双客户端：一个用 Editor Play，一个用 Windows Build。

## 核心同步概念（面试/讲解主线）

1. **服务器权威**：客户端只发意图，最终位置/血量/命中由服务器决定。
2. **Tick + 输入 + 快照**：服务器固定 30 Hz 模拟；客户端按固定频率发输入；服务器按客户端发快照。
3. **本地预测 + 回滚重放**：客户端先用自己的输入移动，收到权威快照后回到 `lastProcessedInputTick` 重放未确认输入。
4. **远端插值**：缓冲几帧快照、落后一小段时间播放，避免一格一格跳。
5. **可靠结果事件**：死亡/重生/击杀/比赛结束这类"必须到达一次"的结果走 ACK + 去重 + 版本号，与可丢的快照分离。
6. **延迟补偿**：开火时服务器回看历史帧做 hitscan，避免高延迟下"明明瞄准了却打不中"。
7. **按客户端复制**：复制范围/频率按每个客户端单独决定，为多人局留扩展空间。

## 给 ElementWar 的对接提示

- CalabiYau 的同步核心与 **角色无关**：`GameWorld`、`ClientReplicator`、`ReliableEventLedger`、`UdpGameServer` 都不依赖坦克；移植时把 `PlayerState` 扩展成 ElementWar 的玩家实体即可。
- 客户端表现层（`NetworkTankAvatar` 的插值/受击/生命状态）同样与坦克解耦，可迁移到任意 TPS 角色。
- CalabiYau 的服务器无 Unity 物理碰撞（服务器权威位置可穿障碍）；ElementWar 若要求墙体碰撞，需在服务器侧补简化碰撞或改用 Unity Headless Server。
- 跨项目修改时注意：两端 DTO 必须同步改；改动会影响 `docs/` 复习文档时，记得同步更新对应文档。

## 参考文档（在 CalabiYau 仓库内）

- `CLAUDE.md` — 面向 Claude Code 的架构与开发说明。
- `README.md` — 项目简介与快速运行。
- `docs/项目复习指南.md` — 当前实现的权威复习/讲解文档。
- `docs/networked-third-person-shooter-roadmap.md` — 阶段 0–9 开发路线与完成记录。
- `docs/stage-11-replication-notes.md`、`docs/stage-12a-reliable-fire-requests.md`、`docs/stage-12b-reliable-result-events.md` — 复制、可靠开火、可靠结果事件各阶段说明。
