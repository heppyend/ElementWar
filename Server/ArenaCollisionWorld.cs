namespace ElementWar.Server;

/// <summary>确定性的 PVP 场景碰撞：XZ 平面圆形角色、轴对齐掩体和玩家推挤。</summary>
public sealed class ArenaCollisionWorld
{
    public readonly record struct Obstacle(float CenterX, float CenterZ, float HalfX, float HalfZ, float MinY, float MaxY);
    public readonly record struct PolygonPoint(float X, float Z);
    public sealed record PolygonObstacle(float MinY, float MaxY, IReadOnlyList<PolygonPoint> Points);
    public readonly record struct BulletHit(float Distance, Vec3 Point, Vec3 Normal, string SurfaceId);
    private readonly record struct BulletTriangle(Vec3 A, Vec3 B, Vec3 C, string SurfaceId);
    private readonly record struct WalkableTriangle(Vec3 A, Vec3 B, Vec3 C, bool BlocksBelow);
    private readonly record struct SideWallTriangle(Vec3 A, Vec3 B, Vec3 C);

    // 与 PVPGame.unity 的四个 Cover BoxCollider 对齐（中心、尺寸均为世界坐标）。
    public static readonly Obstacle[] DefaultObstacles =
    {
        new(0f, 12f, 1.5f, 0.5f, 0f, 2f),
        new(12f, 0f, 1.5f, 0.5f, 0f, 2f),
        new(0f, -12f, 1.5f, 0.5f, 0f, 2f),
        new(-12f, 0f, 1.5f, 0.5f, 0f, 2f),
    };

    // PVPGame/Cover_4 的实际 ProBuilder MeshCollider 外轮廓（世界坐标）。
    // 注意：这是十字形实体，不能以 AABB 取代，否则会阻断玩家本应能走的四个凹角。
    public static readonly PolygonObstacle[] DefaultPolygonObstacles =
    {
        new(-0.04f, 1.646f,
        [
            new(-3.696f, 7.365f), new(3.374f, 7.365f), new(3.374f, 2.624f), new(7.462f, 2.624f),
            new(7.462f, -3.411f), new(3.374f, -3.411f), new(3.374f, -7.397f), new(-3.696f, -7.397f),
            new(-3.696f, -3.411f), new(-7.448f, -3.411f), new(-7.448f, 2.624f), new(-3.696f, 2.624f)
        ])
    };

    public float Radius { get; }
    public float HalfExtent { get; }
    public IReadOnlyList<Obstacle> Obstacles { get; }
    public IReadOnlyList<PolygonObstacle> PolygonObstacles { get; }
    private readonly IReadOnlyList<BulletTriangle> _bulletTriangles;
    private readonly IReadOnlyList<WalkableTriangle> _walkableTriangles;
    private readonly IReadOnlyList<SideWallTriangle> _sideWallTriangles;

    public ArenaCollisionWorld(float radius, float halfExtent, IReadOnlyList<Obstacle>? obstacles = null,
        IReadOnlyList<PolygonObstacle>? polygonObstacles = null, ElementWar.Net.PvpArenaCollisionProfile? profile = null)
    {
        Radius = MathF.Max(0.01f, radius);
        HalfExtent = MathF.Max(Radius, halfExtent);
        if (profile?.movementVolumes is { Length: > 0 })
        {
            Obstacles = Array.Empty<Obstacle>();
            PolygonObstacles = profile.movementVolumes
                .Where(v => v?.points is { Length: >= 3 })
                .Select(v => new PolygonObstacle(v.minY, v.maxY, v.points.Select(p => new PolygonPoint(p.x, p.z)).ToArray()))
                .ToArray();
            _bulletTriangles = (profile.bulletTriangles ?? Array.Empty<ElementWar.Net.PvpBulletTriangle>())
                .Select(t => new BulletTriangle(ToVec3(t.a), ToVec3(t.b), ToVec3(t.c), string.IsNullOrWhiteSpace(t.surfaceId) ? "Default" : t.surfaceId))
                .ToArray();
            _walkableTriangles = (profile.walkableTriangles ?? Array.Empty<ElementWar.Net.PvpWalkableTriangle>())
                .Select(t => new WalkableTriangle(ToVec3(t.a), ToVec3(t.b), ToVec3(t.c), t.blocksBelow))
                .ToArray();
            _sideWallTriangles = (profile.sideWallTriangles ?? Array.Empty<ElementWar.Net.PvpSideWallTriangle>())
                .Select(s => new SideWallTriangle(ToVec3(s.a), ToVec3(s.b), ToVec3(s.c)))
                .ToArray();
        }
        else
        {
            Obstacles = obstacles ?? DefaultObstacles;
            PolygonObstacles = polygonObstacles ?? DefaultPolygonObstacles;
            _bulletTriangles = Array.Empty<BulletTriangle>();
            _walkableTriangles = Array.Empty<WalkableTriangle>();
            _sideWallTriangles = Array.Empty<SideWallTriangle>();
        }
    }

