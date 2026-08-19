using UnityEngine;

/// <summary>
/// 武器音效组件（可替换）：
/// 挂在与 PlayerWeapon 同一 GameObject 上，订阅 PlayerWeapon.Fired 事件播枪声。
/// 开火音效在 Inspector 里自由替换/增删 fireClips 数组即可，无需改代码。
/// 每个武器（荧/芙宁娜）各挂一个，放不同音效即得到不同枪声。
/// </summary>
[RequireComponent(typeof(PlayerWeapon))]
public class WeaponAudio : MonoBehaviour
{
    [Header("开火音效（随机抽一个播放）")]
    [Tooltip("放 1~2 个音效，开火时随机抽一个并随机音高，避免每枪听起来一模一样")]
    public AudioClip[] fireClips;

    [Header("播放参数")]
    [Range(0f, 1f)]
    [Tooltip("开火音量")]
    public float volume = 0.8f;
    [Tooltip("随机音高下限（0.95 = 略低）")]
    public float pitchMin = 0.95f;
    [Tooltip("随机音高上限（1.05 = 略高）")]
    public float pitchMax = 1.05f;

    private AudioSource audioSource;
    private PlayerWeapon weapon;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;//3D 声场：随距离衰减，跟随角色/远处敌人开枪能听出远近
        audioSource.volume = 1f;//实际音量走 PlayOneShot 的 volumeScale

        weapon = GetComponent<PlayerWeapon>();
    }

    private void OnEnable()
    {
        if (weapon != null) weapon.Fired += OnFired;
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.Fired -= OnFired;
    }

    private void OnFired() => NotifyFired();

    /// <summary>外部通知开火（PVP 网络开火视觉用，直接播枪声；PVE 由 PlayerWeapon.Fired 触发）。</summary>
    public void NotifyFired()
    {
        if (audioSource == null) return;
        AudioClip clip = PickRandom(fireClips);
        if (clip == null) return;
        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clip, volume);
    }

    private static AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }
}
