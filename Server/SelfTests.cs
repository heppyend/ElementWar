namespace ElementWar.Server;

/// <summary>无需网络端口的权威世界回归检查；运行 dotnet run -- --self-test。</summary>
public static class SelfTests
{
    public static void Run()
    {
        SpawnContractMatchesSceneOrder();
        FireCountsAsAimingForMovement();
        InputsWaitForMissingSequence();
        EveryAcceptedFireCreatesShotEvent();
        ReliableLedgerHonorsResendInterval();
        CollisionStopsAtCover();
        PlayersAreSeparatedAndWallsBlockShots();
        JumpSurvivesCollisionResolution();
        AuthoritativeAmmoReloadsFromIntent();
        Console.WriteLine("[SelfTest] 9/9 passed");
    }

    private static void AuthoritativeAmmoReloadsFromIntent()
    {
        var settings = new GameWorldSettings { WeaponMagazineCapacity = 2, WeaponReserveAmmo = 3, ReloadDurationSeconds = 0.1f };
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        Assert(world.TryQueueFire(player.PlayerId, new FireRequestMessage { FireSequence = 1, AimY = 1.5f, AimZ = -20f }), "fire intent should queue");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.Weapon.MagazineAmmo == 1, "server must consume authoritative ammo on accepted fire");
        Assert(world.TryQueueReload(player.PlayerId, new ReloadRequestMessage { ReloadSequence = 1 }), "reload intent should queue");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.Weapon.IsReloading, "server must enter reloading after accepted reload intent");
        for (int i = 0; i < 8; i++) world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.Weapon.MagazineAmmo == 2 && player.Weapon.ReserveAmmo == 2 && !player.Weapon.IsReloading, "server must complete reload with reserve transfer");
    }

    private static void SpawnContractMatchesSceneOrder()
    {
        var world = new GameWorld(new GameWorldSettings());
        var p1 = world.AddPlayer("P1", 0);
        var p2 = world.AddPlayer("P2", 1);
        AssertNear(p1.Position.Z, 6f, "P1 spawn Z");
        AssertNear(p2.Position.Z, -6f, "P2 spawn Z");
        AssertNear(p1.Position.Y, 0.025f, "Lumine ground Y");
        AssertNear(p2.Position.Y, 0.15f, "Furina ground Y");
    }

    private static void FireCountsAsAimingForMovement()
    {
        var settings = new GameWorldSettings();
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        bool accepted = world.TryQueueInput(player.PlayerId, new PlayerInputMessage
        {
            InputTick = 1,
            WorldMoveX = 1f,
            IsFire = true,
            BodyYawDeg = 180f,
            AimZ = 20f,
        });
        Assert(accepted, "input should be accepted");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.Position.X > 0.03f, "firing movement should strafe along input");
        AssertNear(player.Position.Z, 6f, "firing movement must not walk backward by body yaw");
    }

    private static void EveryAcceptedFireCreatesShotEvent()
    {
        var settings = new GameWorldSettings();
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        bool accepted = world.TryQueueFire(player.PlayerId, new FireRequestMessage
        {
            FireSequence = 1,
            AimX = 0f,
            AimY = 1.5f,
            AimZ = -20f,
        });
        Assert(accepted, "fire should be accepted");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(world.PendingShots.Count == 1, "accepted fire should create one shot event");
    }

    private static void InputsWaitForMissingSequence()
    {
        var settings = new GameWorldSettings();
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        var tick2 = new PlayerInputMessage
        {
            InputTick = 2, PlayerId = player.PlayerId,
            WorldMoveX = 1f, IsAiming = true, BodyYawDeg = 180f,
        };
        Assert(world.TryQueueInput(player.PlayerId, tick2), "tick2 should enter buffer");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.LastProcessedInputTick == 0, "server must wait for missing tick1");

        var tick1 = new PlayerInputMessage
        {
            InputTick = 1, PlayerId = player.PlayerId,
            WorldMoveX = 1f, IsAiming = true, BodyYawDeg = 180f,
        };
        Assert(world.TryQueueInput(player.PlayerId, tick1), "retransmitted tick1 should enter buffer");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.LastProcessedInputTick == 1, "tick1 should process first");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.LastProcessedInputTick == 2, "buffered tick2 should process second");
    }

    private static void ReliableLedgerHonorsResendInterval()
    {
        var ledger = new ReliableEventLedger();
        DateTime t0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ledger.QueueInitial(new object(), 7, "{}", t0);
        Assert(!ledger.CollectDueResends(t0.AddMilliseconds(100)).Any(), "must not resend before interval");
        var due = ledger.CollectDueResends(t0.AddMilliseconds(250)).ToArray();
        Assert(due.Length == 1 && due[0].eventId == 7, "must resend once at interval");
        Assert(ledger.Acknowledge(7) && ledger.PendingCount == 0, "ack should clear pending event");
    }

    private static void CollisionStopsAtCover()
    {
        var collision = new ArenaCollisionWorld(0.4f, 40f);
        var end = collision.ResolveMovement(new Vec3(0f, 0.025f, 10f), new Vec3(0f, 0.025f, 14f), 0.025f);
        Assert(end.Z <= 11.1001f, $"cover collision failed: {end}");
    }

    private static void PlayersAreSeparatedAndWallsBlockShots()
    {
        var collision = new ArenaCollisionWorld(0.4f, 40f);
        var a = new ServerPlayer { PlayerId = 1, IsAlive = true, GroundY = 0.025f, Position = new Vec3(0f, 0.025f, 10f) };
        var b = new ServerPlayer { PlayerId = 2, IsAlive = true, GroundY = 0.025f, Position = new Vec3(0.5f, 0.025f, 10f) };
        collision.ResolvePlayerPush(new List<ServerPlayer> { a, b });
        Assert(Vec3.Distance(a.Position, b.Position) >= 0.799f, "players must not overlap after push");
        Assert(collision.RaycastWalls(new Vec3(0f, 1.5f, 0f), new Vec3(0f, 0f, 1f), 35f, out float wall)
            && wall > 11f && wall < 12f, "wall raycast should hit Cover_0");
    }

    private static void JumpSurvivesCollisionResolution()
    {
        var settings = new GameWorldSettings();
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        Assert(world.TryQueueInput(player.PlayerId, new PlayerInputMessage { InputTick = 1, IsJumping = true }), "jump input should be accepted");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.Position.Y > player.GroundY, "collision resolution must not reset jump height");
    }

    private static void AssertNear(float actual, float expected, string name)
    {
        if (MathF.Abs(actual - expected) > 0.0001f)
            throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