    private static Vec3 ToVec3(ElementWar.Net.PvpPoint3 p) => new(p.x, p.y, p.z);

    /// <summary>在脚下可走三角面中找到允许到达的最高表面高度。用于低坡上行、下坡贴地和平台落脚。</summary>
    public bool TryGetWalkableHeight(float x, float z, float currentFeetY, float maxStepHeight, out float surfaceY)
    {
        surfaceY = float.NegativeInfinity;
        bool found = false;
        float ceiling = currentFeetY + MathF.Max(0.01f, maxStepHeight);
        foreach (var triangle in _walkableTriangles)
        {
            if (!TryGetTriangleHeight(triangle.A, triangle.B, triangle.C, x, z, out float y)) continue;
            if (y > ceiling + 0.0001f || y <= surfaceY) continue;
            surfaceY = y;
            found = true;
        }
        return found;
    }

    /// <summary>低于可跨越高度时，阻止从可走结构的侧面或底部切入。</summary>
    public bool IsBlockedByWalkableSide(float x, float z, float currentFeetY, float maxStepHeight)
    {
        float ceiling = currentFeetY + MathF.Max(0.01f, maxStepHeight);
        foreach (var triangle in _walkableTriangles)
        {
            if (!triangle.BlocksBelow || !TryGetTriangleHeight(triangle.A, triangle.B, triangle.C, x, z, out float y)) continue;
            if (y > ceiling + 0.0001f) return true;
        }
        return false;
    }

    /// <summary>与客户端一致的侧壁滑动：阻挡切入分量，保留沿坡面边界的分量。</summary>
    public Vec3 ResolveWalkableSideSlide(Vec3 start, Vec3 desired, float currentFeetY, float maxStepHeight)
    {
        if (!IsBlockedByWalkableSide(desired.X, desired.Z, currentFeetY, maxStepHeight)) return desired;

        Vec3 best = start;
        float bestDistanceSq = 0f;
        void Consider(Vec3 candidate)
        {
            if (IsBlockedByWalkableSide(candidate.X, candidate.Z, currentFeetY, maxStepHeight)) return;
            float dx = candidate.X - start.X, dz = candidate.Z - start.Z, distanceSq = dx * dx + dz * dz;
            if (distanceSq > bestDistanceSq) { best = candidate; bestDistanceSq = distanceSq; }
        }

        Consider(new Vec3(desired.X, desired.Y, start.Z));
        Consider(new Vec3(start.X, desired.Y, desired.Z));

        float moveX = desired.X - start.X, moveZ = desired.Z - start.Z;
        foreach (var triangle in _walkableTriangles)
        {
            if (!triangle.BlocksBelow || !TryGetTriangleHeight(triangle.A, triangle.B, triangle.C, desired.X, desired.Z, out float y)
                || y <= currentFeetY + maxStepHeight + 0.0001f) continue;
            Vec3[] points = { triangle.A, triangle.B, triangle.C };
            for (int i = 0; i < 3; i++)
            {
                Vec3 a = points[i], b = points[(i + 1) % 3];
                float ex = b.X - a.X, ez = b.Z - a.Z, lengthSq = ex * ex + ez * ez;
                if (lengthSq <= 1e-8f) continue;
                float invLength = 1f / MathF.Sqrt(lengthSq);
                float tx = ex * invLength, tz = ez * invLength;
                float along = moveX * tx + moveZ * tz;
                Consider(new Vec3(start.X + tx * along, desired.Y, start.Z + tz * along));
            }
        }
        return best;
    }

