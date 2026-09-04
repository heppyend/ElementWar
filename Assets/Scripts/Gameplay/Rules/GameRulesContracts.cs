using System;

namespace ElementWar.Rules
{

/// <summary>版本化玩法规则文档。字段保持 Unity JsonUtility 与 .NET System.Text.Json 均可读取。</summary>
[Serializable]
public sealed class GameRulesDocument
{
    public int schemaVersion = 1;
    public string rulesetId = string.Empty;
    public RuleProfile pve = new();
    public RuleProfile pvp_1v1 = new();
}

[Serializable]
public sealed class RuleProfile
{
    public MovementRules movement = new();
    public WeaponRules weapon = new();
    public LifeRules life = new();
    public SpawnRules spawn = new();
}

[Serializable]
public sealed class MovementRules
{
    public float gravity = -15f;
    public float walkSpeed = 2.2f;
    public float jogSpeed = 5f;
    public float sprintSpeed = 8f;
    public float aimMoveSpeed = 2.5f;
    public float jumpVelocity = 6.7f;
    public float slideDurationSeconds = 0.8f;
    public float slideStartSpeed = 7f;
    public float slideEndSpeed = 1.5f;
    public float sprintSlideBoost = 2f;
    public float rotationSpeedDeg = 300f;
}

[Serializable]
public sealed class WeaponRules
{
    public int magazineCapacity = 30;
    public int reserveAmmo = 90;
    public int damage = 25;
    public float fireCooldownSeconds = 0.15f;
    public float reloadDurationSeconds = 1.5f;
    public float fireRange = 35f;
}

[Serializable]
public sealed class LifeRules
{
    public int maxHealth = 100;
    public float respawnSeconds = 3f;
    public float hitRadius = 1.2f;
    public float hitHeight = 1.8f;
    public float eyeHeight = 1.5f;
}

[Serializable]
public sealed class SpawnRules
{
    public float groundY = 0.15f;
    public float arenaHalfExtent = 40f;
    public float collisionRadius = 0.4f;
    public int winScore = 5;
}
}
