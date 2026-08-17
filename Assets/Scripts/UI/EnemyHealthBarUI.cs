using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 敌人血条
/// </summary>
public class EnemyHealthBarUI : MonoBehaviour
{
    [Tooltip("血条填充块")]
    public Image healthSlider;

    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
        healthSlider.fillAmount = 1; // 填满血条
    }

    void Update()
    {
        // 计算UI到摄像机的方向
        Vector3 dir = cameraTransform.position - transform.position;
        // 让血条始终面向摄像机
        transform.rotation = Quaternion.LookRotation(-dir);
    }

    /// <summary>
    /// 更新血条
    /// </summary>
    /// <param name="healthRatio">血量比例</param>
    public void UpdateHealthBar(float healthRatio)
    {
        healthSlider.fillAmount = healthRatio;
    }
}