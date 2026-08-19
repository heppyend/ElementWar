using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 轨道相机（简化版 TPS 相机，用 New Input System 鼠标视角）：
    /// 跟随本地玩家，鼠标控制视角，屏幕中心即瞄准方向（NetClient 用它算 aimPoint）。
    /// </summary>
    public class PVPCameraFollow : MonoBehaviour
    {
        [Tooltip("跟随目标（NetClient 生成本地玩家后绑定）")]
        public Transform target;
        [SerializeField] float distance = 6f;
        [SerializeField] float height = 2.5f;
        [SerializeField] float mouseSensitivity = 2f;
        [SerializeField] float pitchMin = -20f;
        [SerializeField] float pitchMax = 70f;

        private float _yaw;
        private float _pitch;

        private void LateUpdate()
        {
            if (target == null) return;

            var delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _yaw += delta.x * mouseSensitivity;
            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pos = target.position + Vector3.up * height - rot * Vector3.forward * distance;
            transform.position = pos;
            transform.rotation = rot;
        }
    }
}
