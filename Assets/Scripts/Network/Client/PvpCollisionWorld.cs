using System.Collections.Generic;
using UnityEngine;

namespace ElementWar.Net
{
    /// <summary>客户端 PVP 预测碰撞。数据与 Server/ArenaCollisionWorld 对齐。</summary>
    public static class PvpCollisionWorld
    {
        private readonly struct Obstacle
        {
            public readonly float x, z, hx, hz;
            public Obstacle(float x, float z, float hx, float hz) { this.x = x; this.z = z; this.hx = hx; this.hz = hz; }
        }

        private readonly struct PolygonObstacle
        {
            public readonly Vector2[] points;
            public readonly float minY, maxY;

            public PolygonObstacle(float minY, float maxY, params Vector2[] points)
            {
                this.minY = minY;
                this.maxY = maxY;
                this.points = points;
            }
        }

        private readonly struct WalkableTriangle
        {
            public readonly Vector3 a, b, c;
            public readonly bool blocksBelow;
            public WalkableTriangle(Vector3 a, Vector3 b, Vector3 c, bool blocksBelow)
            { this.a = a; this.b = b; this.c = c; this.blocksBelow = blocksBelow; }
        }

        private readonly struct SideWallTriangle
        {
            public readonly Vector3 a, b, c;
            public SideWallTriangle(Vector3 a, Vector3 b, Vector3 c) { this.a = a; this.b = b; this.c = c; }
        }

        private static readonly Obstacle[] DefaultObstacles =
        {
            new(0f, 12f, 1.5f, 0.5f), new(12f, 0f, 1.5f, 0.5f),
            new(0f, -12f, 1.5f, 0.5f), new(-12f, 0f, 1.5f, 0.5f),
        };

        // PVPGame/Cover_4 的实际 ProBuilder 网格底面（世界坐标），不是其包围盒。
        // 轮廓由该 MeshCollider 的外边界提取；客户端预测必须与服务器保持同一数据。
        private static readonly PolygonObstacle[] DefaultPolygonObstacles =
        {
            new(-0.04f, 1.646f,
                new Vector2(-3.696f, 7.365f), new Vector2(3.374f, 7.365f),
                new Vector2(3.374f, 2.624f), new Vector2(7.462f, 2.624f),
                new Vector2(7.462f, -3.411f), new Vector2(3.374f, -3.411f),
                new Vector2(3.374f, -7.397f), new Vector2(-3.696f, -7.397f),
                new Vector2(-3.696f, -3.411f), new Vector2(-7.448f, -3.411f),
                new Vector2(-7.448f, 2.624f), new Vector2(-3.696f, 2.624f))
        };

        private static Obstacle[] activeObstacles = DefaultObstacles;
        private static PolygonObstacle[] activePolygonObstacles = DefaultPolygonObstacles;
        private static WalkableTriangle[] activeWalkableTriangles = System.Array.Empty<WalkableTriangle>();
        private static SideWallTriangle[] activeSideWallTriangles = System.Array.Empty<SideWallTriangle>();
        public static string ActiveProfileHash { get; private set; } = "legacy-fallback";

        /// <summary>加载用户手动烘焙的 Profile；失败时调用方应继续使用安全的旧场景回退数据。</summary>
        public static bool TryUseBakedProfile(out string error)
        {
            if (!PvpArenaCollisionProfileRuntime.TryLoad(out var profile, out error)) return false;
            var polygons = new List<PolygonObstacle>();
            foreach (var volume in profile.movementVolumes)
            {
                if (volume == null || volume.points == null || volume.points.Length < 3) continue;
                var points = new Vector2[volume.points.Length];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector2(volume.points[i].x, volume.points[i].z);
                polygons.Add(new PolygonObstacle(volume.minY, volume.maxY, points));
            }
            if (polygons.Count == 0)
            {
                error = "碰撞 Profile 中没有合法的移动多边形";
                return false;
            }
            activeObstacles = System.Array.Empty<Obstacle>();
            activePolygonObstacles = polygons.ToArray();
            var walkable = new List<WalkableTriangle>();
            foreach (var triangle in profile.walkableTriangles ?? System.Array.Empty<PvpWalkableTriangle>())
                walkable.Add(new WalkableTriangle(new Vector3(triangle.a.x, triangle.a.y, triangle.a.z),
                    new Vector3(triangle.b.x, triangle.b.y, triangle.b.z), new Vector3(triangle.c.x, triangle.c.y, triangle.c.z), triangle.blocksBelow));
            activeWalkableTriangles = walkable.ToArray();
            var sideWalls = new List<SideWallTriangle>();
            foreach (var triangle in profile.sideWallTriangles ?? System.Array.Empty<PvpSideWallTriangle>())
                sideWalls.Add(new SideWallTriangle(new Vector3(triangle.a.x, triangle.a.y, triangle.a.z),
                    new Vector3(triangle.b.x, triangle.b.y, triangle.b.z), new Vector3(triangle.c.x, triangle.c.y, triangle.c.z)));
            activeSideWallTriangles = sideWalls.ToArray();
            ActiveProfileHash = string.IsNullOrEmpty(profile.contentHash) ? "baked-unhashed" : profile.contentHash;
            error = "";
            return true;
        }

