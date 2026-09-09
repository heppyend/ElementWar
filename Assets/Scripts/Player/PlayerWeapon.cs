using System;
using System.Collections;
using System.Collections.Generic;
using ElementWar.Combat;
using UnityEngine;

/// <summary>
/// 玩家武器（含子弹对象池）
/// </summary>
public class PlayerWeapon : MonoBehaviour
{
    /// <summary>
    /// 开火事件（每次成功发射一枪触发；音效等挂载组件订阅，如 WeaponAudio）
    /// </summary>
    public event Action Fired;
    public event Action<CombatResult> CombatResolved;

    [Tooltip("子弹生成的位置")]
    public Transform bulletSpawnPoint;
    [Tooltip("子弹预制体")]
    public PlayerWeaponBullet bulletEffectPrefab;
    [Tooltip("枪管火花预制体")]
    public GameObject bulletSparkPrefab;
    [Tooltip("子弹发射间隔")]
    public float bulletInterval = 0.15f;
    [Header("共享战斗运行时（不新增资产）")]
    public int magazineCapacity = 30;
    public int initialMagazineAmmo = 30;
    public int initialReserveAmmo = 90;
    public int weaponDamage = 10;
    public float reloadDuration = 1.5f;
    [Header("曳光视觉（Inspector 调试）")]
    public bool showTracer = true;
    [Min(0.01f)] public float tracerMaxRange = 200f;
    [Min(0.001f)] public float tracerLifetime = 0.06f;
    [Min(0.001f)] public float tracerStartWidth = 0.03f;
    [Min(0.001f)] public float tracerEndWidth = 0.004f;
    public Color tracerStartColor = new Color(1f, 0.95f, 0.55f, 0.9f);
    public Color tracerEndColor = new Color(1f, 0.5f, 0.15f, 0.2f);
    [Tooltip("可选。留空时使用代码生成的默认透明材质。")]
    public Material tracerMaterial;

    [Header("弹孔视觉（Inspector 调试）")]
    public bool showBulletHoles = true;
    [Min(0.001f)] public float bulletHoleSize = 0.13f;
    [Min(0.01f)] public float bulletHoleLifetime = 4f;
    [Min(0f)] public float bulletHoleSurfaceOffset = 0.002f;
    public bool randomizeBulletHoleRotation = true;
    [Tooltip("可选。留空时使用代码生成的程序化弹孔材质。")]
    public Material bulletHoleMaterial;

    private Queue<PlayerWeaponBullet> bulletPool = new Queue<PlayerWeaponBullet>();//子弹对象池
    private WeaponRuntime combatRuntime;
    private long nextCombatRequestId;
    private float reloadStartedAt = -1f;
    public int MagazineAmmo => combatRuntime != null ? combatRuntime.MagazineAmmo : initialMagazineAmmo;
    public int ReserveAmmo => combatRuntime != null ? combatRuntime.ReserveAmmo : initialReserveAmmo;
    public bool IsReloading => combatRuntime != null && combatRuntime.IsReloading;
    /// <summary>本地 PVE 换弹的归一化进度；未换弹时为 0。</summary>
    public float ReloadProgress => !IsReloading || reloadStartedAt < 0f
        ? 0f
        : Mathf.Clamp01((Time.time - reloadStartedAt) / Mathf.Max(0.001f, reloadDuration));

    private void Awake()
    {
        int safeCapacity = Mathf.Max(1, magazineCapacity);
        combatRuntime = new WeaponRuntime(new WeaponDefinition(safeCapacity, Mathf.Max(1, weaponDamage), Mathf.Max(0f, bulletInterval), Mathf.Max(0f, reloadDuration)), Mathf.Clamp(initialMagazineAmmo, 0, safeCapacity), Mathf.Max(0, initialReserveAmmo));
    }

    private void Update()
    {
        if (combatRuntime != null && combatRuntime.TryCompleteReload(Time.time, out CombatResult result))
        {
            reloadStartedAt = -1f;
            CombatResolved?.Invoke(result);
        }
    }

    /// <summary>
    /// 朝着targetPos方向发射子弹
    /// </summary>
    /// <param name="targetPos"></param>
    public bool Fire(Vector3 targetPos)
    {
        if (combatRuntime == null) return false;
        CombatResult result = combatRuntime.Resolve(new CombatRequest(++nextCombatRequestId, CombatIntentType.Fire, Time.time));
        CombatResolved?.Invoke(result);
        if (result.kind != CombatResultKind.FireAccepted) return false;
        PlayVisualFire(targetPos);
        return true;
    }

    /// <summary>PVP 预测/权威事件使用：只播放视觉，不在客户端扣弹。</summary>
    public void PlayVisualFire(Vector3 targetPos)
    {
        Fired?.Invoke();//广播开火（音效组件订阅）
        //计算发射方向
        Vector3 direction = targetPos - bulletSpawnPoint.position;
        direction.Normalize();

        //从对象池取出（或实例化）子弹并复位发射
        PlayerWeaponBullet bulletEffect = GetBullet();
        bulletEffect.damage = weaponDamage;
        bulletEffect.ResetBullet(bulletSpawnPoint.position, direction);

        //枪口火花：走全局特效对象池（播完自动回池，替代每次 Instantiate）
        EffectPool.INSTANCE.GetEffect(bulletSparkPrefab, bulletSpawnPoint.position, Quaternion.LookRotation(direction));

        // 子弹轨迹（曳光）+ 击中墙体弹孔：一次射线确定视觉终点（与物理子弹同向，仅视觉表现）
        SpawnTracerAndHole(bulletSpawnPoint.position, direction);
    }

