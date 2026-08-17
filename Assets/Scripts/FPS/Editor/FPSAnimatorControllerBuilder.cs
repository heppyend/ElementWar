#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// 一键生成暗夜猎人的 Animator Controller（含混合树）。
    /// 菜单：Tools → FPS → 生成暗夜猎人 Animator Controller
    ///
    /// 混合树（代码驱动参数实现丝滑衔接）：
    ///   - Locomotion（1D，Speed）：Idle(0) → Walk(0.33) → Jog(0.66) → Sprint/Dash(1.0)
    ///   - Air（1D，VerticalSpeed）：Jump_Down(-1) ↔ Jump_Up(1)
    ///
    /// 片段来源：CLazyRunnerActionAnimPack（每个动画一个独立 FBX）。
    /// 找不到的槽位会 Log Warning 并留空，可手动补拖。
    /// 生成成功后会自动为循环片段开启 Loop Time。
    /// </summary>
    public static class FPSAnimatorControllerBuilder
    {
        private const string PACK_ROOT = "Assets/CLazyRunnerActionAnimPack/Animations/";
        private const string OUT_DIR = "Assets/Resource/Animations/FPS";
        private const string OUT_PATH = OUT_DIR + "/FPS_Hunter.controller";

        [MenuItem("Tools/FPS/生成暗夜猎人 Animator Controller")]
        public static void Build()
        {
            // 排除词：方向斜向/后向/带根位移的变体，取纯前向循环片段
            string[] locoExclude = { "Back", "_L", "_R", "45", "Root", "Arc", "Turn", "Stop", "Charge", "Pw" };

            AnimationClip idleClip = FindClip("P1_CLazyMovement/Mvm_Idle", new[] { "Idle_Wait" }, null);
            AnimationClip walkClip = FindClip("P1_CLazyMovement/Mvm_8way_Walk", new[] { "Mvm_Walk" }, locoExclude);
            AnimationClip jogClip = FindClip("P1_CLazyMovement/Mvm_8way_Jog", new[] { "Mvm_Jog" }, locoExclude);
            AnimationClip dashClip = FindClip("P1_CLazyMovement/Mvm_8way_Dash", new[] { "Mvm_Dash" }, locoExclude);
            AnimationClip jumpUp = FindClip("P3_CLazyJump/Jmp_Air_Loop", new[] { "Jump_Up" }, null);
            AnimationClip jumpDown = FindClip("P3_CLazyJump/Jmp_Air_Loop", new[] { "Jump_Down" }, null);
            AnimationClip slideClip = FindClip("P2_CLazyEscape/Esc_Slide_Loop", new[] { "Esc_Slide_Loop" }, new[] { "Mirror", "OffHand" });

            if (idleClip == null || walkClip == null || jogClip == null || dashClip == null)
            {
                Debug.LogError("[FPS Builder] 关键移动片段缺失（Idle/Walk/Jog/Dash），请确认 CLazyRunner 包已正确导入。");
                return;
            }

            // 覆盖旧资产
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OUT_PATH) != null)
                AssetDatabase.DeleteAsset(OUT_PATH);

            // 确保输出目录存在
            if (!AssetDatabase.IsValidFolder(OUT_DIR))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resource/Animations"))
                    AssetDatabase.CreateFolder("Assets/Resource", "Animations");
                AssetDatabase.CreateFolder("Assets/Resource/Animations", "FPS");
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(OUT_PATH);
            controller.AddParameter(FPSAnimatorParams.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(FPSAnimatorParams.VerticalSpeed, AnimatorControllerParameterType.Float);
            controller.AddParameter(FPSAnimatorParams.IsGrounded, AnimatorControllerParameterType.Bool);
            controller.AddParameter(FPSAnimatorParams.IsSprinting, AnimatorControllerParameterType.Bool);
            controller.AddParameter(FPSAnimatorParams.IsAiming, AnimatorControllerParameterType.Bool);
            controller.AddParameter(FPSAnimatorParams.AimMode, AnimatorControllerParameterType.Float);
            controller.AddParameter(FPSAnimatorParams.AimingX, AnimatorControllerParameterType.Float);
            controller.AddParameter(FPSAnimatorParams.AimingY, AnimatorControllerParameterType.Float);

            // —— 状态机 ——
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // —— Locomotion 1D 混合树 ——
            AnimatorState locoState = CreateState(controller, sm, FPSModel.ANIM_LOCOMOTION, new Vector3(300, 0, 0));
            BlendTree locomotionTree = (BlendTree)CreateBlendTree(controller, locoState, "Locomotion",
                FPSAnimatorParams.Speed,
                new[] { idleClip, walkClip, jogClip, dashClip },
                new[] { FPSModel.IDLE_BLEND, FPSModel.WALK_BLEND, FPSModel.JOG_BLEND, FPSModel.SPRINT_BLEND });

            // —— Air 1D 混合树（升/降）——
            AnimatorState airState = CreateState(controller, sm, FPSModel.ANIM_AIR, new Vector3(480, -90, 0));
            BlendTree airTree = (BlendTree)CreateBlendTree(controller, airState, "Air",
                FPSAnimatorParams.VerticalSpeed,
                new[] { jumpDown, jumpUp },
                new[] { -1f, 1f });

            // —— 其余状态 ——
            AnimatorState idleState = CreateState(controller, sm, FPSModel.ANIM_IDLE, new Vector3(120, 0, 0));
            idleState.motion = idleClip;
            SetLoop(idleClip);

            SetLoop(walkClip);
            SetLoop(jogClip);
            SetLoop(dashClip);

            SetLoop(jumpUp);
            SetLoop(jumpDown);

            AnimatorState slideState = CreateState(controller, sm, FPSModel.ANIM_SLIDE, new Vector3(300, -170, 0));
            if (slideClip != null)
            {
                slideState.motion = slideClip;
                SetLoop(slideClip);
            }

            AnimatorState aimState = CreateState(controller, sm, FPSModel.ANIM_AIM, new Vector3(480, 90, 0));
            aimState.motion = locomotionTree; // 占位：无武器瞄准动画前复用 Locomotion（低速段）

            sm.defaultState = idleState;

            // —— 过渡（主要靠代码 CrossFade，这些作为兜底保证控制器自洽）——
            var idToLoco = idleState.AddTransition(locoState);
            idToLoco.hasExitTime = false;
            idToLoco.duration = 0.15f;
            idToLoco.AddCondition(AnimatorConditionMode.Greater, 0.05f, FPSAnimatorParams.Speed);

            var locoToIdle = locoState.AddTransition(idleState);
            locoToIdle.hasExitTime = false;
            locoToIdle.duration = 0.15f;
            locoToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, FPSAnimatorParams.Speed);

            var locoToAir = locoState.AddTransition(airState);
            locoToAir.hasExitTime = false;
            locoToAir.duration = 0.1f;
            locoToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, FPSAnimatorParams.IsGrounded);

            var airToLoco = airState.AddTransition(locoState);
            airToLoco.hasExitTime = false;
            airToLoco.duration = 0.12f;
            airToLoco.AddCondition(AnimatorConditionMode.If, 0f, FPSAnimatorParams.IsGrounded);

            // AnyState → Aim（任意状态按右键进入瞄准）
            var anyToAim = sm.AddAnyStateTransition(aimState);
            anyToAim.hasExitTime = false;
            anyToAim.duration = 0.15f;
            anyToAim.AddCondition(AnimatorConditionMode.If, 0f, FPSAnimatorParams.IsAiming);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FPS Builder] 生成完成: {OUT_PATH}\n" +
                      $"Idle={idleClip?.name} Walk={walkClip?.name} Jog={jogClip?.name} Sprint={dashClip?.name}\n" +
                      $"JumpUp={jumpUp?.name} JumpDown={jumpDown?.name} Slide={slideClip?.name}");
        }

        /// <summary>在指定包里按 include/exclude 子串匹配动画片段</summary>
        private static AnimationClip FindClip(string folder, string[] include, string[] exclude)
        {
            string assetFolder = PACK_ROOT + folder;
            if (!AssetDatabase.IsValidFolder(assetFolder))
            {
                Debug.LogWarning($"[FPS Builder] 找不到文件夹: {assetFolder}");
                return null;
            }

            // 关键：Application.dataPath 已含 Assets，包内子目录不能带 "Assets/" 前缀，否则路径重复
            // 包内相对 Assets 的路径 = CLazyRunnerActionAnimPack/Animations/ + folder
            string relFolder = "CLazyRunnerActionAnimPack/Animations/" + folder;
            string fullFolder = Path.Combine(Application.dataPath, relFolder.Replace('/', Path.DirectorySeparatorChar));
            string[] files = Directory.GetFiles(fullFolder, "*.FBX");
            foreach (string file in files)
            {
                string relPath = "Assets/" + file.Substring(Application.dataPath.Length + 1).Replace('\\', '/');
                foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(relPath))
                {
                    if (obj is AnimationClip clip && Matches(clip.name, include, exclude))
                        return clip;
                }
            }

            Debug.LogWarning($"[FPS Builder] 在 {assetFolder} 未匹配到片段，include=[{string.Join(",", include)}]（可手动补拖）");
            return null;
        }

        private static bool Matches(string name, string[] include, string[] exclude)
        {
            if (exclude != null)
                foreach (string ex in exclude)
                    if (name.Contains(ex)) return false;
            foreach (string inc in include)
                if (!name.Contains(inc)) return false;
            return true;
        }

        /// <summary>创建状态</summary>
        private static AnimatorState CreateState(AnimatorController controller, AnimatorStateMachine sm, string name, Vector3 pos)
        {
            return sm.AddState(name, pos);
        }

        /// <summary>
        /// 创建 1D 混合树并挂到状态。
        /// 兼容 Unity 2022：BlendTree 不是 ScriptableObject，需用 new BlendTree()（CreateInstance 无法实例化）。
        /// clips 与 thresholds 一一对应；null 片段跳过。
        /// </summary>
        private static Motion CreateBlendTree(AnimatorController controller, AnimatorState state, string name,
            string parameter, AnimationClip[] clips, float[] thresholds)
        {
            BlendTree tree = new BlendTree();
            tree.name = name;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = parameter;
            tree.useAutomaticThresholds = false;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                float t = thresholds != null && i < thresholds.Length ? thresholds[i] : (float)i / (clips.Length - 1);
                tree.AddChild(clips[i], t);
            }

            AssetDatabase.AddObjectToAsset(tree, controller);
            state.motion = tree;
            return tree;
        }

        private static void SetLoop(AnimationClip clip)
        {
            if (clip == null) return;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime)
            {
                settings.loopTime = true;
                settings.loopBlend = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
        }
    }
}
#endif
