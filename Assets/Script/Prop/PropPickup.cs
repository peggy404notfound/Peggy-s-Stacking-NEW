using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PropPickup : MonoBehaviour
{
    [Tooltip("ʰȡ���Ƿ����������������ʵ��")]
    public bool destroyOnPickup = true;

    [Header("ʰȡ�ж�")]
    [Tooltip("Ҫ�����С��ʵ���ص�����͸��ȣ��ף���ֻ�д�͸�ﵽ��ֵ����ʰȡ��")]
    [Min(0f)] public float minPenetration = 0.02f; // ��������絥λ��0.01~0.03����

    bool _picked = false;
    Collider2D _selfCol;

    void Reset()
    {
        // ȷ����ײ���Ǵ�������������ԭ��ƣ�
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        _selfCol = GetComponent<Collider2D>();
        if (_selfCol && !_selfCol.isTrigger)
            _selfCol.isTrigger = true;
    }

    // ��ͳһ��ڣ�Enter/Stay �����ԣ�ֻ�����㡰�㹻��͸���Ż�����ʰȡ��
    private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
    private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

    void TryPickup(Collider2D other)
    {
        if (_picked || !_selfCol || !other) return;

        // ���� ������Ҫ��ﵽ����С��͸��ȡ� ���� //
        // Unity�� Collider2D.Distance�����ص�ʱ distance Ϊ��ֵ�������ֵΪ��͸���
        var d = other.Distance(_selfCol);
        if (!d.isOverlapped) return;                       // û���ص���ֱ�ӷ���
        if (-d.distance < minPenetration) return;          // ��͸��Ȳ��㣬����ʰȡ

        // ֻ��Ӧ��ҵ�ǰ����Ļ�ľ���Լ��򸸼��� BlockMark��
        var mark = other.GetComponent<BlockMark>() ?? other.GetComponentInParent<BlockMark>();
        if (mark == null) return;

        int pid = mark.ownerPlayerId;
        if (pid != 1 && pid != 2) return;

        // �ҵ���Ӧ��ҵĵ��۱���
        var inv = PlayerInventoryOneSlot.GetForPlayer(pid);
        if (inv == null) return;

        // �ӵ�ǰ���߶�ȡ ID������û�� PropItem ʱҲ����������һ��Ĭ�� ID��
        var item = GetComponent<PropItem>();
        string propId = item ? item.propId : "unknown";

        // ���뱳�������ǣ�
        inv.Set(propId);

        // �״λ�õ�����ʾ
        TurnManager.Instance?.TriggerPropUseKeyHintFor(pid);

        _picked = true;

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}