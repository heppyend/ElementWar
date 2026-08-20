namespace ElementWar.Server;

// ============================================================
// 纯权威世界（零 IO、不依赖网络/Unity，可独立测试）。
// 移动模型为简化版：与 Unity 客户端 PvPMotor 数学一致。
// ============================================================

public sealed class GameWorldSettings
{
    public int ServerTickRate = 60;    // 08-21：30→60Hz（命中反馈/远端平滑 2x；命中/计分/滑铲均按 tick 数算，随 tick 率缩放）
    public float Gravity = -15f;
    public float WalkSpeed = 2.2f;
    public float JogSpeed = 5f;
    public float SprintSpeed = 8f;
    public float AimMoveSpeed = 2.5f;
    public float GroundY = 0.15f;              // 角色原点基本在脚底（CC 底≈原点），少量余量防穿模
    public float JumpVelocity = 6.7f;          // sqrt(2*15*1.5)
    public float SlideDurationSeconds = 0.8f;  // 滑铲（与客户端 PvPMotor 一致）
    public float SlideStartSpeed = 7f;
    public float SlideEndSpeed = 1.5f;
    public float SprintSlideBoost = 2f;
    public float RotationSpeedDeg = 300f;
    public int MaxHealth = 100;
    public int FireDamage = 25;
    public float FireCooldownSeconds = 0.15f;   // 与客户端视觉射速一致（PVE bulletInterval=0.15s），否则视觉 6.67 发/秒只有 1/5 结算
    public float RespawnSeconds = 3f;
    public float FireRange = 35f;
    public float HitRadius = 1.2f;
    public float HitHeight = 1.8f;             // 胶囊高度（近似角色身高）
    public float EyeHeight = 1.5f;             // 开火射线高度
    public int InputBufferSize = 64;
    public int FireBufferSize = 16;
    public int InputHoldTimeoutTicks = 6;      // 超过该 tick 无新输入则移动清零（防丢 key-up）
    public int MaxInputAheadTicks = 30;
    public int MaxInputLagTicks = 120;
    public int HistoryTickCount = 60;          // 保留 1s 历史帧供延迟补偿（60Hz 下 60 tick）
    public float MaxLagCompRewindSeconds = 0.35f;
    public int WinScore = 5;
    public float ArenaHalfExtent = 40f;        // 简单边界钳制（XZ），防跑出竞技场
    // ---- 训练 bot ----
    public bool EnableBots = false;            // 默认关闭（单人纯测试）；`--bots` 开训练模式补位
    public string BotName = "训练Bot";
    public float BotChaseRange = 14f;          // 与目标距离超过该值则追击
    public float BotBackRange = 6f;            // 距离过近则后撤
    public float BotAimErrorFactor = 0.06f;    // 瞄准误差 = 距离 * factor + base（米，防激光枪）
    public float BotAimErrorBase = 0.8f;
    public int BotDecisionTicksMin = 30;       // AI 决策间隔（60Hz tick）：0.5s
    public int BotDecisionTicksMax = 50;       // ~0.83s
}

/// <summary>延迟补偿用的历史帧。</summary>
public sealed class HistoryFrame
{
    public int ServerTick;
    public Vec3 Position;
    public bool IsAlive;
}

/// <summary>每个玩家一份权威状态。</summary>
public sealed class ServerPlayer
{
    public int PlayerId;
    public string Name = string.Empty;
    public int CharacterId;
    public Vec3 Position;
    public float BodyYawDeg;
    public Vec3 AimPoint;
    public float VerticalSpeed;
    public bool IsGrounded = true;
    public int Health;
    public int MaxHealth;
    public bool IsAlive = true;
    public int LifeStateVersion = 1;
    public int Score;
    public int RespawnServerTick;
    public int LastProcessedInputTick;
    public int LastQueuedFireSequence;
    public int NextAllowedFireServerTick;
    public float SpeedBlend;
    public int MoveState;                       // 0 idle 1 move 2 sprint 3 aim 4 hover
    public bool JumpQueued;                     // 跳跃锁存：任意一条输入触发后保留到起跳（防 latest-input 吞掉瞬发）
    public bool IsSliding;                      // 滑铲进行中（计时状态，不依赖后续输入）
    public int SlideTicksRemaining;
    public Vec3 SlideDirection;
    public bool SlideSprintBoost;               // 滑铲开始时是否冲刺（加成固定，不用中途输入）
    public float GroundY = 0.05f;               // 每角色脚底偏移（角色原点≠脚底，且角色间不同：荧0.025/芙宁娜0.15）
    public readonly SortedDictionary<int, PlayerInputMessage> PendingInputs = new();
    public readonly SortedDictionary<int, FireRequestMessage> PendingFires = new();
    public readonly List<HistoryFrame> History = new();
    public float NoInputTicks;                  // 距上次输入经过的 tick 数（key-up 超时）
    public PlayerInputMessage? LastInput;       // 最近一次输入（供 key-up 丢失时短窗口复用）
    public bool RecentlyHit;                    // 受击反馈（客户端闪红）
    public bool IsBot;                          // 训练 bot：服务器自驱 AI，无网络端点
    public float SpawnSide = -1f;               // 出生/重生边（±1；不用 id 取模，bot 负数 id 时 C# 取模为负会翻边）
    // bot AI 状态
    public int BotTargetId;
    public int BotNextDecisionTick;             // 下次决策（换侧移方向 / 瞄准误差）
    public int BotStrafeDir = 1;                // 侧移方向 ±1
    public Vec3 BotAimError = new(0f, 0f, 1f);  // 瞄准误差单位偏移（XZ，决策时刷新）
}

