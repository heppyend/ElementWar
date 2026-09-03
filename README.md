# ElementWar 项目基线

> 审计日期：2026-09-03  
> Unity：2022.3.62f3 / URP 14.0.12  
> 当前代码基线：`master` / `2658ae2`  
> 本文是当前项目状态、开发目标和问题优先级的统一入口。PVP 1v1 已在 2026-09-03 完成内部双端验收；下一阶段的唯一入口见 [`项目交接与下一阶段定义`](项目文档和简历面试等/ElementWar_项目交接与下一阶段定义_2026-09-03.md)。代码与实际 Unity 运行结果高于历史文档。

## 1. 项目定位

ElementWar 是一个以第三人称射击为核心的 Unity 学习与作品项目，重点方向是：

- 3C：角色、相机、输入和移动手感；
- 网络：独立 .NET UDP 权威服务器、本地预测、校正和远端插值；
- 性能：对象池、运行指标、Profiler 驱动优化；
- 渲染：URP、角色材质和效果表现；
- 可扩展性：先完成数据边界和可观测性，再逐步评估 Lua 与 ECS。

当前不能把它描述成“完整游戏”。PVE 已有可玩的基础战斗闭环；PVP 已完成固定双人 1v1 的内部验收，但尚未具备正式服务化、扩容与安全能力；FPS 模块仍是实验沙盒。

## 2. 事实来源

发生冲突时按下面顺序判断：

1. 当前 C# 与服务器源码；
2. Unity 场景、Prefab、Animator 和 Inspector 的实际配置；
3. Unity Play Mode 日志、F3 指标、Profiler 和双客户端测试结果；
4. 本文与各模块 `AGENTS.md`；
5. 面试文档、历史评估、旧日志和聊天结论。

服务器自测只能证明纯 C# 权威世界的有限契约，不能证明 Unity 画面、动画、相机或 Inspector 正常。

## 3. 当前系统地图

### PVE 主线

```text
MyInputSystem
  -> PlayerController（输入、相机、角色切换、瞄准）
  -> PlayerModel + StateMachine（移动和动画状态）
  -> CharacterController（主控位移）
  -> NavMeshAgent（非主控随从位移）
  -> PlayerWeapon / PlayerWeaponBullet
  -> EnemyBase / ZombieEnemy
```

已实现：三角色切换、移动/跳跃/冲刺/滑铲/瞄准/射击、敌人追击和攻击、生命值、死亡接管、基础血条、枪声组件和部分特效池。

未完成或不完整：弹药/换弹/武器切换、完整 HUD、暂停和设置、完整音频系统、PVE 复活、正式关卡、玩家受击/死亡动画配置。

### PVP 主线

```text
NetClient
  -> UDP + JSON
  -> UdpGameServer
  -> GameWorld（60 Hz 权威移动、命中、生命、死亡、重生、计分）

客户端：PvPMotor 本地预测 + 未确认输入重放
远端：RemoteAvatar 快照缓冲插值
可靠事件：Death / Respawn / Kill / MatchEnd + ACK/重发
```

已实现：连接握手、最多两名真人、60 Hz 权威模拟、输入冗余、预测校正、快照序列统计、RTT、延迟补偿命中、权威开火广播、死亡/重生/计分、训练 Bot、结算慢动作、返回菜单与 F3 网络指标。移动、跳跃、冲刺、滑铲、碰撞、瞄准、开火、命中、死亡、重生、结算和重复进房已完成双端 1v1 内部验收。

当前限制：

- 仍是单房间、固定两人、固定两个角色的验证框架；
- 使用每客户端 60 Hz 全量 JSON 快照，尚未做带宽、GC 和 MTU 预算；
- 没有正式的重连、会话恢复、身份校验和作弊防护；
- 训练 Bot 的伤害当前为 `0`，这是单客户端同步验证设置，不是正式玩法平衡；
- 客户端和服务端移动参数仍是两份配置，存在再次漂移的风险。

### FPS 沙盒

`Assets/Scripts/FPS` 与 `Assets/Scenes/New Scene.unity` 用于隔离实验移动、相机和瞄准，不是 PVE/PVP 正式路径。它与 PVE 存在明显重复，武器开火仍有 TODO。短期只保留为 3C 试验场，不继续复制正式玩法代码。

## 4. 2026-09-02 工作区整理结果

### 必须保留，不能自动清理

当前 Git 工作区有四组 Unity 资源变化：

- `Assets/Resource/Animations/FPS/FPS_Hunter.controller`：移动混合树引用改到新 FBX；
- `Assets/Scenes/New Scene.unity`：两个 Transform 的极小浮点旋转变化；
- `Assets/Resource/Animations/Player/卡拉彼丘拆包.meta`；
- `Assets/Resource/Animations/Player/卡拉彼丘拆包/HuiXing_Run_Fixed.fbx` 及其 `.meta`。

