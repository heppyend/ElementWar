namespace ElementWar.Server;

/// <summary>只读捕获世界 → 快照 DTO。2 人局全量复制，不做距离分级（保留扩展点）。</summary>
public static class SnapshotBuilder
{
    public static PlayerSnapshotMessage[] Capture(GameWorld world)
    {
        var players = world.Players.Values.OrderBy(p => p.PlayerId);
        var list = new List<PlayerSnapshotMessage>(world.PlayerCount);
        foreach (var p in players)
        {
            list.Add(new PlayerSnapshotMessage
            {
                PlayerId = p.PlayerId,
                X = p.Position.X,
                Y = p.Position.Y,
                Z = p.Position.Z,
                BodyYawDeg = p.BodyYawDeg,
                AimX = p.AimPoint.X,
                AimY = p.AimPoint.Y,
                AimZ = p.AimPoint.Z,
                LastProcessedInputTick = p.LastProcessedInputTick,
                Health = p.Health,
                MaxHealth = p.MaxHealth,
                IsAlive = p.IsAlive,
                LifeStateVersion = p.LifeStateVersion,
                RespawnRemainingSeconds = !p.IsAlive
                    ? Math.Max(0f, (p.RespawnServerTick - world.ServerTick) / (float)world.Settings.ServerTickRate)
                    : 0f,
                CharacterId = p.CharacterId,
                IsBot = p.IsBot,
                MoveState = p.MoveState,
                SpeedBlend = p.SpeedBlend,
                VerticalSpeed = p.VerticalSpeed,
                IsGrounded = p.IsGrounded,
                IsSliding = p.IsSliding,
                SlideTicksRemaining = p.SlideTicksRemaining,
                SlideDirectionX = p.SlideDirection.X,
                SlideDirectionY = p.SlideDirection.Y,
                SlideDirectionZ = p.SlideDirection.Z,
                SlideSprintBoost = p.SlideSprintBoost,
                Score = p.Score,
                MagazineAmmo = p.Weapon.MagazineAmmo,
                ReserveAmmo = p.Weapon.ReserveAmmo,
                IsReloading = p.Weapon.IsReloading,
            });
        }
        return list.ToArray();
    }
}