// ---- 可靠事件（逻辑层，wire eventId 由 UdpGameServer 分配）----
public sealed record DeathEvent(int PlayerId, int LifeStateVersion, int KillerPlayerId, float RespawnRemainingSeconds);
public sealed record RespawnEvent(int PlayerId, int LifeStateVersion, Vec3 Position, int Health, int MaxHealth);
public sealed record KillEvent(int KillerPlayerId, int VictimPlayerId, int KillerScore, int VictimScore);
public sealed record MatchEndEvent(int WinnerPlayerId, int[] FinalScores);

public sealed class GameWorld
{
    public readonly GameWorldSettings Settings;
    public int ServerTick;
    public bool MatchEnded;
    public readonly List<object> PendingReliableEvents = new(); // Death/Respawn/Kill/MatchEnd 逻辑事件（每 tick 清空）
    public readonly List<(int shooter, int target, Vec3 hit, int damage)> PendingHits = new(); // 命中广播（每 tick 清空）

    private readonly Dictionary<int, ServerPlayer> _players = new();
    private int _nextPlayerId = 1;      // 人类 id：始终 1、2、3…（bot 用独立负数 id，不占人类序号）
    private int _nextBotId = -1;        // bot id：-1、-2…（避免计分板/出生点被 bot 抢走 id 2）
    private static readonly Random BotRand = new(20260820);

    /// <summary>诊断输出回调（由宿主 UdpGameServer 接 Console），GameWorld 本身保持零 IO。</summary>
    public Action<string>? Log;

    public GameWorld(GameWorldSettings settings) => Settings = settings;

    public IReadOnlyDictionary<int, ServerPlayer> Players => _players;
    public int PlayerCount => _players.Count;
    /// <summary>人类（非 bot）玩家数：满员 / 补位判断都用它（bot 不占人类名额）。</summary>
    public int HumanCount => _players.Values.Count(p => !p.IsBot);

    /// <summary>注册客户端，返回新建的玩家（服务器分配 playerId 与出生点）。</summary>
    public ServerPlayer AddPlayer(string name, int characterId)
    {
        // 上一局已结束：新玩家加入即开新局（重置 MatchEnded + 全场分数清零）。
        // 否则第二局计分到 WinScore 时因 !MatchEnded 不成立，永远不发 MatchEndEvent，对局无法正常结束。
        if (MatchEnded)
        {
            MatchEnded = false;
            foreach (var x in _players.Values) x.Score = 0;
        }
        int id = _nextPlayerId++;
        var p = new ServerPlayer
        {
            PlayerId = id,
            Name = name,
            CharacterId = characterId,
            Health = Settings.MaxHealth,
            MaxHealth = Settings.MaxHealth,
        };
        // 出生点：玩家按 id 交替分到对称位置；Y 用该角色脚底偏移
        p.GroundY = GetGroundY(characterId);
        p.SpawnSide = (id % 2 == 0) ? 1f : -1f;
        float side = p.SpawnSide;
        p.Position = new Vec3(side * 0f, p.GroundY, side * 6f);
        p.BodyYawDeg = (side < 0f) ? 0f : 180f;
        p.AimPoint = p.Position;
        p.History.Add(new HistoryFrame { ServerTick = ServerTick, Position = p.Position, IsAlive = true });
        _players.Add(id, p);
        EnsureCombatantCount(); // 训练模式自动补位：1 人补 bot，2 人移除 bot
        return p;
    }

    public ServerPlayer? GetPlayer(int playerId) => _players.TryGetValue(playerId, out var p) ? p : null;

