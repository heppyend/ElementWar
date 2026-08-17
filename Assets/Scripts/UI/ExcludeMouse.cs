using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 躲避鼠标（全向、有界、自动回位）
///
/// 关键设计：
/// 1. 用「按钮屏幕中心 ↔ 鼠标」的屏幕像素距离判断，绕开 ScreenPointToLocalPointInRectangle
///    在 Screen Space Camera 下的转换偏差（那会导致"鼠标在哪距离都很小、乱躲"）。
/// 2. 期望位置插值 + 硬边界 clamp：按钮任何情况都不会偏离原位超过 maxAvoidDistance，
///    绝不可能飞出界面。
/// 3. 鼠标远离 → 期望位置回到原位，按钮平滑回位。
/// 4. 入场动画结束（位置稳定 + 最短延迟）后才捕获"回家位置"，避免捕到动画中间值。
/// </summary>
public class ExcludeMouse : MonoBehaviour
{
    [Tooltip("触发躲避的影响半径（屏幕像素）")]
    public float avoidRadius = 100f;
    [Tooltip("最大躲避位移（按钮偏离原位的上限，防止跑出界面）")]
    public float maxAvoidDistance = 120f;
    [Tooltip("位置跟随速度（越大响应越快）")]
    public float moveSpeed = 12f;

    // —— 运行时诊断（切到 Inspector 实时查看，辅助排障）——
    [Header("诊断（运行时）")]
    [SerializeField] private Vector2 diagBtnCenter;//按钮屏幕中心
    [SerializeField] private Vector2 diagMouse;//鼠标屏幕位置
    [SerializeField] private float diagDistance;//鼠标与按钮的屏幕距离
    [SerializeField] private Vector2 diagOriginal;//回家位置
    [SerializeField] private bool diagInitialized;//躲避是否已启用

    private Vector2 originalPosition;//回家位置
    private RectTransform rectTransform;
    private Camera uiCamera;

    private bool initialized;
    private float startTime;//进入场景/激活时刻
    private Vector2 lastPosition;
    private int stableFrames;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        uiCamera = GetComponentInParent<Canvas>()?.worldCamera;
        lastPosition = rectTransform.anchoredPosition;
        startTime = Time.time;
    }

    void Update()
    {
        if (!initialized)
        {
            // 入场动画结束且过了最短延迟后，再捕获回家位置并启用躲避
            // 避免动画播放期间捕获到中间位置导致"一出场就飞"
            Vector2 current = rectTransform.anchoredPosition;
            bool positionStable = Vector2.Distance(current, lastPosition) < 0.01f;
            if (positionStable)
                stableFrames++;
            else
                stableFrames = 0;
            lastPosition = current;

            if (stableFrames >= 6 && Time.time - startTime > 0.8f)
            {
                originalPosition = rectTransform.anchoredPosition;
                diagOriginal = originalPosition;
                initialized = true;
            }
            diagInitialized = initialized;
            return;//初始化前不做任何移动
        }

        if (Mouse.current == null) return;
        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        // 按钮屏幕中心（Screen Space Camera 用其相机，Overlay 传 null 也可）
        Vector2 btnCenter = RectTransformUtility.WorldToScreenPoint(uiCamera, rectTransform.position);
        // 从鼠标指向按钮的屏幕向量
        Vector2 toBtn = btnCenter - mouseScreen;
        float distance = toBtn.magnitude;

        // 写入诊断
        diagBtnCenter = btnCenter;
        diagMouse = mouseScreen;
        diagDistance = distance;
        diagOriginal = originalPosition;

        // 期望位置：鼠标进入影响半径 → 沿远离鼠标方向偏移（越近越远，有界）；否则回原位
        Vector2 desiredPos = originalPosition;
        if (distance < avoidRadius)
        {
            Vector2 avoidDir = distance > 0.001f ? toBtn.normalized : Vector2.up;//中心兜底
            float strength = Mathf.Clamp01(1f - distance / avoidRadius);//0(边缘)~1(正中)
            desiredPos = originalPosition + avoidDir * maxAvoidDistance * strength;
        }

        // 平滑跟随
        Vector2 newPos = Vector2.Lerp(rectTransform.anchoredPosition, desiredPos, moveSpeed * Time.deltaTime);

        // 硬边界 clamp：任何情况都不允许偏离原位超过 maxAvoidDistance（防御性）
        newPos.x = Mathf.Clamp(newPos.x, originalPosition.x - maxAvoidDistance, originalPosition.x + maxAvoidDistance);
        newPos.y = Mathf.Clamp(newPos.y, originalPosition.y - maxAvoidDistance, originalPosition.y + maxAvoidDistance);

        rectTransform.anchoredPosition = newPos;
    }
}