        /// <summary>查询当前位置可抵达的最高可走面。只被预测运动调用，数值规则必须与服务器一致。</summary>
        public static bool TryGetWalkableHeight(float x, float z, float currentFeetY, float maxStepHeight, out float surfaceY)
        {
            surfaceY = float.NegativeInfinity;
            float ceiling = currentFeetY + Mathf.Max(0.01f, maxStepHeight);
            bool found = false;
            foreach (var triangle in activeWalkableTriangles)
            {
                if (!TryGetTriangleHeight(triangle, x, z, out float y) || y > ceiling + 0.0001f || y <= surfaceY) continue;
                surfaceY = y;
                found = true;
            }
            return found;
        }

        /// <summary>与服务器一致：当坡面在当前可跨越高度之上，禁止从侧面或底部穿入。</summary>
        public static bool IsBlockedByWalkableSide(float x, float z, float currentFeetY, float maxStepHeight)
        {
            float ceiling = currentFeetY + Mathf.Max(0.01f, maxStepHeight);
            foreach (var triangle in activeWalkableTriangles)
                if (triangle.blocksBelow && TryGetTriangleHeight(triangle, x, z, out float y) && y > ceiling + 0.0001f)
                    return true;
            return false;
        }

        /// <summary>侧面进入不可达坡面时，保留沿边界的位移，而非整步硬停。</summary>
        public static Vector3 ResolveWalkableSideSlide(Vector3 start, Vector3 desired, float currentFeetY, float maxStepHeight)
        {
            if (!IsBlockedByWalkableSide(desired.x, desired.z, currentFeetY, maxStepHeight)) return desired;

            Vector3 best = start;
            float bestDistanceSq = 0f;
            void Consider(Vector3 candidate)
            {
                if (IsBlockedByWalkableSide(candidate.x, candidate.z, currentFeetY, maxStepHeight)) return;
                float d = (new Vector2(candidate.x - start.x, candidate.z - start.z)).sqrMagnitude;
                if (d > bestDistanceSq) { best = candidate; bestDistanceSq = d; }
            }

            // 对齐轴的掩体边缘：先尝试保留单轴，避免常见直角墙体的粘滞。
            Consider(new Vector3(desired.x, desired.y, start.z));
            Consider(new Vector3(start.x, desired.y, desired.z));

            Vector2 delta = new Vector2(desired.x - start.x, desired.z - start.z);
            foreach (var triangle in activeWalkableTriangles)
            {
                if (!triangle.blocksBelow || !TryGetTriangleHeight(triangle, desired.x, desired.z, out float y)
                    || y <= currentFeetY + maxStepHeight + 0.0001f) continue;
                Vector2[] points = { new(triangle.a.x, triangle.a.z), new(triangle.b.x, triangle.b.z), new(triangle.c.x, triangle.c.z) };
                for (int i = 0; i < 3; i++)
                {
                    Vector2 edge = points[(i + 1) % 3] - points[i];
                    if (edge.sqrMagnitude <= 0.000001f) continue;
                    Vector2 tangent = edge.normalized;
                    float along = Vector2.Dot(delta, tangent);
                    Consider(new Vector3(start.x + tangent.x * along, desired.y, start.z + tangent.y * along));
                }
            }
            return best;
        }

        private static bool TryGetTriangleHeight(WalkableTriangle triangle, float x, float z, out float y)
        {
            float denominator = (triangle.b.z - triangle.c.z) * (triangle.a.x - triangle.c.x)
                + (triangle.c.x - triangle.b.x) * (triangle.a.z - triangle.c.z);
            if (Mathf.Abs(denominator) < 0.0000001f) { y = 0f; return false; }
            float u = ((triangle.b.z - triangle.c.z) * (x - triangle.c.x)
                + (triangle.c.x - triangle.b.x) * (z - triangle.c.z)) / denominator;
            float v = ((triangle.c.z - triangle.a.z) * (x - triangle.c.x)
                + (triangle.a.x - triangle.c.x) * (z - triangle.c.z)) / denominator;
            float w = 1f - u - v;
            if (u < -0.0001f || v < -0.0001f || w < -0.0001f) { y = 0f; return false; }
            y = triangle.a.y * u + triangle.b.y * v + triangle.c.y * w;
            return true;
        }

