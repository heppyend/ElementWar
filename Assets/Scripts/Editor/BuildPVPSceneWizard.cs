using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using ElementWar.Net;

/// <summary>
/// 一键搭建 PVP 场景（PVPGame.unity，幂等可重跑）：
///   竞技场地面 + 掩体 + 出生点 + MainCamera(PVPCameraFollow) + Networking(NetClient/PlayerSpawner/PVPHealthUI/NetworkLauncher)
///   + Managers(MonoManager/UIManager/WorldSpaceCanvas) + EventSystem。
/// 角色预制体自动取 荧(Lumine FBX.prefab) / 芙宁娜(Pilot Furina.prefab)。
/// 菜单：Tools → 玩家 → 搭建 PVP 场景（PVPGame）
/// </summary>
public static class BuildPVPSceneWizard
{
    private const string ScenePath = "Assets/Scenes/PVPGame.unity";

    [MenuItem("Tools/玩家/搭建 PVP 场景（PVPGame）")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 打开 PVP 场景（不存在则新建）
        Scene scene;
        if (System.IO.File.Exists(ScenePath))
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // 清掉上次搭建的物体（幂等）
        foreach (string name in new[] { "Arena", "SpawnPoint_1", "SpawnPoint_2", "Networking", "Managers", "PVP_EventSystem" })
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        BuildArena();
        BuildSpawns();
        BuildCamera();
        BuildNetworking();
        BuildManagers();
        BuildEventSystem();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("✅ PVP 场景搭建完成，已保存。运行：启动 .NET 服务器 → Play 本场景（连 127.0.0.1:7777）");
    }

    private static void BuildArena()
    {
        var arena = new GameObject("Arena");
        // 地面
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(arena.transform);
        ground.transform.localScale = new Vector3(80f, 0.2f, 80f);
        ground.transform.position = new Vector3(0f, -0.1f, 0f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = CreateMat(new Color(0.35f, 0.35f, 0.4f));
        // 四块掩体（纯视觉，服务器无关卡碰撞）
        for (int i = 0; i < 4; i++)
        {
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = $"Cover_{i}";
            cover.transform.SetParent(arena.transform);
            cover.transform.localScale = new Vector3(3f, 2f, 1f);
            float angle = i * 90f;
            float r = 12f;
            cover.transform.position = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * r, 1f, Mathf.Cos(angle * Mathf.Deg2Rad) * r);
            cover.GetComponent<MeshRenderer>().sharedMaterial = CreateMat(new Color(0.5f, 0.4f, 0.3f));
        }
    }

    private static void BuildSpawns()
    {
        var s1 = new GameObject("SpawnPoint_1");
        s1.transform.position = new Vector3(0f, 0f, 6f);
        s1.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 面朝 -Z（对向出生点 2）
        var s2 = new GameObject("SpawnPoint_2");
        s2.transform.position = new Vector3(0f, 0f, -6f);
        s2.transform.rotation = Quaternion.identity;            // 面朝 +Z
    }

    private static void BuildCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }
        cam.transform.position = new Vector3(0f, 6f, -10f);
        // 相机交给 PVPCameraRig（运行时自建双 FreeLook），不再挂简化轨道相机
    }

    private static void BuildNetworking()
    {
        var go = new GameObject("Networking");
        go.AddComponent<NetClient>();
        go.AddComponent<NetworkLauncher>();
        var spawner = go.AddComponent<PlayerSpawner>();
        go.AddComponent<PVPHealthUI>();

        // 角色预制体：0=荧 1=芙宁娜
        var lumine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resource/Prefabs/Lumine FBX.prefab");
        var furina = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resource/Prefabs/Pilot Furina.prefab");
        spawner.characterPrefabs = new[] { lumine, furina };

        // 出生点
        spawner.spawnPoints = new[]
        {
            GameObject.Find("SpawnPoint_1").transform,
            GameObject.Find("SpawnPoint_2").transform,
        };
    }

    private static void BuildManagers()
    {
        var go = new GameObject("Managers");
        go.AddComponent<MonoManager>();
        var ui = go.AddComponent<UIManager>();

        var canvasGo = new GameObject("WorldSpaceCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasGo.transform.SetParent(go.transform);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasGo.transform.localScale = Vector3.one * 0.01f;
        ui.WorldSpaceCanvas = canvasGo;
    }

    private static void BuildEventSystem()
    {
        var go = new GameObject("PVP_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static Material CreateMat(Color c)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat != null) mat.color = c;
        return mat;
    }
}
