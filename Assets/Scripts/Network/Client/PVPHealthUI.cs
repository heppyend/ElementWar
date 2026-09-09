using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP HUD（运行时自建 Canvas，无需场景预置 UI）：
    /// 本地血量条 / 计分板 / 死亡重生提示 / 对局结束面板（含返回主菜单）。
    /// 订阅 NetClient 事件，挂在 PVPGame 场景任意物体上。
    /// </summary>
    public class PVPHealthUI : MonoBehaviour
    {
        private NetClient _net;
        private Canvas _canvas;
        private RectTransform _legacyHealthRoot;
        private TextMeshProUGUI _legacyAimDot;
        private Image _healthFill;
        private TextMeshProUGUI _healthText;
        private TextMeshProUGUI _scoreboardText;
        private RectTransform _deathOverlay;
        private TextMeshProUGUI _deathText;
        private RectTransform _endPanel;
        private RectTransform _backButton;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endScores;
        private TextMeshProUGUI _endCountdown;
        private bool _returningToMenu;
        private float _autoReturnAt = -1f;

        private void Awake()
        {
            // PVP 场景通常预置 EventSystem；若被删除或场景切换时缺失，运行时补齐，
            // 保证结束面板的 GraphicRaycaster/Button 能收到点击。
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("PVP_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            BuildUI();
        }

        private void Start()
        {
            _net = FindObjectOfType<NetClient>();
            if (_net == null) return;
            _net.OnLocalDied += ShowDeath;
            _net.OnMatchEnded += ShowMatchEnd;
        }

        private void OnDestroy()
        {
            if (_net != null)
            {
                _net.OnLocalDied -= ShowDeath;
                _net.OnMatchEnded -= ShowMatchEnd;
            }
        }

        private void Update()
        {
            if (_endPanel != null && _endPanel.gameObject.activeSelf)
            {
                // 光标状态是全局静态值，其他控制器可能在结束后再次锁定；面板显示期间持续保持可点击。
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    OnBackToMenu();
                // 备用点击路径：即使场景中的 InputSystemUIInputModule 配置失效，
                // 也能直接用屏幕坐标命中结束面板按钮。
                if (!_returningToMenu && _backButton != null && Mouse.current != null
                    && Mouse.current.leftButton.wasPressedThisFrame
                    && RectTransformUtility.RectangleContainsScreenPoint(
                        _backButton, Mouse.current.position.ReadValue(), null))
                    OnBackToMenu();
                if (!_returningToMenu && _autoReturnAt > 0f)
                {
                    float remaining = _autoReturnAt - Time.unscaledTime;
                    if (_endCountdown != null)
                        _endCountdown.text = remaining > 0f ? $"{remaining:0.0} 秒后返回主菜单" : "正在返回主菜单…";
                    if (remaining <= 0f) OnBackToMenu();
                }
            }
            if (_net == null || _net.LocalModel == null) return;

            // 血量条
            if (_healthFill != null && _healthText != null)
            {
                float ratio = (float)_net.LocalModel.currentHealth / Mathf.Max(1, _net.LocalModel.maxHealth);
                _healthFill.fillAmount = ratio;
                _healthText.text = $"{_net.LocalModel.currentHealth}/{_net.LocalModel.maxHealth}";
            }

            // 计分板（bot 用负数 id，显示「Bot」；训练模式=有机器人在场）
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(_net.BotIds.Count > 0 ? "训练模式（机器人）" : "对战");
            foreach (var kv in _net.Scores)
            {
                string who = kv.Key == _net.PlayerId ? "你"
                    : _net.BotIds.Contains(kv.Key) ? "Bot"
                    : $"P{kv.Key}";
                sb.AppendLine($"{who}: {kv.Value} 分");
            }
            _scoreboardText.text = sb.ToString();

            // 死亡重生提示：轮询 isDead
            if (_deathOverlay != null)
                _deathOverlay.gameObject.SetActive(_net.LocalModel.isDead);
        }

        /// <summary>
        /// 新版场景 HUD 接管本地血条与准星时调用。
        /// 计分板、死亡重生提示和结算面板仍由本组件保留。
        /// </summary>
        public void SetLegacyCombatHudVisible(bool visible)
        {
            if (_legacyHealthRoot != null)
                _legacyHealthRoot.gameObject.SetActive(visible);
            if (_legacyAimDot != null)
                _legacyAimDot.gameObject.SetActive(visible);
        }

        // ---------------- UI 构建 ----------------

        private void BuildUI()
        {
            var go = new GameObject("PVPHUD");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 本地血量条（左上）
            _legacyHealthRoot = NewRect("HealthBar", new Vector2(40, 1040), new Vector2(360, 36));
            var bg = NewImage("BG", _legacyHealthRoot, new Vector2(0, 0), new Vector2(360, 36));
            bg.color = new Color(0, 0, 0, 0.5f);
            var fill = NewImage("Fill", _legacyHealthRoot, new Vector2(0, 0), new Vector2(350, 26));
            fill.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _healthFill = fill;
            _healthText = NewText("HealthText", _legacyHealthRoot, new Vector2(0, 0), new Vector2(360, 36), "100/100", 20, TextAlignmentOptions.Center);

            // 屏幕中心瞄准点（十字）
            _legacyAimDot = NewText("AimDot", _canvas.transform, Vector2.zero, new Vector2(40, 40), "+", 36, TextAlignmentOptions.Center);

            // 计分板（右上）
            var scoreRoot = NewRect("Scoreboard", new Vector2(1520, 1040), new Vector2(360, 120));
            _scoreboardText = NewText("ScoreText", scoreRoot, new Vector2(0, 0), new Vector2(360, 120), "P1: 0 分", 24, TextAlignmentOptions.TopRight);

            // 死亡重生提示（居中）
            _deathOverlay = NewRect("DeathOverlay", Vector2.zero, new Vector2(600, 120));
            _deathOverlay.gameObject.SetActive(false);
            _deathText = NewText("DeathText", _deathOverlay, Vector2.zero, new Vector2(600, 120), "你已被击杀，等待重生...", 36, TextAlignmentOptions.Center);

            // 对局结束面板（居中，含返回按钮）
            _endPanel = NewRect("EndPanel", Vector2.zero, new Vector2(800, 400));
            _endPanel.gameObject.SetActive(false);
            var panelBg = NewImage("PanelBG", _endPanel, Vector2.zero, new Vector2(800, 400));
            panelBg.color = new Color(0, 0, 0, 0.8f);
            _endTitle = NewText("EndTitle", _endPanel, new Vector2(0, 120), new Vector2(800, 80), "对局结束", 48, TextAlignmentOptions.Center);
            _endScores = NewText("EndScores", _endPanel, new Vector2(0, 0), new Vector2(800, 100), "", 28, TextAlignmentOptions.Center);
            _endCountdown = NewText("EndCountdown", _endPanel, new Vector2(0, -90), new Vector2(800, 40), "", 22, TextAlignmentOptions.Center);
            _backButton = NewButton(_endPanel, new Vector2(0, -150), new Vector2(220, 60), "返回主菜单", OnBackToMenu);
        }

        private void ShowDeath()
        {
            _deathText.text = "你已被击杀，等待重生...";
        }

        private void ShowMatchEnd(int winnerId)
        {
            // 对局中的相机/输入通常会锁定光标；结束面板必须立即恢复系统光标，
            // 否则按钮虽然显示出来，但鼠标仍停留在准心位置，无法点击 UI。
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            bool win = winnerId == _net.PlayerId;
            _endTitle.text = win ? "你获胜！"
                : _net.BotIds.Contains(winnerId) ? "Bot 获胜"
                : $"P{winnerId} 获胜";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _net.Scores)
            {
                string who = kv.Key == _net.PlayerId ? "你"
                    : _net.BotIds.Contains(kv.Key) ? "Bot"
                    : $"P{kv.Key}";
                sb.AppendLine($"{who}: {kv.Value} 分");
            }
            _endScores.text = sb.ToString();
            _autoReturnAt = Time.unscaledTime + 4f;
            _endPanel.gameObject.SetActive(true);
            // 不暂停全局时间：结算阶段由本地角色/远端角色各自以慢速播放视觉回放，
            // UI 仍可立即交互，网络封盘也不会被 Time.timeScale 影响。
        }

        private void OnBackToMenu()
        {
            if (_returningToMenu) return;
            _returningToMenu = true;
            Debug.Log("[PVPHealthUI] 返回主菜单");
            // 结算后客户端不再发送输入；先显式释放服务器席位，下一局无需等待超时清理。
            _net?.DisconnectGracefully();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("GameStart");
        }

        // ---------------- 控件工厂 ----------------

        private RectTransform NewRect(string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        private Image NewImage(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return go.GetComponent<Image>();
        }

        private TextMeshProUGUI NewText(string name, Transform parent, Vector2 anchoredPos, Vector2 size, string content, int fontSize, TextAlignmentOptions anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private RectTransform NewButton(Transform parent, Vector2 anchoredPos, Vector2 size, string label, Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f, 0.9f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var t = labelGo.GetComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 24;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.enableWordWrapping = false;
            return rt;
        }
    }
}
