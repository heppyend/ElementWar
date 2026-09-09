using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// PVE HUD 的纯显示层。
/// 所有字段由 Unity Inspector 绑定；本类不读取玩法对象，也不修改战斗状态。
/// </summary>
public class PveHudView : MonoBehaviour
{
    [Header("左下 - 玩家信息")]
    [SerializeField] private Image playerPortraitImage;
    [SerializeField] private TMP_Text playerIndexText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;

    [Header("右下 - 弹药")]
    [SerializeField] private GameObject weaponStatusRoot;
    [FormerlySerializedAs("magazineText")]
    [SerializeField] private TMP_Text ammoText;

    [Header("右下 - 武器资料")]
    [SerializeField] private Image primaryWeaponImage;
    [SerializeField] private TMP_Text primaryWeaponNameText;
    [SerializeField] private Image secondaryWeaponImage;
    [SerializeField] private Image meleeWeaponImage;
    [SerializeField] private TMP_Text meleeWeaponNameText;

    [Header("中央 - 换弹过渡提示")]
    [SerializeField] private GameObject reloadHintRoot;
    [SerializeField] private TMP_Text reloadHintText;
    [Tooltip("需是 Image，Type 设为 Filled、Fill Method 设为 Radial 360。")]
    [SerializeField] private Image reloadProgressFill;
    [SerializeField] private string reloadingMessage = "正在换弹";

    [Header("准星")]
    [SerializeField] private GameObject crosshairRoot;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Color hipFireColor = Color.white;
    [SerializeField] private Color aimingColor = new Color(0.65f, 0.9f, 1f, 1f);
    [SerializeField] private Color firingColor = new Color(1f, 0.8f, 0.35f, 1f);

    private void Awake()
    {
        SetActiveIfChanged(reloadHintRoot, false);
        if (reloadProgressFill != null)
            reloadProgressFill.fillAmount = 0f;
    }

    public void SetPlayerInfo(Sprite portrait, int playerIndex, string playerName)
    {
        SetImageSprite(playerPortraitImage, portrait);
        if (playerIndexText != null)
            playerIndexText.SetText("{0}", Mathf.Max(1, playerIndex));
        if (playerNameText != null)
            playerNameText.text = playerName ?? string.Empty;
    }

    public void SetHealth(int current, int max)
    {
        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        if (healthFill != null)
            healthFill.fillAmount = (float)safeCurrent / safeMax;
        if (healthText != null)
            healthText.SetText("{0}/{1}", safeCurrent, safeMax);
    }

    public void SetWeaponVisible(bool visible)
    {
        SetActiveIfChanged(weaponStatusRoot, visible);
    }

    public void SetAmmo(int magazineAmmo, int reserveAmmo)
    {
        if (ammoText == null) return;
        int safeMagazine = Mathf.Max(0, magazineAmmo);
        int safeReserve = Mathf.Max(0, reserveAmmo);
        ammoText.SetText("{0}/{1}", safeMagazine, safeReserve);
    }

    public void SetLoadout(PveHudCharacterPresentation presentation)
    {
        SetImageSprite(primaryWeaponImage, presentation != null ? presentation.PrimaryWeaponIcon : null);
        if (primaryWeaponNameText != null)
            primaryWeaponNameText.text = presentation != null ? presentation.PrimaryWeaponName ?? string.Empty : string.Empty;
        SetImageSprite(secondaryWeaponImage, presentation != null ? presentation.SecondaryWeaponIcon : null);
        SetImageSprite(meleeWeaponImage, presentation != null ? presentation.MeleeWeaponIcon : null);
        if (meleeWeaponNameText != null)
            meleeWeaponNameText.text = presentation != null ? presentation.MeleeWeaponName ?? string.Empty : string.Empty;
    }

    public void SetReloading(bool isReloading, float progress)
    {
        SetActiveIfChanged(reloadHintRoot, isReloading);
        if (reloadHintText != null)
            reloadHintText.text = isReloading ? reloadingMessage : string.Empty;
        if (reloadProgressFill != null)
            reloadProgressFill.fillAmount = isReloading ? Mathf.Clamp01(progress) : 0f;
    }

    public void SetCrosshairVisible(bool visible)
    {
        SetActiveIfChanged(crosshairRoot, visible);
    }

    public void SetCrosshairState(bool isAiming, bool isFiring)
    {
        if (crosshairImage == null) return;
        crosshairImage.color = isFiring ? firingColor : isAiming ? aimingColor : hipFireColor;
    }

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null || image.sprite == sprite) return;
        image.sprite = sprite;
    }

    private static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