    /// <summary>各角色脚底偏移（CharacterController 中心 - 高度/2；与客户端 PvPMotor 计算一致）。</summary>
    private static float GetGroundY(int characterId) => characterId switch
    {
        1 => 0.15f,   // 芙宁娜
        _ => 0.025f,  // 荧
    };

    public void RemovePlayer(int playerId)
    {
        _players.Remove(playerId);
        if (HumanCount == 0)
        {
            // 全部人类离开：连带清空训练 bot，id 重置（下次开局从 1 开始）
            var bots = _players.Where(kv => kv.Value.IsBot).Select(kv => kv.Key).ToList();
            foreach (var id in bots) _players.Remove(id);
            _nextPlayerId = 1;
            _nextBotId = -1;
            return;
        }
        EnsureCombatantCount(); // 人类走了一个：若只剩 1 人则补 bot 续局
    }

    /// <summary>
    /// 训练模式自动补位：总战斗人数恒为 2。
    /// 1 个人类 → 补 1 个 bot（角色取唯一人类的相反角）；第 2 个人类加入 → 移除 bot（纯 1v1）。
    /// </summary>
    private void EnsureCombatantCount()
    {
        int humans = HumanCount;
        var bots = _players.Values.Where(p => p.IsBot).ToList();

        if (!Settings.EnableBots)
        {
            // 训练模式关闭：移除现有 bot，不再补位（单人纯测试，不被 bot 秒杀干扰）
            foreach (var b in bots) _players.Remove(b.PlayerId);
            return;
        }

        if (humans >= 2)
        {
            foreach (var b in bots) _players.Remove(b.PlayerId);
            if (bots.Count > 0) Log?.Invoke($"[Bot] 第二个人类加入，移除 {bots.Count} 个 bot（纯 1v1）");
            return;
        }
        if (humans == 1)
        {
            if (bots.Count < 1)
            {
                AddBot();
            }
            else if (bots.Count > 1)
            {
                foreach (var b in bots.Skip(1)) _players.Remove(b.PlayerId);
            }
        }
    }

    /// <summary>生成一个训练 bot：无网络端点，AI 由 SimulateBot 自驱，走与人类相同的生命/计分/重生流程。</summary>
    private void AddBot()
    {
        var human = _players.Values.FirstOrDefault(p => !p.IsBot);
        int humanCharId = human?.CharacterId ?? 0;
        int botCharId = (humanCharId + 1) % 2;   // 取唯一人类的相反角色（0 荧 ↔ 1 芙宁娜）

        int id = _nextBotId--;                   // 负数 id：不占人类序号（人类恒 1、2…）
        var b = new ServerPlayer
        {
            PlayerId = id,
            Name = Settings.BotName,
            CharacterId = botCharId,
            IsBot = true,
            Health = Settings.MaxHealth,
            MaxHealth = Settings.MaxHealth,
        };
        b.GroundY = GetGroundY(botCharId);
        // 出生在人类对面（人类 -Z 则 bot +Z），与人类初始对位
        b.SpawnSide = (human != null && human.Position.Z < 0f) ? 1f : -1f;
        float side = b.SpawnSide;
        b.Position = new Vec3(side * 0f, b.GroundY, side * 6f);
        b.BodyYawDeg = (side < 0f) ? 0f : 180f;
        b.AimPoint = b.Position;
        b.History.Add(new HistoryFrame { ServerTick = ServerTick, Position = b.Position, IsAlive = true });
        _players.Add(id, b);
        Log?.Invoke($"[Bot] 训练 bot 加入: id={id} char={botCharId} 对位人类 char={humanCharId}");
    }

    // ---------------- 输入门 ----------------

    /// <summary>尝试入队一条输入（返回是否接受）。</summary>
    public bool TryQueueInput(int playerId, PlayerInputMessage msg)
    {
        if (!_players.TryGetValue(playerId, out var p)) return false;
        if (!p.IsAlive) return false;                       // 死亡不接受新移动输入
        if (!IsFinite(msg.WorldMoveX) || !IsFinite(msg.WorldMoveY) || !IsFinite(msg.WorldMoveZ)) return false;
        if (!IsFinite(msg.AimX) || !IsFinite(msg.AimY) || !IsFinite(msg.AimZ)) return false;
        if (msg.InputTick <= p.LastProcessedInputTick) return false;      // 太旧/重复
        if (msg.InputTick > ServerTick + Settings.MaxInputAheadTicks) return false;
        if (msg.InputTick < ServerTick - Settings.MaxInputLagTicks) return false;
        if (p.PendingInputs.Count >= Settings.InputBufferSize) return false;
        if (p.PendingInputs.ContainsKey(msg.InputTick)) return false;     // 重复 tick
        p.PendingInputs[msg.InputTick] = msg;
        return true;
    }

