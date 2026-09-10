using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public enum PlayerState
{
    Idle,Move,Hover,Aiming,Slide,Sprint
}
/// <summary>
/// 角色模型
/// </summary>
public class PlayerModel : MonoBehaviour,IStateMachineOwner
{
    [Tooltip("角色武器")]
    public PlayerWeapon weapon;

    [HideInInspector]
    public Animator animator;
    [HideInInspector]
    public CharacterController cc;
    private StateMachine stateMachine;//动画状态机
    private PlayerState currentState;//当前状态
    /// <summary>当前状态机状态（只读，诊断用）。</summary>
    public PlayerState CurrentState => currentState;

    #region 约束相关
    public TwoBoneIKConstraint rightHandConstraint;//正常状态下的右手约束
    public MultiAimConstraint rightHandAimConstraint;//瞄准状态下的右手约束
    public MultiAimConstraint bodyAimConstraint;//身躯约束
    #endregion

    #region 垂直速度相关
    [Tooltip("重力")]
    public float gravity = -15;
    [Tooltip("跳跃高度")]
    public float jumpHeight = 1.5f;
    [HideInInspector]
    public float verticalSpeed;//当前垂直方向的速度
    [Tooltip("离地触发跳跃阈值")]
    public float fallHeight = 0.2f;//离地触发跳跃阈值

    /// <summary>
    /// 连续离地帧数计数器（防止斜坡 isGrounded 抖动导致状态来回切换）
    /// </summary>
    [HideInInspector]
    public int ungroundedFrameCount = 0;
    /// <summary>
    /// 需要连续离地多少帧才真正切为 Hover 状态（同时也决定重力延迟生效窗口）
    /// 值越大对斜坡/冲刺抖动容忍度越高，但走平台边缘到开始下落的"土狼时间"也越长
    /// 当前 5 帧 ≈ 0.083s@60fps，兼顾陡坡冲刺稳定性与手感
    /// </summary>
    public const int HOVER_STABILITY_FRAMES = 5;
    #endregion

    #region 滑铲参数（Inspector 可调）
    [Tooltip("滑铲持续时间（秒）")]
    public float slideDuration = 0.8f;
    [Tooltip("滑铲初速度")]
    public float slideStartSpeed = 7f;
    [Tooltip("滑铲末速度（衰减到该值）")]
    public float slideEndSpeed = 1.5f;
    [Tooltip("冲刺时滑铲初速度加成")]
    public float sprintSlideBoost = 2f;
    #endregion

    #region FPS 式移动（useFPSMovement=true 时生效，New Scene 方案）
    [Tooltip("是否使用 FPS 式代码驱动移动（普通移动用 CLazyRunner 跑酷系，瞄准走射保留 X Bot）。\n" +
             "开启后 applyRootMotion 保持 true（Animation Rigging 依赖它求值），根运动由 OnAnimatorMove 拦截丢弃，位移全由 LateUpdate 的 cc.Move 驱动")]
    public bool useFPSMovement = true;
    [Tooltip("跳跃是否在 Hunter_Parkour 控制器的 3 个跑酷跳跃片段（jmp_base_B / jmp_Move_left / jmp_BackAir）中随机选 1 个播放。\n" +
             "仅 Hunter 启用，其他角色为 false 不受影响")]
    public bool randomJumpClips = false;
    [Tooltip("跳跃空中水平速度衰减（0=真空斜抛水平匀速，最远；越大落点越近越可控）")]
    public float jumpAirDrag = 1.5f;
    [HideInInspector]
    public Vector3 jumpHorizontalVelocity;//跳跃水平初速度（斜抛：PlayerHoverState.Enter 记录，空中带空气阻力衰减）
    [Tooltip("禁用状态机与 CharacterController 位移（PVP 网络玩家用）：由 PvPMotor 驱动 transform.position 和 Animator，\n" +
             "服务器与客户端使用同一简化移动模型。PVE 为 false 不受影响")]
    public bool disableStateMachine = false;
    [Tooltip("行走速度")]
    public float walkSpeed = 2.2f;
    [Tooltip("移动(慢跑)速度")]
    public float jogSpeed = 5f;
    [Tooltip("冲刺速度")]
    public float sprintSpeed = 8f;
    [Tooltip("Speed 参数变化速率（越大越灵敏）")]
    public float speedLerpSpeed = 4f;
    [Tooltip("空中水平控制速度")]
    public float airControlSpeed = 6f;
    [Tooltip("瞄准时移动速度")]
    public float aimMoveSpeed = 2.5f;

