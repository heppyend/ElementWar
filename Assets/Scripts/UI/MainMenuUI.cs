using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 主菜单
/// </summary>
public class MainMenuUI : UIBase<MainMenuUI>
{
    public Button btnOnline;
    public Button btnContinue;
    public Button btnNewGame;
    public Button btnRead;
    public Button btnCharacter;
    public Button btnClothes;
    public Button btnSetting;
    public Button btnAchievement;
    public Button btnAuthor;
    public Button btnLanguage;
    public Button btnVoice;
    public Button btnExit;

    protected override void Awake()
    {
        base.Awake();
        btnOnline.onClick.AddListener(() => ElementWar.Net.PVPLobbyUI.Show());
        btnContinue.onClick.AddListener(showTipMenu);
        btnNewGame.onClick.AddListener(() => {
            SceneManager.LoadScene("PVEGame");
        });
        btnRead.onClick.AddListener(showTipMenu);
        btnCharacter.onClick.AddListener(showTipMenu);
        btnClothes.onClick.AddListener(showTipMenu);
        btnSetting.onClick.AddListener(showTipMenu);
        btnAchievement.onClick.AddListener(showTipMenu);
        btnAuthor.onClick.AddListener(showTipMenu);
        btnLanguage.onClick.AddListener(showTipMenu);
        btnVoice.onClick.AddListener(showTipMenu);
        btnExit.onClick.AddListener(ShowExitMenu);
    }

    protected override void Start()
    {
        base.Start();
        // 主菜单强制解锁光标：防止从游戏场景返回时 Cursor.lockState 仍为 Locked 导致鼠标不可见无法点击
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Enter();
    }

    private void showTipMenu()
    {
        Exit(() =>
        {
            // TipMenu 在场景中默认是隐藏物体，某些加载顺序下不会先执行 Awake，
            // 因此不能直接依赖静态 INSTANCE。
            var tipMenu = FindObjectOfType<TipMenuUI>(true);
            if (tipMenu != null)
                tipMenu.Enter();
            else
                Debug.LogWarning("[MainMenuUI] 找不到 TipMenu，跳过提示菜单显示。");
        });
    }

    private void ShowExitMenu()
    {
        Exit(() =>
        {
            // 与 TipMenu 相同：退出确认框可能默认隐藏，不能依赖尚未 Awake 的静态 INSTANCE。
            var exitMenu = FindObjectOfType<ExitMenuUI>(true);
            if (exitMenu != null)
                exitMenu.Enter();
            else
                Debug.LogWarning("[MainMenuUI] 找不到 ExitMenu，跳过退出确认框显示。");
        });
    }

    protected override void DisableButtons()
    {
        // 禁用主菜单所有按钮，防止动画期间误触
        btnOnline.interactable = false;
        btnContinue.interactable = false;
        btnNewGame.interactable = false;
        btnRead.interactable = false;
    }

    protected override void ResumeButtons()
    {
        // 恢复主菜单所有按钮
        btnOnline.interactable = true;
        btnContinue.interactable = true;
        btnNewGame.interactable = true;
        btnRead.interactable = true;
    }
}
