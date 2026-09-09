using UnityEngine;

namespace ElementWar.Net
{
    public enum PvpSceneGeometryRole
    {
        /// <summary>墙、箱子、掩体：角色不可穿越。</summary>
        Blocking,
        /// <summary>地面、斜坡、平台：当前烘焙为子弹面和导航来源，不作为平面角色阻挡体。</summary>
        Walkable,
    }

    /// <summary>
    /// 挂在一个静态场景物体根节点上，显式声明其玩法几何来源。
    /// movementProxies 用低复杂度 BoxCollider 构成角色碰撞；bulletSurfaces 用 MeshCollider 提供精确子弹表面。
    /// 此组件本身不在运行时修改 Collider、NavMesh 或 Scene。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PvpSceneGeometryAuthoring : MonoBehaviour
    {
        [Header("身份")]
        [SerializeField] private string geometryId = "";
        [SerializeField] private string surfaceId = "Concrete";
        [SerializeField] private PvpSceneGeometryRole role = PvpSceneGeometryRole.Blocking;

        [Header("自动收集（推荐）")]
        [Tooltip("自动递归收集子节点的 BoxCollider 与 MeshCollider；特殊物体才关闭后改用下方手工数组。")]
        [SerializeField] private bool autoCollectChildColliders = true;

        [Header("角色碰撞 - Blocking 时使用 BoxCollider 代理")]
        [SerializeField] private BoxCollider[] movementProxies = System.Array.Empty<BoxCollider>();

        [Header("子弹阻挡与弹孔 - 精确 MeshCollider")]
        [SerializeField] private MeshCollider[] bulletSurfaces = System.Array.Empty<MeshCollider>();
        [SerializeField] private bool useMovementProxiesForBullets = true;

        [Header("PVE 导航审计")]
        [SerializeField] private bool blocksNavigation = true;

        [Header("可走结构侧面")]
        [Tooltip("Walkable 结构开启后：角色不能从坡腰、平台侧面或底部穿入；只能从高度连续的坡脚上行。Ground 保持关闭。")]
        [SerializeField] private bool walkableSurfaceBlocksBelow = true;

        public string GeometryId => string.IsNullOrWhiteSpace(geometryId) ? gameObject.name : geometryId.Trim();
        public string SurfaceId => string.IsNullOrWhiteSpace(surfaceId) ? "Default" : surfaceId.Trim();
        public PvpSceneGeometryRole Role => role;
        public BoxCollider[] MovementProxies => role == PvpSceneGeometryRole.Blocking
            ? autoCollectChildColliders ? GetComponentsInChildren<BoxCollider>(true) : movementProxies
            : System.Array.Empty<BoxCollider>();
        public MeshCollider[] BulletSurfaces => autoCollectChildColliders
            ? GetComponentsInChildren<MeshCollider>(true) : bulletSurfaces;
        public bool UseMovementProxiesForBullets => useMovementProxiesForBullets;
        public bool BlocksNavigation => role == PvpSceneGeometryRole.Blocking && blocksNavigation;
        public bool WalkableSurfaceBlocksBelow => role == PvpSceneGeometryRole.Walkable && walkableSurfaceBlocksBelow;
    }
}
