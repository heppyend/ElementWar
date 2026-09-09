using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// GUI 配置的 PVE GAME OVER 表现层。
/// 挂在始终激活的 PVEHUD 上，实际面板、文案、图片和按钮均由 Inspector 引用。
/// </summary>
public class PveGameOverView : MonoBehaviour
{
    [SerializeField] private GameObject gameOverRoot;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool returnOnAnyKey = true;
    [SerializeField] private Key returnKey = Key.Space;
    [SerializeField] private string returnSceneName = "GameStart";

    private bool visible;

    private void Awake()
    {
        if (hideOnAwake)
            SetVisible(false);
    }

    private void Update()
    {
        if (!visible) return;

        bool anyKey = returnOnAnyKey
            && ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                    || Mouse.current.rightButton.wasPressedThisFrame)));
        bool configuredKey = !returnOnAnyKey && Keyboard.current != null && returnKey != Key.None
            && Keyboard.current[returnKey].wasPressedThisFrame;
        if (anyKey || configuredKey)
            ReturnToMenu();
    }

    public void Show()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void ReturnToMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(returnSceneName);
    }

    private void SetVisible(bool value)
    {
        visible = value;
        if (gameOverRoot != null && gameOverRoot.activeSelf != value)
            gameOverRoot.SetActive(value);
    }
}