    public bool RequestReload()
    {
        if (combatRuntime == null) return false;
        CombatResult result = combatRuntime.Resolve(new CombatRequest(++nextCombatRequestId, CombatIntentType.Reload, Time.time));
        if (result.kind == CombatResultKind.ReloadStarted)
            reloadStartedAt = Time.time;
        CombatResolved?.Invoke(result);
        return result.kind == CombatResultKind.ReloadStarted;
    }

    public void ApplyAuthoritativeAmmo(int magazineAmmo, int reserveAmmo, bool isReloading)
    {
        combatRuntime?.ApplyAuthoritativeState(magazineAmmo, reserveAmmo, isReloading);
        reloadStartedAt = isReloading ? Time.time : -1f;
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

    // ---------------- 子弹轨迹 / 弹孔（纯视觉，与物理子弹同向） ----------------

    private static Material _tracerMat;
    private static Material _holeMat;
    private static Texture2D _holeTex;

    /// <summary>沿开火方向射线确定视觉终点，画曳光 + 在环境表面贴弹孔（角色身上不贴）。</summary>
    private void SpawnTracerAndHole(Vector3 origin, Vector3 dir)
    {
        if (!showTracer && !showBulletHoles) return;
        float maxRange = Mathf.Max(0.01f, tracerMaxRange);
        var selfModel = GetComponentInParent<PlayerModel>();

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 end = origin + dir * maxRange;
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            // 跳过自身角色碰撞体（枪管靠近身体，避免曳光只画出一点/弹孔贴自己脸上）
            var model = hit.collider.GetComponentInParent<PlayerModel>();
            if (model != null && model == selfModel) continue;

            end = hit.point;
            // 弹孔只贴"环境表面"：角色（PlayerModel）与敌人（EnemyBase）身上不贴
            if (showBulletHoles && model == null && hit.collider.GetComponentInParent<EnemyBase>() == null)
                SpawnBulletHole(hit.point, hit.normal);
            break;
        }
        if (showTracer)
            SpawnTracer(origin, end);
    }

    /// <summary>画一条短命曳光线（枪口 → 终点）。</summary>
    private void SpawnTracer(Vector3 start, Vector3 end)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = tracerStartWidth;
        lr.endWidth = tracerEndWidth;
        lr.material = GetTracerMaterial();
        lr.startColor = tracerStartColor;
        lr.endColor = tracerEndColor;
        Destroy(go, tracerLifetime);
    }

    /// <summary>在表面贴一张程序化生成的弹孔贴花（小四边形，几秒后销毁）。</summary>
    private void SpawnBulletHole(Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.name = "BulletHole";
        float size = bulletHoleSize;
        go.transform.localScale = new Vector3(size, size, 1f);
        go.transform.position = point + normal * bulletHoleSurfaceOffset;   // 略离表面防 Z-fight
        go.transform.rotation = Quaternion.LookRotation(-normal); // Quad 可视面为 -Z，让 -Z 朝外
        if (randomizeBulletHoleRotation)
            go.transform.Rotate(0f, 0f, UnityEngine.Random.Range(0f, 360f), Space.Self); // 随机角度避免千篇一律
        go.GetComponent<MeshRenderer>().material = GetHoleMaterial();
        Destroy(go, bulletHoleLifetime);
    }

    private Material GetTracerMaterial()
    {
        if (tracerMaterial != null) return tracerMaterial;
        if (_tracerMat == null)
        {
            var shader = Shader.Find("Sprites/Default"); // URP 兼容的简单透明
            _tracerMat = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            _tracerMat.renderQueue = 4000;
        }
        return _tracerMat;
    }

    private Material GetHoleMaterial()
    {
        if (bulletHoleMaterial != null) return bulletHoleMaterial;
        if (_holeMat == null)
        {
            var shader = Shader.Find("Sprites/Default");
            _holeMat = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            _holeMat.mainTexture = GetHoleTexture();
        }
        return _holeMat;
    }

    /// <summary>程序化弹孔贴图：中心小孔（黑）+ 四周暗色晕染渐隐。</summary>
    private static Texture2D GetHoleTexture()
    {
        if (_holeTex != null) return _holeTex;
        int s = 64;
        _holeTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        _holeTex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s / 2f) / (s / 2f);
                float dy = (y - s / 2f) / (s / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 简单噪声让边缘不规则（程序化生成，无需美术素材）
                float noise = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.2f;
                float dd = d + (noise - 0.1f);
                Color c;
                if (dd < 0.16f) c = new Color(0f, 0f, 0f, 1f);                                  // 中心孔
                else if (dd < 0.55f) c = new Color(0.06f, 0.05f, 0.04f, 1f - (dd - 0.16f) / 0.39f); // 晕染渐隐
                else c = new Color(0f, 0f, 0f, 0f);                                              // 透明
                _holeTex.SetPixel(x, y, c);
            }
        }
        _holeTex.Apply();
        return _holeTex;
    }
}
