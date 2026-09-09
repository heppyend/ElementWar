using UnityEngine;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP HUD 数据适配层。
    /// 生命、弹药和换弹只读取 NetClient 保存的最近一份服务器快照，绝不参与预测或结算。
    /// </summary>
    [DisallowMultipleComponent]
    public class PvpHudPresenter : MonoBehaviour
    {
        [SerializeField] private PveHudView view;
        [SerializeField] private NetClient netClient;
        [Tooltip("启用新版场景 HUD 后，自动隐藏旧 PVPHealthUI 的本地血条和准星，保留计分/死亡/结算面板。")]
        [SerializeField] private bool suppressLegacyCombatHud = true;

        private PVPHealthUI legacyHealthUi;
        private PlayerModel observedPlayer;
        private PveHudCharacterPresentation observedPresentation;
        private int lastHealth = int.MinValue;
        private int lastMaxHealth = int.MinValue;
        private int lastMagazineAmmo = int.MinValue;
        private int lastReserveAmmo = int.MinValue;
        private Sprite lastPortrait;
        private string lastPlayerName;
        private int lastCharacterIndex = int.MinValue;
        private bool lastReloading;
        private bool lastCrosshairVisible;
        private bool lastAiming;
        private bool lastFiring;
        private bool lastWeaponVisible;
        private bool loadoutInitialized;
        private bool legacyCombatHudSuppressed;

        private void Awake()
        {
            if (view == null)
                view = GetComponent<PveHudView>();
            if (netClient == null)
                netClient = FindObjectOfType<NetClient>();
            ResetCachedState();
        }

        private void LateUpdate()
        {
            if (netClient == null)
                netClient = FindObjectOfType<NetClient>();
            if (view == null || netClient == null) return;

            SuppressLegacyCombatHud();
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
            PlayerModel player = netClient.LocalModel;
            if (player != observedPlayer)
            {
                observedPlayer = player;
                observedPresentation = null;
                ResetCachedState();
            }

            if (player == null || !netClient.HasLocalSnapshot)
            {
                SetWeaponVisible(false);
                SetReloading(false, 0f);
                SetCrosshairVisible(false);
                return;
            }

            SetHealth(netClient.LocalHealth, netClient.LocalMaxHealth);
            SetPlayerPresentation(player);

            bool hasWeapon = player.weapon != null;
            SetWeaponVisible(hasWeapon);
            if (hasWeapon)
            {
                SetAmmo(netClient.LocalMagazineAmmo, netClient.LocalReserveAmmo);
                SetReloading(netClient.LocalIsReloading, netClient.LocalReloadProgress);
            }
            else
            {
                SetReloading(false, 0f);
            }

            bool crosshairVisible = !player.isDead && !netClient.MatchEnded;
            SetCrosshairVisible(crosshairVisible);
            if (crosshairVisible)
                SetCrosshairState(netClient.LocalIsAiming, netClient.LocalIsFiring);
        }

        private void SuppressLegacyCombatHud()
        {
            if (!suppressLegacyCombatHud || legacyCombatHudSuppressed) return;
            if (legacyHealthUi == null)
                legacyHealthUi = FindObjectOfType<PVPHealthUI>();
            if (legacyHealthUi == null) return;

            legacyHealthUi.SetLegacyCombatHudVisible(false);
            legacyCombatHudSuppressed = true;
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
                : netClient.LocalPlayerName;
            int characterIndex = Mathf.Max(1, netClient.LocalCharacterId + 1);

            if (portrait != lastPortrait || displayName != lastPlayerName || characterIndex != lastCharacterIndex)
            {
                lastPortrait = portrait;
                lastPlayerName = displayName;
                lastCharacterIndex = characterIndex;
                view.SetPlayerInfo(portrait, characterIndex, displayName);
            }

            if (!loadoutInitialized || presentation != observedPresentation)
            {
                observedPresentation = presentation;
                loadoutInitialized = true;
                view.SetLoadout(presentation);
            }
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
            lastMagazineAmmo = int.MinValue;
            lastReserveAmmo = int.MinValue;
            lastPortrait = null;
            lastPlayerName = null;
            lastCharacterIndex = int.MinValue;
            lastReloading = !lastReloading;
            lastCrosshairVisible = !lastCrosshairVisible;
            lastAiming = !lastAiming;
            lastFiring = !lastFiring;
            lastWeaponVisible = !lastWeaponVisible;
            loadoutInitialized = false;
        }
    }
}