        public static Vector3 ResolveMovement(Vector3 start, Vector3 desired, float radius, float halfExtent, float groundY)
        {
            Vector3 current = start;
            Vector3 delta = desired - current;
            for (int i = 1; i <= 4; i++)
            {
                Vector3 next = current + delta * 0.25f;
                next.x = Mathf.Clamp(next.x, -halfExtent, halfExtent);
                next.z = Mathf.Clamp(next.z, -halfExtent, halfExtent);
                current = ResolveStatic(current, next, radius, halfExtent, groundY);
            }
            return current;
        }

        private static Vector3 ResolveStatic(Vector3 start, Vector3 desired, float radius, float halfExtent, float groundY)
        {
            Vector3 result = desired;
            foreach (var o in activeObstacles)
            {
                float minX = o.x - o.hx - radius, maxX = o.x + o.hx + radius;
                float minZ = o.z - o.hz - radius, maxZ = o.z + o.hz + radius;
                if (start.x >= minX && start.x <= maxX)
                {
                    if (start.z < minZ && result.z >= minZ) result.z = minZ;
                    else if (start.z > maxZ && result.z <= maxZ) result.z = maxZ;
                }
                if (start.z >= minZ && start.z <= maxZ)
                {
                    if (start.x < minX && result.x >= minX) result.x = minX;
                    else if (start.x > maxX && result.x <= maxX) result.x = maxX;
                }

                float dx = result.x - Mathf.Clamp(result.x, o.x - o.hx, o.x + o.hx);
                float dz = result.z - Mathf.Clamp(result.z, o.z - o.hz, o.z + o.hz);
                float d2 = dx * dx + dz * dz;
                if (d2 >= radius * radius) continue;
                if (d2 > 0.000001f)
                {
                    float d = Mathf.Sqrt(d2), push = (radius - d) / d;
                    result.x += dx * push; result.z += dz * push;
                }
                else
                {
                    float left = result.x - (o.x - o.hx), right = o.x + o.hx - result.x;
                    float back = result.z - (o.z - o.hz), front = o.z + o.hz - result.z;
                    float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(back, front));
                    if (start.z <= o.z && min <= back + 0.0001f) result.z = o.z - o.hz - radius;
                    else if (start.z >= o.z && min <= front + 0.0001f) result.z = o.z + o.hz + radius;
                    else if (min <= left) result.x = o.x - o.hx - radius;
                    else result.x = o.x + o.hx + radius;
                }
                result.x = Mathf.Clamp(result.x, -halfExtent, halfExtent);
                result.z = Mathf.Clamp(result.z, -halfExtent, halfExtent);
            }

            foreach (var obstacle in activePolygonObstacles)
                result = ResolvePolygon(result, obstacle, radius, halfExtent);

            // 先用可走三角面的高度场处理从坡侧切入，避免侧墙高度依赖少量水平切片。
            result = ResolveWalkableSideSlide(start, result, result.y - groundY, 0.65f);

            // 侧墙三角面不参与角色移动：它们是由可走面边界派生的双面几何，
            // 若用于移动会把“从坡面向外下行”也当成普通实体墙。坡侧移动只由
            // ResolveWalkableSideSlide 按 blocksBelow 做单向阻断。

            return result;
        }

