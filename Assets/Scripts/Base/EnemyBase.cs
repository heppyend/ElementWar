using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle, Move, Attack, Dead
}

/// <summary>
/// 敌人基类
/// </summary>
public abstract class EnemyBase : MonoBehaviour, IStateMachineOwner
{
    [HideInInspector]
    public Animator animator;
    protected StateMechaine stateMachine;

    #region 寻路相关
    [HideInInspector]
    public NavMeshAgent navMeshAgent;//寻路代理
    [Tooltip("转向速度")]
    public float rotationSpeed = 300f;
    [Tooltip("最小攻击距离")]
    public float minAttackDistance = 1f;
    [HideInInspector]
    public PlayerModel attackTarget;
    #endregion

    #region 流血相关预制体
    [Tooltip("喷血溅射特效")]
    public GameObject bloodSmashPrefab;
    [Tooltip("滴血特效")]
    public GameObject bloodDrippingPrefab;
    #endregion

    #region 受击相关
    protected int hitHash;
    protected int moveSpeedHash;
    protected float normalMoveSpeed = 1;
    protected float slowMoveSpeed = 0.5f;
    protected Coroutine recoverSpeedCoroutine;//恢复速度的协程
    [HideInInspector]
    public Collider bodyCollider;//身体碰撞体（死亡时禁用）
    #endregion

    #region 攻击相关
    [Tooltip("攻击伤害")]
    public int attackDamage = 10;
    [Tooltip("攻击冷却时间（秒）")]
    public float attackCooldown = 1.5f;
    [HideInInspector]
    public float lastAttackTime = -999f;//上次攻击完成时间（初始为负值保证首次立即可攻）
    [Tooltip("攻击目标刷新间隔（秒，定期重新寻找最近的存活玩家）")]
    public float attackTargetRefreshInterval = 0.5f;
    private float attackTargetRefreshTimer;//目标刷新计时器
    #endregion

    #region 血条相关
    [Tooltip("生命值")]
    public int health = 100;
    private float currentHealth;
    private bool isDead = false;
    [Tooltip("血条预制体")]
    public GameObject healthBarPrefab;
    [Tooltip("血条的位置")]
    public Transform healthBarPos;
    public GameObject healthBar;//实例化后的血条
    [Tooltip("血条框显示时间")]
    public float healthBarShowTime = 6;
    private float healthBarShow_timer;
    #endregion

    protected virtual void Awake()
    {
        stateMachine = new StateMechaine(this);
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.stoppingDistance = minAttackDistance;
        bodyCollider = GetComponent<Collider>();
        navMeshAgent.angularSpeed= rotationSpeed;
        hitHash = Animator.StringToHash("Hit");
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        currentHealth = health;
        healthBarShow_timer = healthBarShowTime;
    }

    protected virtual void Start()
    {
        SwitchState(EnemyState.Idle);
        FIndAttackTarget();

        #region 实例化血条框
        healthBar = Instantiate(healthBarPrefab, healthBarPos.position, Quaternion.identity);
        healthBar.transform.SetParent(UIManager.INSTANCE.WorldSpaceCanvas.transform);
        #endregion
    }

    protected virtual void Update()
    {
        if(isDead) return;
        #region 定期刷新攻击目标（始终追击最近的存活玩家，避免锁定已死/过远目标）
        attackTargetRefreshTimer += Time.deltaTime;
        if (attackTargetRefreshTimer >= attackTargetRefreshInterval)
        {
            attackTargetRefreshTimer = 0;
            FIndAttackTarget();
        }
        #endregion
        #region 血条相关
        if (healthBarShow_timer < healthBarShowTime)
        {
            healthBar.SetActive(true);
            healthBar.transform.position = healthBarPos.transform.position;
            healthBarShow_timer+= Time.deltaTime;
        }
        else
        {
            healthBar.SetActive(false);
        }
        #endregion
    }



