using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ElementWar.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 将 PvpSceneGeometryAuthoring 显式标记的玩法几何烘焙为跨客户端/服务器 JSON。
/// 本工具绝不修改场景对象、Collider、Prefab 或 NavMesh；只在用户手动点击菜单时写出 Profile 文件。
/// </summary>
public static class PvpArenaCollisionProfileBaker
{
    private const string AssetPath = "Assets/Resources/PVP/PvpArenaCollisionProfile.json";

    [MenuItem("Tools/PVP/校验场景玩法几何（不写入）")]
    public static void ValidateOnly()
    {
        BuildProfile(writeAsset: false, out _);
    }

    [MenuItem("Tools/PVP/烘焙场景玩法碰撞 Profile（客户端 + 服务器）")]
    public static void Bake()
    {
        if (!BuildProfile(writeAsset: true, out string message))
            Debug.LogError($"[PvpGeometry] 烘焙失败：{message}");
    }

    private static bool BuildProfile(bool writeAsset, out string message)
    {
        if (Application.isPlaying)
        {
            message = "必须在退出 Play Mode 后烘焙。";
            return false;
        }

        var authorings = UnityEngine.Object.FindObjectsOfType<PvpSceneGeometryAuthoring>(true)
            .Where(x => x.gameObject.scene == SceneManager.GetActiveScene()).OrderBy(x => PathOf(x.transform)).ToArray();
        if (authorings.Length == 0)
        {
            message = "当前场景没有 PvpSceneGeometryAuthoring。请先在每个静态玩法物体根节点添加该组件；默认会自动收集其子节点 Collider。";
            return false;
        }

        var profile = new PvpArenaCollisionProfile { sceneName = SceneManager.GetActiveScene().name };
        var movement = new List<PvpMovementVolume>();
        var triangles = new List<PvpBulletTriangle>();
        var walkable = new List<PvpWalkableTriangle>();
        var sideWalls = new List<PvpSideWallTriangle>();
        var nav = new List<PvpNavigationSource>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var usedColliders = new HashSet<Collider>();
        var errors = new List<string>();

        foreach (var authoring in authorings)
        {
            string id = authoring.GeometryId;
            if (!usedIds.Add(id)) { errors.Add($"重复 geometryId：{id}（{PathOf(authoring.transform)}）"); continue; }
            nav.Add(new PvpNavigationSource { id = id, blocksNavigation = authoring.BlocksNavigation });

            foreach (var box in authoring.MovementProxies ?? Array.Empty<BoxCollider>())
            {
                if (box == null) { errors.Add($"{id} 有空 movementProxies 引用"); continue; }
                if (!usedColliders.Add(box)) { errors.Add($"Collider 被重复引用：{PathOf(box.transform)}"); continue; }
                movement.Add(BuildMovementVolume(id + "/" + box.name, box));
                if (authoring.UseMovementProxiesForBullets) AddBoxTriangles(triangles, authoring.SurfaceId, box);
            }

            foreach (var meshCollider in authoring.BulletSurfaces ?? Array.Empty<MeshCollider>())
            {
                if (meshCollider == null) { errors.Add($"{id} 有空 bulletSurfaces 引用"); continue; }
                if (meshCollider.sharedMesh == null) { errors.Add($"{PathOf(meshCollider.transform)} 没有 sharedMesh"); continue; }
                AddMeshTriangles(triangles, authoring.SurfaceId, meshCollider.sharedMesh, meshCollider.transform);
                if (authoring.Role == PvpSceneGeometryRole.Walkable)
                {
                    AddMeshWalkableTriangles(walkable, id + "/" + meshCollider.name, authoring.WalkableSurfaceBlocksBelow,
                        meshCollider.sharedMesh, meshCollider.transform);
                    AddMeshSideWallTriangles(sideWalls, id + "/" + meshCollider.name, meshCollider.sharedMesh, meshCollider.transform);
                }
            }
        }

        if (movement.Count == 0) errors.Add("没有有效的 BoxCollider 角色碰撞代理。");
        if (triangles.Count == 0) errors.Add("没有子弹阻挡表面：请启用 movement proxy 子弹阻挡或指定 MeshCollider。");
        if (errors.Count > 0)
        {
            message = string.Join("\n", errors);
            Debug.LogError("[PvpGeometry] 校验失败：\n" + message);
            return false;
        }

        profile.movementVolumes = movement.ToArray();
        profile.bulletTriangles = triangles.ToArray();
        profile.walkableTriangles = walkable.ToArray();
        profile.sideWallTriangles = sideWalls.ToArray();
        profile.navigationSources = nav.ToArray();
        string beforeHash = JsonUtility.ToJson(profile, false);
        profile.contentHash = Sha256(beforeHash);
        string json = JsonUtility.ToJson(profile, true);

        message = $"场景={profile.sceneName}，角色代理={movement.Count}，坡面侧墙={sideWalls.Count}，子弹三角面={triangles.Count}，可走三角面={walkable.Count}，导航来源={nav.Count}，hash={profile.contentHash}";
        if (!writeAsset)
        {
            Debug.Log("[PvpGeometry] 校验通过（未写入）：" + message);
            return true;
        }

        string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[PvpGeometry] 烘焙完成：" + message + "\n输出：" + AssetPath);
        return true;
    }

