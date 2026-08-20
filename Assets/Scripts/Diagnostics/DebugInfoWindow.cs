using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using ElementWar.Net;

namespace ElementWar
{
    /// <summary>
    /// 运行时信息窗（游戏左上角）：帧率 / 帧耗时 / 分辨率 / 场景 / 网络状态。
    /// 运行时自建 Overlay Canvas + TMP，按 F3 开关，DontDestroyOnLoad 跨场景常驻。
    /// 由 NetworkLauncher（PVP）自动确保存在；PVE 场景可在向导里挂或手动拖到场景任意物体。
    /// </summary>
    public class DebugInfoWindow : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        private Canvas _canvas;
        private float _fpsSmooth = 60f;

        private void Awake()
        {
            BuildUI();
            Debug.Log("[DebugInfoWindow] 已创建（左上角信息窗，F3 开关）");
        }

        private void Update()
        {
            // F3 开关
            if (Keyboard.current != null && Keyboard.current[Key.F3].wasPressedThisFrame)
                _canvas.gameObject.SetActive(!_canvas.gameObject.activeSelf);
            if (_text == null) return;

            // 帧率（unscaled：对局结束 timeScale=0 也显示真实帧率）
            float dt = Time.unscaledDeltaTime;
            float fps = 1f / Mathf.Max(0.0001f, dt);
            _fpsSmooth = Mathf.Lerp(_fpsSmooth, fps, 0.08f);
            float ms = dt * 1000f;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"FPS: {_fpsSmooth:F0}  ({ms:F1} ms)");
            sb.AppendLine($"分辨率: {Screen.width}x{Screen.height}");
            sb.AppendLine($"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

            // 网络信息（PVP 场景存在 NetClient 时附加）
            var net = FindObjectOfType<NetClient>();
            if (net != null)
            {
                sb.AppendLine(net.Connected
                    ? $"网络: 在线  P{net.PlayerId}  玩家 {net.Scores.Count}"
                    : "网络: 连接中…");
            }
            _text.text = sb.ToString();
        }

        private void BuildUI()
        {
            // 与 PVPHealthUI 相同的画布模式（它能正常显示）：Canvas 作为本物体子物体，ScreenSpaceOverlay
            var canvasGo = new GameObject("DebugInfoCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 半透明深色底：保证在任何背景上都清晰可见
            var bgGo = new GameObject("InfoBG", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = new Vector2(10, -8);
            bgRt.sizeDelta = new Vector2(360, 170);
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var textGo = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16, -14);
            rt.sizeDelta = new Vector2(420, 180);

            _text = textGo.GetComponent<TextMeshProUGUI>();
            _text.fontSize = 20;
            _text.color = new Color(1f, 1f, 1f, 1f);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = false;
            _text.text = "FPS: --\n分辨率: --\n场景: --";
        }
    }
}
