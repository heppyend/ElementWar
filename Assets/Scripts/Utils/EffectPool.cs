using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用特效对象池（全局单例，未挂载时自动创建）
///
/// 按「预制体」分池：Dictionary<预制体, Queue<实例>>。
/// 三个角色统一走这里：枪口火花、子弹命中特效、敌人受击特效。
/// 回池依据 ParticleSystem 是否存活（IsAlive），播完自动禁用并回收复用，
/// 避免每次 Instantiate/Destroy 的 GC 压力。
/// </summary>
public class EffectPool : SingleMonoBase<EffectPool>
{
    private readonly Dictionary<GameObject, Queue<GameObject>> pools =
        new Dictionary<GameObject, Queue<GameObject>>();

    /// <summary>
    /// 安全访问单例：场景未手动挂载时自动创建（保证任何场景特效可用）
    /// </summary>
    public static new EffectPool INSTANCE
    {
        get
        {
            if (SingleMonoBase<EffectPool>.INSTANCE == null)
            {
                var go = new GameObject("EffectPool");
                go.AddComponent<EffectPool>();
            }
            return SingleMonoBase<EffectPool>.INSTANCE;
        }
    }

    /// <summary>
    /// 从池中取出（或实例化）特效，播完自动回池
    /// </summary>
    /// <param name="prefab">特效预制体</param>
    /// <param name="position">生成位置</param>
    /// <param name="rotation">生成旋转</param>
    /// <returns>取出的特效对象（可能为 null：预制体未配置）</returns>
    public GameObject GetEffect(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        GameObject effect;
        if (TryDequeue(prefab, out effect))
        {
            // 从池中复用：重新定位并激活
            effect.transform.position = position;
            effect.transform.rotation = rotation;
            effect.SetActive(true);
            // 显式重启粒子，确保复用时从头播放
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }
        }
        else
        {
            // 池空 → 实例化
            effect = Instantiate(prefab, position, rotation);
        }

        // 启动"播完回池"协程
        StartCoroutine(ReturnWhenDone(effect, prefab));
        return effect;
    }

    /// <summary>
    /// 尝试从池中取出一个未激活的实例
    /// </summary>
    private bool TryDequeue(GameObject prefab, out GameObject effect)
    {
        effect = null;
        Queue<GameObject> queue;
        if (pools.TryGetValue(prefab, out queue) && queue.Count > 0)
        {
            // 跳过可能已被销毁的失效对象（防御）
            while (queue.Count > 0)
            {
                effect = queue.Dequeue();
                if (effect != null) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 等待粒子播完（或超时）后回池
    /// </summary>
    private IEnumerator ReturnWhenDone(GameObject effect, GameObject prefab)
    {
        ParticleSystem ps = effect != null ? effect.GetComponent<ParticleSystem>() : null;

        // 等待粒子播完：
        // - stopAction=Disable 时播完会 SetActive(false)，直接命中
        // - 未配置 stopAction 时靠 IsAlive() 判断播放是否结束
        float timeout = 10f;
        float timer = 0f;
        while (effect != null && timer < timeout)
        {
            timer += Time.deltaTime;
            if (!effect.activeSelf)
            {
                break;
            }
            if (ps != null && !ps.IsAlive() && ps.isPlaying == false)
            {
                break;
            }
            yield return null;
        }

        if (effect == null) yield break;//已被外部销毁

        // 手动停用（粒子未播完被超时兜底时也停）
        if (effect.activeSelf)
            effect.SetActive(false);

        // 回池
        Queue<GameObject> queue;
        if (!pools.TryGetValue(prefab, out queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }
        queue.Enqueue(effect);
    }
}
