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

        private static readonly Obstacle[] Obstacles =
        {
            new(0f, 12f, 1.5f, 0.5f), new(12f, 0f, 1.5f, 0.5f),
            new(0f, -12f, 1.5f, 0.5f), new(-12f, 0f, 1.5f, 0.5f),
        };

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
            foreach (var o in Obstacles)
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
            return result;
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
