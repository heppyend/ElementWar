using UnityEngine;

/// <summary>
/// 入地诊断：Play 后打印每个角色的运行时状态，定位「半身入地」原因。
/// 重点看：
///  - transform.position.y：角色根位置（正常应在地面之上）
///  - 模型子物体（带 SkinnedMeshRenderer 的）相对根的 y 偏移
///  - CharacterController 的 center/height/radius + 底部位置
///  - applyRootMotion 运行时值
///  - Animator controller 名
/// </summary>
public class GroundDiag : MonoBehaviour
{
    void Update()
    {
        if (Time.frameCount % 30 != 0) return; // 每 30 帧打印一次
        if (PlayerController.INSTANCE == null) return;

        foreach (var m in FindObjectsOfType<PlayerModel>())
        {
            bool isControl = m == PlayerController.INSTANCE.currentPlayerModel;
            var anim = m.animator;
            string controllerName = anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null";
            string rootMotion = anim != null ? anim.applyRootMotion.ToString() : "?";

            // 模型子物体（渲染器）相对根的位置
            var renders = m.GetComponentsInChildren<Renderer>(true);
            float lowestY = float.MaxValue;
            Transform lowestT = null;
            foreach (var r in renders)
            {
                if (r == null) continue;
                Vector3 worldY = r.bounds.min;
                if (worldY.y < lowestY) { lowestY = worldY.y; lowestT = r.transform; }
            }

            // CC 底部
            float ccBottom = m.transform.position.y + m.cc.center.y - m.cc.height * 0.5f + m.cc.skinWidth;

            Debug.Log($"[GroundDiag] {m.name} [{(isControl ? "主控" : "随从")}] " +
                      $"posY={m.transform.position.y:F3} " +
                      $"controller={controllerName} applyRoot={rootMotion} " +
                      $"cc: center={m.cc.center} h={m.cc.height:F2} r={m.cc.radius:F2} bottom={ccBottom:F3} " +
                      $"renderer最低={lowestY:F3} ({lowestT?.name}) " +
                      $"状态机={m.GetType().Name}");
        }
    }
}