这些变化彼此有关，像是一次 Hunter/FPS 动画试验，而不是无意义垃圾。它们涉及 Animator、Scene 和导入资源，已原样保留，需由用户在 Unity 中确认动画效果后再决定提交或放弃。

### 可再生成，但不属于 Git 脏工作区

- `Library`：约 4.75 GB；Unity 导入缓存，清除会触发完整重导入；
- `.vs`：约 9 MB；IDE 索引和布局；
- `obj`、`Server/bin`、`Server/obj`：约 3.5 MB；编译产物；
- `Logs`：约 0.2 MB；最近 Unity 导入和 Shader 编译日志；
- 根目录 `*.csproj` / `*.sln`：Unity 生成文件，目前仍引用已不存在的 Low Poly FPS Pack 脚本，不能作为项目编译真相。

它们都已被 `.gitignore` 排除。本轮没有为了“看起来干净”而删除正在使用或会导致大规模重导入的缓存。需要释放空间时，应先关闭 Unity 和 IDE，再通过 Unity 重新生成工程文件。

### 不应删除

`Assets/Plugins/MMD4Mecanim/Shaders/*.shader.bak` 是第三方插件随包提交的历史文件，不是本轮产生的脏文件。第三方资源、许可证和示例在完成 GUID、Scene/Prefab 引用与授权检查前都不做删除。

## 5. 主要问题与优先级

### P0：先得到可信的可玩基线

1. 完成单客户端 + 零伤害 Bot 的 Unity 复测，确认不再出现零伤害震屏和不存在的 `Dead` 状态警告。
2. 完成双客户端 1v1，在不同 RTT/丢包条件下记录 F3 指标、画面录像和服务器拒绝计数。
3. 确认当前 Hunter/FPS 动画试验资源的效果，处理四组未提交 Unity 资源。
4. 建立最小 Unity EditMode/PlayMode 测试；当前只有服务器 `SelfTests.cs`，Unity Test Framework 尚未真正使用。

### P1：把 PVP 从验证框架推进到可用 1v1

1. 建立服务器可查询的竞技场碰撞数据，客户端只负责表现，避免穿墙和掩体无效。
2. 将移动、武器、生命和网络参数提取为经过校验的共享配置或生成产物，避免双端手工同步。
3. 增加可重复的网络模拟和验收指标：RTT、丢包、抖动、带宽、校正距离分位数、硬校正次数。
4. 对全量 JSON 快照测量字节/秒和 GC，再决定增量快照、二进制协议或降低快照率；不能先凭感觉重写。
5. 补齐重连、会话超时后的恢复策略，并明确可靠事件超过 20 次重发后丢弃的处理。

建议验收门槛：在 80 ms RTT、1% 丢包的受控测试中，连续 10 分钟无断线；服务器输入/开火拒绝有可解释原因；出生/重生外没有硬校正；普通移动校正距离 P95 小于 0.15 m；无肉眼可见的周期性瞬移。门槛可在第一轮测量后修订。

### P1：收敛 PVE/3C 架构

1. 拆分 `PlayerController` 的输入、相机、角色编队、IK 和死亡接管职责。
2. 保留现有 `PlayerModel` 行为，先引入输入快照和角色运行数据，再拆状态；不整体替换已有 3C。
3. 修正基础设施生命周期：`SingleMonoBase` 的重复实例策略、状态机销毁、集中 Update 订阅释放。
4. 统一对象池边界。当前粒子和子弹已部分池化，但曳光、弹孔、血条等仍有 `Instantiate/Destroy`。
5. 用 ScriptableObject 或纯数据对象承载角色、移动、武器和技能配置，为 Lua 数据层留下稳定边界。

### P2：工程与内容建设

- 建立根目录项目说明、变更记录、测试清单和按模块提交规范；
- 避免再次出现网络、场景、URP、字体和文档混在一个提交中的情况；
- 评估 Git LFS 和二进制属性。当前约 7234 个跟踪文件、1.18 GB，仓库没有 `.gitattributes`；
- 用 Profiler 数据设定 CPU、GC、Draw Call、显存和加载时间预算；
- 在数据边界稳定后引入 Lua；只有大量同构实体的 Profiler 证据出现后，再做局部 ECS 试点。

## 6. 三个开源框架的吸收方案

开源源码目录只读，不改变其结构。结论是“吸收思想、做隔离试点”，不是把三个框架一起导入。

### BBB-Nexus

适合借鉴：

- `IInputSource`：把玩家输入、AI 输入、网络输入统一为输入源；
- RuntimeData / InputData：区分瞬时输入、动作意图和持续运行状态；
- Intent Processor + Arbiter：在跳跃、滑铲、瞄准、受击等冲突动作之间做集中仲裁；
- Animation / IK / Audio Facade：让玩法逻辑不直接依赖具体表现后端；
- 分模块 ScriptableObject 配置。

