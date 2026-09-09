using System;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 静态场景几何的跨运行时数据契约。
    /// Unity 客户端与 .NET 权威服务器必须消费同一份 JSON，禁止再各自维护掩体坐标。
    /// 只包含基础数值，不能引用 UnityEngine 类型。
    /// </summary>
    [Serializable]
    public sealed class PvpArenaCollisionProfile
    {
        public const int CurrentSchemaVersion = 5;

        public int schemaVersion = CurrentSchemaVersion;
        public string sceneName = "";
        public string contentHash = "";
        public PvpMovementVolume[] movementVolumes = Array.Empty<PvpMovementVolume>();
        public PvpBulletTriangle[] bulletTriangles = Array.Empty<PvpBulletTriangle>();
        /// <summary>地面、坡道、平台的可走三角面；客户端与服务器共同做贴地高度查询。</summary>
        public PvpWalkableTriangle[] walkableTriangles = Array.Empty<PvpWalkableTriangle>();
        /// <summary>可走结构的非可走侧面，作为角色侧向实体墙使用。</summary>
        public PvpSideWallTriangle[] sideWallTriangles = Array.Empty<PvpSideWallTriangle>();
        public PvpNavigationSource[] navigationSources = Array.Empty<PvpNavigationSource>();
    }

    /// <summary>角色胶囊在 XZ 平面使用的挤出多边形。顶点必须按轮廓顺序给出。</summary>
    [Serializable]
    public sealed class PvpMovementVolume
    {
        public string id = "";
        public float minY;
        public float maxY;
        public PvpPoint2[] points = Array.Empty<PvpPoint2>();
    }

    [Serializable]
    public struct PvpPoint2
    {
        public float x;
        public float z;
        public PvpPoint2(float x, float z) { this.x = x; this.z = z; }
    }

    /// <summary>子弹精确命中的世界空间三角面；surfaceId 用于客户端选择弹孔效果。</summary>
    [Serializable]
    public sealed class PvpBulletTriangle
    {
        public string surfaceId = "Default";
        public PvpPoint3 a;
        public PvpPoint3 b;
        public PvpPoint3 c;
    }

    [Serializable]
    public sealed class PvpWalkableTriangle
    {
        public string id = "";
        /// <summary>高于当前可跨越高度时，禁止从结构侧面/下方穿入该三角面的 XZ 投影。</summary>
        public bool blocksBelow;
        public PvpPoint3 a;
        public PvpPoint3 b;
        public PvpPoint3 c;
    }

    [Serializable]
    public sealed class PvpSideWallTriangle
    {
        public string id = "";
        public PvpPoint3 a;
        public PvpPoint3 b;
        public PvpPoint3 c;
    }

    [Serializable]
    public struct PvpPoint3
    {
        public float x, y, z;
        public PvpPoint3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    /// <summary>供 PVE NavMesh 审计使用；PVP .NET 服务器不模拟 NavMesh。</summary>
    [Serializable]
    public sealed class PvpNavigationSource
    {
        public string id = "";
        public bool blocksNavigation;
    }
}
