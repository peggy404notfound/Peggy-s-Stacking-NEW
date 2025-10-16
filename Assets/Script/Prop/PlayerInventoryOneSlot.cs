using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerInventoryOneSlot : MonoBehaviour
{
    // --- 静态注册表：支持 GetForPlayer(old API 兼容) ---
    private static readonly Dictionary<int, PlayerInventoryOneSlot> s_registry =
        new Dictionary<int, PlayerInventoryOneSlot>();

    /// <summary>按玩家ID获取该玩家的单槽背包实例（兼容旧代码）。</summary>
    public static PlayerInventoryOneSlot GetForPlayer(int playerId)
    {
        if (s_registry.TryGetValue(playerId, out var inv) && inv != null) return inv;

        // 兜底：如果注册表里没有，尝试在场景里找并注册
        var all = FindObjectsOfType<PlayerInventoryOneSlot>(includeInactive: true);
        foreach (var it in all)
        {
            if (!s_registry.ContainsKey(it.playerId)) s_registry[it.playerId] = it;
            if (it.playerId == playerId) return it;
        }
        return null;
    }

    // --- 实例区 ---
    [Header("玩家ID（用于区分多玩家&静态查询）")]
    public int playerId = 1;

    [Header("当前道具ID（Inspector可见；请通过 CurrentPropId 访问）")]
    [SerializeField] private string currentPropId = "";

    /// <summary>对外只读：当前背包中的道具id（为空=没有道具）</summary>
    public string CurrentPropId => currentPropId;

    /// <summary>是否为空槽</summary>
    public bool IsEmpty => string.IsNullOrEmpty(currentPropId);

    [Header("（可选）声音反馈")]
    public AudioClip pickupSfx;
    public AudioClip consumeSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioSource audioSource; // 不填则自动用本物体上的 AudioSource（若存在）

    [Header("事件：背包内容变化时触发（供UI/动画/特效直接在Inspector里绑定）")]
    public UnityEvent onChangedUnity = new UnityEvent();

    /// <summary>事件：背包内容变化时触发（供代码订阅，如UI脚本）</summary>
    public event Action OnChanged;

    // --- 生命周期：注册/反注册 ---
    private void Awake()
    {
        // 注册本实例
        s_registry[playerId] = this;
    }
    private void OnEnable()
    {
        s_registry[playerId] = this;
    }
    private void OnDisable()
    {
        // 只在被禁用且仍指向自己时才移除，防止覆盖
        if (s_registry.TryGetValue(playerId, out var who) && who == this)
            s_registry.Remove(playerId);
    }
    private void OnDestroy()
    {
        if (s_registry.TryGetValue(playerId, out var who) && who == this)
            s_registry.Remove(playerId);
    }

    /// <summary>
    /// 设置/覆盖当前道具（拾取时调用）
    /// </summary>
    public void Set(string propId)
    {
        currentPropId = propId ?? string.Empty;
        PlaySfx(pickupSfx);
        NotifyChanged();
#if UNITY_EDITOR
        Debug.Log($"[Inv] P{playerId} got '{currentPropId}'");
#endif
    }

    /// <summary>
    /// 使用并清空（返回被使用的道具ID；若为空返回空串）
    /// </summary>
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

    /// <summary>仅查看不清空</summary>
    public string Peek() => currentPropId;

    /// <summary>外部强制清空（如回合重置）</summary>
    public void Clear()
    {
        if (string.IsNullOrEmpty(currentPropId)) return;
        currentPropId = string.Empty;
        NotifyChanged();
#if UNITY_EDITOR
        Debug.Log($"[Inv] P{playerId} cleared (now empty)");
#endif
    }

    private void NotifyChanged()
    {
        onChangedUnity?.Invoke();
        OnChanged?.Invoke();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        var src = audioSource ? audioSource : GetComponent<AudioSource>();
        if (src) src.PlayOneShot(clip, sfxVolume);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        sfxVolume = Mathf.Clamp01(sfxVolume);
        // 确保注册表里是最新的playerId映射
        if (isActiveAndEnabled) s_registry[playerId] = this;
    }
#endif
}