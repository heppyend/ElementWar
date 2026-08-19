using System;

namespace ElementWar.Server;

/// <summary>极简三维向量（服务器不依赖 Unity，自己实现需要的运算）。</summary>
public readonly struct Vec3 : IEquatable<Vec3>
{
    public readonly float X, Y, Z;

    public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public static readonly Vec3 Zero = new(0f, 0f, 0f);

    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>XZ 平面长度。</summary>
    public float MagnitudeXZ => MathF.Sqrt(X * X + Z * Z);

    public Vec3 NormalizedXZ()
    {
        float m = MagnitudeXZ;
        if (m < 1e-6f) return Zero;
        return new Vec3(X / m, 0f, Z / m);
    }

    public Vec3 Normalized()
    {
        float m = Magnitude;
        if (m < 1e-6f) return Zero;
        return new Vec3(X / m, Y / m, Z / m);
    }

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(float s, Vec3 a) => a * s;

    public static float Distance(Vec3 a, Vec3 b) => (a - b).Magnitude;

    /// <summary>世界 XZ 方向 → 偏航角（度）。direction 无需归一化。</summary>
    public static float ToYawDeg(Vec3 direction)
    {
        // 与 Unity 一致：forward=+Z 时 yaw=0
        return MathF.Atan2(direction.X, direction.Z) * (180f / MathF.PI);
    }

    /// <summary>偏航角（度）→ 世界前向（XZ）。</summary>
    public static Vec3 FromYawDeg(float yawDeg)
    {
        float rad = yawDeg * (MathF.PI / 180f);
        return new Vec3(MathF.Sin(rad), 0f, MathF.Cos(rad));
    }

    /// <summary>两个角度间按最短路径的差值（-180~180）。</summary>
    public static float AngleDeltaDeg(float from, float to)
    {
        float d = (to - from) % 360f;
        if (d > 180f) d -= 360f;
        if (d < -180f) d += 360f;
        return d;
    }

    public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is Vec3 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
