using UnityEngine;

public class PlayerInventoryOneSlot : MonoBehaviour
{
    [Range(1, 2)] public int playerId = 1;

    [Header("当前道具（调试可见）")]
    public string currentPropId = "";

    // 静态索引，方便全局获取
    public static PlayerInventoryOneSlot P1, P2;

    void Awake()
    {
        if (playerId == 1) P1 = this;
        else if (playerId == 2) P2 = this;
    }

    public static PlayerInventoryOneSlot GetForPlayer(int id)
        => (id == 1) ? P1 : (id == 2 ? P2 : null);

    /// 放入（覆盖）道具
    public void Set(string propId)
    {
        currentPropId = propId;
        // TODO: 刷新背包UI
        Debug.Log($"[Inv] P{playerId} got '{propId}'");
    }

    /// 消耗并清空（返回被消费的ID；没有则返回空串）
    public string Consume()
    {
        var id = currentPropId;
        currentPropId = "";
        // TODO: 刷新背包UI为空
        if (!string.IsNullOrEmpty(id))
            Debug.Log($"[Inv] P{playerId} used '{id}'");
        return id;
    }

    public bool HasProp() => !string.IsNullOrEmpty(currentPropId);
}