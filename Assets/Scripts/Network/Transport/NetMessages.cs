using System;
using System.Collections.Generic;

namespace ElementWar.Net
{
    /// <summary>
    /// 网络消息 DTO（Unity 端，JsonUtility，public 字段 camelCase）。
    /// ⚠️ 与 .NET 服务器 Server/Messages.cs 字段名严格一致（JsonPropertyName），两端必须同步改。
    /// </summary>
    public static class Msg
    {
        public const string Hello = "hello";
        public const string Welcome = "welcome";
        public const string WelcomeFull = "welcome_full";
        public const string Input = "input";
        public const string Fire = "fire";
        public const string FireReceipt = "fireReceipt";
        public const string Snapshot = "snapshot";
        public const string Hit = "hit";
        public const string HealthChanged = "healthChanged";
        public const string Death = "death";
        public const string Respawn = "respawn";
        public const string Kill = "kill";
        public const string MatchEnd = "matchEnd";
        public const string Ack = "ack";
        public const string Goodbye = "goodbye";
    }

    [Serializable] public class ClientHelloMessage
    {
        public string type = Msg.Hello;
        public string playerName = "";
        public int characterId;
    }

    [Serializable] public class ServerWelcomeMessage
    {
        public string type = Msg.Welcome;
        public int playerId;
        public int serverTickRate;
        public int winScore;
        public int serverTick;
    }

    [Serializable] public class PlayerInputMessage
    {
        public string type = Msg.Input;
        public int playerId;
        public int inputTick;
        public float moveX, moveY;
        public bool isSprint, isAiming, isFire, isJumping, isSlide;
        public float worldMoveX, worldMoveY, worldMoveZ;
        public float bodyYawDeg;
        public float aimX, aimY, aimZ;
        public float estimatedRttSeconds, interpolationDelaySeconds;
    }

    [Serializable] public class FireRequestMessage
    {
        public string type = Msg.Fire;
        public int playerId;
        public int fireSequence;
        public int requestTick;
        public float aimX, aimY, aimZ;
        public float estimatedRttSeconds, interpolationDelaySeconds;
    }

    [Serializable] public class FireReceiptMessage
    {
        public string type = Msg.FireReceipt;
        public int playerId;
        public int fireSequence;
        public bool accepted;
        public string reason = "";
        public int serverTick;
    }

    [Serializable] public class PlayerSnapshotMessage
    {
        public int playerId;
        public float x, y, z;
        public float bodyYawDeg;
        public float aimX, aimY, aimZ;
        public int lastProcessedInputTick;
        public int health, maxHealth;
        public bool isAlive;
        public int lifeStateVersion;
        public float respawnRemainingSeconds;
        public int characterId;
        public bool isBot;
        public int moveState;
        public float speedBlend;
        public float verticalSpeed;
        public bool isGrounded;
        public int score;
    }

    [Serializable] public class WorldSnapshotMessage
    {
        public string type = Msg.Snapshot;
        public int serverTick;
        public int snapshotSequence;
        public PlayerSnapshotMessage[] players = Array.Empty<PlayerSnapshotMessage>();
        public int[] replicatedPlayerIds = Array.Empty<int>();
        public bool isFullState = true;
    }

    [Serializable] public class HitEventMessage
    {
        public string type = Msg.Hit;
        public int serverTick;
        public int shooterPlayerId;
        public int targetPlayerId;
        public float hitX, hitY, hitZ;
        public int damage;
    }

    [Serializable] public class HealthChangedMessage
    {
        public string type = Msg.HealthChanged;
        public int serverTick;
        public int playerId;
        public int health, maxHealth;
        public bool isAlive;
        public float respawnRemainingSeconds;
    }

    [Serializable] public class ReliableEventMessage
    {
        public string type = "";
        public long eventId;
        public int serverTick;
        public int playerId;
        public int lifeStateVersion;
        public int killerPlayerId;
        public int victimPlayerId;
        public float respawnRemainingSeconds;
        public float x, y, z;
        public int health, maxHealth;
        public int killerScore, victimScore;
        public int winnerPlayerId;
        public int[] finalScores = Array.Empty<int>();
    }

    [Serializable] public class EventAckMessage
    {
        public string type = Msg.Ack;
        public long eventId;
    }

    [Serializable] public class ClientGoodbyeMessage
    {
        public string type = Msg.Goodbye;
    }

    /// <summary>仅用于读 JSON 的 type 字段做路由。</summary>
    [Serializable] public class NetMessageHeader
    {
        public string type = "";
    }
}
