using UnityEngine;

/// <summary>
/// 可选的武器开火动画桥接。
///
/// 只监听 PlayerWeapon 已确认成功发射的 Fired 事件，不参与子弹、伤害或网络同步。
/// 将它挂到角色根节点后，可让使用独立 Animator Controller 的角色播放单发/连发动画。
/// </summary>
[DisallowMultipleComponent]
public class WeaponFireAnimationBridge : MonoBehaviour
{
    [Tooltip("要监听的玩家武器。留空时从子物体中查找。")]
    public PlayerWeapon weapon;

    [Tooltip("播放开火参数的 Animator。留空时使用本物体或父物体上的 Animator。")]
    public Animator animator;

    [Tooltip("每次 PlayerWeapon 成功发射一枪时触发。留空则不触发单发动画。")]
    public string fireTrigger = "Fire";

    [Tooltip("按住开火期间维持为 true。留空则不驱动连发 Bool。")]
    public string firingBool = "IsFiring";

    private PlayerModel playerModel;
    private int fireTriggerHash;
    private int firingBoolHash;

    private void Awake()
    {
        playerModel = GetComponent<PlayerModel>();

        if (weapon == null)
            weapon = GetComponentInChildren<PlayerWeapon>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        CacheAnimatorHashes();
    }

    private void OnEnable()
    {
        if (weapon != null)
            weapon.Fired += PlayFireAnimation;
    }

    private void OnDisable()
    {
        if (weapon != null)
            weapon.Fired -= PlayFireAnimation;

        SetFiringBool(false);
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(firingBool))
            return;

        bool isCurrentPlayerFiring = PlayerController.INSTANCE != null
            && playerModel != null
            && PlayerController.INSTANCE.currentPlayerModel == playerModel
            && !playerModel.isDead
            && PlayerController.INSTANCE.isFire;

        SetFiringBool(isCurrentPlayerFiring);
    }

    private void OnValidate()
    {
        CacheAnimatorHashes();
    }

    private void CacheAnimatorHashes()
    {
        fireTriggerHash = string.IsNullOrEmpty(fireTrigger)
            ? 0
            : Animator.StringToHash(fireTrigger);
        firingBoolHash = string.IsNullOrEmpty(firingBool)
            ? 0
            : Animator.StringToHash(firingBool);
    }

    private void PlayFireAnimation()
    {
        if (animator == null || fireTriggerHash == 0)
            return;

        animator.SetTrigger(fireTriggerHash);
    }

    private void SetFiringBool(bool value)
    {
        if (animator == null || firingBoolHash == 0)
            return;

        animator.SetBool(firingBoolHash, value);
    }
}
