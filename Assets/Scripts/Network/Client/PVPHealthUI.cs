using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        private Image _healthFill;
        private TextMeshProUGUI _healthText;
        private TextMeshProUGUI _scoreboardText;
        private RectTransform _deathOverlay;
        private TextMeshProUGUI _deathText;
        private RectTransform _endPanel;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endScores;

        private void Awake()
        {
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
            if (_net == null || _net.LocalModel == null) return;

            // 血量条
            float ratio = (float)_net.LocalModel.currentHealth / Mathf.Max(1, _net.LocalModel.maxHealth);
            _healthFill.fillAmount = ratio;
            _healthText.text = $"{_net.LocalModel.currentHealth}/{_net.LocalModel.maxHealth}";

            // 计分板
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _net.Scores)
                sb.AppendLine($"P{kv.Key}: {kv.Value} 分");
            _scoreboardText.text = sb.ToString();

            // 死亡重生提示：轮询 isDead
            _deathOverlay.gameObject.SetActive(_net.LocalModel.isDead);
        }

        // ---------------- UI 构建 ----------------

        private void BuildUI()
        {
            var go = new GameObject("PVPHUD");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 本地血量条（左上）
            var healthRoot = NewRect("HealthBar", new Vector2(40, 1040), new Vector2(360, 36));
            var bg = NewImage("BG", healthRoot, new Vector2(0, 0), new Vector2(360, 36));
            bg.color = new Color(0, 0, 0, 0.5f);
            var fill = NewImage("Fill", healthRoot, new Vector2(0, 0), new Vector2(350, 26));
            fill.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            _healthFill = fill;
            _healthText = NewText("HealthText", healthRoot, new Vector2(0, 0), new Vector2(360, 36), "100/100", 20, TextAlignmentOptions.Center);

            // 屏幕中心瞄准点（十字）
            NewText("AimDot", _canvas.transform, Vector2.zero, new Vector2(40, 40), "+", 36, TextAlignmentOptions.Center);

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
            NewButton(_endPanel, new Vector2(0, -150), new Vector2(220, 60), "返回主菜单", OnBackToMenu);
        }

        private void ShowDeath()
        {
            _deathText.text = "你已被击杀，等待重生...";
        }

        private void ShowMatchEnd(int winnerId)
        {
            bool win = winnerId == _net.PlayerId;
            _endTitle.text = win ? "你获胜！" : $"P{winnerId} 获胜";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _net.Scores)
                sb.AppendLine($"P{kv.Key}: {kv.Value} 分");
            _endScores.text = sb.ToString();
            _endPanel.gameObject.SetActive(true);
            Time.timeScale = 0f; // 暂停
        }

        private void OnBackToMenu()
        {
            Time.timeScale = 1f;
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

        private void NewButton(Transform parent, Vector2 anchoredPos, Vector2 size, string label, Action onClick)
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
        }
    }
}
