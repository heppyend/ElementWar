using UnityEngine;

/// <summary>
/// 单个角色在 PVE HUD 中使用的展示资料。
/// 挂在对应 PlayerModel 根物体，由 Inspector 配置图标和文案；不参与战斗结算。
/// </summary>
[DisallowMultipleComponent]
public class PveHudCharacterPresentation : MonoBehaviour
{
    [Header("玩家信息")]
    [SerializeField] private Sprite portrait;
    [SerializeField] private string displayName;

    [Header("主武器")]
    [SerializeField] private Sprite primaryWeaponIcon;
    [SerializeField] private string primaryWeaponName;

    [Header("副武器（枪械）")]
    [SerializeField] private Sprite secondaryWeaponIcon;

    [Header("副武器（近战）")]
    [SerializeField] private Sprite meleeWeaponIcon;
    [SerializeField] private string meleeWeaponName;

    public Sprite Portrait => portrait;
    public string DisplayName => displayName;
    public Sprite PrimaryWeaponIcon => primaryWeaponIcon;
    public string PrimaryWeaponName => primaryWeaponName;
    public Sprite SecondaryWeaponIcon => secondaryWeaponIcon;
    public Sprite MeleeWeaponIcon => meleeWeaponIcon;
    public string MeleeWeaponName => meleeWeaponName;
}
