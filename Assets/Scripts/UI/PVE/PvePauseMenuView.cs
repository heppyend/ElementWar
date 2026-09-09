using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PVE 游戏内暂停菜单控制器。
/// 挂在始终激活的 PVEHUD 上；set main 面板和各页面的视觉内容均由场景 GUI 配置。
/// </summary>
[DisallowMultipleComponent]
public class PvePauseMenuView : MonoBehaviour
{
    [Header("面板引用")]
    [Tooltip("set main：包含四个 Toggle 页面和各页面退出按钮的总根节点。")]
    [SerializeField] private GameObject menuRoot;
    [Tooltip("为空时自动使用场景中的 PlayerController。")]
    [SerializeField] private PlayerController playerController;

    [Header("快捷键")]
    [SerializeField] private Key toggleKey = Key.Escape;
    [SerializeField] private bool hideOnAwake = true;

    private bool isOpen;
    private float timeScaleBeforeOpen = 1f;

    /// <summary>供外部按钮、调试和页面逻辑读取当前暂停状态。</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (playerController == null)
            playerController = PlayerController.INSTANCE;
        if (hideOnAwake)
            SetMenuRootActive(false);
    }

    private void Update()
    {
        if (toggleKey == Key.None || Keyboard.current == null || !Keyboard.current[toggleKey].wasPressedThisFrame)
            return;

        if (isOpen) Close();
        else Open();
    }

    /// <summary>绑定左上角 set Button 的 On Click()。</summary>
    public void Open()
    {
        if (isOpen || IsBlockedByGameOver()) return;

        if (playerController == null)
            playerController = PlayerController.INSTANCE;

        timeScaleBeforeOpen = Time.timeScale;
        isOpen = true;
        SetMenuRootActive(true);
        if (playerController != null)
            playerController.SetPauseInputLocked(true);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>绑定每个页面右上角退出 Button 的 On Click()。</summary>
    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        SetMenuRootActive(false);
        Time.timeScale = timeScaleBeforeOpen;
        if (playerController == null)
            playerController = PlayerController.INSTANCE;
        if (playerController != null)
            playerController.SetPauseInputLocked(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>如需单个按钮兼任打开/关闭，可绑定此方法。</summary>
    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private bool IsBlockedByGameOver()
    {
        if (playerController == null)
            playerController = PlayerController.INSTANCE;
        return playerController != null && playerController.IsGameOverInputLocked;
    }

    private void SetMenuRootActive(bool value)
    {
        if (menuRoot != null && menuRoot.activeSelf != value)
            menuRoot.SetActive(value);
    }

    private void OnDisable()
    {
        // 若包含该脚本的 HUD 被切场景或禁用，不允许把 Time.timeScale=0 遗留给下一场景。
        if (isOpen)
            Close();
    }
}
