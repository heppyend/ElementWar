using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// 玩家控制器
/// </summary>
public class PlayerController : SingleMonoBase<PlayerController>
{

    public PlayerModel currentPlayerModel;//当前所操作的角色模型
    private Transform cameraTransform;

    [Tooltip("正常视角相机")]
    public CinemachineFreeLook freeLookCamera;
    [Tooltip("瞄准视角相机")]
    public CinemachineFreeLook aimingCamera;



    #region 玩家输入相关
    private MyInputSystem input;//输入系统
    [HideInInspector]
    public Vector2 moveInput;//移动输入
    [HideInInspector]
    public bool isSprint;//冲刺输入
    [HideInInspector]
    public bool isAiming;//瞄准输入
    [HideInInspector]
    public bool isJumping;//跳跃输入
    [HideInInspector]
    public bool isFire;//开火输入
    [HideInInspector]
    public bool isSlide;//滑铲输入
    #endregion

    #region 瞄准相关

    [Tooltip("瞄准目标")]
    public Transform AimTarget;
    [Tooltip("射线检测的最大距离")]
    public float maxRayDistance = 1000f;
    [Tooltip("射线检测的层级")]
    public LayerMask aimLayerMask = ~0;

    #endregion

    #region 开火抖动
    private CinemachineImpulseSource impulseSource;
    #endregion

    [Tooltip("转向速度")]
    public float rotationSpeed = 300;


    [HideInInspector]
    public Vector3 localMovement;//本地空间下的玩家移动方向
    [HideInInspector]
    public Vector3 worldMovement;//世界空间下的玩家移动方向



    protected override void Awake()
    {
        base.Awake();
        input = new MyInputSystem();
    }
    void Start()
    {
        cameraTransform=Camera.main.transform;
        Cursor.lockState = CursorLockMode.Locked;//锁定光标
        ExitAim();//默认游戏开始时退出瞄准视角,使用自由相机
        impulseSource=aimingCamera.GetComponent<CinemachineImpulseSource>();
        ResetCameraTarget();
        EnsureImpulseListener();//保证射击/受击抖动生效
    }

    /// <summary>
    /// 为两架虚拟相机补上 CinemachineImpulseListener。
    /// ⚠️ ImpulseListener 是 CinemachineExtension，必须挂在虚拟相机（CinemachineFreeLook）上，
    /// 挂到 Main Camera（只有 CinemachineBrain）会报 "CinemachineExtension requires a virtual camera"。
    /// 两架相机都挂：正常/瞄准任一视角生效，发射的 Impulse 由当前激活相机接收。
    /// </summary>
    private void EnsureImpulseListener()
    {
        if (freeLookCamera != null && freeLookCamera.GetComponent<CinemachineImpulseListener>() == null)
            freeLookCamera.gameObject.AddComponent<CinemachineImpulseListener>();
        if (aimingCamera != null && aimingCamera.GetComponent<CinemachineImpulseListener>() == null)
            aimingCamera.gameObject.AddComponent<CinemachineImpulseListener>();
    }

    
    void Update()
    {
        #region 更新玩家输入
        moveInput = input.Player.Move.ReadValue<Vector2>().normalized;
        isSprint = input.Player.IsSprint.IsPressed();
        isAiming = input.Player.IsAiming.IsPressed();
        isJumping = input.Player.IsJumping.triggered;
        isFire=input.Player.Fire.IsPressed();
        isSlide = input.Player.IsSlide.triggered;//单击触发一次滑铲（避免长按循环触发）
        #endregion

        #region 计算玩家移动方向
        //获取相机方向向量
        Vector3 cameraForwardProjection =new Vector3(cameraTransform.forward.x,0,cameraTransform.forward.z).normalized;
        //计算世界空间下的方向向量
        worldMovement=cameraForwardProjection*moveInput.y+cameraTransform.right*moveInput.x;
        //将世界空间下的方向向量转换为模型本地空间下的方向向量
        localMovement = currentPlayerModel.transform.InverseTransformVector(worldMovement);
        #endregion

        #region 切换角色输入监听
        if (input.Player.First.triggered)
        {
            SwitchPlayerModel(0);
        }
        else if (input.Player.Second.triggered) { 
            SwitchPlayerModel(1);

        }
        else if (input.Player.Third.triggered)
        {
            SwitchPlayerModel(2);

        }

        #endregion

    }

