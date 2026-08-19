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
        btnExit.onClick.AddListener(() => {
            // 退出逻辑
            Exit(() =>
            {
                ExitMenuUI.INSTANCE.Enter();
            });
        });
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
            TipMenuUI.INSTANCE.Enter();
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