    /// <summary>尝试入队一条开火请求（返回是否接受）。</summary>
    public bool TryQueueFire(int playerId, FireRequestMessage msg)
    {
        if (!_players.TryGetValue(playerId, out var p)) return false;
        if (!p.IsAlive) return false;
        if (!IsFinite(msg.AimX) || !IsFinite(msg.AimY) || !IsFinite(msg.AimZ)) return false;
        if (msg.FireSequence <= p.LastQueuedFireSequence) return false;   // 重复/乱序
        if (p.PendingFires.Count >= Settings.FireBufferSize) return false;
        p.PendingFires[msg.FireSequence] = msg;
        return true;
    }

    // ---------------- Tick 模拟 ----------------

    public void StepFrame(float dt)
    {
        ServerTick++;
        PendingReliableEvents.Clear();
        PendingHits.Clear();

        // ① 重生
        foreach (var p in _players.Values)
        {
            if (!p.IsAlive && ServerTick >= p.RespawnServerTick)
                Respawn(p);
        }

        // ② 消费输入 + 移动模拟（bot 无网络输入，由 AI 自驱同一简化数学）
        foreach (var p in _players.Values)
        {
            if (p.IsAlive)
            {
                if (p.IsBot) SimulateBot(p, dt);
                else
                {
                    var input = ConsumeLatestInput(p);
                    SimulatePlayer(p, input, dt);
                }
            }
            StoreHistory(p);
        }

        // ③ 处理开火（人类按 FireRequest，bot 按 AI 决策）
        foreach (var p in _players.Values)
        {
            if (!p.IsAlive) continue;
            if (p.IsBot) ResolveBotFire(p);
            else ResolveFires(p);
        }
    }

    /// <summary>
    /// 消费输入：latest-input 语义（每 tick 取最新一条，更早丢弃）。
    /// 无新输入时在 key-up 丢失窗口内复用最近一次输入，超过窗口返回 null（停止移动）。
    /// </summary>
    private PlayerInputMessage? ConsumeLatestInput(ServerPlayer p)
    {
        if (p.PendingInputs.Count == 0)
        {
            p.NoInputTicks++;
            if (p.NoInputTicks <= Settings.InputHoldTimeoutTicks && p.LastInput != null)
            {
                return p.LastInput;   // 平滑单包丢失
            }
            return null;              // 超时：停住（key-up 已丢失）
        }

        // latest-input：只消费最新一条
        var kv = p.PendingInputs.Last();
        var input = kv.Value;
        p.PendingInputs.Clear();
        p.LastProcessedInputTick = input.InputTick;
        p.NoInputTicks = 0;
        p.LastInput = input;
        if (input.IsJumping) p.JumpQueued = true;   // 锁存跳跃（latest-input 语义下瞬发 true 会被后续输入覆盖）

        p.AimPoint = new Vec3(input.AimX, input.AimY, input.AimZ);
        p.BodyYawDeg = input.BodyYawDeg;
        return input;
    }

    private int SlideTotalTicks => (int)Math.Round(Settings.SlideDurationSeconds * Settings.ServerTickRate);