    private static PvpMovementVolume BuildMovementVolume(string id, BoxCollider box)
    {
        Vector3 center = box.center;
        Vector3 ext = box.size * 0.5f;
        Vector3[] corners =
        {
            box.transform.TransformPoint(center + new Vector3(-ext.x, -ext.y, -ext.z)),
            box.transform.TransformPoint(center + new Vector3(-ext.x, -ext.y, ext.z)),
            box.transform.TransformPoint(center + new Vector3(ext.x, -ext.y, ext.z)),
            box.transform.TransformPoint(center + new Vector3(ext.x, -ext.y, -ext.z)),
        };
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            float py = box.transform.TransformPoint(center + Vector3.Scale(ext, new Vector3(x, y, z))).y;
            minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
        }
        return new PvpMovementVolume
        {
            id = id, minY = minY, maxY = maxY,
            points = corners.Select(p => new PvpPoint2(p.x, p.z)).ToArray()
        };
    }

    private static void AddBoxTriangles(List<PvpBulletTriangle> output, string surfaceId, BoxCollider box)
    {
        Vector3 center = box.center, e = box.size * 0.5f;
        Vector3[] p =
        {
            box.transform.TransformPoint(center + new Vector3(-e.x,-e.y,-e.z)), box.transform.TransformPoint(center + new Vector3(e.x,-e.y,-e.z)),
            box.transform.TransformPoint(center + new Vector3(e.x,-e.y,e.z)), box.transform.TransformPoint(center + new Vector3(-e.x,-e.y,e.z)),
            box.transform.TransformPoint(center + new Vector3(-e.x,e.y,-e.z)), box.transform.TransformPoint(center + new Vector3(e.x,e.y,-e.z)),
            box.transform.TransformPoint(center + new Vector3(e.x,e.y,e.z)), box.transform.TransformPoint(center + new Vector3(-e.x,e.y,e.z)),
        };
        int[] indices = { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7 };
        for (int i = 0; i < indices.Length; i += 3) AddTriangle(output, surfaceId, p[indices[i]], p[indices[i + 1]], p[indices[i + 2]]);
    }

    private static void AddMeshTriangles(List<PvpBulletTriangle> output, string surfaceId, Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        for (int i = 0; i + 2 < indices.Length; i += 3)
            AddTriangle(output, surfaceId, transform.TransformPoint(vertices[indices[i]]), transform.TransformPoint(vertices[indices[i + 1]]), transform.TransformPoint(vertices[indices[i + 2]]));
    }

    private static void AddMeshWalkableTriangles(List<PvpWalkableTriangle> output, string id, bool blocksBelow, Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(vertices[indices[i]]);
            Vector3 b = transform.TransformPoint(vertices[indices[i + 1]]);
            Vector3 c = transform.TransformPoint(vertices[indices[i + 2]]);
            // 只烘焙朝上的可走面；侧壁/底面既不会用于角色贴地，也不浪费服务器查询。
            if (Vector3.Cross(b - a, c - a).normalized.y < 0.45f) continue;
            output.Add(new PvpWalkableTriangle
            {
                id = id,
                blocksBelow = blocksBelow,
                a = new PvpPoint3(a.x, a.y, a.z), b = new PvpPoint3(b.x, b.y, b.z), c = new PvpPoint3(c.x, c.y, c.z)
            });
        }
    }

    private static void AddMeshSideWallTriangles(List<PvpSideWallTriangle> output, string id, Mesh mesh, Transform transform)
    {
        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        var walkableEdges = new Dictionary<string, (Vector3 a, Vector3 b, int count)>();
        float baseY = float.MaxValue;

        foreach (Vector3 vertex in vertices)
            baseY = Mathf.Min(baseY, transform.TransformPoint(vertex).y);

        // 只出现一次的可走面边才是外边界；共享边不能生成墙，避免坡面接缝被误判为墙。
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(vertices[indices[i]]);
            Vector3 b = transform.TransformPoint(vertices[indices[i + 1]]);
            Vector3 c = transform.TransformPoint(vertices[indices[i + 2]]);
            if (Vector3.Cross(b - a, c - a).normalized.y < 0.45f) continue;

            AddWalkableEdge(a, b, walkableEdges);
            AddWalkableEdge(b, c, walkableEdges);
            AddWalkableEdge(c, a, walkableEdges);
        }

        foreach (var edge in walkableEdges.Values)
        {
            if (edge.count != 1) continue;
            AddExtrudedBoundaryWall(output, id, edge.a, edge.b, baseY);
        }
    }

    private static void AddWalkableEdge(Vector3 a, Vector3 b, Dictionary<string, (Vector3 a, Vector3 b, int count)> edges)
    {
        string key = EdgeKey(a, b);
        if (edges.TryGetValue(key, out var edge))
            edges[key] = (edge.a, edge.b, edge.count + 1);
        else
            edges.Add(key, (a, b, 1));
    }

    private static void AddExtrudedBoundaryWall(List<PvpSideWallTriangle> output, string id, Vector3 a, Vector3 b, float baseY)
    {
        Vector3 baseA = new Vector3(a.x, baseY, a.z);
        Vector3 baseB = new Vector3(b.x, baseY, b.z);
        if ((a - baseA).sqrMagnitude <= 0.000001f && (b - baseB).sqrMagnitude <= 0.000001f) return;

        output.Add(new PvpSideWallTriangle
        {
            id = id,
            a = new PvpPoint3(a.x, a.y, a.z),
            b = new PvpPoint3(b.x, b.y, b.z),
            c = new PvpPoint3(baseB.x, baseB.y, baseB.z)
        });
        output.Add(new PvpSideWallTriangle
        {
            id = id,
            a = new PvpPoint3(a.x, a.y, a.z),
            b = new PvpPoint3(baseB.x, baseB.y, baseB.z),
            c = new PvpPoint3(baseA.x, baseA.y, baseA.z)
        });
    }

    private static string EdgeKey(Vector3 a, Vector3 b)
    {
        string PointKey(Vector3 p) => $"{Mathf.RoundToInt(p.x * 10000f)},{Mathf.RoundToInt(p.y * 10000f)},{Mathf.RoundToInt(p.z * 10000f)}";
        string first = PointKey(a), second = PointKey(b);
        return string.CompareOrdinal(first, second) <= 0 ? first + "|" + second : second + "|" + first;
    }

    private static void AddTriangle(List<PvpBulletTriangle> output, string surfaceId, Vector3 a, Vector3 b, Vector3 c) => output.Add(new PvpBulletTriangle
    {
        surfaceId = surfaceId,
        a = new PvpPoint3(a.x, a.y, a.z), b = new PvpPoint3(b.x, b.y, b.z), c = new PvpPoint3(c.x, c.y, c.z)
    });

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }

    private static string PathOf(Transform t)
    {
        var names = new List<string>();
        while (t != null) { names.Insert(0, t.name); t = t.parent; }
        return string.Join("/", names);
    }
}
