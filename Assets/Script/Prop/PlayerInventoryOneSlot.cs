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

    [Header("���ID")]
    public int playerId = 1;

    [Header("��ǰ���ߣ�ֻ����")]
    [SerializeField] private string currentPropId = "";
    public string CurrentPropId => currentPropId;
    public bool IsEmpty => string.IsNullOrEmpty(currentPropId);

    // ---------- ��Ч ----------
    [Header("��Ч")]
    public AudioSource audioSource;
    [Tooltip("�����������˲�䣨�Ӵ�����ʱ��")]
    public AudioClip pickupSfx;
    [Tooltip("������ʽ�����λʱ")]
    public AudioClip putInSlotSfx;
    [Tooltip("����/ʹ�õ���ʱ")]
    public AudioClip consumeSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // ---------- �Ӿ���ͼ�굯�� + ���ӣ� ----------
    [Header("UI ͼ�꣨���")]
    public RectTransform slotIcon;

    [Header("�������ࣨ������ X �룬�١��顱�طŴ�ص���")]
    [Tooltip("��ۺ󣬱���ԭʼ��С��ʱ�䣨�룩")]
    public float delayBeforePulse = 0.15f;
    [Tooltip("�Ŵ� pulseScale ����ʱ")]
    public float scaleUpDuration = 0.08f;
    [Tooltip("�� pulseScale �ص� 1 ����ʱ")]
    public float scaleDownDuration = 0.12f;
    [Tooltip("�Ŵ�Ŀ����������� 1.2 = 120%")]
    public float pulseScale = 1.20f;

    [Header("����")]
    [Tooltip("�ŷ����������Ԥ���壨���� Canvas ���� Particles/Unlit �� URP Particles Unlit Additive��")]
    public GameObject slotBurstPrefab;
    [Tooltip("ʵ�������������Ӹ����壨������� slotIcon �� parent��")]
    public Transform particleParent;
    [Tooltip("���������ڣ��룩����ʱ�Զ�����ʵ��")]
    public float particleLife = 1.2f;

    // ---------- �¼� ----------
    [Header("�¼����������ݱ仯ʱ����")]
    public UnityEvent onChangedUnity = new UnityEvent();
    public event Action OnChanged;

    // ---------- �ڲ� ----------
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

    // =============== �ⲿ���ÿ� ===============

    /// <summary>��ҡ��Ӵ������ߡ���˲�䣨����ʰȡ��Ч������ۣ���</summary>
    public void OnPropTouched() => PlaySfx(pickupSfx);

    /// <summary>�ѵ��߷�����һ�񣨻ᴥ����Ч�����ӡ�ͼ�굯������</summary>
    public void Set(string propId)
    {
        currentPropId = propId ?? string.Empty;

        PlaySfx(putInSlotSfx);
        PlaySlotFeedback();      // ���� + ����

        NotifyChanged();
#if UNITY_EDITOR
        Debug.Log($"[Inv] P{playerId} got '{currentPropId}'");
#endif
    }

    /// <summary>����/ʹ�õ�ǰ���ߣ����ص���ID��</summary>
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

    // =============== �Ӿ����� ===============

    void PlaySlotFeedback()
    {
        // ����
        if (slotBurstPrefab && slotIcon)
        {
            var parent = particleParent ? particleParent : slotIcon.parent;
            var go = Instantiate(slotBurstPrefab, slotIcon.position, Quaternion.identity, parent);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps) ps.Play();
            Destroy(go, particleLife);
        }

        // �������ȱ���ԭʼ��С delayBeforePulse �룬�١��顱�طŴ���ص�
        if (slotIcon)
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(PulseWithDelay());
        }
    }

    IEnumerator PulseWithDelay()
    {
        // �ȱ���ԭʼ��С X ��
        slotIcon.localScale = _iconScale0;
        if (delayBeforePulse > 0f)
            yield return new WaitForSecondsRealtime(delayBeforePulse);

        // �Ŵ�
        float t = 0f;
        while (t < scaleUpDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, scaleUpDuration));
            // ��΢�ġ�EaseOutBack���о�
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            slotIcon.localScale = _iconScale0 * Mathf.Lerp(1f, pulseScale, ease);
            yield return null;
        }

        // �ص�
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

    // =============== ���� ===============

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