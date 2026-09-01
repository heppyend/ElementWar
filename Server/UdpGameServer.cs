using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ElementWar.Server;

public sealed class UdpGameServerOptions
{
    public int ListenPort = 7777;
    public int TickRate = 60;   // 与 GameWorldSettings.ServerTickRate 一致（Program.cs --tickrate 默认 60）
    public GameWorldSettings WorldSettings = new();
    public double ClientTimeoutSeconds = 8;
    public bool LogTelemetry = true;
}

/// <summary>一个已连接的客户端。</summary>
public sealed class ClientConnection
{
    public int PlayerId;
    public IPEndPoint Endpoint = null!;
    public long LastReceiveUnixMs;
    public uint SnapshotSequence;
    public readonly ReliableEventLedger Ledger = new();
    public bool WelcomeSent;
    public int RejectedInputs;
    public int RejectedFires;
}

/// <summary>
/// UDP 游戏服务器：接收路由 + 固定权威 Tick + 每客户端快照 + 可靠事件重发。
/// 世界/注册表修改都在 stateLock 内；锁内算好待发送内容，锁外发送。
/// </summary>
public sealed class UdpGameServer : IDisposable
{
    private readonly UdpGameServerOptions _options;
    private readonly UdpClient _udp;
    private readonly GameWorld _world;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, ClientConnection> _clients = new(); // endpointKey → conn
    private readonly Dictionary<int, string> _playerToEndpointKey = new();  // playerId → endpointKey
    private readonly List<(IPEndPoint ep, string json)> _pendingSends = new();
    private long _nextEventId = 1;
    private bool _disposed;

    public GameWorld World => _world;
    public int ClientCount => _clients.Count;

    public UdpGameServer(UdpGameServerOptions options)
    {
        _options = options;
        _world = new GameWorld(options.WorldSettings);
        _world.Log = Console.WriteLine; // GameWorld 零 IO，诊断输出交给宿主
        _udp = new UdpClient(options.ListenPort);
        Console.WriteLine($"[ElementWarServer] listening on 0.0.0.0:{options.ListenPort} @ {options.TickRate}Hz");
    }

    public async Task RunAsync(CancellationToken token)
    {
        var receiveTask = Task.Run(() => ReceiveLoop(token), token);
        try
        {
            await TickLoop(token);
        }
        finally
        {
            receiveTask.Wait(TimeSpan.FromSeconds(1));
        }
    }

    // ---------------- 接收 ----------------