    private void SimulatePlayer(ServerPlayer p, PlayerInputMessage? input, float dt)
    {
        // 跳跃：独立于当前输入帧处理（JumpQueued 跨 tick 保留，即使本 tick 无新输入也起跳）。
        // 跳跃可中断滑铲。
        if (p.IsGrounded && p.JumpQueued)
        {
            p.VerticalSpeed = Settings.JumpVelocity;
            p.IsGrounded = false;
            p.JumpQueued = false;
            p.IsSliding = false;
            p.SlideTicksRemaining = 0;
        }

        // 滑铲：计时状态，优先于正常移动（不依赖后续输入，无输入也滑完）
        if (p.IsSliding)
        {
            StepSlide(p, dt);
            return;
        }

        if (input is null)
        {
            // 无新输入且超过 key-up 窗口：权威静止（等客户端重发/校正）
            if (p.IsGrounded)
            {
                p.SpeedBlend = 0f;
                p.MoveState = 0;
            }
            return;
        }

        // 滑铲触发：地面 + 本帧 isSlide（客户端 isSlide 只在一个 30Hz 帧为 true，发完即清锁存）
        if (p.IsGrounded && input.IsSlide)
        {
            p.IsSliding = true;
            p.SlideTicksRemaining = SlideTotalTicks;
            Vec3 slideDir = new Vec3(input.WorldMoveX, 0f, input.WorldMoveZ).NormalizedXZ();
            if (slideDir.Magnitude <= 0.01f)
            {
                // 无移动输入：沿用当前朝向
                float yawRad = p.BodyYawDeg * MathF.PI / 180f;
                slideDir = new Vec3(MathF.Sin(yawRad), 0f, MathF.Cos(yawRad));
            }
            p.SlideDirection = slideDir;
            p.SlideSprintBoost = input.IsSprint;
            StepSlide(p, dt);
            return;
        }

        // 移动速度选择
        Vec3 inputDir = new Vec3(input.WorldMoveX, 0f, input.WorldMoveZ).NormalizedXZ();
        bool moving = inputDir.Magnitude > 0.01f;
        float speed = 0f;
        if (moving)
        {
            speed = input.IsSprint ? Settings.SprintSpeed
                  : input.IsAiming ? Settings.AimMoveSpeed
                  : Settings.JogSpeed;
        }

        // 位移方向：瞄准沿输入（侧移）；非瞄准沿当前朝向（与客户端 PvPMotor 一致——
        // 换向走弧线、面朝=移动方向，否则换向/反方向时角色倒车/侧跑 = 对方视角"乱跑"）
        Vec3 moveDir;
        if (input.IsAiming)
            moveDir = inputDir;
        else
        {
            float yawRad = p.BodyYawDeg * MathF.PI / 180f;
            moveDir = new Vec3(MathF.Sin(yawRad), 0f, MathF.Cos(yawRad));
        }

        // 水平位移
        Vec3 delta = moveDir * (speed * dt);
        Vec3 newPos = p.Position + delta;

        // 垂直：重力（跳跃已在函数开头按 JumpQueued 处理）
        if (!p.IsGrounded)
        {
            p.VerticalSpeed += Settings.Gravity * dt;
            newPos = new Vec3(newPos.X, p.Position.Y + p.VerticalSpeed * dt, newPos.Z);
        }

        // 地面钳制（简化地面；Y 是该角色脚底偏移，脚才贴地）
        if (newPos.Y <= p.GroundY)
        {
            newPos = new Vec3(newPos.X, p.GroundY, newPos.Z);
            p.VerticalSpeed = 0f;
            p.IsGrounded = true;
        }

        // 竞技场边界钳制（XZ）
        newPos = new Vec3(
            Math.Clamp(newPos.X, -Settings.ArenaHalfExtent, Settings.ArenaHalfExtent),
            newPos.Y,
            Math.Clamp(newPos.Z, -Settings.ArenaHalfExtent, Settings.ArenaHalfExtent));

        p.Position = newPos;
        p.IsGrounded = newPos.Y <= p.GroundY + 0.001f;

        // 动画状态（供远端 Avatar 驱动 Animator）
        if (!p.IsGrounded)
        {
            p.MoveState = 4; // hover（保留原 speedBlend 让空中动画连贯）
        }
        else if (moving)
        {
            p.MoveState = input.IsSprint ? 2 : input.IsAiming ? 3 : 1;
            p.SpeedBlend = input.IsSprint ? 1f : input.IsAiming ? 0.5f : 0.66f;
        }
        else if (input.IsAiming)
        {
            // 瞄准待机：发瞄准态（否则对方看到瞄准者播 Idle 待机姿势 = 枪放下，姿态对不上）
            p.MoveState = 3;
            p.SpeedBlend = 0f;
        }
        else
        {
            p.MoveState = 0;
            p.SpeedBlend = 0f;
        }
    }

