using UnityEngine;

/// <summary>
/// PVE HUD 的数据适配层。
/// 只读取当前主控 PlayerModel 与其 PlayerWeapon，将变化后的状态提交给 PveHudView。
/// </summary>
public class PveHudPresenter : MonoBehaviour
{
    [SerializeField] private PveHudView view;
    private PlayerModel observedPlayer;
    private PlayerWeapon observedWeapon;
    private PveHudCharacterPresentation observedPresentation;
    private int lastHealth = int.MinValue;
    private int lastMaxHealth = int.MinValue;
    private Sprite lastPortrait;
    private int lastPlayerIndex = int.MinValue;
    private string lastPlayerName;
    private int lastMagazineAmmo = int.MinValue;
    private int lastReserveAmmo = int.MinValue;
    private bool lastReloading;
    private bool lastCrosshairVisible;
    private bool lastAiming;
    private bool lastFiring;
    private bool lastWeaponVisible;
    private bool loadoutInitialized;

    private void Awake()
    {
        if (view == null)
            view = GetComponent<PveHudView>();
        ResetCachedState();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    [ContextMenu("Force Refresh")]
    public void ForceRefresh()
    {
        ResetCachedState();
        Refresh();
    }

    private void Refresh()
    {
        if (view == null) return;

        PlayerController controller = PlayerController.INSTANCE;
        PlayerModel player = controller != null ? controller.currentPlayerModel : null;
        if (player != observedPlayer)
        {
            observedPlayer = player;
            observedWeapon = null;
            ResetCachedState();
        }

        if (observedPlayer == null)
        {
            SetWeaponVisible(false);
            SetCrosshairVisible(false);
            return;
        }

        SetHealth(observedPlayer.currentHealth, observedPlayer.maxHealth);
        SetPlayerPresentation(observedPlayer);

        PlayerWeapon weapon = observedPlayer.weapon;
        if (weapon != observedWeapon)
        {
            observedWeapon = weapon;
            lastMagazineAmmo = int.MinValue;
            lastReserveAmmo = int.MinValue;
            lastReloading = !lastReloading;
        }

        bool hasWeapon = observedWeapon != null;
        SetWeaponVisible(hasWeapon);
        if (hasWeapon)
        {
            SetAmmo(observedWeapon.MagazineAmmo, observedWeapon.ReserveAmmo);
            SetReloading(observedWeapon.IsReloading, observedWeapon.ReloadProgress);
        }
        else
            SetReloading(false, 0f);

        bool isAlive = !observedPlayer.isDead;
        SetCrosshairVisible(isAlive);
        if (isAlive)
            SetCrosshairState(controller.isAiming, controller.isFire);
    }

    private void SetHealth(int current, int max)
    {
        if (current == lastHealth && max == lastMaxHealth) return;
        lastHealth = current;
        lastMaxHealth = max;
        view.SetHealth(current, max);
    }

    private void SetPlayerPresentation(PlayerModel player)
    {
        PveHudCharacterPresentation presentation = player.GetComponent<PveHudCharacterPresentation>();
        Sprite portrait = presentation != null ? presentation.Portrait : null;
        string displayName = presentation != null && !string.IsNullOrWhiteSpace(presentation.DisplayName)
            ? presentation.DisplayName
            : player.name;
        int playerIndex = GetPlayerIndex(player);

        if (portrait != lastPortrait || displayName != lastPlayerName || playerIndex != lastPlayerIndex)
        {
            lastPortrait = portrait;
            lastPlayerName = displayName;
            lastPlayerIndex = playerIndex;
            view.SetPlayerInfo(portrait, playerIndex, displayName);
        }

        if (!loadoutInitialized || presentation != observedPresentation)
        {
            observedPresentation = presentation;
            loadoutInitialized = true;
            view.SetLoadout(presentation);
        }
    }

    private static int GetPlayerIndex(PlayerModel player)
    {
        if (GameManager.INSTANCE == null || GameManager.INSTANCE.playerModels == null)
            return 1;

        for (int i = 0; i < GameManager.INSTANCE.playerModels.Length; i++)
        {
            if (GameManager.INSTANCE.playerModels[i] == player)
                return i + 1;
        }
        return 1;
    }

    private void SetWeaponVisible(bool visible)
    {
        if (visible == lastWeaponVisible) return;
        lastWeaponVisible = visible;
        view.SetWeaponVisible(visible);
    }

    private void SetAmmo(int magazine, int reserve)
    {
        if (magazine == lastMagazineAmmo && reserve == lastReserveAmmo) return;
        lastMagazineAmmo = magazine;
        lastReserveAmmo = reserve;
        view.SetAmmo(magazine, reserve);
    }

    private void SetReloading(bool isReloading, float progress)
    {
        if (isReloading)
        {
            lastReloading = true;
            view.SetReloading(true, progress);
            return;
        }

        if (!lastReloading) return;
        lastReloading = false;
        view.SetReloading(false, 0f);
    }

    private void SetCrosshairVisible(bool visible)
    {
        if (visible == lastCrosshairVisible) return;
        lastCrosshairVisible = visible;
        view.SetCrosshairVisible(visible);
    }

    private void SetCrosshairState(bool aiming, bool firing)
    {
        if (aiming == lastAiming && firing == lastFiring) return;
        lastAiming = aiming;
        lastFiring = firing;
        view.SetCrosshairState(aiming, firing);
    }

    private void ResetCachedState()
    {
        lastHealth = int.MinValue;
        lastMaxHealth = int.MinValue;
        lastPortrait = null;
        lastPlayerIndex = int.MinValue;
        lastPlayerName = null;
        lastMagazineAmmo = int.MinValue;
        lastReserveAmmo = int.MinValue;
        lastReloading = !lastReloading;
        lastCrosshairVisible = !lastCrosshairVisible;
        lastAiming = !lastAiming;
        lastFiring = !lastFiring;
        lastWeaponVisible = !lastWeaponVisible;
        loadoutInitialized = false;
    }
}
