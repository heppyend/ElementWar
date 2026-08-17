using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家武器（含子弹对象池）
/// </summary>
public class PlayerWeapon : MonoBehaviour
{
    [Tooltip("子弹生成的位置")]
    public Transform bulletSpawnPoint;
    [Tooltip("子弹预制体")]
    public PlayerWeaponBullet bulletEffectPrefab;
    [Tooltip("枪管火花预制体")]
    public GameObject bulletSparkPrefab;
    [Tooltip("子弹发射间隔")]
    public float bulletInterval = 0.15f;

    private float lastFireTime;//上一次子弹发射的时间
    private Queue<PlayerWeaponBullet> bulletPool = new Queue<PlayerWeaponBullet>();//子弹对象池

    /// <summary>
    /// 朝着targetPos方向发射子弹
    /// </summary>
    /// <param name="targetPos"></param>
    public void Fire(Vector3 targetPos)
    {
        //检测发射间隔
        if (Time.time - lastFireTime < bulletInterval)
        {
            return;
        }
        lastFireTime = Time.time;
        //计算发射方向
        Vector3 direction = targetPos - bulletSpawnPoint.position;
        direction.Normalize();

        //从对象池取出（或实例化）子弹并复位发射
        PlayerWeaponBullet bulletEffect = GetBullet();
        bulletEffect.ResetBullet(bulletSpawnPoint.position, direction);

        //枪口火花：走全局特效对象池（播完自动回池，替代每次 Instantiate）
        EffectPool.INSTANCE.GetEffect(bulletSparkPrefab, bulletSpawnPoint.position, Quaternion.LookRotation(direction));
    }

    /// <summary>
    /// 从对象池取出子弹（池空则实例化新子弹）
    /// </summary>
    private PlayerWeaponBullet GetBullet()
    {
        if (bulletPool.Count > 0)
        {
            PlayerWeaponBullet bullet = bulletPool.Dequeue();
            bullet.ownerWeapon = this;
            bullet.gameObject.SetActive(true);//激活（触发 OnEnable 启动生命周期）
            return bullet;
        }
        PlayerWeaponBullet newBullet = Instantiate(bulletEffectPrefab, bulletSpawnPoint.position, Quaternion.identity);
        newBullet.ownerWeapon = this;
        return newBullet;
    }

    /// <summary>
    /// 回收子弹到对象池
    /// </summary>
    /// <param name="bullet"></param>
    public void RecycleBullet(PlayerWeaponBullet bullet)
    {
        bullet.gameObject.SetActive(false);//停用（触发 OnDisable 停止计时/清速度）
        bulletPool.Enqueue(bullet);
    }
}