    /// <summary>混合树关键速度节点（与 TPS_Movement.controller Locomotion 树阈值对应）</summary>
    public const float IDLE_BLEND = 0f;
    public const float WALK_BLEND = 0.33f;
    public const float JOG_BLEND = 0.66f;
    public const float SPRINT_BLEND = 1f;

    /// <summary>TPS 移动控制器参数 hash（TPS_Movement.controller）</summary>
    public static readonly int SpeedHash = Animator.StringToHash("Speed");
    public static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    public static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    public static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    public static readonly int AimingXHash = Animator.StringToHash("AimingX");
    public static readonly int AimingYHash = Animator.StringToHash("AimingY");
    public static readonly int HoverClipHash = Animator.StringToHash("HoverClip");//Hunter 跑酷包随机跳跃混合树参数

    [HideInInspector]
    public Vector3 horizontalVelocity;//当前水平移动速度（状态类每帧写入）
    [HideInInspector]
    public float speedBlend;//Locomotion 混合树 Speed 参数当前值
    [HideInInspector]
    public bool isSprinting;//是否冲刺
    #endregion

    #region 玩家在地面时的前三帧速度的缓存
    private static readonly int CACHE_SIZE = 3;
    Vector3[] speedCache = new Vector3[CACHE_SIZE]; //动画前三帧的玩家速度
    private int speedCache_index= 0;//缓存保留的位置
    private Vector3 averageDeltaMovement;//平均速度
    #endregion

    #region 生命值相关
    // 对应 Assets/CombatGirlsCharacterPack/RifleGirl/Animations/Normal 下 FBX 导出的 Clip 名。
    private const string DEFAULT_HIT_ANIMATION = "Hit1";
    private const string DEFAULT_DEAD_ANIMATION = "Die2";

    [Tooltip("最大生命值")]
    public int maxHealth = 100;
    [HideInInspector]
    public int currentHealth;//当前生命值
    [HideInInspector]
    public bool isDead;//是否已死亡
    [Tooltip("受击动画名（留空则不播放，需 Animator 中存在对应 clip）")]
    public string hitAnimationName = "";
    [Tooltip("受击专用 Animator 层名。该层必须是 Override + 上半身 Avatar Mask；未配置时受击动作安全跳过，不再覆盖移动层。")]
    public string hitReactionLayerName = "HitUpperBody";
    [Tooltip("受击层显示时长（秒）。到期后淡出，底层 Move/Sprint 始终持续驱动下半身。")]
    [Range(0.05f, 1f)]
    public float hitReactionDuration = 0.32f;
    [Tooltip("受击层淡出耗时（秒）。")]
    [Range(0.01f, 0.3f)]
    public float hitReactionFadeOutDuration = 0.06f;
    [Tooltip("死亡动画名（留空则不播放，需 Animator 中存在对应 clip）")]
    public string deadAnimationName = "";
    [Tooltip("受击时是否震动相机")]
    public bool shakeOnHit = true;
    [Tooltip("玩家血条预制体（复用敌人 HealthBar.prefab，含 EnemyHealthBarUI 血条逻辑）")]
    public GameObject healthBarPrefab;
    [Tooltip("血条相对角色头顶的高度")]
    public float healthBarHeight = 2.3f;
    [HideInInspector]
    public PlayerHealthBar playerHealthBar;//玩家血条（Awake 时动态挂载，仅主控显示）
    #endregion

