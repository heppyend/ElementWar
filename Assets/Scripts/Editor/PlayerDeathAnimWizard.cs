using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementWarEditor
{
    /// <summary>
    /// 接入角色死亡动画（2026-08-21）：
    /// 1. 从 MotusMan（Rifle_01_v25）取 4 方向死亡 clip（B/L/F/R，Humanoid 可 retarget）
    /// 2. 给 TPS_Movement / Hunter_Parkour 控制器加 4 个普通死亡状态 Dead_B/L/F/R（各挂一个方向 clip）。
    ///    —— 不用混合树（Unity 2022.3 无 CreateBlendTree 系 API），方向由 PlayerModel.Die() 随机挑状态名
    /// 3. 把 PlayerModel.deadAnimationName 置 "Dead_B"（prefab 资产 + 全部场景；网络死亡/远端复用固定后倒）
    /// 幂等可重跑。Tools/玩家 菜单。
    /// </summary>
    public static class PlayerDeathAnimWizard
    {
        /// <summary>网络死亡（PVP ApplyDeathNetwork / RemoteAvatar）复用的固定死亡状态：向后倒。</summary>
        private const string StateName = "Dead_B";
        /// <summary>4 个死亡状态名，与 DeathFbxPaths 一一对应（B 后 / L 左 / F 前 / R 右）。</summary>
        private static readonly string[] StateNames = { "Dead_B", "Dead_L", "Dead_F", "Dead_R" };

        private static readonly string[] ControllerPaths =
        {
            "Assets/Resource/Animations/Player/TPS_Movement.controller",
            "Assets/Resource/Animations/Player/Hunter_Parkour.controller",
        };

        // 4 方向死亡（B 后 / L 左 / F 前 / R 右，顺序与 StateNames 一一对应）
        private static readonly string[] DeathFbxPaths =
        {
            "Assets/Rifle_01_v25/FBX/Animation/MIL2_M3_W2_Stand_Relaxed_Death_B.fbx",
            "Assets/Rifle_01_v25/FBX/Animation/MIL2_M3_W2_Stand_Relaxed_Death_L.fbx",
            "Assets/Rifle_01_v25/FBX/Animation/MIL2_M3_W2_Stand_Relaxed_Death_F.fbx",
            "Assets/Rifle_01_v25/FBX/Animation/MIL2_M3_W2_Stand_Relaxed_Death_R.fbx",
        };

        private static readonly string[] PrefabPaths =
        {
            "Assets/Resource/Prefabs/Lumine FBX.prefab",
            "Assets/Resource/Prefabs/Pilot Furina.prefab",
            "Assets/Resource/Prefabs/Hunter.prefab",
        };

        [MenuItem("Tools/玩家/接入角色死亡动画（Dead 状态 + 4 方向）")]
        public static void AddDeathAnimation()
        {
            // ① 加载 4 方向死亡 clip
            var clips = new List<AnimationClip>();
            foreach (var path in DeathFbxPaths)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault();
                if (clip == null)
                {
                    Debug.LogError($"[PlayerDeath] 找不到死亡 clip：{path}");
                    return;
                }
                clips.Add(clip);
                Debug.Log($"[PlayerDeath] 加载死亡 clip：{clip.name}（时长 {clip.length:F2}s）");
            }

            // ①.5 关闭死亡 clip 的 Loop Time：FBX 自动导入的 clip 默认 loopTime=true（循环），
            // 死亡动画不该循环（循环导致 normalizedTime 永不达标，播放逻辑要卡到超时）。
            // ⚠️ SetAnimationClipSettings 对 FBX 内嵌 clip 是内存/库级修改，FBX 重新导入会还原——重跑本向导即恢复。
            foreach (var clip in clips)
            {
                try
                {
                    var cs = AnimationUtility.GetAnimationClipSettings(clip);
                    if (cs.loopTime)
                    {
                        cs.loopTime = false;
                        AnimationUtility.SetAnimationClipSettings(clip, cs);
                        Debug.Log($"[PlayerDeath] {clip.name} Loop Time 已关闭");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PlayerDeath] 设置 {clip.name} 不循环失败（{e.Message}）");
                }
            }

            // ② 控制器加 Dead 状态：先处理已知控制器（prefab 用的），再扫描场景收集 PlayerModel 实际使用的控制器。
            //    ⚠️ 场景（PVEGame 已 Unpack）里的角色可能引用其它控制器（如旧 Player.controller），必须按场景实际引用来加。
            var processed = new HashSet<string>();
            foreach (var controllerPath in ControllerPaths)
            {
                AddDeadState(controllerPath, clips);
                processed.Add(controllerPath);
            }

            // ③ deadAnimationName 置 "Dead_B"（prefab + 场景，网络死亡复用）+ 收集场景实际控制器
            int set = 0;
            foreach (var prefabPath in PrefabPaths)
                set += SetDeadAnimationNameInPrefab(prefabPath);
            var sceneControllers = new List<AnimatorController>();
            set += ProcessScenes(sceneControllers);

            foreach (var ac in sceneControllers)
            {
                string path = AssetDatabase.GetAssetPath(ac);
                if (path.StartsWith("Assets/") && !processed.Contains(path))
                {
                    AddDeadState(path, clips);
                    processed.Add(path);
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[PlayerDeath] 完成：{processed.Count} 个控制器加 Dead 状态；{set} 个 PlayerModel.deadAnimationName 置为 {StateName}");
        }

        /// <summary>给控制器加 4 个普通死亡状态 Dead_B/L/F/R（各挂一个方向 clip）。幂等：已存在则跳过。</summary>
        private static void AddDeadState(string controllerPath, List<AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerDeath] 找不到控制器：{controllerPath}");
                return;
            }

            var sm = controller.layers[0].stateMachine;
            int added = 0;
            for (int i = 0; i < StateNames.Length; i++)
            {
                string stateName = StateNames[i];
                var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == stateName).state;
                if (existing != null)
                {
                    // 幂等：已有状态只补 speed（死亡 clip 3.5~4s 偏长，2x 加速到 ~2s 播完；协程用 length/speed 算实际时长）
                    if (existing.speed != 2f)
                    {
                        existing.speed = 2f;
                        added++;
                    }
                    continue;
                }
                var st = sm.AddState(stateName);
                st.motion = clips[i];
                st.speed = 2f;
                added++;
            }

            EditorUtility.SetDirty(controller);
            Debug.Log($"[PlayerDeath] {controllerPath} 已添加 {added} 个死亡状态：{string.Join("/", StateNames)}");
        }

        private static int SetDeadAnimationNameInPrefab(string prefabPath)
        {
            int n = 0;
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var pm in contents.GetComponentsInChildren<PlayerModel>(true))
            {
                if (pm.deadAnimationName != StateName)
                {
                    pm.deadAnimationName = StateName;
                    n++;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return n;
        }

        /// <summary>
        /// 扫描 Build 设置 + Assets/Scenes 全部场景：① 把 PlayerModel.deadAnimationName 置 Dead_B；
        /// ② 收集每个 PlayerModel 的 Animator 实际使用的控制器（给 AddDeathAnimation 加 Dead 状态用）。
        /// Additive 不打断当前场景。
        /// </summary>
        private static int ProcessScenes(List<AnimatorController> outControllers)
        {
            int n = 0;
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToList();
            scenes.AddRange(Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories));
            var activePath = SceneManager.GetActiveScene().path;

            foreach (var scenePath in scenes.Distinct())
            {
                try
                {
                    if (scenePath == activePath)
                    {
                        n += ReplaceInRoots(SceneManager.GetActiveScene().GetRootGameObjects(), outControllers);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                    else
                    {
                        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                        n += ReplaceInRoots(scene.GetRootGameObjects(), outControllers);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PlayerDeath] 处理场景失败（跳过）：{scenePath} {e.Message}");
                }
            }
            return n;
        }

        private static int ReplaceInRoots(GameObject[] roots, List<AnimatorController> outControllers)
        {
            int n = 0;
            foreach (var root in roots)
            {
                foreach (var pm in root.GetComponentsInChildren<PlayerModel>(true))
                {
                    // 收集实际使用的控制器
                    var anim = pm.GetComponent<Animator>();
                    if (anim != null && anim.runtimeAnimatorController is AnimatorController ac && !outControllers.Contains(ac))
                    {
                        outControllers.Add(ac);
                        Debug.Log($"[PlayerDeath] 场景角色 {pm.name} 使用控制器：{ac.name}");
                    }

                    if (pm.deadAnimationName != StateName)
                    {
                        pm.deadAnimationName = StateName;
                        n++;
                        // 场景对象若是 prefab 实例则记录覆盖（Game 已 Unpack 时无副作用）
                        if (PrefabUtility.GetPrefabInstanceStatus(pm.gameObject) != PrefabInstanceStatus.NotAPrefab)
                            PrefabUtility.RecordPrefabInstancePropertyModifications(pm);
                    }
                }
            }
            return n;
        }
    }
}
