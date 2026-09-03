namespace ElementWar.Server;

/// <summary>确定性的 PVP 场景碰撞：XZ 平面圆形角色、轴对齐掩体和玩家推挤。</summary>
public sealed class ArenaCollisionWorld
{
    public readonly record struct Obstacle(float CenterX, float CenterZ, float HalfX, float HalfZ, float MinY, float MaxY);

    // 与 PVPGame.unity 的四个 Cover BoxCollider 对齐（中心、尺寸均为世界坐标）。
    public static readonly Obstacle[] DefaultObstacles =
    {
        new(0f, 12f, 1.5f, 0.5f, 0f, 2f),
        new(12f, 0f, 1.5f, 0.5f, 0f, 2f),
        new(0f, -12f, 1.5f, 0.5f, 0f, 2f),
        new(-12f, 0f, 1.5f, 0.5f, 0f, 2f),
    };

    public float Radius { get; }
    public float HalfExtent { get; }
    public IReadOnlyList<Obstacle> Obstacles { get; }

    public ArenaCollisionWorld(float radius, float halfExtent, IReadOnlyList<Obstacle>? obstacles = null)
    {
        Radius = MathF.Max(0.01f, radius);
        HalfExtent = MathF.Max(Radius, halfExtent);
        Obstacles = obstacles ?? DefaultObstacles;
    }

    public Vec3 ResolveMovement(Vec3 start, Vec3 desired, float minY)
    {
        Vec3 current = start;
        Vec3 delta = desired - current;
        const int substeps = 4;
        for (int i = 1; i <= substeps; i++)
        {
            Vec3 next = current + delta * (1f / substeps);
            next = new Vec3(Math.Clamp(next.X, -HalfExtent, HalfExtent), next.Y,
                Math.Clamp(next.Z, -HalfExtent, HalfExtent));
            current = ResolveStatic(current, next, minY);
        }
        return current;
    }

    private Vec3 ResolveStatic(Vec3 start, Vec3 desired, float minY)
    {
        Vec3 result = desired;
        foreach (var obstacle in Obstacles)
        {
            float minX = obstacle.CenterX - obstacle.HalfX - Radius, maxX = obstacle.CenterX + obstacle.HalfX + Radius;
            float minZ = obstacle.CenterZ - obstacle.HalfZ - Radius, maxZ = obstacle.CenterZ + obstacle.HalfZ + Radius;
            // 先处理从矩形外部穿入扩张边界的连续扫掠，避免单个子步从墙前跳到墙后。
            if (start.X >= minX && start.X <= maxX)
            {
                if (start.Z < minZ && result.Z >= minZ) result = new Vec3(result.X, result.Y, minZ);
                else if (start.Z > maxZ && result.Z <= maxZ) result = new Vec3(result.X, result.Y, maxZ);
            }
            if (start.Z >= minZ && start.Z <= maxZ)
            {
                if (start.X < minX && result.X >= minX) result = new Vec3(minX, result.Y, result.Z);
                else if (start.X > maxX && result.X <= maxX) result = new Vec3(maxX, result.Y, result.Z);
            }
            float dx = result.X - Math.Clamp(result.X, obstacle.CenterX - obstacle.HalfX, obstacle.CenterX + obstacle.HalfX);
            float dz = result.Z - Math.Clamp(result.Z, obstacle.CenterZ - obstacle.HalfZ, obstacle.CenterZ + obstacle.HalfZ);
            float d2 = dx * dx + dz * dz;
            if (d2 >= Radius * Radius) continue;

            if (d2 > 1e-8f)
            {
                float d = MathF.Sqrt(d2);
                float push = (Radius - d) / d;
                result = new Vec3(result.X + dx * push, result.Y, result.Z + dz * push);
            }
            else
            {
                float left = result.X - (obstacle.CenterX - obstacle.HalfX);
                float right = obstacle.CenterX + obstacle.HalfX - result.X;
                float back = result.Z - (obstacle.CenterZ - obstacle.HalfZ);
                float front = obstacle.CenterZ + obstacle.HalfZ - result.Z;
                float min = MathF.Min(MathF.Min(left, right), MathF.Min(back, front));
                // 中心恰在矩形内部时，沿进入方向退回最近边界，避免连续子步穿到墙后。
                if (start.Z <= obstacle.CenterZ && min <= back + 0.0001f)
                    result = new Vec3(result.X, result.Y, obstacle.CenterZ - obstacle.HalfZ - Radius);
                else if (start.Z >= obstacle.CenterZ && min <= front + 0.0001f)
                    result = new Vec3(result.X, result.Y, obstacle.CenterZ + obstacle.HalfZ + Radius);
                else if (min <= back && min <= front)
                    result = new Vec3(result.X, result.Y, back <= front ? obstacle.CenterZ - obstacle.HalfZ - Radius : obstacle.CenterZ + obstacle.HalfZ + Radius);
                else if (min <= left)
                    result = new Vec3(obstacle.CenterX - obstacle.HalfX - Radius, result.Y, result.Z);
                else
                    result = new Vec3(obstacle.CenterX + obstacle.HalfX + Radius, result.Y, result.Z);
            }
            result = new Vec3(Math.Clamp(result.X, -HalfExtent, HalfExtent), result.Y,
                Math.Clamp(result.Z, -HalfExtent, HalfExtent));
        }
        return result;
    }

