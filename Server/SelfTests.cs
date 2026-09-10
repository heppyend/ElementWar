using ElementWar.Rules;
using ElementWar.Net;

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
        CenterCoverUsesConcaveOutline();
        BakedProfileBlocksMovementAndReturnsSurfaceHit();
        TrainingBotInitializesCombatState();
        TrainingBotHonorsFieldOfViewAndFireRange();
        TrainingBotSightIsBlockedByStaticCover();
        TrainingBotFollowsWallAfterRepeatedSearchBlocks();
        JumpSurvivesCollisionResolution();
        AirbornePlayerLandsWhenInputStops();
        AuthoritativeAmmoReloadsFromIntent();
        RulesValidatorRejectsInvalidProfiles();
        Console.WriteLine("[SelfTest] 19/19 passed");
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

    private static void RulesValidatorRejectsInvalidProfiles()
    {
        var valid = new GameRulesDocument { rulesetId = "self-test" };
        Assert(GameRulesValidator.TryValidate(valid, out _), "default rules document should validate");
        valid.pvp_1v1.movement.slideEndSpeed = valid.pvp_1v1.movement.slideStartSpeed + 1f;
        Assert(!GameRulesValidator.TryValidate(valid, out string slideError)
            && slideError.Contains("slideEndSpeed", StringComparison.Ordinal),
            "slide end speed above start speed must be rejected");
        valid.pvp_1v1.movement.slideEndSpeed = 1.5f;
        valid.pvp_1v1.weapon = null!;
        Assert(!GameRulesValidator.TryValidate(valid, out string missingError)
            && missingError.Contains("pvp_1v1.weapon", StringComparison.Ordinal),
            "missing weapon rules must be rejected");
        string hash = GameRulesHash.Compute(new byte[] { 1, 2, 3 });
        Assert(hash.Length == 64, "rules content hash must be SHA-256 hex");
    }

    private static void SpawnContractMatchesSceneOrder()
    {
        var world = new GameWorld(new GameWorldSettings());
        var p1 = world.AddPlayer("P1", 0);
        var p2 = world.AddPlayer("P2", 1);
        Assert(IsOuterCoverSpawn(p1.Position), $"P1 must use an outer-cover spawn: {p1.Position}");
        Assert(IsOuterCoverSpawn(p2.Position), $"P2 must use an outer-cover spawn: {p2.Position}");
        Assert(p1.SpawnSlot != p2.SpawnSlot, "two living players must not share an initial spawn slot");
        AssertNear(p1.Position.Y, 0.025f, "Lumine ground Y");
        AssertNear(p2.Position.Y, 0.15f, "Furina ground Y");
    }

    private static void FireCountsAsAimingForMovement()
    {
        var settings = new GameWorldSettings();
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        // 出生点现在随机在掩体外侧；移动语义测试必须脱离静态障碍，避免把碰撞结果误判成输入方向错误。
        player.Position = new Vec3(20f, player.GroundY, 20f);
        float initialX = player.Position.X;
        float initialZ = player.Position.Z;
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
        Assert(player.Position.X > initialX + 0.03f, "firing movement should strafe along input");
        AssertNear(player.Position.Z, initialZ, "firing movement must not walk backward by body yaw");
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
            && wall > 7.3f && wall < 7.4f, "wall raycast should hit the center cover before Cover_0");
    }

    private static void CenterCoverUsesConcaveOutline()
    {
        var collision = new ArenaCollisionWorld(0.4f, 40f);
        // 十字右上凹角属于实体外部；若退化成 AABB，这个点会被错误弹出。
        var gap = collision.ResolveMovement(new Vec3(5f, 0.025f, 4f), new Vec3(5f, 0.025f, 4f), 0.025f);
        AssertNear(gap.X, 5f, "concave gap x");
        AssertNear(gap.Z, 4f, "concave gap z");

        // 从右臂北侧推进，角色中心必须停在真实边缘 z=2.624 的半径之外。
        var blocked = collision.ResolveMovement(new Vec3(5f, 0.025f, 4f), new Vec3(5f, 0.025f, 0f), 0.025f);
        Assert(blocked.Z >= 3.023f && blocked.Z <= 3.025f, $"center cover collision failed: {blocked}");
    }

    private static void BakedProfileBlocksMovementAndReturnsSurfaceHit()
    {
        var profile = new PvpArenaCollisionProfile
        {
            contentHash = "self-test",
            movementVolumes =
            [new PvpMovementVolume
            {
                id = "test-wall", minY = 0f, maxY = 2f,
                points = [new(-1f, 4f), new(1f, 4f), new(1f, 6f), new(-1f, 6f)]
            }],
            bulletTriangles =
            [new PvpBulletTriangle
            {
                surfaceId = "Metal",
                a = new PvpPoint3(-1f, 0f, 4f), b = new PvpPoint3(1f, 0f, 4f), c = new PvpPoint3(0f, 2f, 4f)
            }],
            walkableTriangles =
            [new PvpWalkableTriangle
            {
                id = "test-ramp",
                blocksBelow = true,
                a = new PvpPoint3(-1f, 0f, 0f), b = new PvpPoint3(1f, 0f, 0f), c = new PvpPoint3(0f, 1f, 2f)
            }],
            sideWallTriangles =
            [new PvpSideWallTriangle
            {
                id = "test-side", a = new PvpPoint3(-1f, 0f, 3f), b = new PvpPoint3(1f, 0f, 3f), c = new PvpPoint3(0f, 2f, 3f)
            }]
        };
        var collision = new ArenaCollisionWorld(0.4f, 40f, profile: profile);
        var end = collision.ResolveMovement(new Vec3(0f, 0.025f, 2f), new Vec3(0f, 0.025f, 5f), 0.025f);
        Assert(end.Z <= 3.6001f, "baked movement proxy must block capsule movement");
        Assert(collision.RaycastStaticGeometry(new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f), 10f, out var hit)
            && hit.SurfaceId == "Metal" && hit.Normal.Z < -0.99f && hit.Distance > 3.9f && hit.Distance < 4.1f,
            "baked triangle must return exact authoritative surface hit");
        Assert(collision.TryGetWalkableHeight(0f, 1f, 0f, 0.65f, out float slopeY)
            && slopeY > 0.49f && slopeY < 0.51f, "baked walkable triangle must provide ramp height");
        Assert(collision.IsBlockedByWalkableSide(0f, 1.8f, 0f, 0.1f), "steep walkable surface must block side entry below its height");
        Vec3 slide = collision.ResolveWalkableSideSlide(new Vec3(-1.2f, 0f, 1.8f), new Vec3(0.2f, 0f, 1.8f), 0f, 0.1f);
        Assert(slide.X > -1.19f || MathF.Abs(slide.Z - 1.8f) > 0.001f, "side collision should preserve a tangential slide component");
        Vec3 slopeEntry = collision.ResolveMovement(new Vec3(0f, 0.025f, 0.8f), new Vec3(0f, 0.025f, 1.8f), 0.025f);
        Assert(slopeEntry.Z < 1.8f, "low-side entry into a high walkable surface must be blocked");
        Vec3 slopeExit = collision.ResolveMovement(new Vec3(0f, 0.025f, 1.8f), new Vec3(0f, 0.025f, 2.5f), 0.025f);
        Assert(slopeExit.Z > 1.8f, "leaving a walkable surface toward the low side must remain possible");
    }

    private static void TrainingBotInitializesCombatState()
    {
        var settings = new GameWorldSettings { EnableBots = true };
        var world = new GameWorld(settings);
        world.AddPlayer("P1", 0);
        var bot = world.Players.Values.Single(p => p.IsBot);
        Assert(bot.Weapon != null, "training bot must receive a WeaponRuntime on spawn");
        world.StepFrame(1f / settings.ServerTickRate);
    }

    private static void TrainingBotHonorsFieldOfViewAndFireRange()
    {
        var settings = new GameWorldSettings
        {
            EnableBots = true,
            BotFireRange = 10f,
            BotFieldOfViewDegrees = 120f,
            BotAimErrorBase = 0f,
            BotAimErrorFactor = 0f,
        };
        var world = new GameWorld(settings);
        var human = world.AddPlayer("P1", 0);
        var bot = world.Players.Values.Single(p => p.IsBot);
        bot.Position = new Vec3(20f, bot.GroundY, 20f);
        human.Position = new Vec3(20f, human.GroundY, 15f); // bot 身后
        bot.BodyYawDeg = 0f;
        int healthBefore = human.Health;
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(!bot.BotHasVisualContact, "target behind bot must be outside the 120 degree field of view");
        Assert(human.Health == healthBefore, "bot must not fire on the tick where target is outside its field of view");

        bot.BodyYawDeg = 180f;
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(bot.BotHasVisualContact, "target directly ahead must enter bot field of view");
        Assert(human.Health == healthBefore - settings.BotFireDamage,
            "bot fire inside field of view and BotFireRange must apply configured training damage");
    }

    private static void TrainingBotSightIsBlockedByStaticCover()
    {
        var settings = new GameWorldSettings
        {
            EnableBots = true,
            BotFireRange = 30f,
            BotVisionRange = 30f,
            BotAimErrorBase = 0f,
            BotAimErrorFactor = 0f,
        };
        var world = new GameWorld(settings);
        var human = world.AddPlayer("P1", 0);
        var bot = world.Players.Values.Single(p => p.IsBot);
        bot.Position = new Vec3(0f, bot.GroundY, 0f);
        bot.BodyYawDeg = 0f;
        // 旧场景回退的 (0, 12) 掩体位于两者正中；视线命中静态体后不可见、不可开火。
        human.Position = new Vec3(0f, human.GroundY, 20f);
        int healthBefore = human.Health;
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(!bot.BotHasVisualContact, "static cover must block bot line of sight");
        Assert(human.Health == healthBefore, "bot must not fire through static cover");
    }

    private static void TrainingBotFollowsWallAfterRepeatedSearchBlocks()
    {
        var settings = new GameWorldSettings
        {
            EnableBots = true,
            BotLostSightSeconds = 0f,
            BotBlockedMoveTickThreshold = 3,
            BotWallFollowTicks = 30,
            BotDecisionTicksMin = 10000,
            BotDecisionTicksMax = 10000,
        };
        var world = new GameWorld(settings);
        var human = world.AddPlayer("P1", 0);
        var bot = world.Players.Values.Single(p => p.IsBot);
        // bot 已贴近 (0, 12) 掩体南侧，目标在北侧；直线搜索会连续顶墙。
        bot.Position = new Vec3(0f, bot.GroundY, 11.08f);
        bot.BodyYawDeg = 0f;
        bot.BotLastSeenTick = -1000;
        bot.BotNextDecisionTick = int.MaxValue;
        bot.BotSearchOffset = Vec3.Zero;
        human.Position = new Vec3(0f, human.GroundY, 20f);

        for (int i = 0; i < 12; i++) world.StepFrame(1f / settings.ServerTickRate);
        Assert(bot.BotWallFollowTicksRemaining > 0, "repeated blocked search movement must enter wall-follow state");
        Assert(MathF.Abs(bot.Position.X) > 0.02f, "wall-follow state must create tangential movement instead of repeatedly pushing into the wall");
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

    private static void AirbornePlayerLandsWhenInputStops()
    {
        var settings = new GameWorldSettings { InputHoldTimeoutTicks = 0 };
        var world = new GameWorld(settings);
        var player = world.AddPlayer("P1", 0);
        Assert(world.TryQueueInput(player.PlayerId, new PlayerInputMessage { InputTick = 1, IsJumping = true }),
            "jump input should queue before no-input landing test");
        world.StepFrame(1f / settings.ServerTickRate);
        Assert(!player.IsGrounded, "player should be airborne after jump");
        for (int i = 0; i < 120; i++) world.StepFrame(1f / settings.ServerTickRate);
        Assert(player.IsGrounded && MathF.Abs(player.Position.Y - player.GroundY) < 0.0001f,
            "airborne player must land even after input timeout");
    }

    private static void AssertNear(float actual, float expected, string name)
    {
        if (MathF.Abs(actual - expected) > 0.0001f)
            throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }

    private static bool IsOuterCoverSpawn(Vec3 p)
    {
        const float tolerance = 0.001f;
        return (Math.Abs(p.X) <= tolerance && Math.Abs(Math.Abs(p.Z) - 13.5f) <= tolerance)
            || (Math.Abs(p.Z) <= tolerance && Math.Abs(Math.Abs(p.X) - 13.5f) <= tolerance);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