    #region 受击表现与运行时诊断
    [Header("PVE 运行时状态诊断")]
    [Tooltip("PVE 运行时在头顶显示 FSM、Animator Clip 与受击层状态，便于排查动画覆盖。")]
    public bool showStateDebugLabel = true;
    [Tooltip("状态诊断标签相对角色根节点的高度。")]
    public float stateDebugLabelHeight = 2.95f;
    [Tooltip("状态标签字体；留空时使用 TMP 默认字体。")]
    public TMP_FontAsset stateDebugFont;
    [Tooltip("状态标签文字大小。")]
    [Range(0.5f, 8f)]
    public float stateDebugFontSize = 3.2f;
    [Tooltip("状态标签世界缩放。")]
    [Range(0.03f, 0.3f)]
    public float stateDebugLabelScale = 0.1f;
    [Tooltip("状态标签文字颜色。")]
    public Color stateDebugTextColor = new Color(0.35f, 0.95f, 1f, 1f);
    [Tooltip("状态标签描边颜色。")]
    public Color stateDebugOutlineColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("状态标签描边宽度。")]
    [Range(0f, 0.5f)]
    public float stateDebugOutlineWidth = 0.16f;
    [Tooltip("勾选时额外显示 Animator 当前 Clip；关闭后只显示业务 FSM 状态。")]
    public bool stateDebugShowAnimatorClips = true;
    [Tooltip("状态标签刷新间隔（秒）。")]
    [Range(0.02f, 1f)]
    public float stateDebugRefreshInterval = 0.1f;
    [HideInInspector]
    public PlayerStateDebugLabel stateDebugLabel;

    private int hitReactionLayerIndex = -1;
    private float hitReactionEndTime = -1f;
    #endregion

    #region 人机相关
    [HideInInspector]
    public NavMeshAgent navMeshAgent;
    public float stoppingDistance = 2f;//停止跟随距离
    #endregion
    private void Awake()
    {
        stateMachine = new StateMachine(this);
        animator = GetComponent<Animator>();
        cc = GetComponent<CharacterController>();
        navMeshAgent=GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
            navMeshAgent.stoppingDistance = stoppingDistance;
        currentHealth = maxHealth;//初始化生命值
        // FPS 式移动：位移全由代码（LateUpdate）驱动。
        // ⚠️ Apply Root Motion 必须保持【开启】：Animation Rigging 的约束（TwoBoneIK/MultiAim）依赖它才求值
        //    （Unity 已知问题：applyRootMotion=false 时约束失效）。根运动位移由 OnAnimatorMove 拦截丢弃
        //    （不应用到 transform），位移仍由 LateUpdate 的 cc.Move 驱动，杜绝 root motion 绕过 CC 穿模。
        if (useFPSMovement && animator != null)
            animator.applyRootMotion = true;
        // 动态挂载玩家血条（复用敌人 HealthBar 预制体逻辑，仅主控显示）
        playerHealthBar = GetComponent<PlayerHealthBar>();
        if (playerHealthBar == null)
            playerHealthBar = gameObject.AddComponent<PlayerHealthBar>();
    }
    void Start()
    {
        // angularSpeed 依赖 PlayerController.INSTANCE，延迟到 Start 赋值（此时所有 Awake 已执行完毕）；PVP 场景无 PlayerController，判空兜底
        if (PlayerController.INSTANCE != null && navMeshAgent != null)
            navMeshAgent.angularSpeed = PlayerController.INSTANCE.rotationSpeed;
        // PVP 网络玩家（disableStateMachine）：状态机/位移由 PvPMotor 接管，跳过状态机启动
        if (!disableStateMachine)
            SwitchState(PlayerState.Idle);
        ExitAim();

        CacheHitReactionLayer();
        // PVE 才自动建立头顶诊断；PVP 由 NetClient/RemoteAvatar 在网络身份就绪后显式建立。
        if (!disableStateMachine && PlayerController.INSTANCE != null && showStateDebugLabel)
            EnsureStateDebugLabel();
    }

   
    void Update()
    {

    }

    void LateUpdate()
    {
        UpdateHitReactionLayer();
        // FPS 式移动：位移完全由代码驱动（水平 horizontalVelocity + 垂直 verticalSpeed）。
        // 用 LateUpdate 而非 Update：确保状态类（经 MonoManager 集中式 Update，通常早于本帧）
        // 先写入 horizontalVelocity，本 LateUpdate 再 Move——避免"先移动后写值"导致位移被吞。
        if (!useFPSMovement || cc == null || isDead) return;
        // PVP 网络玩家：位移由 PvPMotor 驱动 transform，CC 位移完全关闭
        if (disableStateMachine) return;
        // 人机（非主控）：位移由 NavMeshAgent 全权驱动，不调用 cc.Move——
        // 否则 CharacterController 每帧改 transform 与 NavMeshAgent 抢位置，寻路被干扰导致原地不动
        if (PlayerController.INSTANCE == null || PlayerController.INSTANCE.currentPlayerModel != this) return;
        Vector3 delta = horizontalVelocity * Time.deltaTime;
        delta.y = verticalSpeed * Time.deltaTime;
        cc.Move(delta);
    }

    /// <summary>
    /// 进入模型
    /// </summary>
    public void Enter()
    {
        navMeshAgent.enabled = false;
    }

    /// <summary>
    ///退出模型（转为随从跟随）
    /// </summary>
    public void Exit()
    {
        if (navMeshAgent != null)
        {
            // 角色不在 NavMesh 附近时先校正到最近网格点，
            // 否则 enabled=true 会报 "not close enough to the NavMesh"，导致随从无法跟随
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            navMeshAgent.enabled = true;
        }
        SwitchState(PlayerState.Idle);
    }

    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage">伤害值</param>
    public void TakeDamage(int damage)
    {
        if (isDead) return;//已死亡不再受击
        currentHealth -= damage;

        // 受击只在专用上半身层播放；第 0 层继续维持 Move/Sprint，避免随从待机平移。
        PlayHitReaction(GetAnimationName(hitAnimationName, DEFAULT_HIT_ANIMATION));

        // 相机震动反馈
        if (shakeOnHit && PlayerController.INSTANCE != null)
            PlayerController.INSTANCE.ShakeCamera();

        // 更新血条 HUD（仅主控显示）
        if (playerHealthBar != null && PlayerController.INSTANCE != null &&
            PlayerController.INSTANCE.currentPlayerModel == this)
            playerHealthBar.SetHealth((float)currentHealth / maxHealth);

        // 生命值归零 → 死亡
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    /// <summary>
    /// 死亡
    /// </summary>
    private void Die()
    {
        isDead = true;

        // 死亡动画：未在 Inspector 覆盖时，3 个 PVE 角色统一使用 RifleGirl 的死亡状态名。
        ClearHitReaction();
        PlayReactionAnimation(GetAnimationName(deadAnimationName, DEFAULT_DEAD_ANIMATION), 0.1f);

        // 停止状态机（状态 Update 注销），禁用移动与寻路
        stateMachine.Stop();
        cc.enabled = false;
        navMeshAgent.enabled = false;
        Debug.LogWarning($"{name} 已死亡。");//当前无死亡动画，Log 提示玩家角色状态
        // 通知 PlayerController：随从死亡仅拦截切换；主控死亡触发自动接管/游戏结束
        if (PlayerController.INSTANCE != null)
            PlayerController.INSTANCE.OnPlayerDied(this);
    }



    /// <summary>PVP 网络死亡（disableStateMachine 模式下由 NetClient 调用）：置死 + 播死亡动画。</summary>
    public void ApplyDeathNetwork()
    {
        if (isDead) return;
        isDead = true;
        if (cc != null) cc.enabled = false;
        ClearHitReaction();
        PlayReactionAnimation(GetAnimationName(deadAnimationName, DEFAULT_DEAD_ANIMATION), 0.1f);
    }

    /// <summary>PVP 网络重生（disableStateMachine 模式下由 NetClient 调用）：复位血/位置/CC。</summary>
    public void ApplyRespawnNetwork(Vector3 pos, int health, int maxHealthValue)
    {
        isDead = false;
        ClearHitReaction();
        currentHealth = health;
        maxHealth = maxHealthValue;
        transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    /// <summary>PVP 仅在收到服务器权威 HitEvent 后调用，不参与伤害或生命判定。</summary>
    public void PlayAuthoritativeHitReaction()
    {
        if (isDead) return;
        PlayHitReaction(GetAnimationName(hitAnimationName, DEFAULT_HIT_ANIMATION));
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    /// <param name="state">状态</param>
    public void SwitchState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Idle:
                stateMachine.EnterState<PlayerIdleState>();
                break;
            case PlayerState.Move:
                stateMachine.EnterState<PlayerMoveState>();
                break;
            case PlayerState.Hover:
                stateMachine.EnterState<PlayerHoverState>();
                break;
            case PlayerState.Aiming:
                stateMachine.EnterState<PlayerAimingState>();
                break;
            case PlayerState.Slide:
                stateMachine.EnterState<PlayerSlideState>();
                break;
            case PlayerState.Sprint:
                stateMachine.EnterState<PlayerSprintState>();
                break;
        }
        currentState = state;
    }

    /// <summary>
    /// 播放动画
    /// </summary>
    /// <param name="animationName">动画名称</param>
    /// <param name="transition">过渡时间</param>
    /// <param name="layer">动画层</param>
    public void PlayStateAnimation(string animationName,float transition=0.25f,int layer = 0)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationName)) return;
        animator.CrossFadeInFixedTime(animationName, transition, layer);
    }

    /// <summary>
    /// 播放受击/死亡等可选动作。空字段使用第 0 层的默认状态名；Inspector 也可以填完整状态路径。
    /// 先用 HasState 校验，避免某个角色尚未配置该动作时 Animator 自己刷 GotoState 警告。
    /// </summary>
    private void PlayReactionAnimation(string animationStateName, float transition)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationStateName)) return;

        string fullPath = animationStateName.Contains(".")
            ? animationStateName
            : $"{animator.GetLayerName(0)}.{animationStateName}";
        int stateHash = Animator.StringToHash(fullPath);
        if (!animator.HasState(0, stateHash)) return;

        animator.CrossFadeInFixedTime(stateHash, transition, 0);
    }

    private void CacheHitReactionLayer()
    {
        hitReactionLayerIndex = animator != null && !string.IsNullOrWhiteSpace(hitReactionLayerName)
            ? animator.GetLayerIndex(hitReactionLayerName)
            : -1;
        if (hitReactionLayerIndex >= 0)
            animator.SetLayerWeight(hitReactionLayerIndex, 0f);
    }

    private void PlayHitReaction(string animationStateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationStateName)) return;
        if (hitReactionLayerIndex < 0) CacheHitReactionLayer();
        if (hitReactionLayerIndex < 0) return;

        string fullPath = $"{animator.GetLayerName(hitReactionLayerIndex)}.{animationStateName}";
        int stateHash = Animator.StringToHash(fullPath);
        if (!animator.HasState(hitReactionLayerIndex, stateHash)) return;

        animator.SetLayerWeight(hitReactionLayerIndex, 1f);
        animator.CrossFadeInFixedTime(stateHash, 0.04f, hitReactionLayerIndex);
        hitReactionEndTime = Time.time + hitReactionDuration;
    }

    private void UpdateHitReactionLayer()
    {
        if (animator == null || hitReactionLayerIndex < 0 || hitReactionEndTime < 0f) return;
        if (Time.time < hitReactionEndTime) return;

        float nextWeight = Mathf.MoveTowards(
            animator.GetLayerWeight(hitReactionLayerIndex),
            0f,
            Time.deltaTime / Mathf.Max(0.01f, hitReactionFadeOutDuration));
        animator.SetLayerWeight(hitReactionLayerIndex, nextWeight);
        if (nextWeight <= 0f)
            hitReactionEndTime = -1f;
    }

    private void ClearHitReaction()
    {
        hitReactionEndTime = -1f;
        if (animator != null && hitReactionLayerIndex >= 0)
            animator.SetLayerWeight(hitReactionLayerIndex, 0f);
    }

    /// <summary>建立头顶状态标签；PVP 可传入网络表现状态提供器，避免误报为旧 PVE FSM。</summary>
    public void EnsureStateDebugLabel(System.Func<string> customTextProvider = null)
    {
        if (!showStateDebugLabel) return;
        stateDebugLabel = GetComponent<PlayerStateDebugLabel>();
        if (stateDebugLabel == null)
            stateDebugLabel = gameObject.AddComponent<PlayerStateDebugLabel>();
        stateDebugLabel.SetTextProvider(customTextProvider);
    }

    /// <summary>供头顶诊断使用：FSM 状态、底层/受击层实际 Clip 与控制身份。</summary>
    public string GetStateDebugText()
    {
        string control = PlayerController.INSTANCE != null && PlayerController.INSTANCE.currentPlayerModel == this
            ? "主控"
            : "跟随";
        string life = isDead ? "死亡" : "存活";
        if (!stateDebugShowAnimatorClips)
            return $"{life} | {control}\nFSM: {currentState}";

        string baseClip = GetCurrentClipName(0);
        string hitClip = hitReactionLayerIndex >= 0
            ? (hitReactionEndTime >= 0f ? GetCurrentClipName(hitReactionLayerIndex) : "-")
            : "层缺失";
        return $"{life} | {control}\nFSM: {currentState}\nL0: {baseClip}\n上身: {hitClip}";
    }

    /// <summary>PVP 头顶诊断：网络表现状态由 PvPMotor/RemoteAvatar 传入，Animator Clip 由此模型读取。</summary>
    public string GetPvpStateDebugText(string ownership, string visualState)
    {
        string life = isDead ? "死亡" : "存活";
        if (!stateDebugShowAnimatorClips)
            return $"{life} | PVP {ownership}\n状态: {visualState}";

        string baseClip = GetCurrentClipName(0);
        string hitClip = hitReactionLayerIndex >= 0
            ? (hitReactionEndTime >= 0f ? GetCurrentClipName(hitReactionLayerIndex) : "-")
            : "层缺失";
        return $"{life} | PVP {ownership}\n状态: {visualState}\nL0: {baseClip}\n上身: {hitClip}";
    }

    private string GetCurrentClipName(int layer)
    {
        if (animator == null || layer < 0 || layer >= animator.layerCount) return "-";
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(layer);
        return clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "-";
    }

    /// <summary>
    /// RifleGirl 的 Die2 带有遗留的武器挂点 Animation Event（字符串参数）。
    /// 旧 PVE 不使用该套挂点切换，保留空接收器以消除无接收者报错。
    /// </summary>
    public void SwitchSocket(string socketName)
    {
    }

    private static string GetAnimationName(string configuredName, string defaultName)
    {
        return string.IsNullOrWhiteSpace(configuredName) ? defaultName : configuredName;
    }

    /// <summary>Locomotion Speed 参数按速率平滑逼近目标值（FPS 式移动）</summary>
    public void LerpSpeedTo(float target)
    {
        speedBlend = Mathf.MoveTowards(speedBlend, target, speedLerpSpeed * Time.deltaTime);
        SetSpeed(speedBlend);
    }

    /// <summary>立即设置 Speed 参数</summary>
    public void SetSpeed(float value)
    {
        speedBlend = value;
        if (animator != null) animator.SetFloat(SpeedHash, speedBlend);
    }

    /// <summary>把混合树 Speed 值映射为世界移动速度（与 Locomotion 树阈值线性对应）</summary>
    public float GetMoveSpeed(float blend)
    {
        if (blend <= WALK_BLEND)
            return Mathf.Lerp(0f, walkSpeed, blend / WALK_BLEND);
        if (blend <= JOG_BLEND)
            return Mathf.Lerp(walkSpeed, jogSpeed, (blend - WALK_BLEND) / (JOG_BLEND - WALK_BLEND));
        return Mathf.Lerp(jogSpeed, sprintSpeed, (blend - JOG_BLEND) / (SPRINT_BLEND - JOG_BLEND));
    }

    /// <summary>写入 bool 动画参数（null 安全）</summary>
    public void SetBoolParam(int hash, bool value) { if (animator != null) animator.SetBool(hash, value); }

    /// <summary>写入 float 动画参数（null 安全）</summary>
    public void SetFloatParam(int hash, float value) { if (animator != null) animator.SetFloat(hash, value); }

    /// <summary>
    /// 是否悬空（从角色 CC 底部用球形投射检测离地距离是否超过 fallHeight）
    /// </summary>
    /// <returns>true 表示地面距离超过阈值，处于悬空状态</returns>
    public bool IsHover()
    {
        // 计算 CharacterController 的真实底部位置
        float ccBottom = transform.position.y + cc.center.y - cc.height * 0.5f + cc.skinWidth;
        Vector3 bottomPoint = new Vector3(transform.position.x, ccBottom, transform.position.z);

        // 用 SphereCast 替代 Raycast：在斜面上更可靠，不会漏检
        float sphereRadius = cc.radius * 0.6f;
        float castDistance = fallHeight + cc.skinWidth;

        return !Physics.SphereCast(
            bottomPoint + Vector3.up * sphereRadius, // 起点：脚底略上方
            sphereRadius,
            Vector3.down,
            out _,
            castDistance
        );
    }

    /// <summary>
    /// 计算模型前三帧的平均速度
    /// </summary>
    /// <param name="newSpeed">当前速度</param>
    private void UpdateAverageCachsSpeed(Vector3 newSpeed)
    {
        speedCache[speedCache_index++] = newSpeed;
        speedCache_index %= CACHE_SIZE;
        //计算缓存池中的平均速度
        Vector3 sum = Vector3.zero;
        foreach(Vector3 cache in speedCache)
            sum+= cache;
        averageDeltaMovement = sum / CACHE_SIZE;
    }

    private void OnAnimatorMove()
    {
        if (disableStateMachine) return;//PVP 网络玩家：根运动完全丢弃，位移由 PvPMotor 驱动
        if (useFPSMovement) return;//FPS 式移动：拦截并丢弃根运动（applyRootMotion=true 时本回调每帧触发，根运动不应用，位移仍由 LateUpdate 的 cc.Move 驱动）
        if (animator == null) return;
        if (isDead) return;//死亡后不再移动（死亡动画仍由 Animator 播放）
        Vector3 playerDeltaMovement = animator.deltaPosition;//获取动画控制器当前帧的位置信息
        if (currentState == PlayerState.Slide)
        {
            // 滑铲：横向位移完全由 PlayerSlideState 自行驱动（朝主视角），动画根运动只保留姿态。
            // 原因：Running Slide 动画自带的根位移方向是骨骼侧向（非角色正前方），直接使用会导致"向左滑铲/空中飞踢"。
            playerDeltaMovement = Vector3.zero;
        }
        else if (currentState == PlayerState.Sprint || (currentState == PlayerState.Move && isSprinting))
        {
            // 冲刺动画（跑酷 Mvm_Dash）是原地动作无根运动，位移由代码 horizontalVelocity 驱动
            //（FPS 式 PlayerSprintState / 旧方案 PlayerMoveState 每帧写入 worldMovement * 冲刺速度）
            playerDeltaMovement = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z) * Time.deltaTime;
        }
        else if (currentState != PlayerState.Hover)
        {
            UpdateAverageCachsSpeed(animator.velocity);
        }
        else
        {
            // 斜抛：水平用起跳初速度（jumpHorizontalVelocity，Enter 时记录），带空气阻力衰减；
            // 垂直 vy 由下方统一叠加（上抛 + 重力）。
            playerDeltaMovement = jumpHorizontalVelocity * Time.deltaTime;
            jumpHorizontalVelocity *= (1f - jumpAirDrag * Time.deltaTime);
        }
        playerDeltaMovement.y=verticalSpeed*Time.deltaTime;

        // 人机（非主控）：位移由 NavMeshAgent 全权驱动，不调用 cc.Move——
        // 否则 CharacterController 每帧改 transform 与 NavMeshAgent 抢位置，寻路被干扰导致随从原地不动
        if (PlayerController.INSTANCE != null && PlayerController.INSTANCE.currentPlayerModel != this)
        {
            // 人机仍保留动画姿态，但位移交给 NavMeshAgent；这里只消费 root motion 的方向不位移
            return;
        }
        cc.Move(playerDeltaMovement);
    }

    /// <summary>
    /// 进入瞄准
    /// </summary>
    public void EnterAim()
    {
        //启动瞄准约束（Hunter 无武器/无约束时全部为空，null 保护跳过）
        if (rightHandAimConstraint != null) rightHandAimConstraint.weight = 1;
        if (bodyAimConstraint != null) bodyAimConstraint.weight = 1;
        if (rightHandConstraint != null) rightHandConstraint.weight = 0;

    }


    /// <summary>
    /// 退出瞄准
    /// </summary>
    public void ExitAim()
    {
        //关闭瞄准约束（null 保护同 EnterAim）
        if (rightHandAimConstraint != null) rightHandAimConstraint.weight = 0;
        if (bodyAimConstraint != null) bodyAimConstraint.weight = 0;
        if (rightHandConstraint != null) rightHandConstraint.weight = 1;
    }

    /// <summary>
    /// 计算该模型与该玩家当前所控制的模型的距离
    /// </summary>
    /// <returns></returns>
    public float DistanceOfCurrentPlayerModel()
    {
        return Vector3.Distance(transform.position, PlayerController.INSTANCE.currentPlayerModel.transform.position);
    }

    /// <summary>
    /// 人机跟随的目标位置：三角形队形——主控在最前（顶点），随从在主控后方两侧展开（底边）。
    /// 索引 1 → 主控左后，索引 2 → 主控右后，更多随从继续向两侧交替展开。
    /// 主控始终在最前面，随从在后方两侧，形成倒三角/楔形队列。
    /// </summary>
    /// <param name="spacing">随从离主控的后方距离</param>
    /// <param name="spread">随从相对主控前向的侧向张开角度（度），越大两翼越宽</param>
    /// <returns>该随从应前往的目标世界坐标</returns>
    public Vector3 GetFollowerTargetPosition(float spacing = 2.5f, float spread = 35f)
    {
        var pc = PlayerController.INSTANCE;
        if (pc == null || pc.currentPlayerModel == null)
            return transform.position;

        Transform leader = pc.currentPlayerModel.transform;
        int index = 0;
        if (GameManager.INSTANCE != null && GameManager.INSTANCE.playerModels != null)
        {
            for (int i = 0; i < GameManager.INSTANCE.playerModels.Length; i++)
            {
                if (GameManager.INSTANCE.playerModels[i] == this)
                {
                    index = i;
                    break;
                }
            }
        }

        // 以主控为基准：两侧对称展开。索引 1 → 左，索引 2 → 右，索引 3 → 更左，索引 4 → 更右...
        // 后方距离 spacing，侧向按 spread 角度交替左/右
        float sign = (index % 2 == 1) ? -1f : 1f; // 左负右正
        float side = Mathf.Sin(spread * Mathf.Deg2Rad) * sign;
        float back = Mathf.Cos(spread * Mathf.Deg2Rad);

        Vector3 right = leader.right;
        Vector3 backDir = -leader.forward;
        backDir.y = 0f; right.y = 0f;
        right.Normalize(); backDir.Normalize();

        Vector3 dir = (right * side + backDir * back).normalized;
        return leader.position + dir * spacing;
    }

}
