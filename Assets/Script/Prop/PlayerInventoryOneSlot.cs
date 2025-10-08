using UnityEngine;

public class PlayerInventoryOneSlot : MonoBehaviour
{
    [Range(1, 2)] public int playerId = 1;

    [Header("��ǰ���ߣ����Կɼ���")]
    public string currentPropId = "";

    // ��̬����������ȫ�ֻ�ȡ
    public static PlayerInventoryOneSlot P1, P2;

    void Awake()
    {
        if (playerId == 1) P1 = this;
        else if (playerId == 2) P2 = this;
    }

    public static PlayerInventoryOneSlot GetForPlayer(int id)
        => (id == 1) ? P1 : (id == 2 ? P2 : null);

    /// ���루���ǣ�����
    public void Set(string propId)
    {
        currentPropId = propId;
        // TODO: ˢ�±���UI
        Debug.Log($"[Inv] P{playerId} got '{propId}'");
    }

    /// ���Ĳ���գ����ر����ѵ�ID��û���򷵻ؿմ���
    public string Consume()
    {
        var id = currentPropId;
        currentPropId = "";
        // TODO: ˢ�±���UIΪ��
        if (!string.IsNullOrEmpty(id))
            Debug.Log($"[Inv] P{playerId} used '{id}'");
        return id;
    }

    public bool HasProp() => !string.IsNullOrEmpty(currentPropId);
}