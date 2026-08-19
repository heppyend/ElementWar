using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 相机（照搬 PVE 方案）：双 CinemachineFreeLook（正常 + 瞄准），鼠标转视角，瞄准切换 Priority。
    /// 运行时自建（无需场景预置相机），自动清理旧的 PVPCameraFollow（防止与 CinemachineBrain 抢 Main Camera）。
    /// 由 NetworkLauncher 在启动时确保存在，NetClient 生成本地玩家后自动跟随。
    /// </summary>
    public class PVPCameraRig : MonoBehaviour
    {
        [Header("正常视角")]
        public float normalRadius = 3.5f;
        public float normalHeight = 2.4f;
        public float normalFov = 40f;
        [Header("瞄准视角")]
        public float aimRadius = 1.2f;
        public float aimHeight = 1.5f;
        public float aimFov = 45f;
        [Header("鼠标")]
        public float mouseSensitivity = 1.5f;
        public float yawRange = 360f;      // XAxis 无限转
        public float pitchMin = 0.05f;     // YAxis 0..1（0=上轨道 1=下轨道）
        public float pitchMax = 0.95f;
        [Header("视线锚点（相对角色）")]
        public Vector3 normalLookOffset = new Vector3(0f, 1.15f, 0f);   // 胸口
        public Vector3 aimLookOffset = new Vector3(0.3f, 1.15f, 0f);    // 右肩（向右偏一点）

        private CinemachineFreeLook _normal;
        private CinemachineFreeLook _aim;
        private Transform _target;
        private Transform _normalLook;
        private Transform _aimLook;
        private float _xAxis;
        private float _yAxis = 0.5f;
        private bool _aiming;
        private CinemachineImpulseSource _impulse;   // 开火/受击相机震动（PVE 同款：挂瞄准相机）

        private void Awake()
        {
            // 清理旧简化相机（场景 wizard 可能已挂过），避免与 CinemachineBrain 打架
            foreach (var old in FindObjectsOfType<PVPCameraFollow>())
                Destroy(old);

            // 确保 Main Camera 有 CinemachineBrain（FreeLook 依赖它驱动相机）
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
                Camera.main.gameObject.AddComponent<CinemachineBrain>();
            // 瞄准切换要快：默认混合时长压到 0.15s（否则 FreeLook 切换约 2s 慢慢拉，瞄准感迟钝）
            if (Camera.main != null)
            {
                var brain = Camera.main.GetComponent<CinemachineBrain>();
                if (brain != null)
                {
                    var def = brain.m_DefaultBlend;
                    def.m_Time = 0.15f;
                    brain.m_DefaultBlend = def;
                }
            }

            _normal = CreateFreeLook("PVP_FreeLook_Normal", normalRadius, normalHeight, normalFov);
            _aim = CreateFreeLook("PVP_FreeLook_Aim", aimRadius, aimHeight, aimFov);
            _normal.Priority = 100;
            _aim.Priority = 0;

            // 开火/受击相机震动（PVE 同款）：ImpulseSource + ImpulseListener 都挂瞄准虚拟相机
            // （ImpulseListener 挂 Main Camera 会报 "requires a virtual camera"，必须挂 vcam）
            _impulse = _aim.gameObject.AddComponent<CinemachineImpulseSource>();
            _aim.gameObject.AddComponent<CinemachineImpulseListener>();
        }

        /// <summary>开火/受击相机震动（NetClient 调用）。</summary>
        public void ShakeCamera()
        {
            if (_impulse != null) _impulse.GenerateImpulse();
        }

        private void Update()
        {
            var net = FindObjectOfType<NetClient>();
            if (net != null && net.LocalModel != null && net.LocalModel.transform != _target)
            {
                _target = net.LocalModel.transform;
                _normal.Follow = _target;
                _aim.Follow = _target;
                // 视线锚点（胸口/右肩）：相机对准胸口而不是 transform（在脚部）→ 否则视角偏低在小腿
                if (_normalLook == null)
                {
                    _normalLook = new GameObject("Look_Normal").transform;
                    _normalLook.SetParent(_target, false);
                    _aimLook = new GameObject("Look_Aim").transform;
                    _aimLook.SetParent(_target, false);
                }
                _normalLook.localPosition = normalLookOffset;
                _aimLook.localPosition = aimLookOffset;
                _normal.LookAt = _normalLook;
                _aim.LookAt = _aimLook;
            }
            if (_target == null) return;

            // 鼠标转视角（与 PVE FreeLook 手感一致：上移抬高相机，下移压低）
            var delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _xAxis += delta.x * mouseSensitivity;
            _yAxis = Mathf.Clamp(_yAxis - delta.y * mouseSensitivity * 0.001f, pitchMin, pitchMax);
            _normal.m_XAxis.Value = _xAxis;
            _normal.m_YAxis.Value = _yAxis;
            _aim.m_XAxis.Value = _xAxis;
            _aim.m_YAxis.Value = _yAxis;

            // 瞄准切换（按 isAiming / isFire，与 PVE PlayerStateBase 一致）
            bool aiming = net != null && net.LocalMotor != null && (net.LocalMotor.isAiming || net.LocalMotor.isFire);
            if (aiming != _aiming)
            {
                _aiming = aiming;
                _normal.Priority = aiming ? 0 : 100;
                _aim.Priority = aiming ? 100 : 0;
            }
        }

        private CinemachineFreeLook CreateFreeLook(string name, float radius, float height, float fov)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<CinemachineFreeLook>();
            cam.m_Lens.FieldOfView = fov;
            // 禁掉内置输入（项目仅 New Input System，旧 AxisName 无效），由本类直接写 Value
            cam.m_XAxis.m_InputAxisName = "";
            cam.m_YAxis.m_InputAxisName = "";
            cam.m_XAxis.m_InputAxisValue = 0f;
            cam.m_YAxis.m_InputAxisValue = 0f;
            cam.m_XAxis.m_MaxSpeed = 0f;
            cam.m_YAxis.m_MaxSpeed = 0f;
            cam.m_XAxis.m_AccelTime = 0f;
            cam.m_XAxis.m_DecelTime = 0f;
            cam.m_YAxis.m_AccelTime = 0f;
            cam.m_YAxis.m_DecelTime = 0f;
            cam.m_Orbits[0].m_Height = height + 1f;
            cam.m_Orbits[0].m_Radius = radius;
            cam.m_Orbits[1].m_Height = height;
            cam.m_Orbits[1].m_Radius = radius;
            cam.m_Orbits[2].m_Height = height - 1f;
            cam.m_Orbits[2].m_Radius = radius * 0.8f;
            cam.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            return cam;
        }
    }
}