    private void ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _udp.Receive(ref remote);
                HandleDatagram(remote, data);
            }
            catch (SocketException) { /* 端口关闭/超时忽略 */ }
            catch (ObjectDisposedException) { break; }
        }
    }

    private void HandleDatagram(IPEndPoint from, byte[] data)
    {
        string json;
        try { json = Encoding.UTF8.GetString(data); }
        catch { return; }

        string type = ReadType(json);
        lock (_stateLock)
        {
            switch (type)
            {
                case "hello": HandleHello(from, json); break;
                case "input": HandleInput(from, json); break;
                case "inputBatch": HandleInputBatch(from, json); break;
                case "fire": HandleFire(from, json); break;
                case "ping": HandlePing(from, json); break;
                case "ack": HandleAck(from, json); break;
                case "goodbye": HandleGoodbye(from); break;
                default: break;
            }
            string key = from.ToString();
            if (_clients.TryGetValue(key, out var c))
                c.LastReceiveUnixMs = NowUnixMs();
        }
    }

    private void HandleHello(IPEndPoint from, string json)
    {
        var msg = JsonSerializer.Deserialize<ClientHelloMessage>(json);
        if (msg is null) return;

        string key = from.ToString();
        if (_clients.TryGetValue(key, out var existing))
        {
            // 重复 Hello：回 Welcome
            SendWelcome(existing, key);
            return;
        }

        if (_world.HumanCount >= 2)
        {
            Send(from, JsonSerializer.Serialize(new { type = "welcome_full", message = "房间已满（最多 2 人）" }));
            return;
        }

        var player = _world.AddPlayer(msg.PlayerName, msg.CharacterId);
        var conn = new ClientConnection
        {
            PlayerId = player.PlayerId,
            Endpoint = from,
            LastReceiveUnixMs = NowUnixMs(),
        };
        _clients[key] = conn;
        _playerToEndpointKey[player.PlayerId] = key;
        SendWelcome(conn, key);
        Console.WriteLine($"[{NowHHmmss()}] 玩家加入: id={player.PlayerId} name={msg.PlayerName} char={msg.CharacterId} (在线 {_clients.Count}/2)");
    }

    private void SendWelcome(ClientConnection conn, string key)
    {
        conn.WelcomeSent = true;
        var welcome = new ServerWelcomeMessage
        {
            Type = "welcome",
            PlayerId = conn.PlayerId,
            ServerTickRate = _options.TickRate,
            WinScore = _options.WorldSettings.WinScore,
            ServerTick = _world.ServerTick,
        };
        Send(conn.Endpoint, Serialize(welcome));
    }

    private void HandleInput(IPEndPoint from, string json)
    {
        var key = from.ToString();
        if (!_clients.TryGetValue(key, out var conn)) return;
        var msg = JsonSerializer.Deserialize<PlayerInputMessage>(json);
        if (msg is null) return;
        if (msg.PlayerId != conn.PlayerId) return; // 端点与 playerId 必须一致
        if (!_world.TryQueueInput(conn.PlayerId, msg)) conn.RejectedInputs++;
    }

    private void HandleInputBatch(IPEndPoint from, string json)
    {
        var key = from.ToString();
        if (!_clients.TryGetValue(key, out var conn)) return;
        var batch = JsonSerializer.Deserialize<PlayerInputBatchMessage>(json);
        if (batch is null || batch.PlayerId != conn.PlayerId || batch.Inputs.Length > 4) return;
        foreach (var input in batch.Inputs)
        {
            if (input.PlayerId != conn.PlayerId) continue;
            // 批内包含最近命令的冗余副本，重复/已确认命令被输入门静默丢弃，不计为异常拒绝。
            _world.TryQueueInput(conn.PlayerId, input);
        }
    }

    private void HandleFire(IPEndPoint from, string json)
    {
        var key = from.ToString();
        if (!_clients.TryGetValue(key, out var conn)) return;
        var msg = JsonSerializer.Deserialize<FireRequestMessage>(json);
        if (msg is null) return;
        if (msg.PlayerId != conn.PlayerId) return;

        bool accepted = _world.TryQueueFire(conn.PlayerId, msg);
        if (!accepted) conn.RejectedFires++;
        var receipt = new FireReceiptMessage
        {
            Type = "fireReceipt",
            PlayerId = conn.PlayerId,
            FireSequence = msg.FireSequence,
            Accepted = accepted,
            Reason = accepted ? "ok" : "rejected",
            ServerTick = _world.ServerTick,
        };
        Send(conn.Endpoint, Serialize(receipt));
    }

    private void HandlePing(IPEndPoint from, string json)
    {
        var key = from.ToString();
        if (!_clients.TryGetValue(key, out var conn)) return;
        var ping = JsonSerializer.Deserialize<PingMessage>(json);
        if (ping is null) return;
        Send(conn.Endpoint, Serialize(new PongMessage
        {
            Type = "pong",
            Sequence = ping.Sequence,
            ClientTimeSeconds = ping.ClientTimeSeconds,
        }));
    }

    private void HandleAck(IPEndPoint from, string json)
    {
        var key = from.ToString();
        if (!_clients.TryGetValue(key, out var conn)) return;
        var msg = JsonSerializer.Deserialize<EventAckMessage>(json);
        if (msg is null) return;
        conn.Ledger.Acknowledge(msg.EventId);
    }

    private void HandleGoodbye(IPEndPoint from)
    {
        string key = from.ToString();
        if (!_clients.TryGetValue(key, out var conn)) return;
        RemoveClient(conn, key, "客户端主动离开");
    }

    private void RemoveClient(ClientConnection conn, string key, string reason)
    {
        _world.RemovePlayer(conn.PlayerId);
        _clients.Remove(key);
        _playerToEndpointKey.Remove(conn.PlayerId);
        Console.WriteLine($"[{NowHHmmss()}] 玩家离开: id={conn.PlayerId} ({reason}) 在线 {_clients.Count}/2");
    }

    // ---------------- Tick ----------------

    private async Task TickLoop(CancellationToken token)
    {
        float dt = 1f / _options.TickRate;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(dt));
        while (await timer.WaitForNextTickAsync(token))
        {
            _pendingSends.Clear();
            lock (_stateLock)
            {
                _world.StepFrame(dt);

                foreach (var conn in _clients.Values)
                    BuildSnapshotFor(conn);

                BuildReliableEventsForAll();
                BuildReliableResendsForAll();
                BuildShotBroadcast();
                BuildHitBroadcast();

                CleanupTimeouts();
                Telemetry();
            }
            // 锁外发送
            foreach (var (ep, j) in _pendingSends)
            {
                try { Send(ep, j); }
                catch (SocketException) { }
            }
        }
    }

    private void BuildSnapshotFor(ClientConnection conn)
    {
        var players = SnapshotBuilder.Capture(_world);
        var replicated = ClientReplicator.ComputeReplicatedIds(_world);
        var snap = new WorldSnapshotMessage
        {
            Type = "snapshot",
            ServerTick = _world.ServerTick,
            SnapshotSequence = ++conn.SnapshotSequence,
            Players = players,
            ReplicatedPlayerIds = replicated,
            IsFullState = true,
        };
        _pendingSends.Add((conn.Endpoint, Serialize(snap)));
    }

    /// <summary>把本 tick 世界产生的可靠事件入队到每个客户端，并立即发送（含 eventId）。</summary>
    private void BuildReliableEventsForAll()
    {
        if (_world.PendingReliableEvents.Count == 0) return;

        foreach (var conn in _clients.Values)
        {
            foreach (var ev in _world.PendingReliableEvents)
            {
                object wire = ToWireMessage(ev, out long eventId);
                string json = Serialize(wire);
                conn.Ledger.QueueInitial(wire, eventId, json);
                _pendingSends.Add((conn.Endpoint, json));
            }
        }
        _world.PendingReliableEvents.Clear();
    }

    private object ToWireMessage(object logical, out long eventId)
    {
        eventId = _nextEventId++;
        return logical switch
        {
            DeathEvent d => new DeathEventMessage
            {
                Type = "death", EventId = eventId, ServerTick = _world.ServerTick,
                PlayerId = d.PlayerId, LifeStateVersion = d.LifeStateVersion,
                KillerPlayerId = d.KillerPlayerId, RespawnRemainingSeconds = d.RespawnRemainingSeconds,
            },
            RespawnEvent r => new RespawnEventMessage
            {
                Type = "respawn", EventId = eventId, ServerTick = _world.ServerTick,
                PlayerId = r.PlayerId, LifeStateVersion = r.LifeStateVersion,
                X = r.Position.X, Y = r.Position.Y, Z = r.Position.Z,
                Health = r.Health, MaxHealth = r.MaxHealth,
            },
            KillEvent k => new KillEventMessage
            {
                Type = "kill", EventId = eventId, ServerTick = _world.ServerTick,
                KillerPlayerId = k.KillerPlayerId, VictimPlayerId = k.VictimPlayerId,
                KillerScore = k.KillerScore, VictimScore = k.VictimScore,
            },
            MatchEndEvent m => new MatchEndEventMessage
            {
                Type = "matchEnd", EventId = eventId, ServerTick = _world.ServerTick,
                WinnerPlayerId = m.WinnerPlayerId, FinalScores = m.FinalScores,
            },
            _ => throw new InvalidOperationException($"未知可靠事件 {logical.GetType().Name}"),
        };
    }

    private void BuildReliableResendsForAll()
    {
        foreach (var conn in _clients.Values)
        {
            foreach (var (json, eventId) in conn.Ledger.CollectDueResends(DateTime.UtcNow))
                _pendingSends.Add((conn.Endpoint, json));
        }
    }

    /// <summary>所有权威开火广播；远端客户端据此播放枪口、枪声和曳光。</summary>
    private void BuildShotBroadcast()
    {
        if (_world.PendingShots.Count == 0) return;
        foreach (var conn in _clients.Values)
        {
            foreach (var shot in _world.PendingShots)
            {
                var msg = new ShotEventMessage
                {
                    Type = "shot",
                    ServerTick = _world.ServerTick,
                    ShooterPlayerId = shot.shooter,
                    TargetPlayerId = shot.target,
                    OriginX = shot.origin.X,
                    OriginY = shot.origin.Y,
                    OriginZ = shot.origin.Z,
                    EndX = shot.end.X,
                    EndY = shot.end.Y,
                    EndZ = shot.end.Z,
                };
                _pendingSends.Add((conn.Endpoint, Serialize(msg)));
            }
        }
        _world.PendingShots.Clear();
    }

    /// <summary>命中事件（不可靠，广播给所有人做反馈）。</summary>
    private void BuildHitBroadcast()
    {
        if (_world.PendingHits.Count == 0) return;
        foreach (var conn in _clients.Values)
        {
            foreach (var h in _world.PendingHits)
            {
                var msg = new HitEventMessage
                {
                    Type = "hit", ServerTick = _world.ServerTick,
                    ShooterPlayerId = h.shooter, TargetPlayerId = h.target,
                    HitX = h.hit.X, HitY = h.hit.Y, HitZ = h.hit.Z, Damage = h.damage,
                };
                _pendingSends.Add((conn.Endpoint, Serialize(msg)));
            }
        }
        _world.PendingHits.Clear();
    }

    private void CleanupTimeouts()
    {
        long now = NowUnixMs();
        double timeoutMs = _options.ClientTimeoutSeconds * 1000;
        var stale = _clients.Where(kv => now - kv.Value.LastReceiveUnixMs > timeoutMs).ToList();
        foreach (var kv in stale)
            RemoveClient(kv.Value, kv.Key, "超时");
    }

    private void Telemetry()
    {
        if (!_options.LogTelemetry || _world.ServerTick % (_options.TickRate * 5) != 0) return; // 每 5s
        int rejectedInputs = _clients.Values.Sum(c => c.RejectedInputs);
        int rejectedFires = _clients.Values.Sum(c => c.RejectedFires);
        Console.WriteLine($"[{NowHHmmss()}] tick={_world.ServerTick} 在线={_clients.Count}/2 " +
                          $"rejectInput={rejectedInputs} rejectFire={rejectedFires} " +
                          $"players={string.Join(",", _world.Players.Keys)}");
    }

    // ---------------- 发送/序列化 ----------------

    private void Send(IPEndPoint ep, string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        _udp.Send(data, data.Length, ep);
    }

    private static string Serialize(object msg)
        => JsonSerializer.Serialize(msg);

    private static string ReadType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("type", out var t)) return t.GetString() ?? "";
        }
        catch { }
        return "";
    }

    private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static string NowHHmmss() => DateTime.Now.ToString("HH:mm:ss");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _udp.Dispose();
    }
}
