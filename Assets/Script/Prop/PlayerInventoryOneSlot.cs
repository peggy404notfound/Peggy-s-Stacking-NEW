using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerInventoryOneSlot : MonoBehaviour
{
    // --- ��̬ע���֧�� GetForPlayer(old API ����) ---
    private static readonly Dictionary<int, PlayerInventoryOneSlot> s_registry =
        new Dictionary<int, PlayerInventoryOneSlot>();

    /// <summary>�����ID��ȡ����ҵĵ��۱���ʵ�������ݾɴ��룩��</summary>
    public static PlayerInventoryOneSlot GetForPlayer(int playerId)
    {
        if (s_registry.TryGetValue(playerId, out var inv) && inv != null) return inv;

        // ���ף����ע�����û�У������ڳ������Ҳ�ע��
        var all = FindObjectsOfType<PlayerInventoryOneSlot>(includeInactive: true);
        foreach (var it in all)
        {
            if (!s_registry.ContainsKey(it.playerId)) s_registry[it.playerId] = it;
            if (it.playerId == playerId) return it;
        }
        return null;
    }

    // --- ʵ���� ---
    [Header("���ID���������ֶ����&��̬��ѯ��")]
    public int playerId = 1;

    [Header("��ǰ����ID��Inspector�ɼ�����ͨ�� CurrentPropId ���ʣ�")]
    [SerializeField] private string currentPropId = "";

    /// <summary>����ֻ������ǰ�����еĵ���id��Ϊ��=û�е��ߣ�</summary>
    public string CurrentPropId => currentPropId;

    /// <summary>�Ƿ�Ϊ�ղ�</summary>
    public bool IsEmpty => string.IsNullOrEmpty(currentPropId);

    [Header("����ѡ����������")]
    public AudioClip pickupSfx;
    public AudioClip consumeSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioSource audioSource; // �������Զ��ñ������ϵ� AudioSource�������ڣ�

    [Header("�¼����������ݱ仯ʱ��������UI/����/��Чֱ����Inspector��󶨣�")]
    public UnityEvent onChangedUnity = new UnityEvent();

    /// <summary>�¼����������ݱ仯ʱ�����������붩�ģ���UI�ű���</summary>
    public event Action OnChanged;

    // --- �������ڣ�ע��/��ע�� ---
    private void Awake()
    {
        // ע�᱾ʵ��
        s_registry[playerId] = this;
    }
    private void OnEnable()
    {
        s_registry[playerId] = this;
    }
    private void OnDisable()
    {
        // ֻ�ڱ���������ָ���Լ�ʱ���Ƴ�����ֹ����
        if (s_registry.TryGetValue(playerId, out var who) && who == this)
            s_registry.Remove(playerId);
    }
    private void OnDestroy()
    {
        if (s_registry.TryGetValue(playerId, out var who) && who == this)
            s_registry.Remove(playerId);
    }

    /// <summary>
    /// ����/���ǵ�ǰ���ߣ�ʰȡʱ���ã�
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
    /// ʹ�ò���գ����ر�ʹ�õĵ���ID����Ϊ�շ��ؿմ���
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

    /// <summary>���鿴�����</summary>
    public string Peek() => currentPropId;

    /// <summary>�ⲿǿ����գ���غ����ã�</summary>
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
        // ȷ��ע����������µ�playerIdӳ��
        if (isActiveAndEnabled) s_registry[playerId] = this;
    }
#endif
}