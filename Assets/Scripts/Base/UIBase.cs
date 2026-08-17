using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI基类
/// </summary>
public abstract class UIBase<T> : SingleMonoBase<T> where T :UIBase<T>
{
    public bool show;
    private Animator animator;
    protected virtual void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
    }

    protected virtual void Start()
    {
        // StartCoroutine(Enter());
        gameObject.SetActive(show);
    }

    /// <summary>
    /// 显示UI
    /// </summary>
    public virtual void Enter()
    {
        gameObject.SetActive(true);
        animator.enabled = true;//重新启用（上次 FadeIn 播完后被停用，用于播放本次入场动画）
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        StartCoroutine(_Enter());
    }
    private  IEnumerator _Enter()
    {
        PlayAnimation("FadeIn");
        yield return new WaitForSeconds(0.01f);
        yield return new WaitUntil(() => IsAnimationBreak());
        ResumeButtons();
        // FadeIn 播完后停用 Animator：Animator 停在 FadeIn 状态会每帧覆写子元素的
        // m_AnchoredPosition.x / m_Color 等属性。由于 FadeIn 只动画了 x 未动画 y，
        // 退出按钮（ExcludeMouse）的水平位移被持续锁定，只能垂直躲避。
        // 停用后位置完全交由代码控制，实现全向躲避。
        animator.enabled = false;
    }

    /// <summary>
    /// 隐藏UI
    /// </summary>
    /// <returns></returns>
    public virtual void Exit(Action action)
    {

        DisableButtons();
        animator.enabled = true;//重新启用以播放 FadeOut
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        StartCoroutine(_Exit(action));
    }
    public virtual IEnumerator _Exit(Action action)
    {
        PlayAnimation("FadeOut");
        // 等待淡出动画播放完毕（带超时保护，防止动画缺失时死等）
        float timeout = 2f;
        float timer = 0;
        while (timer < timeout && !IsAnimationBreak())
        {
            timer += Time.deltaTime;
            yield return null;
        }
        // 先执行回调（显示目标菜单），再停用本菜单。
        // 必须停用：否则已淡出的菜单 GameObject 仍保持激活，其透明面板的 raycastTarget
        // 会挡住下层菜单（如主菜单）按钮的鼠标射线，导致按钮不高亮、点不动。
        action?.Invoke();
        gameObject.SetActive(false);
    }



    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="animationName">动画名称</param>
    public void PlayAnimation(string animationName)
    {
        animator.CrossFadeInFixedTime(animationName, 0);
    }

    /// <summary>
    /// 动画是否播放完毕
    /// </summary>
    /// <param name="layer">动画层</param>
    protected bool IsAnimationBreak()
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.normalizedTime >= 1.0f && !animator.IsInTransition(0);
    }

    /// <summary>
    /// 禁用所以按钮
    /// </summary>
    protected abstract void DisableButtons();

    /// <summary>
    /// 恢复所以按钮
    /// </summary>
    protected abstract void ResumeButtons();
}
