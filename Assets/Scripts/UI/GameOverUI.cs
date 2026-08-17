using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏结束界面：全部角色死亡时由 PlayerController 动态挂载。
/// 显示 GAME OVER 大字 + 半透明遮罩，按任意键返回游戏开始界面（GameStart）。
/// </summary>
public class GameOverUI : MonoBehaviour
{
    private void Awake()
    {
        Build();
    }

    private void Update()
    {
        // 任意键（键鼠）返回游戏开始界面
        // 项目仅启用 New Input System（旧 Input API 不可用），故用 InputSystem 读取
        bool anyKey = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                      || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                          || Mouse.current.rightButton.wasPressedThisFrame));
        if (anyKey)
        {
            // 解锁光标：Cursor.lockState 是跨场景的静态状态，
            // Game 场景中 PlayerController 会锁定，这里必须在切场景前解锁，否则主菜单鼠标不可见无法点击
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("GameStart");
        }
    }

    /// <summary>
    /// 动态构建 GAME OVER 界面（Screen Space Overlay）
    /// </summary>
    private void Build()
    {
        // 全屏 Canvas
        GameObject canvasGO = new GameObject("GameOver Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        // 半透明黑色遮罩
        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasGO.transform, false);
        dim.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        RectTransform dimRT = dim.GetComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        // "GAME OVER" 大字
        GameObject title = new GameObject("GameOverTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(canvasGO.transform, false);
        TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
        titleText.text = "GAME OVER";
        titleText.fontSize = 100;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.85f, 0.2f, 0.2f, 1f);
        RectTransform titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta = new Vector2(900, 200);

        // 提示文字
        GameObject hint = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        hint.transform.SetParent(canvasGO.transform, false);
        TextMeshProUGUI hintText = hint.GetComponent<TextMeshProUGUI>();
        hintText.text = "按任意键返回主菜单";
        hintText.fontSize = 32;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = Color.white;
        RectTransform hintRT = hint.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0.5f, 0.5f);
        hintRT.anchorMax = new Vector2(0.5f, 0.5f);
        hintRT.anchoredPosition = new Vector2(0, -150);
        hintRT.sizeDelta = new Vector2(600, 60);
    }
}