    private static bool TryGetTriangleHeight(Vec3 a, Vec3 b, Vec3 c, float x, float z, out float y)
    {
        float denominator = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
        if (MathF.Abs(denominator) < 1e-7f) { y = 0f; return false; }
        float u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / denominator;
        float v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / denominator;
        float w = 1f - u - v;
        if (u < -0.0001f || v < -0.0001f || w < -0.0001f) { y = 0f; return false; }
        y = a.Y * u + b.Y * v + c.Y * w;
        return true;
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

        foreach (var obstacle in PolygonObstacles)
            result = ResolvePolygon(result, obstacle);

        // 先用可走三角面的高度场处理从坡侧切入，避免侧墙高度依赖少量水平切片。
        result = ResolveWalkableSideSlide(start, result, result.Y - minY, GameWorldSettings.WalkableStepHeight);

        // 侧墙三角面不参与角色移动：它们是由可走面边界派生的双面几何，
        // 若用于移动会把“从坡面向外下行”也当成普通实体墙。坡侧移动只由
        // ResolveWalkableSideSlide 按 BlocksBelow 做单向阻断。

        return result;
    }

    private Vec3 ResolveSideWallTriangle(Vec3 start, Vec3 position, SideWallTriangle wall, float characterGroundOffset)
    {
        float feetY = position.Y - characterGroundOffset;
        // 覆盖角色从脚底到头部的多个高度，避免只命中膝盖附近。
        foreach (float sampleY in new[] { feetY + 0.05f, feetY + 0.25f, feetY + 0.5f, feetY + 0.75f,
            feetY + 1.0f, feetY + 1.25f, feetY + 1.5f, feetY + 1.7f })
        {
            if (!TryGetHorizontalSlice(wall, sampleY, out var a, out var b)) continue;
            var point = new PolygonPoint(position.X, position.Z);
            var nearest = ClosestPointOnSegment(point, a, b);
            float dx = point.X - nearest.X, dz = point.Z - nearest.Z, distanceSq = dx * dx + dz * dz;
            if (distanceSq >= Radius * Radius) continue;
            if (distanceSq > 1e-8f)
            {
                float distance = MathF.Sqrt(distanceSq), scale = (Radius - distance) / distance;
                position = new Vec3(position.X + dx * scale, position.Y, position.Z + dz * scale);
                continue;
            }
            float ex = b.X - a.X, ez = b.Z - a.Z, length = MathF.Sqrt(ex * ex + ez * ez);
            if (length <= 1e-8f) continue;
            float nx = -ez / length, nz = ex / length;
            float side = (start.X - nearest.X) * nx + (start.Z - nearest.Z) * nz;
            if (MathF.Abs(side) <= 1e-6f) side = 1f; else side = MathF.Sign(side);
            position = new Vec3(position.X + nx * side * Radius, position.Y, position.Z + nz * side * Radius);
        }
        return position;
    }

    private static bool TryGetHorizontalSlice(SideWallTriangle triangle, float y, out PolygonPoint first, out PolygonPoint second)
    {
        var points = new List<PolygonPoint>(2);
        AddSliceIntersection(triangle.A, triangle.B, y, points);
        AddSliceIntersection(triangle.B, triangle.C, y, points);
        AddSliceIntersection(triangle.C, triangle.A, y, points);
        if (points.Count < 2) { first = default; second = default; return false; }
        first = points[0]; second = points[1]; return true;
    }

    private static void AddSliceIntersection(Vec3 a, Vec3 b, float y, List<PolygonPoint> points)
    {
        float dy = b.Y - a.Y;
        if (MathF.Abs(dy) <= 1e-7f) return;
        float t = (y - a.Y) / dy;
        if (t < -0.0001f || t > 1.0001f) return;
        var point = new PolygonPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
        if (!points.Any(p => (p.X - point.X) * (p.X - point.X) + (p.Z - point.Z) * (p.Z - point.Z) < 1e-8f)) points.Add(point);
    }

