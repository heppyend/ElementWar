using System;
using System.Collections.Generic;

namespace ElementWar.Rules
{

public static class GameRulesValidator
{
    public const int SupportedSchemaVersion = 1;

    public static bool TryValidate(GameRulesDocument document, out string error)
    {
        var errors = new List<string>();
        if (document is null)
            errors.Add("document is null");
        else
        {
            if (document.schemaVersion != SupportedSchemaVersion)
                errors.Add($"unsupported schemaVersion={document.schemaVersion}");
            if (string.IsNullOrWhiteSpace(document.rulesetId))
                errors.Add("rulesetId is required");
            ValidateProfile("pve", document.pve, errors);
            ValidateProfile("pvp_1v1", document.pvp_1v1, errors);
        }

        error = string.Join("; ", errors);
        return errors.Count == 0;
    }

    private static void ValidateProfile(string name, RuleProfile profile, List<string> errors)
    {
        if (profile is null)
        {
            errors.Add($"{name} profile is required");
            return;
        }

        var movement = profile.movement;
        if (movement is null)
        {
            errors.Add($"{name}.movement is required");
        }
        else
        {
            Positive(movement.walkSpeed, $"{name}.movement.walkSpeed", errors);
            Positive(movement.jogSpeed, $"{name}.movement.jogSpeed", errors);
            Positive(movement.sprintSpeed, $"{name}.movement.sprintSpeed", errors);
            Positive(movement.aimMoveSpeed, $"{name}.movement.aimMoveSpeed", errors);
            Positive(movement.jumpVelocity, $"{name}.movement.jumpVelocity", errors);
            Positive(movement.slideDurationSeconds, $"{name}.movement.slideDurationSeconds", errors);
            Positive(movement.slideStartSpeed, $"{name}.movement.slideStartSpeed", errors);
            NonNegative(movement.slideEndSpeed, $"{name}.movement.slideEndSpeed", errors);
            NonNegative(movement.sprintSlideBoost, $"{name}.movement.sprintSlideBoost", errors);
            Positive(movement.rotationSpeedDeg, $"{name}.movement.rotationSpeedDeg", errors);
            if (movement.gravity >= 0f) errors.Add($"{name}.movement.gravity must be negative");
            if (movement.slideEndSpeed > movement.slideStartSpeed)
                errors.Add($"{name}.movement.slideEndSpeed cannot exceed slideStartSpeed");
        }

        var weapon = profile.weapon;
        if (weapon is null)
        {
            errors.Add($"{name}.weapon is required");
        }
        else
        {
            if (weapon.magazineCapacity <= 0) errors.Add($"{name}.weapon.magazineCapacity must be positive");
            NonNegative(weapon.reserveAmmo, $"{name}.weapon.reserveAmmo", errors);
            if (weapon.damage <= 0) errors.Add($"{name}.weapon.damage must be positive");
            Positive(weapon.fireCooldownSeconds, $"{name}.weapon.fireCooldownSeconds", errors);
            Positive(weapon.reloadDurationSeconds, $"{name}.weapon.reloadDurationSeconds", errors);
            Positive(weapon.fireRange, $"{name}.weapon.fireRange", errors);
        }

        var life = profile.life;
        if (life is null)
        {
            errors.Add($"{name}.life is required");
        }
        else
        {
            if (life.maxHealth <= 0) errors.Add($"{name}.life.maxHealth must be positive");
            NonNegative(life.respawnSeconds, $"{name}.life.respawnSeconds", errors);
            Positive(life.hitRadius, $"{name}.life.hitRadius", errors);
            Positive(life.hitHeight, $"{name}.life.hitHeight", errors);
            Positive(life.eyeHeight, $"{name}.life.eyeHeight", errors);
        }

        var spawn = profile.spawn;
        if (spawn is null)
        {
            errors.Add($"{name}.spawn is required");
        }
        else
        {
            Positive(spawn.arenaHalfExtent, $"{name}.spawn.arenaHalfExtent", errors);
            Positive(spawn.collisionRadius, $"{name}.spawn.collisionRadius", errors);
            if (spawn.winScore <= 0) errors.Add($"{name}.spawn.winScore must be positive");
        }
    }

    private static void Positive(float value, string field, List<string> errors)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            errors.Add($"{field} must be positive and finite");
    }

    private static void NonNegative(float value, string field, List<string> errors)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            errors.Add($"{field} must be non-negative and finite");
    }
}
}
