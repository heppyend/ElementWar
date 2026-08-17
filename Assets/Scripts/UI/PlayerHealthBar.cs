using UnityEngine;

/// <summary>
/// 玩家血条（复用敌人 HealthBar.prefab + EnemyHealthBarUI 逻辑）
/// 由 PlayerModel.Awake 动态挂载，仅当前控制的角色显示。
/// 血条填充更新走 EnemyHealthBarUI.UpdateHealthBar，Billboard 面向相机也由它处理。
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    private PlayerModel owner;//所属角色
    private GameObject healthBarInstance;//实例化后的血条
    private EnemyHealthBarUI healthBarUI;//实例上的血条逻辑组件

    private void Awake()
    {
        owner = GetComponent<PlayerModel>();
    }

    private void Start()
    {
        Build();
    }

    private void LateUpdate()
    {
        if (healthBarInstance == null) return;

        // 仅当前被控制的角色显示血条（切换角色后自动切换显示）
        bool isControlled = owner != null && PlayerController.INSTANCE != null
                            && PlayerController.INSTANCE.currentPlayerModel == owner;
        if (healthBarInstance.activeSelf != isControlled)
            healthBarInstance.SetActive(isControlled);

        // 跟随角色头顶
        if (isControlled)
            healthBarInstance.transform.position = owner.transform.position + Vector3.up * owner.healthBarHeight;
    }

    /// <summary>
    /// 实例化血条预制体并挂到世界空间 Canvas 下
    /// </summary>
    private void Build()
    {
        if (healthBarInstance != null) return;
        if (owner == null || owner.healthBarPrefab == null)
        {
            Debug.LogWarning($"{name}: PlayerModel.healthBarPrefab 未配置，玩家血条不显示。请将 HealthBar.prefab 拖入 PlayerModel。");
            return;
        }

        healthBarInstance = Instantiate(owner.healthBarPrefab, owner.transform.position + Vector3.up * owner.healthBarHeight, Quaternion.identity);
        if (UIManager.INSTANCE != null && UIManager.INSTANCE.WorldSpaceCanvas != null)
            healthBarInstance.transform.SetParent(UIManager.INSTANCE.WorldSpaceCanvas.transform);
        healthBarUI = healthBarInstance.GetComponent<EnemyHealthBarUI>();//复用敌人血条逻辑（fill 更新 + Billboard）
    }

    /// <summary>
    /// 更新血条显示
    /// </summary>
    /// <param name="ratio">0~1 血量比例</param>
    public void SetHealth(float ratio)
    {
        if (healthBarUI != null)
            healthBarUI.UpdateHealthBar(ratio);
    }

    private void OnDestroy()
    {
        // 角色销毁时一并销毁血条，避免残留
        if (healthBarInstance != null)
            Destroy(healthBarInstance);
    }
}
