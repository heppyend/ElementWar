namespace ElementWar.Server;

// ============================================================
// 纯权威世界（零 IO、不依赖网络/Unity，可独立测试）。
// 移动模型为简化版：与 Unity 客户端 PvPMotor 数学一致。
// ============================================================

public sealed class GameWorldSettings
{
    public int ServerTickRate = 30;
    public float Gravity = -15f;
    public float WalkSpeed = 2.2f;
    public float JogSpeed = 5f;
    public float SprintSpeed = 8f;
    public float AimMoveSpeed = 2.5f;
    public float GroundY = 0.15f;              // 角色原点基本在脚底（CC 底≈原点），少量余量防穿模
    public float JumpVelocity = 6.7f;          // sqrt(2*15*1.5)
    public float RotationSpeedDeg = 300f;
    public int MaxHealth = 100;
    public int FireDamage = 25;
    public float FireCooldownSeconds = 0.75f;
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
    public int HistoryTickCount = 30;          // 保留 1s 历史帧供延迟补偿
    public float MaxLagCompRewindSeconds = 0.35f;
    public int WinScore = 5;
    public float ArenaHalfExtent = 40f;        // 简单边界钳制（XZ），防跑出竞技场
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
    public float GroundY = 0.05f;               // 每角色脚底偏移（角色原点≠脚底，且角色间不同：荧0.025/芙宁娜0.15）
    public readonly SortedDictionary<int, PlayerInputMessage> PendingInputs = new();
    public readonly SortedDictionary<int, FireRequestMessage> PendingFires = new();
    public readonly List<HistoryFrame> History = new();
    public float NoInputTicks;                  // 距上次输入经过的 tick 数（key-up 超时）
    public PlayerInputMessage? LastInput;       // 最近一次输入（供 key-up 丢失时短窗口复用）
    public bool RecentlyHit;                    // 受击反馈（客户端闪红）
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
    private int _nextPlayerId = 1;

    public GameWorld(GameWorldSettings settings) => Settings = settings;

    public IReadOnlyDictionary<int, ServerPlayer> Players => _players;
    public int PlayerCount => _players.Count;

    /// <summary>注册客户端，返回新建的玩家（服务器分配 playerId 与出生点）。</summary>
    public ServerPlayer AddPlayer(string name, int characterId)
    {
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
        float side = (id % 2 == 0) ? 1f : -1f;
        p.Position = new Vec3(side * 0f, p.GroundY, side * 6f);
        p.BodyYawDeg = (side < 0f) ? 0f : 180f;
        p.AimPoint = p.Position;
        p.History.Add(new HistoryFrame { ServerTick = ServerTick, Position = p.Position, IsAlive = true });
        _players.Add(id, p);
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
        if (_players.Count == 0) _nextPlayerId = 1; // 全部离开后重置，方便再开一局
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

        // ② 消费输入 + 移动模拟
        foreach (var p in _players.Values)
        {
            if (p.IsAlive)
            {
                var input = ConsumeLatestInput(p);
                SimulatePlayer(p, input, dt);
            }
            StoreHistory(p);
        }

        // ③ 处理开火
        foreach (var p in _players.Values)
        {
            if (p.IsAlive) ResolveFires(p);
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

    private void SimulatePlayer(ServerPlayer p, PlayerInputMessage? input, float dt)
    {
        // 跳跃：独立于当前输入帧处理（JumpQueued 跨 tick 保留，即使本 tick 无新输入也起跳）
        if (p.IsGrounded && p.JumpQueued)
        {
            p.VerticalSpeed = Settings.JumpVelocity;
            p.IsGrounded = false;
            p.JumpQueued = false;
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

        // 移动速度选择
        Vec3 moveDir = new Vec3(input.WorldMoveX, 0f, input.WorldMoveZ).NormalizedXZ();
        bool moving = moveDir.Magnitude > 0.01f;
        float speed = 0f;
        if (moving)
        {
            speed = input.IsSprint ? Settings.SprintSpeed
                  : input.IsAiming ? Settings.AimMoveSpeed
                  : Settings.JogSpeed;
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
        else
        {
            p.MoveState = 0;
            p.SpeedBlend = 0f;
        }
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
        int hitTestTick = ServerTick - (int)Math.Round(rewind * Settings.ServerTickRate);

        // 找最近命中
        int? bestTarget = null;
        float bestDist = Settings.FireRange;
        Vec3 bestHit = origin;

        foreach (var other in _players.Values)
        {
            if (other.PlayerId == p.PlayerId || !other.IsAlive) continue;
            Vec3 targetPos = GetHistoryPosition(other, hitTestTick, rewind);
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

        // 计分到达 → 对局结束
        if (shooter.Score >= Settings.WinScore && !MatchEnded)
        {
            MatchEnded = true;
            int[] scores = _players.Values.OrderBy(x => x.PlayerId).Select(x => x.Score).ToArray();
            PendingReliableEvents.Add(new MatchEndEvent(shooter.PlayerId, scores));
        }
    }

    private void Respawn(ServerPlayer p)
    {
        float side = (p.PlayerId % 2 == 0) ? 1f : -1f;
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
        PendingReliableEvents.Add(new RespawnEvent(p.PlayerId, p.LifeStateVersion, p.Position, p.Health, p.MaxHealth));
    }

    private static bool IsFinite(float v) => float.IsFinite(v);
}