不直接导入：本地源码 132 个 C# 文件但没有测试和独立许可证文件；核心强依赖 Animancer，部分源码直接引用 FinalIK；编辑器脚本会修改宏；它将 `applyRootMotion=false`，与 ElementWar 当前 Animation Rigging 契约冲突。README 的授权说明不能替代可留档许可证。

### Fantasy

适合借鉴：

- 纯逻辑世界与网络宿主分离；
- 协议和实体系统源码生成，减少客户端/服务端 DTO 手工漂移；
- 会话、路由、服务发现、跨服和可观测性的工程分层；
- ECS 用于服务器大量实体，而不是替换 Unity 角色表现。

不直接替换当前 PVP：Fantasy 是完整分布式服务器体系，迁移会同时改变协议、会话、实体模型和部署，无法判断同步问题究竟来自传输还是玩法模拟。本地 Unity 包版本为 `2026.1.2001`，依赖 Newtonsoft JSON。其许可证文本虽标题为 MIT，但额外排除了特定主体，法律上不能简单按标准 MIT 处理，正式使用前需单独确认。

未来触发条件：当前 1v1 UDP 通过稳定性验收后，若出现多房间、跨服、AOI、热更或协议维护成本，再建立并行 `FantasyPilot`，只替换连接/消息入口，复用同一权威玩法核心做 A/B 对照。

### YokiFrame

适合借鉴：

- Kit 化边界：EventKit、LogKit、PoolKit、TableKit 各自独立；
- Architecture 的注册与获取契约；
- 日志、事件、配置、资源和 UI 不互相反向依赖；
- 测试和工具链按模块组织。

暂不导入：本地源码是 `2.0.0-preview`，外层 unitypackage 是 `1.8.5`，不能混用；源码包有大量测试，但部分 Unity 模块直接引用 UniTask，而包清单没有声明依赖；2.0 仍为预览版。优先借鉴接口和目录边界，不替换 `StateMachine`、`EffectPool` 或现有 PVP。

### 在 ElementWar 中的落地顺序

1. 先用 BBB-Nexus 思想定义 `PlayerInputSnapshot`、`CharacterRuntimeData` 和动作仲裁接口，但保持现有状态与动画行为。
2. 用 YokiFrame 思想建立最小 `ILogSink`、事件总线和配置边界，不导入整包。
3. 抽出 PVP 纯权威玩法核心与传输接口，为协议生成或 Fantasy 并行试点准备边界。
4. 所有试点必须可移除、可对照、带测试和性能数据；未证明收益前不进入 PVE/PVP 主路径。

## 7. 新开发路线

### 阶段 A：基线封板

- 完成当前动画资源确认；
- 完成 Bot 和双客户端 Unity 验收；
- 固化网络测试记录；
- 补最小自动化测试。

### 阶段 B：PVP 可用 1v1

- 服务器碰撞；
- 配置单一来源；
- 可复现的网络模拟；
- 带宽和校正预算；
- 重连和错误反馈。

### 阶段 C：3C 与玩法完整度

- 拆分输入/相机/动作仲裁；
- 完成武器、弹药、换弹和 HUD；
- 补正式受击/死亡/重生表现；
- 以 FPS 沙盒验证后再合入 PVE。

### 阶段 D：数据化与规模化

- 角色、武器、技能配置解耦；
- Lua 只进入内容配置和受控玩法脚本；
- ECS 只进入有数据证明的高数量实体领域；
- Fantasy 仅在多房间/跨服需求成立时做并行试点。

## 8. 当前验证证据

- `Server/ElementWarServer.csproj`：构建成功，0 警告、0 错误；
- `dotnet ... ElementWarServer.dll --self-test`：`[SelfTest] 5/5 passed`；
- Unity 生成的 `Assembly-CSharp*.csproj`：静态构建失败，原因是仍引用已不存在的 Low Poly FPS Pack 脚本。这证明工程文件已过期，不等同于 Unity Editor 当前编译失败；需要由 Unity 重新生成后再复核；
- Unity Play Mode、双客户端画面、Animator、场景和 Inspector：本轮未运行，仍待用户验证。

## 9. 下一步唯一目标

在导入任何框架或继续扩展玩法前，先完成“可重复验证的 PVP 1v1 基线”：

> 单客户端 Bot 不干扰画面；双客户端在受控延迟/丢包下移动、射击、受击和重生可用；F3 与服务器日志能解释校正、丢包和拒绝；测试结果可以重复。

达到这个目标后，再开始动作仲裁、配置单一来源和服务器碰撞三个小步改造。三个开源框架只作为设计参考或并行试点，不进入当前主工程依赖。
