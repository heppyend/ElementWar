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
        Console.WriteLine("[SelfTest] 5/5 passed");
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
