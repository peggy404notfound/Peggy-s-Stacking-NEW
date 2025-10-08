using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PropPickup : MonoBehaviour
{
    [Tooltip("ʰȡ���Ƿ����������������ʵ��")]
    public bool destroyOnPickup = true;

    bool _picked = false;

    void Reset()
    {
        // ȷ����ײ���Ǵ�����
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_picked) return;

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

        // ���� �ؼ����������״λ�õ��߰��ĸ���ʹ�á�����ʾ ���� //
        // TurnManager �ڲ��ᴦ������ֻ����һ�Ρ��͡�����һ�غ��䶨�ٵ�����
        // ��������ֱ�ӵ��ü��ɣ������� P1 ��ص�����/��Ч��
        TurnManager.Instance?.TriggerPropUseKeyHintFor(pid);

        _picked = true;

        if (destroyOnPickup)
            Destroy(gameObject); // �ó��ϵ�ʰȡ����ʧ
    }
}