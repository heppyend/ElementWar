using System.Text.Json.Serialization;

namespace ElementWar.Server;

// ============================================================
// 网络消息 DTO（服务端）。⚠️ 与 Unity 客户端 Assets/Scripts/Network/Transport/NetMessages.cs
// 字段名严格一致（camelCase），两端必须同步改。
// ============================================================

public sealed class ClientHelloMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("playerName")] public string PlayerName { get; set; } = string.Empty;
    [JsonPropertyName("characterId")] public int CharacterId { get; set; }   // 0=荧 1=芙宁娜
}

public sealed class ServerWelcomeMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("serverTickRate")] public int ServerTickRate { get; set; }
    [JsonPropertyName("winScore")] public int WinScore { get; set; }
    // 当前服务器 tick：客户端据此对齐自己的 inputTick 基线，避免被判"太旧"拒绝
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
}

public sealed class PlayerInputMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("inputTick")] public int InputTick { get; set; }
    [JsonPropertyName("moveX")] public float MoveX { get; set; }
    [JsonPropertyName("moveY")] public float MoveY { get; set; }
    [JsonPropertyName("isSprint")] public bool IsSprint { get; set; }
    [JsonPropertyName("isAiming")] public bool IsAiming { get; set; }
    [JsonPropertyName("isFire")] public bool IsFire { get; set; }
    [JsonPropertyName("isJumping")] public bool IsJumping { get; set; }
    [JsonPropertyName("isSlide")] public bool IsSlide { get; set; }
    [JsonPropertyName("worldMoveX")] public float WorldMoveX { get; set; }
    [JsonPropertyName("worldMoveY")] public float WorldMoveY { get; set; }
    [JsonPropertyName("worldMoveZ")] public float WorldMoveZ { get; set; }
    [JsonPropertyName("bodyYawDeg")] public float BodyYawDeg { get; set; }
    [JsonPropertyName("aimX")] public float AimX { get; set; }
    [JsonPropertyName("aimY")] public float AimY { get; set; }
    [JsonPropertyName("aimZ")] public float AimZ { get; set; }
    [JsonPropertyName("estimatedRttSeconds")] public float EstimatedRttSeconds { get; set; }
    [JsonPropertyName("interpolationDelaySeconds")] public float InterpolationDelaySeconds { get; set; }
}

public sealed class FireRequestMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("fireSequence")] public int FireSequence { get; set; }
    [JsonPropertyName("requestTick")] public int RequestTick { get; set; }
    [JsonPropertyName("aimX")] public float AimX { get; set; }
    [JsonPropertyName("aimY")] public float AimY { get; set; }
    [JsonPropertyName("aimZ")] public float AimZ { get; set; }
    [JsonPropertyName("estimatedRttSeconds")] public float EstimatedRttSeconds { get; set; }
    [JsonPropertyName("interpolationDelaySeconds")] public float InterpolationDelaySeconds { get; set; }
}

public sealed class FireReceiptMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("fireSequence")] public int FireSequence { get; set; }
    [JsonPropertyName("accepted")] public bool Accepted { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
}

public sealed class WorldSnapshotMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("snapshotSequence")] public uint SnapshotSequence { get; set; }
    [JsonPropertyName("players")] public PlayerSnapshotMessage[] Players { get; set; } = Array.Empty<PlayerSnapshotMessage>();
    [JsonPropertyName("replicatedPlayerIds")] public int[] ReplicatedPlayerIds { get; set; } = Array.Empty<int>();
    [JsonPropertyName("isFullState")] public bool IsFullState { get; set; } = true;
}

public sealed class PlayerSnapshotMessage
{
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
    [JsonPropertyName("bodyYawDeg")] public float BodyYawDeg { get; set; }
    [JsonPropertyName("aimX")] public float AimX { get; set; }
    [JsonPropertyName("aimY")] public float AimY { get; set; }
    [JsonPropertyName("aimZ")] public float AimZ { get; set; }
    [JsonPropertyName("lastProcessedInputTick")] public int LastProcessedInputTick { get; set; }
    [JsonPropertyName("health")] public int Health { get; set; }
    [JsonPropertyName("maxHealth")] public int MaxHealth { get; set; }
    [JsonPropertyName("isAlive")] public bool IsAlive { get; set; }
    [JsonPropertyName("lifeStateVersion")] public int LifeStateVersion { get; set; }
    [JsonPropertyName("respawnRemainingSeconds")] public float RespawnRemainingSeconds { get; set; }
    [JsonPropertyName("characterId")] public int CharacterId { get; set; }
    [JsonPropertyName("moveState")] public int MoveState { get; set; }
    [JsonPropertyName("speedBlend")] public float SpeedBlend { get; set; }
    [JsonPropertyName("verticalSpeed")] public float VerticalSpeed { get; set; }
    [JsonPropertyName("isGrounded")] public bool IsGrounded { get; set; }
    [JsonPropertyName("score")] public int Score { get; set; }
}

public sealed class HitEventMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("shooterPlayerId")] public int ShooterPlayerId { get; set; }
    [JsonPropertyName("targetPlayerId")] public int TargetPlayerId { get; set; }
    [JsonPropertyName("hitX")] public float HitX { get; set; }
    [JsonPropertyName("hitY")] public float HitY { get; set; }
    [JsonPropertyName("hitZ")] public float HitZ { get; set; }
    [JsonPropertyName("damage")] public int Damage { get; set; }
}

public sealed class HealthChangedMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("health")] public int Health { get; set; }
    [JsonPropertyName("maxHealth")] public int MaxHealth { get; set; }
    [JsonPropertyName("isAlive")] public bool IsAlive { get; set; }
    [JsonPropertyName("respawnRemainingSeconds")] public float RespawnRemainingSeconds { get; set; }
}

public sealed class DeathEventMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("eventId")] public long EventId { get; set; }
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("lifeStateVersion")] public int LifeStateVersion { get; set; }
    [JsonPropertyName("killerPlayerId")] public int KillerPlayerId { get; set; }
    [JsonPropertyName("respawnRemainingSeconds")] public float RespawnRemainingSeconds { get; set; }
}

public sealed class RespawnEventMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("eventId")] public long EventId { get; set; }
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("lifeStateVersion")] public int LifeStateVersion { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
    [JsonPropertyName("health")] public int Health { get; set; }
    [JsonPropertyName("maxHealth")] public int MaxHealth { get; set; }
}

public sealed class KillEventMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("eventId")] public long EventId { get; set; }
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("killerPlayerId")] public int KillerPlayerId { get; set; }
    [JsonPropertyName("victimPlayerId")] public int VictimPlayerId { get; set; }
    [JsonPropertyName("killerScore")] public int KillerScore { get; set; }
    [JsonPropertyName("victimScore")] public int VictimScore { get; set; }
}

public sealed class MatchEndEventMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("eventId")] public long EventId { get; set; }
    [JsonPropertyName("serverTick")] public int ServerTick { get; set; }
    [JsonPropertyName("winnerPlayerId")] public int WinnerPlayerId { get; set; }
    [JsonPropertyName("finalScores")] public int[] FinalScores { get; set; } = Array.Empty<int>();
}

public sealed class EventAckMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("eventId")] public long EventId { get; set; }
}

public sealed class ClientGoodbyeMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
}