        private static Vector3 ResolveSideWallTriangle(Vector3 start, Vector3 position, SideWallTriangle wall, float radius, float characterGroundOffset)
        {
            float feetY = position.y - characterGroundOffset;
            // 覆盖角色从脚底到头部的多个高度，避免只命中膝盖附近。
            float[] samples = { feetY + 0.05f, feetY + 0.25f, feetY + 0.5f, feetY + 0.75f,
                feetY + 1.0f, feetY + 1.25f, feetY + 1.5f, feetY + 1.7f };
            foreach (float sampleY in samples)
            {
                if (!TryGetHorizontalSlice(wall, sampleY, out Vector2 a, out Vector2 b)) continue;
                Vector2 point = new Vector2(position.x, position.z);
                Vector2 nearest = ClosestPointOnSegment(point, a, b);
                Vector2 delta = point - nearest;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= radius * radius) continue;
                if (distanceSq > 0.000001f)
                {
                    float distance = Mathf.Sqrt(distanceSq);
                    delta *= (radius - distance) / distance;
                    position.x += delta.x; position.z += delta.y;
                    continue;
                }
                Vector2 edge = b - a;
                if (edge.sqrMagnitude <= 0.000001f) continue;
                edge.Normalize();
                Vector2 normal = new Vector2(-edge.y, edge.x);
                float side = Vector2.Dot(new Vector2(start.x, start.z) - nearest, normal);
                if (Mathf.Abs(side) <= 0.000001f) side = 1f; else side = Mathf.Sign(side);
                position.x += normal.x * side * radius;
                position.z += normal.y * side * radius;
            }
            return position;
        }

        private static bool TryGetHorizontalSlice(SideWallTriangle triangle, float y, out Vector2 first, out Vector2 second)
        {
            var points = new List<Vector2>(2);
            AddSliceIntersection(triangle.a, triangle.b, y, points);
            AddSliceIntersection(triangle.b, triangle.c, y, points);
            AddSliceIntersection(triangle.c, triangle.a, y, points);
            if (points.Count < 2) { first = default; second = default; return false; }
            first = points[0]; second = points[1]; return true;
        }

        private static void AddSliceIntersection(Vector3 a, Vector3 b, float y, List<Vector2> points)
        {
            float dy = b.y - a.y;
            if (Mathf.Abs(dy) <= 0.0000001f) return;
            float t = (y - a.y) / dy;
            if (t < -0.0001f || t > 1.0001f) return;
            Vector2 point = new Vector2(a.x + (b.x - a.x) * t, a.z + (b.z - a.z) * t);
            foreach (var known in points) if ((known - point).sqrMagnitude < 0.00000001f) return;
            points.Add(point);
        }

        private static Vector3 ResolvePolygon(Vector3 position, PolygonObstacle obstacle, float radius, float halfExtent)
        {
            Vector2 point = new Vector2(position.x, position.z);
            bool inside = IsInsidePolygon(point, obstacle.points);
            float nearestDistanceSq = float.MaxValue;
            Vector2 nearest = Vector2.zero;

            for (int i = 0; i < obstacle.points.Length; i++)
            {
                Vector2 a = obstacle.points[i];
                Vector2 b = obstacle.points[(i + 1) % obstacle.points.Length];
                Vector2 candidate = ClosestPointOnSegment(point, a, b);
                float distanceSq = (point - candidate).sqrMagnitude;
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearest = candidate;
                }
            }

            if (!inside && nearestDistanceSq >= radius * radius) return position;

            float distance = Mathf.Sqrt(nearestDistanceSq);
            Vector2 direction;
            if (distance > 0.00001f)
                direction = (point - nearest) / distance;
            else
                direction = OutwardNormal(nearest, obstacle.points);

            // 点在实体内时，point-nearest 指向内侧，因此需要反向退到边界外。
            Vector2 resolved = inside
                ? nearest - direction * radius
                : nearest + direction * radius;
            return new Vector3(Mathf.Clamp(resolved.x, -halfExtent, halfExtent), position.y,
                Mathf.Clamp(resolved.y, -halfExtent, halfExtent));
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float lengthSq = edge.sqrMagnitude;
            if (lengthSq <= 0.000001f) return a;
            return a + edge * Mathf.Clamp01(Vector2.Dot(point - a, edge) / lengthSq);
        }

        private static bool IsInsidePolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) == (b.y > point.y)) continue;
                if (point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static Vector2 OutwardNormal(Vector2 boundaryPoint, Vector2[] polygon)
        {
            // 对凹多边形，向任一极短外侧采样，选落在实体外的一边。
            const float probe = 0.01f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                if ((ClosestPointOnSegment(boundaryPoint, a, b) - boundaryPoint).sqrMagnitude > 0.000001f) continue;
                Vector2 edge = (b - a).normalized;
                Vector2 normal = new Vector2(-edge.y, edge.x);
                if (!IsInsidePolygon(boundaryPoint + normal * probe, polygon)) return normal;
                if (!IsInsidePolygon(boundaryPoint - normal * probe, polygon)) return -normal;
            }
            return Vector2.right;
        }

        public static Vector3 ResolveAgainstPlayers(Vector3 position, IReadOnlyList<Vector3> others, float radius, float halfExtent, float groundY)
        {
            Vector3 result = position;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                bool changed = false;
                for (int i = 0; i < others.Count; i++)
                {
                    Vector3 delta = result - others[i]; delta.y = 0f;
                    float minDistance = radius * 2f, distance = delta.magnitude;
                    if (distance >= minDistance) continue;
                    Vector3 direction = distance > 0.0001f ? delta / distance : Vector3.right;
                    result = ResolveMovement(result, result + direction * (minDistance - distance), radius, halfExtent, groundY);
                    changed = true;
                }
                if (!changed) break;
            }
            return result;
        }
    }
}