    private Vec3 ResolvePolygon(Vec3 position, PolygonObstacle obstacle)
    {
        var point = new PolygonPoint(position.X, position.Z);
        bool inside = IsInsidePolygon(point, obstacle.Points);
        float nearestDistanceSq = float.MaxValue;
        PolygonPoint nearest = default;

        for (int i = 0; i < obstacle.Points.Count; i++)
        {
            var candidate = ClosestPointOnSegment(point, obstacle.Points[i], obstacle.Points[(i + 1) % obstacle.Points.Count]);
            float dx = point.X - candidate.X, dz = point.Z - candidate.Z, distanceSq = dx * dx + dz * dz;
            if (distanceSq < nearestDistanceSq) { nearestDistanceSq = distanceSq; nearest = candidate; }
        }

        if (!inside && nearestDistanceSq >= Radius * Radius) return position;

        float distance = MathF.Sqrt(nearestDistanceSq);
        PolygonPoint direction = distance > 1e-5f
            ? new PolygonPoint((point.X - nearest.X) / distance, (point.Z - nearest.Z) / distance)
            : OutwardNormal(nearest, obstacle.Points);
        var resolved = inside
            ? new PolygonPoint(nearest.X - direction.X * Radius, nearest.Z - direction.Z * Radius)
            : new PolygonPoint(nearest.X + direction.X * Radius, nearest.Z + direction.Z * Radius);
        return new Vec3(Math.Clamp(resolved.X, -HalfExtent, HalfExtent), position.Y,
            Math.Clamp(resolved.Z, -HalfExtent, HalfExtent));
    }

    private static PolygonPoint ClosestPointOnSegment(PolygonPoint point, PolygonPoint a, PolygonPoint b)
    {
        float ex = b.X - a.X, ez = b.Z - a.Z, lengthSq = ex * ex + ez * ez;
        if (lengthSq <= 1e-8f) return a;
        float t = Math.Clamp(((point.X - a.X) * ex + (point.Z - a.Z) * ez) / lengthSq, 0f, 1f);
        return new PolygonPoint(a.X + ex * t, a.Z + ez * t);
    }

