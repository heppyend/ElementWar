using TMPro;
using UnityEngine;

/// <summary>
/// PVE 玩家头顶的运行时状态诊断标签。
/// 由 PlayerModel 在 Start 时动态挂载，不依赖 Scene/Prefab 预配置。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStateDebugLabel : MonoBehaviour
{
    private static bool warnedMissingCjkFont;

    private PlayerModel owner;
    private TextMeshPro label;
    private Transform labelTransform;
    private float nextRefreshTime;
    private System.Func<string> textProvider;

    private void Awake()
    {
        owner = GetComponent<PlayerModel>();
        BuildLabel();
    }

    private void LateUpdate()
    {
        if (owner == null || label == null) return;
        if (!owner.showStateDebugLabel)
        {
            label.enabled = false;
            return;
        }
        label.enabled = true;

        labelTransform.position = owner.transform.position + Vector3.up * owner.stateDebugLabelHeight;
        Camera camera = Camera.main;
        if (camera != null)
            labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - camera.transform.position, Vector3.up);

        ApplyInspectorStyle();
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + owner.stateDebugRefreshInterval;
        label.text = textProvider != null ? textProvider() : owner.GetStateDebugText();
    }

    /// <summary>PVP 使用网络表现状态覆盖默认 PVE FSM 文本；传 null 恢复默认显示。</summary>
    public void SetTextProvider(System.Func<string> provider)
    {
        textProvider = provider;
        nextRefreshTime = 0f;
    }

    private void BuildLabel()
    {
        var go = new GameObject("PlayerStateDebugLabel", typeof(TextMeshPro));
        labelTransform = go.transform;
        labelTransform.SetParent(transform, false);
        labelTransform.localScale = Vector3.one * owner.stateDebugLabelScale;

        label = go.GetComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        ApplyInspectorStyle();

        if (!warnedMissingCjkFont && (label.font == null || label.font.name.StartsWith("LiberationSans")))
        {
            warnedMissingCjkFont = true;
            Debug.LogWarning("[PlayerStateDebugLabel] TMP 默认字体不含中文，状态标签可能显示为口口口。请运行 Tools/玩家/烘焙中文默认字体。");
        }
    }

    private void ApplyInspectorStyle()
    {
        if (owner == null || label == null) return;
        labelTransform.localScale = Vector3.one * owner.stateDebugLabelScale;
        label.font = owner.stateDebugFont != null ? owner.stateDebugFont : TMP_Settings.defaultFontAsset;
        label.fontSize = owner.stateDebugFontSize;
        label.color = owner.stateDebugTextColor;
        label.outlineWidth = owner.stateDebugOutlineWidth;
        label.outlineColor = owner.stateDebugOutlineColor;
    }
}