    public void ResolvePlayerPush(IList<ServerPlayer> players, Func<ServerPlayer, bool>? canCollide = null)
    {
        for (int iteration = 0; iteration < 4; iteration++)
        {
            bool changed = false;
            for (int i = 0; i < players.Count; i++)
            for (int j = i + 1; j < players.Count; j++)
            {
                var a = players[i]; var b = players[j];
                if ((canCollide != null && (!canCollide(a) || !canCollide(b))) || !a.IsAlive || !b.IsAlive) continue;
                float dx = b.Position.X - a.Position.X, dz = b.Position.Z - a.Position.Z;
                float d2 = dx * dx + dz * dz, minDist = Radius * 2f;
                if (d2 >= minDist * minDist) continue;
                float d = MathF.Sqrt(d2);
                Vec3 dir = d > 1e-5f ? new Vec3(dx / d, 0f, dz / d) : new Vec3(a.PlayerId < b.PlayerId ? 1f : -1f, 0f, 0f);
                float amount = (minDist - d) * 0.5f;
                a.Position = ResolveMovement(a.Position, a.Position - dir * amount, a.GroundY);
                b.Position = ResolveMovement(b.Position, b.Position + dir * amount, b.GroundY);
                changed = true;
            }
            if (!changed) break;
        }
    }

    /// <summary>返回 XZ 射线命中任一有高度的掩体的最近距离。</summary>
    public bool RaycastWalls(Vec3 origin, Vec3 direction, float maxDistance, out float distance)
    {
        distance = maxDistance;
        bool hit = false;
        foreach (var o in Obstacles)
        {
            float tMin = 0f, tMax = maxDistance;
            if (!Slab(origin.X, direction.X, o.CenterX - o.HalfX, o.CenterX + o.HalfX, ref tMin, ref tMax)
                || !Slab(origin.Z, direction.Z, o.CenterZ - o.HalfZ, o.CenterZ + o.HalfZ, ref tMin, ref tMax)
                || !Slab(origin.Y, direction.Y, o.MinY, o.MaxY, ref tMin, ref tMax)) continue;
            if (tMin < distance) { distance = MathF.Max(0f, tMin); hit = true; }
        }
        return hit;
    }

    private static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 1e-6f) return origin >= min && origin <= max;
        float a = (min - origin) / direction, b = (max - origin) / direction;
        if (a > b) (a, b) = (b, a);
        tMin = MathF.Max(tMin, a); tMax = MathF.Min(tMax, b);
        return tMin <= tMax;
    }
}
