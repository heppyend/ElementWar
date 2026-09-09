using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 本地暂停菜单。只暂停当前客户端的玩法输入，绝不暂停服务器或其他玩家。
    /// 挂在始终激活的 PVPHUD 上；菜单内容、Toggle 页面与 Button 由 Unity GUI 配置。
    /// </summary>
    [DisallowMultipleComponent]
    public class PvpPauseMenuView : MonoBehaviour
    {
        [Header("面板引用")]
        [Tooltip("PVP 的 set main 总根节点。")]
        [SerializeField] private GameObject menuRoot;
        [Tooltip("为空时自动查找场景 NetClient。")]
        [SerializeField] private NetClient netClient;

        [Header("快捷键")]
        [SerializeField] private Key toggleKey = Key.Escape;
        [SerializeField] private bool hideOnAwake = true;

        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            ResolveNetClient();
            if (hideOnAwake)
                SetMenuRootActive(false);
        }

        private void OnEnable()
        {
            ResolveNetClient();
            if (netClient != null)
                netClient.OnMatchEnded += CloseForMatchEnd;
        }

        private void OnDisable()
        {
            if (netClient != null)
                netClient.OnMatchEnded -= CloseForMatchEnd;
            if (isOpen)
                Close();
        }

        private void Update()
        {
            if (toggleKey == Key.None || Keyboard.current == null || !Keyboard.current[toggleKey].wasPressedThisFrame)
                return;
            if (isOpen) Close();
            else Open();
        }

        /// <summary>绑定 PVP 左上角 set Button 的 On Click()。</summary>
        public void Open()
        {
            ResolveNetClient();
            if (isOpen || (netClient != null && netClient.MatchEnded)) return;

            isOpen = true;
            SetMenuRootActive(true);
            if (netClient != null)
                netClient.SetLocalGameplayPaused(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>绑定每个 PVP 页面右上角退出 Button 的 On Click()。</summary>
        public void Close()
        {
            if (!isOpen) return;

            isOpen = false;
            SetMenuRootActive(false);
            ResolveNetClient();
            if (netClient != null)
                netClient.SetLocalGameplayPaused(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        private void CloseForMatchEnd(int _)
        {
            if (!isOpen) return;
            isOpen = false;
            SetMenuRootActive(false);
            if (netClient != null)
                netClient.SetLocalGameplayPaused(false);
            // 对局结算面板也需要鼠标；不要在事件顺序不确定时重新锁定它。
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ResolveNetClient()
        {
            if (netClient == null)
                netClient = FindObjectOfType<NetClient>();
        }

        private void SetMenuRootActive(bool value)
        {
            if (menuRoot != null && menuRoot.activeSelf != value)
                menuRoot.SetActive(value);
        }
    }
}