    /// <summary>
    /// 寻找离自身最近的PlayerModel
    /// </summary>
    public virtual void FIndAttackTarget()
    {
        PlayerModel[] playerModels = GameManager.INSTANCE.playerModels;
        if (playerModels != null && playerModels.Length > 0)
        {
            PlayerModel closestPlayer = null;
            float minDistance = float.MaxValue;
            foreach (PlayerModel player in playerModels)
            {
                // 排除空引用与已死亡角色，避免锁定打不动的尸体
                if (player != null && !player.isDead)
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPlayer = player;
                    }

                }

            }
            //设置攻击目标
            attackTarget = closestPlayer;
        }
    }

    /// <summary>
    /// 减慢移动动画播放速度,持续一段时间后恢复
    /// </summary>
    protected virtual void SlowMoveAnimation()
    {
        animator.SetFloat(moveSpeedHash, slowMoveSpeed);
        if (recoverSpeedCoroutine != null) { StopCoroutine(recoverSpeedCoroutine); }
        recoverSpeedCoroutine = StartCoroutine(RecoverMoveSpeed(0.5f));
    
    }
    /// <summary>
    /// 恢复速度
    /// </summary>
    /// <param name="delay"></param>
    /// <returns></returns>
    protected IEnumerator RecoverMoveSpeed(float delay)
    {
        //等待指定时间
        yield return new WaitForSeconds(delay);
        //恢复正常移动速度
        animator.SetFloat(moveSpeedHash, normalMoveSpeed);
        recoverSpeedCoroutine = null;
    }



    /// <summary>
    /// 受击
    /// </summary>
    /// <param name="bullet"></param>
    /// <param name="damageMultiplier"></param>
    public virtual void Hurt(PlayerWeaponBullet bullet,float damageMultiplier = 1)
    {
        #region 受击动画相关
        animator.SetTrigger(hitHash);
        SlowMoveAnimation();
        #endregion

        #region 生成喷血特效（走全局特效对象池，播完自动回池）
        //计算子弹的方向
        Vector3 bulletDir = bullet.transform.forward;
        //根据子弹的方向计算旋转
        Quaternion rotation = Quaternion.LookRotation(-bulletDir);
        EffectPool.INSTANCE.GetEffect(bloodSmashPrefab, bullet.transform.position, rotation);
        #endregion

        #region 生成流血滴落特效（走全局特效对象池）
        EffectPool.INSTANCE.GetEffect(bloodDrippingPrefab, transform.position + Vector3.up * 0.1f, Quaternion.Euler(0, 0, 0));
        #endregion

        #region 血条相关
        currentHealth -= bullet.damage * damageMultiplier;

        if (currentHealth > 0)
        {
            healthBarShow_timer = 0;
            // 更新血条
            healthBar.GetComponent<EnemyHealthBarUI>().UpdateHealthBar(currentHealth / health);
        }
        else
        {
            SwitchState(EnemyState.Dead);
            navMeshAgent.enabled = false;
            if (bodyCollider != null) bodyCollider.enabled = false;//禁用碰撞（null 防御，兼容无 BoxCollider 的敌人）
            currentHealth = 0;
            isDead = true;
            Destroy(healthBar);//销毁血条
        }
        #endregion
    }







    /// <summary>
    /// 是否存在攻击目标
    /// </summary>
    /// <returns></returns>
    public virtual bool HasAttackTarget()
    {
        return attackTarget != null;
    }

    /// <summary>
    /// 攻击目标是否在最短攻击范围内
    /// </summary>
    /// <returns></returns>
    public virtual bool IsAttackTargetInAttackRange()
    {
        if (HasAttackTarget()) {
            return Vector3.Distance(transform.position, attackTarget.transform.position) < minAttackDistance;
        
        }
        return false;
    }


    /// <summary>
    /// 追击目标
    /// </summary>
    public virtual void chaseTarget()
    {
        // NavMeshAgent 未启用或不在烘焙网格上时 SetDestination 会抛错，先校验
        if (HasAttackTarget() && navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.SetDestination(attackTarget.transform.position);
        }
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    /// <param name="state"></param>
    public abstract void SwitchState(EnemyState state);

    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="animationName">动画名称</param>
    /// <param name="transition">过渡时间</param>
    /// <param name="layer">动画层</param>
    public void PlayStateAnimation(string animationName, float transition = 0.25f, int layer = 0)
    {
        animator.CrossFadeInFixedTime(animationName, transition, layer);
    }

    //销毁敌人
    public void Clear()
    {
        stateMachine.Stop();
        Destroy(gameObject);
    }



}
