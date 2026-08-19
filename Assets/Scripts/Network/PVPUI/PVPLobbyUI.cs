using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 连接大厅（运行时构建，挂在主菜单 Canvas 上）：
    /// 服务器 IP + 角色选择（荧/芙宁娜）+ 连接按钮。
    /// 服务器是独立 .NET 进程，本端只填 IP 连接。
    /// ⚠️ 自建独立 Overlay Canvas + TMP 文字：主菜单命名 Canvas 是 Screen Space Camera 会被 3D 模型遮挡；
    /// 运行时 legacy Text 无字体渲染空白，故用 TMP（同 GameOverUI）。
    /// </summary>
    public static class PVPLobbyUI
    {
        private static GameObject _panel;
        private static TMP_InputField _ipInput;
        private static int _characterId;

        public static void Show()
        {
            if (_panel != null) UnityEngine.Object.Destroy(_panel);

            // 自建独立 Overlay Canvas：主菜单的命名 Canvas 是 Screen Space Camera（planeDistance=100），
            // 3D 展示模型在 UI 平面之前会把面板整个挡住——独立 Overlay 保证大厅始终最上层。
            var canvasGo = new GameObject("PVPLobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // 场景若无 EventSystem（通常主菜单有），补一个否则按钮点不了（随场景销毁，不进 PVPGame 与自带的重叠）
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            _panel = new GameObject("PVPLobby", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(600, 420);
            _panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f);

            int y = 160;
            NewText("标题", _panel.transform, new Vector2(0, y), new Vector2(600, 50), "PVP 联机对战", 40, TextAlignmentOptions.Center);
            y -= 60;

            NewText("IP 标签", _panel.transform, new Vector2(-180, y), new Vector2(140, 40), "服务器 IP", 24, TextAlignmentOptions.MidlineLeft);
            _ipInput = NewInputField(_panel.transform, new Vector2(20, y), new Vector2(260, 44), NetLobbyConfig.serverIp);
            y -= 70;

            NewText("角色标签", _panel.transform, new Vector2(-180, y), new Vector2(140, 40), "选择角色", 24, TextAlignmentOptions.MidlineLeft);
            NewButton(_panel.transform, new Vector2(0, y), new Vector2(140, 48), "荧 (Lumine)", () => SelectCharacter(0));
            NewButton(_panel.transform, new Vector2(170, y), new Vector2(140, 48), "芙宁娜 (Furina)", () => SelectCharacter(1));
            y -= 90;

            NewButton(_panel.transform, new Vector2(0, y), new Vector2(260, 56), "连接服务器", OnConnect);
        }

        private static void SelectCharacter(int id)
        {
            _characterId = id;
            Debug.Log($"[PVPLobbyUI] 选择角色 {id}（0=荧 1=芙宁娜）");
        }

        private static void OnConnect()
        {
            NetLobbyConfig.serverIp = string.IsNullOrEmpty(_ipInput.text) ? "127.0.0.1" : _ipInput.text.Trim();
            NetLobbyConfig.serverPort = 7777;
            NetLobbyConfig.characterId = _characterId;
            NetLobbyConfig.playerName = $"P{_characterId + 1}";
            Debug.Log($"[PVPLobbyUI] 连接 {NetLobbyConfig.serverIp}:{NetLobbyConfig.serverPort} 角色={NetLobbyConfig.characterId}");
            SceneManager.LoadScene("PVPGame");
        }

        // ---- 控件工厂（TMP，复用 GameOverUI 风格）----

        private static void NewText(string name, Transform parent, Vector2 pos, Vector2 size, string content, int fontSize, TextAlignmentOptions anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.enableWordWrapping = false;
        }

        private static TMP_InputField NewInputField(Transform parent, Vector2 pos, Vector2 size, string defaultValue)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
            var field = go.GetComponent<TMP_InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 4); trt.offsetMax = new Vector2(-10, -4);
            var t = textGo.GetComponent<TextMeshProUGUI>();
            t.fontSize = 24;
            t.color = Color.white;
            t.enableWordWrapping = false;
            field.textViewport = rt;
            field.textComponent = t;
            field.text = defaultValue;
            return field;
        }

        private static void NewButton(Transform parent, Vector2 pos, Vector2 size, string label, Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f, 0.9f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var t = labelGo.GetComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 22;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.enableWordWrapping = false;
        }
    }
}
