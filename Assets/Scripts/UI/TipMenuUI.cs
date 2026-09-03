using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

/// <summary>
/// 提示菜单
/// </summary>
public class TipMenuUI : UIBase<TipMenuUI>
{
    public Button btnYes;

    protected override void Awake()
    {
        base.Awake();
        btnYes.onClick.AddListener(() =>
        {
            Exit(() =>
            {
                var mainMenu = FindObjectOfType<MainMenuUI>(true);
                if (mainMenu != null)
                    mainMenu.Enter();
                else
                    Debug.LogWarning("[TipMenuUI] 找不到 MainMenu，无法返回主菜单。");
            });
        });
    }

    protected override void Start()
    {
        base.Start();
        
    }

    protected override void DisableButtons()
    {
        btnYes.interactable = false;
    }

    protected override void ResumeButtons()
    {
        btnYes.interactable = true;
    }

}
