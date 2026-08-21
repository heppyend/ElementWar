# Assets/Scripts/Network — PVP 网络同步

> 改本目录或 `Server/` 前必读根 `AGENTS.md`。Codex 可改客户端/服务端代码，但 PVP 场景、出生点、角色 Prefab、Rig、相机和 UI 的 GUI 配置必须由用户完成并确认。

## 架构

```text
NetClient → UDP → UdpGameServer → GameWorld（权威）
  ├─ PvPMotor：本地预测              ├─ 输入/开火队列、60Hz 模拟
  ├─ RemoteAvatar：快照插值          ├─ hitscan/历史帧回退
  ├─ PVPCameraRig / PVPHealthUI      └─ 快照与可靠事件
  └─ NetMessages.cs ↔ Server/Messages.cs
```

## 强制同步项

- PVP 默认 60Hz。`GameWorldSettings.ServerTickRate`、`UdpGameServerOptions.TickRate` 与所有 tick 时间窗口必须一起审查。
- `PvPMotor` 和 `GameWorld.SimulatePlayer` 的 walk/jog/sprint/aim、重力、跳跃、滑铲和转向数学必须一致；PVP 不做服务器墙体碰撞。
- `NetMessages.cs` 与 `Server/Messages.cs` 的 DTO、camelCase JSON 字段、消息类型和语义必须同步修改。
- 客户端只发送输入与开火意图；命中、HP、死亡、重生、计分均由 `GameWorld` 决定。PVE 子弹在 PVP 中只作视觉。
- 快照全量且不可靠；死亡/重生/击杀/结束走 eventId + ACK + 去重的可靠事件账本。
- 本地 PVP 玩家设 `disableStateMachine=true`，由 `PvPMotor` 驱动；不可重新启用 PVE 随从/CharacterController 位移。

## 运行与 GUI 交接

服务端可用 `dotnet build Server/ElementWarServer.csproj`、`dotnet run --project Server/ElementWarServer.csproj -- --bots` 验证。连接 PVP、搭建场景、拖入角色/相机/出生点等由用户在 Unity GUI 操作；Codex 只给步骤并等待结果。
