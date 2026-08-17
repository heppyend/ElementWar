using UnityEngine;

namespace FPS
{
    /// <summary>
    /// FPS 运行时诊断（Play 模式每帧打印）。挂在 Hunter 上，用后删除。
    /// 定位「进入瞄准就卡帧+平移+上下抖+穿模」：打印状态机状态 / horizontalVelocity / speedBlend / cc 信息。
    /// </summary>
    public class FPSRuntimeDiag : MonoBehaviour
    {
        [Tooltip("打印间隔（秒）")]
        public float interval = 0.1f;

        private FPSModel model;
        private FPSController controller;
        private float timer;

        private void Awake()
        {
            model = GetComponent<FPSModel>();
            controller = GetComponent<FPSController>();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;

            if (model == null || model.cc == null) return;

            string state = model.GetType().Name;
            // 通过反射读 currentState（私有字段）
            var field = typeof(FPSModel).GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) state = field.GetValue(model)?.ToString() ?? "?";

            string animState = "?";
            if (model.animator != null)
            {
                var info = model.animator.GetCurrentAnimatorStateInfo(0);
                animState = $"{info.shortNameHash} (t={info.normalizedTime:F2})";
            }

            Debug.Log($"[FPSDiag] state={state} | hVel={model.horizontalVelocity} | speedBlend={model.speedBlend:F2} | " +
                      $"ccGrounded={model.cc.isGrounded} | ccPos={model.transform.position} | anim={animState} | " +
                      $"isAiming={controller?.isAiming} isFire={controller?.isFire} move={controller?.moveInput} | " +
                      $"applyRM={model.animator?.applyRootMotion} | wMove={controller?.worldMovement}");
        }
    }
}
