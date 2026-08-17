using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家子弹（对象池复用）
/// </summary>
public class PlayerWeaponBullet : MonoBehaviour
{
    [Tooltip("伤害")]
    public int damage = 10;
    [HideInInspector]
    public Rigidbody rb;
    [Tooltip("推力")]
    public float flyPower = 30f;
    [Tooltip("存活时间")]
    public float lifetime = 10f;
    [Tooltip("命中特效预制体（打到任意碰撞体时播放，走特效对象池）")]
    public GameObject impactPrefab;

    [HideInInspector]
    public PlayerWeapon ownerWeapon;//归属武器（回池引用，由武器在取出/实例化时赋值）

    private Vector3 prevPosition;//上一帧位置（帧间射线起点）
    private Coroutine lifetimeCoroutine;//生命周期协程


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        prevPosition = transform.position;
        lifetimeCoroutine = StartCoroutine(AutoReturn(lifetime));//启用时启动生命周期计时
    }

    private void OnDisable()
    {
        // 回池停用：停止计时协程并清空速度，避免复活时残留
        if (lifetimeCoroutine != null)
        {
            StopCoroutine(lifetimeCoroutine);
            lifetimeCoroutine = null;
        }
        rb.velocity = Vector3.zero;
    }

    /// <summary>
    /// 从对象池取出后复位子弹并发射
    /// </summary>
    /// <param name="position">出生位置</param>
    /// <param name="direction">飞行方向</param>
    public void ResetBullet(Vector3 position, Vector3 direction)
    {
        transform.position = position;
        transform.rotation = Quaternion.identity;
        transform.forward = direction;
        prevPosition = position;
        rb.velocity = direction * flyPower;//给子弹一个推力
        CheckInitialOverlap();//若生成在敌人体内立即命中
    }

    /// <summary>
    /// 生命周期结束自动回池
    /// </summary>
    private IEnumerator AutoReturn(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    private void Update()
    {
        CheckCollision();
        prevPosition = transform.position;
    }

    /// <summary>
    /// 检查子弹是否生成在敌人的碰撞体内部
    /// </summary>
    void CheckInitialOverlap()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.1f);
        foreach (var hitCollider in hitColliders)
        {
            EnemyBase enemy = hitCollider.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.Hurt(this, 1);
                PlayImpactEffect(transform.position, -transform.forward);
                ReturnToPool();
                return;
            }
        }
    }

    /// <summary>
    /// 帧间射线碰撞检测（防高速穿透）
    /// </summary>
    void CheckCollision()
    {
        RaycastHit hit;
        Vector3 dir = transform.position - prevPosition;//子弹方向
        float distance = Vector3.Distance(transform.position, prevPosition);//两帧之间的子弹飞行距离

        //绘制线段检测碰撞
        if (Physics.Raycast(prevPosition, dir.normalized, out hit, distance))
        {
            //命中特效（打任意碰撞体都播，走特效对象池）
            PlayImpactEffect(hit.point, hit.normal);

            //检测是否为敌人
            if (hit.collider.CompareTag("Enemy"))
            {
                EnemyBase enemy = hit.collider.GetComponent<EnemyBase>();
                if (enemy != null)
                    enemy.Hurt(this, 1);
            }
            //击中任何碰撞体后回池（防止穿墙）
            ReturnToPool();
        }
    }

    /// <summary>
    /// 播放命中特效（走全局特效对象池）
    /// </summary>
    private void PlayImpactEffect(Vector3 point, Vector3 normal)
    {
        if (impactPrefab == null) return;
        // 特效朝向与表面法线对齐（火花朝向碰撞面）
        Quaternion rot = Quaternion.LookRotation(-normal);
        EffectPool.INSTANCE.GetEffect(impactPrefab, point + normal * 0.01f, rot);
    }

    /// <summary>
    /// 回收到所属武器的对象池（无归属时兜底销毁）
    /// </summary>
    public void ReturnToPool()
    {
        if (ownerWeapon != null)
            ownerWeapon.RecycleBullet(this);
        else
            Destroy(gameObject);
    }
}
