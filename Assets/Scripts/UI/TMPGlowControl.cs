using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 控制发光
/// </summary>
public class TMPGlowControl : MonoBehaviour,IPointerEnterHandler, IPointerExitHandler
{
    #region 文字发光
    public TMP_Text textConmponent;
    [Tooltip("最大发光值")]
    public float maxGlowPower = 5;
    [Tooltip("总发光时间")]
    public float glowTime = 0.2f;
    [Tooltip("淡入发光的时间")]
    public float fadeInTime = 0.05f;
    private Material glowMaterial;//文本材质
    #endregion

    void Start()
    {
        glowMaterial=textConmponent.fontMaterial;
        DisableUnderlay();//默认关闭阴影
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        #region 让文字产生发光效果
        StopAllCoroutines();
        StartCoroutine(AnimateGlowEffect());
        #endregion

        #region 让文字产生阴影效果
        EnableUnderlay();
        #endregion
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        //停止发光动画并归零，关闭阴影（原 Update 中每帧 DisableUnderlay 会覆盖阴影，已移除）
        StopAllCoroutines();
        if (glowMaterial != null) glowMaterial.SetFloat("_GlowPower", 0f);
        DisableUnderlay();
    }

    /// <summary>
    /// 进入发光效果
    /// </summary>
    /// <returns></returns>
    private IEnumerator AnimateGlowEffect()
    {
        //淡入发光效果
        float timer = 0;
        while (timer < fadeInTime)
        {
            float t = timer / fadeInTime;
            float currentPower = Mathf.Lerp(0f, maxGlowPower, t);
            glowMaterial.SetFloat("_GlowPower", currentPower);
            timer += Time.deltaTime;
            yield return null;
        }
        //强制设置为最大发光强度
        glowMaterial.SetFloat("_GlowPower", maxGlowPower);

        //淡出发光效果
        timer = 0;
        float fadeOutTime = glowTime - fadeInTime;//计算淡出时间
        while (timer < fadeOutTime)
        {
            float t = timer / fadeOutTime;
            float currentPower = Mathf.Lerp(maxGlowPower, 0, t);
            glowMaterial.SetFloat("_GlowPower", currentPower);
            timer += Time.deltaTime;
            yield return null;
        }
        glowMaterial.SetFloat("_GlowPower", 0f);
    }

    /// <summary>
    /// 开启文字阴影
    /// </summary>
    private void EnableUnderlay()
    {
        if (glowMaterial != null)
        {
            glowMaterial.EnableKeyword("UNDERLAY_ON");
        }
    }
    /// <summary>
    /// 关闭文字阴影
    /// </summary>
    private void DisableUnderlay()
    {
        if (glowMaterial != null)
        {
            glowMaterial.DisableKeyword("UNDERLAY_ON");
        }
    }

}