    /// <summary>滑铲推进：方向锁定、速度衰减、强制贴地（与客户端 PvPMotor 数学一致）。</summary>
    private void StepSlide(ServerPlayer p, float dt)
    {
        float total = SlideTotalTicks;
        float t = total > 0 ? p.SlideTicksRemaining / total : 0f;   // 1→0
        float speed = Lerp(Settings.SlideEndSpeed, Settings.SlideStartSpeed, t);
        if (p.SlideSprintBoost) speed += Settings.SprintSlideBoost * t;

        Vec3 delta = p.SlideDirection * (speed * dt);
        Vec3 newPos = new Vec3(
            Math.Clamp(p.Position.X + delta.X, -Settings.ArenaHalfExtent, Settings.ArenaHalfExtent),
            p.GroundY,
            Math.Clamp(p.Position.Z + delta.Z, -Settings.ArenaHalfExtent, Settings.ArenaHalfExtent));

        p.Position = newPos;
        p.VerticalSpeed = 0f;
        p.IsGrounded = true;
        p.SlideTicksRemaining--;
        if (p.SlideTicksRemaining <= 0) p.IsSliding = false;

        p.MoveState = 5;   // slide（客户端 RemoteAvatar 播 RunningSlide）
        p.SpeedBlend = 0.66f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // ---------------- 训练 bot AI ----------------

    /// <summary>bot 自驱移动：追击 / 后撤 / 侧移走位 + 面向目标。合成输入喂 SimulatePlayer（复用同一简化数学）。</summary>
    private void SimulateBot(ServerPlayer p, float dt)
    {
        var target = GetBotTarget(p);
        if (target is null)
        {
            // 无目标（全体死亡）：原地待命
            SimulatePlayer(p, new PlayerInputMessage { IsAiming = false, IsSprint = false }, dt);
            return;
        }

        // 周期决策：换侧移方向 + 刷新瞄准误差
        if (ServerTick >= p.BotNextDecisionTick)
        {
            p.BotNextDecisionTick = ServerTick + BotRand.Next(Settings.BotDecisionTicksMin, Settings.BotDecisionTicksMax + 1);
            p.BotStrafeDir = BotRand.Next(2) == 0 ? 1 : -1;
            p.BotAimError = RandomUnitVectorXZ();
        }

        Vec3 toTarget = target.Position - p.Position;
        float dist = toTarget.Magnitude;
        Vec3 toDir = dist > 1e-4f ? toTarget * (1f / dist) : new Vec3(0f, 0f, 1f);

        // 移动模式：远→追击（冲刺），过近→后撤，中距→侧移走位（瞄准移动）
        Vec3 move = Vec3.Zero;
        bool sprint = false, aiming = false;
        if (dist > Settings.BotChaseRange)
        {
            move = toDir;
            sprint = true;
        }
        else if (dist < Settings.BotBackRange)
        {
            move = toDir * -1f;   // 后撤
        }
        else
        {
            Vec3 right = new(toDir.Z, 0f, -toDir.X);   // 绕 Y 右向量
            move = right * p.BotStrafeDir;
            aiming = true;
        }

        p.BodyYawDeg = Vec3.ToYawDeg(toDir);   // 面向目标（SimulatePlayer 不设 yaw，bot 自行设）
        var input = new PlayerInputMessage
        {
            WorldMoveX = move.X,
            WorldMoveZ = move.Z,
            IsSprint = sprint,
            IsAiming = aiming,
            BodyYawDeg = p.BodyYawDeg,
        };
        SimulatePlayer(p, input, dt);
    }

    /// <summary>bot 目标：最近存活的人类（训练）；无人类则最近存活其他玩家。</summary>
    private ServerPlayer? GetBotTarget(ServerPlayer bot)
    {
        ServerPlayer? human = null;
        float hd = float.MaxValue;
        ServerPlayer? other = null;
        float od = float.MaxValue;
        foreach (var p in _players.Values)
        {
            if (p.PlayerId == bot.PlayerId || !p.IsAlive) continue;
            float d = Vec3.Distance(bot.Position, p.Position);
            if (!p.IsBot && d < hd) { hd = d; human = p; }
            if (d < od) { od = d; other = p; }
        }
        return human ?? other;
    }

    private static Vec3 RandomUnitVectorXZ()
    {
        double ang = BotRand.NextDouble() * Math.PI * 2.0;
        return new Vec3((float)Math.Cos(ang), 0f, (float)Math.Sin(ang));
    }

    /// <summary>bot 开火：射程 + 冷却 + 瞄准（带误差与朝向校验），复用与人类同一射线命中。</summary>
    private void ResolveBotFire(ServerPlayer p)
    {
        var target = GetBotTarget(p);
        if (target is null) return;

        Vec3 toTarget = target.Position - p.Position;
        float dist = toTarget.Magnitude;
        if (dist > Settings.FireRange) return;
        if (ServerTick < p.NextAllowedFireServerTick) return;

        // 瞄准点：目标脚底 + 眼睛高度 + 瞄准误差（随距离放大，防激光枪）
        Vec3 targetFeet = new(target.Position.X, target.Position.Y - target.GroundY, target.Position.Z);
        Vec3 aimBase = targetFeet + new Vec3(0f, Settings.EyeHeight, 0f);
        float errRadius = dist * Settings.BotAimErrorFactor + Settings.BotAimErrorBase;
        aimBase += p.BotAimError * errRadius;

        Vec3 origin = new(p.Position.X, p.Position.Y + (Settings.EyeHeight - p.GroundY), p.Position.Z);
        Vec3 aimDir = (aimBase - origin).Normalized();

        // 大致面向目标才开枪（防止后撤/转向时乱扫）
        Vec3 toDir = dist > 1e-4f ? toTarget * (1f / dist) : new Vec3(0f, 0f, 1f);
        float facing = aimDir.X * toDir.X + aimDir.Y * toDir.Y + aimDir.Z * toDir.Z;
        if (facing < 0.85f) return;

        p.NextAllowedFireServerTick = ServerTick + (int)Math.Round(Settings.FireCooldownSeconds * Settings.ServerTickRate);
        p.AimPoint = aimBase;
        ResolveShot(p, origin, aimDir, 0f);
    }

    private void StoreHistory(ServerPlayer p)
    {
        p.History.Add(new HistoryFrame { ServerTick = ServerTick, Position = p.Position, IsAlive = p.IsAlive });
        while (p.History.Count > Settings.HistoryTickCount)
            p.History.RemoveAt(0);
    }

    // ---------------- 开火 / 命中 ----------------

    private void ResolveFires(ServerPlayer p)
    {
        if (p.PendingFires.Count == 0) return;
        if (ServerTick < p.NextAllowedFireServerTick) return; // 冷却中

        // 取最新一次开火请求
        var fire = p.PendingFires.Last().Value;
        p.PendingFires.Clear();
        p.LastQueuedFireSequence = fire.FireSequence;
        p.NextAllowedFireServerTick = ServerTick + (int)Math.Round(Settings.FireCooldownSeconds * Settings.ServerTickRate);

        // 射线（视线从脚底 +EyeHeight；原点在该角色 GroundY 之上，故减去）
        Vec3 origin = new(p.Position.X, p.Position.Y + (Settings.EyeHeight - p.GroundY), p.Position.Z);
        Vec3 aimDir = new Vec3(fire.AimX, fire.AimY, fire.AimZ) - origin;
        aimDir = aimDir.Normalized();

        // 延迟补偿：回到开火时刻的目标位置
        float rewind = Math.Min(
            fire.EstimatedRttSeconds + fire.InterpolationDelaySeconds,
            Settings.MaxLagCompRewindSeconds);

        ResolveShot(p, origin, aimDir, rewind);
    }

    /// <summary>共享命中结算：射线 vs 全体其他玩家（历史帧胶囊，延迟补偿）+ 伤害。人类与 bot 共用。</summary>
    private void ResolveShot(ServerPlayer p, Vec3 origin, Vec3 aimDir, float rewindSeconds)
    {
        int hitTestTick = ServerTick - (int)Math.Round(rewindSeconds * Settings.ServerTickRate);

        // 找最近命中
        int? bestTarget = null;
        float bestDist = Settings.FireRange;
        Vec3 bestHit = origin;

        foreach (var other in _players.Values)
        {
            if (other.PlayerId == p.PlayerId || !other.IsAlive) continue;
            Vec3 targetPos = GetHistoryPosition(other, hitTestTick, rewindSeconds);
            // 命中胶囊从脚底算起：历史位置 Y 含该角色 GroundY，减回脚底
            Vec3 feetPos = new(targetPos.X, targetPos.Y - other.GroundY, targetPos.Z);
            if (CapsuleRayHit(feetPos, Settings.HitRadius, Settings.HitHeight, origin, aimDir, out float t, out Vec3 hit))
            {
                if (t <= bestDist)
                {
                    bestDist = t;
                    bestTarget = other.PlayerId;
                    bestHit = hit;
                }
            }
        }

        if (bestTarget is null) return; // 未命中

        ApplyDamage(p, GetPlayer(bestTarget.Value)!, bestHit);
    }

    private Vec3 GetHistoryPosition(ServerPlayer target, int hitTestTick, float rewind)
    {
        if (rewind <= 0f) return target.Position;
        // 取 <= hitTestTick 的最近历史帧
        for (int i = target.History.Count - 1; i >= 0; i--)
        {
            if (target.History[i].ServerTick <= hitTestTick)
                return target.History[i].Position;
        }
        return target.History.Count > 0 ? target.History[0].Position : target.Position;
    }

    /// <summary>射线 vs 竖直胶囊（segment + 半径）。返回射线参数 t 与命中点。</summary>
    private static bool CapsuleRayHit(Vec3 basePos, float radius, float height, Vec3 origin, Vec3 dir, out float t, out Vec3 hit)
    {
        t = 0f; hit = origin;
        // 胶囊 = 线段 [base, base + up*height] 膨胀 radius
        Vec3 a = basePos;
        Vec3 b = new(basePos.X, basePos.Y + height, basePos.Z);
        if (RaySegmentDistance(origin, dir, a, b, out float distAlongRay, out float segDist) <= radius
            && distAlongRay <= 35f)
        {
            t = distAlongRay;
            hit = origin + dir * distAlongRay;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 射线(origin, 单位向量 dir, t≥0) 与线段 ab(s∈[0,1]) 的最近距离。
    /// 标准两线段最近点算法：射线视为半无限线段。
    /// </summary>
    private static float RaySegmentDistance(Vec3 origin, Vec3 dir, Vec3 a, Vec3 b, out float rayT, out float segDist)
    {
        Vec3 v = b - a;                    // 线段方向
        Vec3 w0 = origin - a;
        float uu = 1f;                     // dir·dir（dir 已单位化）
        float uv = dir.X * v.X + dir.Y * v.Y + dir.Z * v.Z;
        float vv = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        float uw = dir.X * w0.X + dir.Y * w0.Y + dir.Z * w0.Z;
        float vw = v.X * w0.X + v.Y * w0.Y + v.Z * w0.Z;

        float det = uu * vv - uv * uv;
        float s, t;
        if (MathF.Abs(det) < 1e-6f)
        {
            // 平行：取线段中点投影
            t = uw;
            s = 0.5f;
        }
        else
        {
            t = (uv * vw - vv * uw) / det;
            s = (uu * vw - uv * uw) / det;
            // 钳制 s 到 [0,1]，再按端点重新投影射线参数 t
            if (s < 0f) { s = 0f; t = uw; }
            else if (s > 1f) { s = 1f; t = uw + uv; }
        }
        if (t < 0f) t = 0f;                // 射线不向后

        Vec3 rayPoint = origin + dir * t;
        Vec3 segPoint = a + v * s;
        segDist = Vec3.Distance(rayPoint, segPoint);
        rayT = t;
        return segDist;
    }

    private void ApplyDamage(ServerPlayer shooter, ServerPlayer target, Vec3 hit)
    {
        target.Health -= Settings.FireDamage;
        target.RecentlyHit = true;
        PendingHits.Add((shooter.PlayerId, target.PlayerId, hit, Settings.FireDamage));
        Console.WriteLine($"[闭环] 命中: P{shooter.PlayerId} → P{target.PlayerId} hp={Math.Max(0, target.Health)}");
        if (target.Health <= 0)
        {
            target.Health = 0;
            Die(shooter, target);
        }
        else
        {
            // 非致命：HealthChanged 走不可靠广播（每 tick 由服务器统一发）
            // PendingReliableEvents 只放生命周期事件
        }
    }

    private void Die(ServerPlayer shooter, ServerPlayer victim)
    {
        victim.IsAlive = false;
        victim.LifeStateVersion++;
        victim.RespawnServerTick = ServerTick + (int)Math.Round(Settings.RespawnSeconds * Settings.ServerTickRate);
        shooter.Score++;
        victim.PendingInputs.Clear();
        victim.PendingFires.Clear();

        PendingReliableEvents.Add(new DeathEvent(victim.PlayerId, victim.LifeStateVersion, shooter.PlayerId, Settings.RespawnSeconds));
        PendingReliableEvents.Add(new KillEvent(shooter.PlayerId, victim.PlayerId, shooter.Score, victim.Score));
        Console.WriteLine($"[闭环] 击杀: P{shooter.PlayerId}(score={shooter.Score}) 击杀 P{victim.PlayerId}");

        // 计分到达 → 对局结束
        if (shooter.Score >= Settings.WinScore && !MatchEnded)
        {
            MatchEnded = true;
            int[] scores = _players.Values.OrderBy(x => x.PlayerId).Select(x => x.Score).ToArray();
            PendingReliableEvents.Add(new MatchEndEvent(shooter.PlayerId, scores));
            Console.WriteLine($"[闭环] 对局结束: 胜者 P{shooter.PlayerId} 比分={string.Join(":", scores)}");
        }
    }

    private void Respawn(ServerPlayer p)
    {
        float side = p.SpawnSide;
        p.Position = new Vec3(0f, p.GroundY, side * 6f);
        p.BodyYawDeg = (side < 0f) ? 0f : 180f;
        p.AimPoint = p.Position;
        p.VerticalSpeed = 0f;
        p.IsGrounded = true;
        p.Health = Settings.MaxHealth;
        p.MaxHealth = Settings.MaxHealth;
        p.IsAlive = true;
        p.LifeStateVersion++;
        p.RecentlyHit = false;
        p.IsSliding = false;           // 重生复位：滑铲/跳跃锁存是瞬发状态，不能残留到下一命（客户端 PvPMotor.Teleport 同步复位）
        p.SlideTicksRemaining = 0;
        p.JumpQueued = false;
        PendingReliableEvents.Add(new RespawnEvent(p.PlayerId, p.LifeStateVersion, p.Position, p.Health, p.MaxHealth));
    }

    private static bool IsFinite(float v) => float.IsFinite(v);
}
