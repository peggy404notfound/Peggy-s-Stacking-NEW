using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerInventoryOneSlot : MonoBehaviour
{
    private static readonly Dictionary<int, PlayerInventoryOneSlot> s_registry =
        new Dictionary<int, PlayerInventoryOneSlot>();

    public static PlayerInventoryOneSlot GetForPlayer(int playerId)
    {
        if (s_registry.TryGetValue(playerId, out var inv) && inv != null) return inv;

        var all = FindObjectsOfType<PlayerInventoryOneSlot>(includeInactive: true);
        foreach (var it in all)
        {
            if (!s_registry.ContainsKey(it.playerId)) s_registry[it.playerId] = it;
            if (it.playerId == playerId) return it;
        }
        return null;
    }

    [Header("玩家ID")]
    public int playerId = 1;

    [Header("当前道具（只读）")]
    [SerializeField] private string currentPropId = "";
    public string CurrentPropId => currentPropId;
    public bool IsEmpty => string.IsNullOrEmpty(currentPropId);

    // ---------- 音效 ----------
    [Header("音效")]
    public AudioSource audioSource;
    [Tooltip("玩家碰到道具瞬间（接触道具时）")]
    public AudioClip pickupSfx;
    [Tooltip("道具正式进入槽位时")]
    public AudioClip putInSlotSfx;
    [Tooltip("消耗/使用道具时")]
    public AudioClip consumeSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // ---------- 视觉（图标弹跳 + 粒子） ----------
    [Header("UI 图标（必填）")]
    public RectTransform slotIcon;

    [Header("弹跳节奏（先正常 X 秒，再“砰”地放大回弹）")]
    [Tooltip("入槽后，保持原始大小的时间（秒）")]
    public float delayBeforePulse = 0.15f;
    [Tooltip("放大到 pulseScale 的用时")]
    public float scaleUpDuration = 0.08f;
    [Tooltip("从 pulseScale 回到 1 的用时")]
    public float scaleDownDuration = 0.12f;
    [Tooltip("放大目标比例，例如 1.2 = 120%")]
    public float pulseScale = 1.20f;

    [Header("粒子")]
    [Tooltip("迸发亮点的粒子预制体（放在 Canvas 内用 Particles/Unlit 或 URP Particles Unlit Additive）")]
    public GameObject slotBurstPrefab;
    [Tooltip("实例化出来的粒子父物体（不填就用 slotIcon 的 parent）")]
    public Transform particleParent;
    [Tooltip("粒子生存期（秒），到时自动销毁实例")]
    public float particleLife = 1.2f;

    // ---------- 事件 ----------
    [Header("事件：背包内容变化时触发")]
    public UnityEvent onChangedUnity = new UnityEvent();
    public event Action OnChanged;

    // ---------- 内部 ----------
    Vector3 _iconScale0;
    Coroutine _pulseCo;

    void Awake()
    {
        s_registry[playerId] = this;
        if (slotIcon) _iconScale0 = slotIcon.localScale;
    }
    void OnEnable() { s_registry[playerId] = this; }
    void OnDisable() { if (s_registry.TryGetValue(playerId, out var who) && who == this) s_registry.Remove(playerId); }
    void OnDestroy() { if (s_registry.TryGetValue(playerId, out var who) && who == this) s_registry.Remove(playerId); }

    // =============== 外部调用口 ===============

    /// <summary>玩家“接触到道具”的瞬间（仅播拾取音效，不入槽）。</summary>
    public void OnPropTouched() => PlaySfx(pickupSfx);

    /// <summary>把道具放入这一格（会触发音效、粒子、图标弹跳）。</summary>
    public void Set(string propId)
    {
        currentPropId = propId ?? string.Empty;

        PlaySfx(putInSlotSfx);
        PlaySlotFeedback();      // 粒子 + 弹跳

        NotifyChanged();
#if UNITY_EDITOR
        Debug.Log($"[Inv] P{playerId} got '{currentPropId}'");
#endif
    }

    /// <summary>消耗/使用当前道具，返回道具ID。</summary>
    public string Consume()
    {
        if (string.IsNullOrEmpty(currentPropId))
            return string.Empty;

        string used = currentPropId;
        currentPropId = string.Empty;
        PlaySfx(consumeSfx);
        NotifyChanged();
#if UNITY_EDITOR
        Debug.Log($"[Inv] P{playerId} used '{used}' (now empty)");
#endif
        return used;
    }

    // =============== 视觉反馈 ===============

    void PlaySlotFeedback()
    {
        // 粒子
        if (slotBurstPrefab && slotIcon)
        {
            var parent = particleParent ? particleParent : slotIcon.parent;
            var go = Instantiate(slotBurstPrefab, slotIcon.position, Quaternion.identity, parent);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps) ps.Play();
            Destroy(go, particleLife);
        }

        // 弹跳：先保持原始大小 delayBeforePulse 秒，再“砰”地放大→回弹
        if (slotIcon)
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(PulseWithDelay());
        }
    }

    IEnumerator PulseWithDelay()
    {
        // 先保持原始大小 X 秒
        slotIcon.localScale = _iconScale0;
        if (delayBeforePulse > 0f)
            yield return new WaitForSecondsRealtime(delayBeforePulse);

        // 放大
        float t = 0f;
        while (t < scaleUpDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, scaleUpDuration));
            // 轻微的“EaseOutBack”感觉
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            slotIcon.localScale = _iconScale0 * Mathf.Lerp(1f, pulseScale, ease);
            yield return null;
        }

        // 回弹
        t = 0f;
        while (t < scaleDownDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, scaleDownDuration));
            // EaseOut
            float ease = 1f - Mathf.Pow(1f - k, 2f);
            slotIcon.localScale = _iconScale0 * Mathf.Lerp(pulseScale, 1f, ease);
            yield return null;
        }
        slotIcon.localScale = _iconScale0;
        _pulseCo = null;
    }

    // =============== 辅助 ===============

    void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        var src = audioSource ? audioSource : GetComponent<AudioSource>();
        if (src) src.PlayOneShot(clip, sfxVolume);
    }

    void NotifyChanged()
    {
        onChangedUnity?.Invoke();
        OnChanged?.Invoke();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        sfxVolume = Mathf.Clamp01(sfxVolume);
        if (slotIcon && _iconScale0 == Vector3.zero) _iconScale0 = slotIcon.localScale;
        if (isActiveAndEnabled) s_registry[playerId] = this;
    }
#endif
}