    private static bool IsInsidePolygon(PolygonPoint point, IReadOnlyList<PolygonPoint> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Z > point.Z) == (b.Z > point.Z)) continue;
            if (point.X < (b.X - a.X) * (point.Z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
        }
        return inside;
    }

    private static PolygonPoint OutwardNormal(PolygonPoint boundaryPoint, IReadOnlyList<PolygonPoint> polygon)
    {
        const float probe = 0.01f;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
            var closest = ClosestPointOnSegment(boundaryPoint, a, b);
            float cx = closest.X - boundaryPoint.X, cz = closest.Z - boundaryPoint.Z;
            if (cx * cx + cz * cz > 1e-8f) continue;
            float ex = b.X - a.X, ez = b.Z - a.Z, length = MathF.Sqrt(ex * ex + ez * ez);
            if (length <= 1e-8f) continue;
            var normal = new PolygonPoint(-ez / length, ex / length);
            if (!IsInsidePolygon(new PolygonPoint(boundaryPoint.X + normal.X * probe, boundaryPoint.Z + normal.Z * probe), polygon)) return normal;
            if (!IsInsidePolygon(new PolygonPoint(boundaryPoint.X - normal.X * probe, boundaryPoint.Z - normal.Z * probe), polygon))
                return new PolygonPoint(-normal.X, -normal.Z);
        }
        return new PolygonPoint(1f, 0f);
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
        if (RaycastStaticGeometry(origin, direction, maxDistance, out BulletHit hit))
        {
            distance = hit.Distance;
            return true;
        }
        distance = maxDistance;
        return false;
    }

    /// <summary>权威静态命中：Profile 存在时走精确三角面，同时返回法线和 surfaceId 供客户端弹孔表现。</summary>
    public bool RaycastStaticGeometry(Vec3 origin, Vec3 direction, float maxDistance, out BulletHit hit)
    {
        float distance = maxDistance;
        Vec3 normal = Vec3.Zero;
        string surfaceId = "Default";
        bool hasHit = false;
        if (_bulletTriangles.Count > 0)
        {
            foreach (var triangle in _bulletTriangles)
            {
                if (!RaycastTriangle(origin, direction, distance, triangle, out float t, out Vec3 n)) continue;
                distance = t; normal = n; surfaceId = triangle.SurfaceId; hasHit = true;
            }
        }
        foreach (var o in Obstacles)
        {
            float tMin = 0f, tMax = maxDistance;
            if (!Slab(origin.X, direction.X, o.CenterX - o.HalfX, o.CenterX + o.HalfX, ref tMin, ref tMax)
                || !Slab(origin.Z, direction.Z, o.CenterZ - o.HalfZ, o.CenterZ + o.HalfZ, ref tMin, ref tMax)
                || !Slab(origin.Y, direction.Y, o.MinY, o.MaxY, ref tMin, ref tMax)) continue;
            if (tMin < distance) { distance = MathF.Max(0f, tMin); normal = Vec3.Zero; surfaceId = "Legacy"; hasHit = true; }
        }
        foreach (var o in PolygonObstacles)
        {
            if (RaycastPolygon(origin, direction, maxDistance, o, out float polygonDistance) && polygonDistance < distance)
            {
                distance = polygonDistance;
                normal = Vec3.Zero; surfaceId = "Legacy"; hasHit = true;
            }
        }
        if (hasHit)
        {
            hit = new BulletHit(distance, origin + direction * distance, normal, surfaceId);
            return true;
        }
        hit = default;
        return false;
    }

    private static bool RaycastTriangle(Vec3 origin, Vec3 direction, float maxDistance, BulletTriangle triangle, out float distance, out Vec3 normal)
    {
        Vec3 e1 = triangle.B - triangle.A, e2 = triangle.C - triangle.A;
        Vec3 p = Cross(direction, e2);
        float determinant = Dot(e1, p);
        if (MathF.Abs(determinant) < 1e-7f) { distance = 0f; normal = Vec3.Zero; return false; }
        float inv = 1f / determinant;
        Vec3 t = origin - triangle.A;
        float u = Dot(t, p) * inv;
        if (u < 0f || u > 1f) { distance = 0f; normal = Vec3.Zero; return false; }
        Vec3 q = Cross(t, e1);
        float v = Dot(direction, q) * inv;
        if (v < 0f || u + v > 1f) { distance = 0f; normal = Vec3.Zero; return false; }
        distance = Dot(e2, q) * inv;
        if (distance < 0f || distance > maxDistance) { normal = Vec3.Zero; return false; }
        normal = Cross(e1, e2).Normalized();
        if (Dot(normal, direction) > 0f) normal = normal * -1f;
        return true;
    }

    private static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static bool RaycastPolygon(Vec3 origin, Vec3 direction, float maxDistance, PolygonObstacle obstacle, out float distance)
    {
        distance = maxDistance;
        bool hit = false;
        // 垂直侧面。
        for (int i = 0; i < obstacle.Points.Count; i++)
        {
            var a = obstacle.Points[i]; var b = obstacle.Points[(i + 1) % obstacle.Points.Count];
            float ex = b.X - a.X, ez = b.Z - a.Z;
            float cross = direction.X * ez - direction.Z * ex;
            if (MathF.Abs(cross) <= 1e-7f) continue;
            float ax = a.X - origin.X, az = a.Z - origin.Z;
            float t = (ax * ez - az * ex) / cross;
            float u = (ax * direction.Z - az * direction.X) / cross;
            if (t < 0f || t > distance || u < 0f || u > 1f) continue;
            float y = origin.Y + direction.Y * t;
            if (y < obstacle.MinY || y > obstacle.MaxY) continue;
            distance = t;
            hit = true;
        }

        // 顶/底面，保证与 MeshCollider 的高度体积一致。
        if (MathF.Abs(direction.Y) > 1e-7f)
        {
            foreach (float yPlane in new[] { obstacle.MinY, obstacle.MaxY })
            {
                float t = (yPlane - origin.Y) / direction.Y;
                if (t < 0f || t > distance) continue;
                if (!IsInsidePolygon(new PolygonPoint(origin.X + direction.X * t, origin.Z + direction.Z * t), obstacle.Points)) continue;
                distance = t;
                hit = true;
            }
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