    /// <summary>
    /// 切换角色（数字键 1/2/3/4）
    /// </summary>
    /// <param name="index"></param>
    public void SwitchPlayerModel(int index)
    {
        if (GameManager.INSTANCE == null || GameManager.INSTANCE.playerModels == null)
            return;
        if (index < 0 || index >= GameManager.INSTANCE.playerModels.Length)
            return;
        PlayerModel target = GameManager.INSTANCE.playerModels[index];
        if (target == null || target.isDead)
        {
            Debug.LogWarning("该角色已死亡，无法切换。");
            return;//已死亡角色不可访问/切换
        }
        if (target == currentPlayerModel) return;//已是当前控制角色
        SwitchToPlayer(target);
    }

    /// <summary>
    /// 切换控制权到指定角色（旧角色仅当存活时才走 Exit 转随从）
    /// </summary>
    private void SwitchToPlayer(PlayerModel target)
    {
        if (currentPlayerModel != null && !currentPlayerModel.isDead)
            currentPlayerModel.Exit();//旧角色转随从跟随（死亡则不执行）
        currentPlayerModel = target;
        currentPlayerModel.Enter();//新角色转主控
        ResetCameraTarget();
    }

    /// <summary>
    /// 角色死亡回调：
    /// - 随从死亡 → 仅不可访问（已由 isDead 拦截切换）
    /// - 主控死亡 → 自动切换视角到下一位存活的角色（按 1/2/3/4 数组顺序）
    /// - 全部死亡 → 弹出 GAME OVER，任意键返回游戏开始界面
    /// </summary>
    public void OnPlayerDied(PlayerModel deadPlayer)
    {
        if (deadPlayer != currentPlayerModel) return;//随从死亡：无需处理

        PlayerModel next = FindNextAlivePlayer();
        if (next != null)
        {
            // 主控死亡但还有随从存活 → 自动接管下一角色
            currentPlayerModel = next;
            currentPlayerModel.Enter();
            ResetCameraTarget();
            Debug.LogWarning($"主控 {deadPlayer.name} 死亡，自动切换至 {next.name}。");
        }
        else
        {
            // 全部死亡 → GAME OVER
            Debug.LogWarning("所有角色死亡，游戏结束。");
            if (GetComponent<GameOverUI>() == null)
                gameObject.AddComponent<GameOverUI>();
        }
    }

    /// <summary>
    /// 按数组顺序查找下一个存活角色
    /// </summary>
    private PlayerModel FindNextAlivePlayer()
    {
        if (GameManager.INSTANCE == null || GameManager.INSTANCE.playerModels == null)
            return null;
        foreach (PlayerModel player in GameManager.INSTANCE.playerModels)
        {
            if (player != null && !player.isDead)
                return player;
        }
        return null;
    }



    /// <summary>
    /// 进入瞄准
    /// </summary>
    public void EnterAim()
    {
        //同步瞄准相机和自由相机的旋转角度
        aimingCamera.m_XAxis.Value = freeLookCamera.m_XAxis.Value;
        aimingCamera.m_YAxis.Value = freeLookCamera.m_YAxis.Value;

        currentPlayerModel.EnterAim();


        //设置相机的优先级,使瞄准相机生效
        freeLookCamera.Priority = 0;
        aimingCamera.Priority = 100;
    }

    /// <summary>
    /// 退出瞄准
    /// </summary>
    public void ExitAim()
    {
        //同步自由相机和瞄准相机的旋转角度
        freeLookCamera.m_XAxis.Value = aimingCamera.m_XAxis.Value;
        freeLookCamera.m_YAxis.Value = aimingCamera.m_YAxis.Value;

        currentPlayerModel.ExitAim();

        //设置相机的优先级,使瞄准相机生效
        freeLookCamera.Priority = 100;
        aimingCamera.Priority = 0;
    }

    /// <summary>
    /// 重置摄像机瞄准目标
    /// </summary>
    public void ResetCameraTarget()
    {
        aimingCamera.Follow = currentPlayerModel.transform;
        aimingCamera.LookAt = currentPlayerModel.transform;
        freeLookCamera.Follow = currentPlayerModel.transform;
        freeLookCamera.LookAt = currentPlayerModel.transform;
    }





    /// <summary>
    /// 抖动屏幕
    /// </summary>
    public void ShakeCamera()
    {
        impulseSource.GenerateImpulse();
    }

    private void OnEnable()
    {
        input.Enable();
    }

    private void OnDisable()
    {
        input.Disable();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        input?.Dispose();
    }

}
