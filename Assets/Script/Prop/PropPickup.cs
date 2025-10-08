using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PropPickup : MonoBehaviour
{
    [Tooltip("拾取后是否立刻销毁这个道具实例")]
    public bool destroyOnPickup = true;

    bool _picked = false;

    void Reset()
    {
        // 确保碰撞体是触发器
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_picked) return;

        // 只响应玩家当前在玩的积木（自己或父级有 BlockMark）
        var mark = other.GetComponent<BlockMark>() ?? other.GetComponentInParent<BlockMark>();
        if (mark == null) return;

        int pid = mark.ownerPlayerId;
        if (pid != 1 && pid != 2) return;

        // 找到对应玩家的单槽背包
        var inv = PlayerInventoryOneSlot.GetForPlayer(pid);
        if (inv == null) return;

        // 从当前道具读取 ID（允许没有 PropItem 时也能正常放入一个默认 ID）
        var item = GetComponent<PropItem>();
        string propId = item ? item.propId : "unknown";

        // 放入背包（覆盖）
        inv.Set(propId);

        // —— 关键：触发“首次获得道具按哪个键使用”的提示 —— //
        // TurnManager 内部会处理“本局只尝试一次”和“等上一回合落定再弹”，
        // 所以这里直接调用即可，不会打断 P1 落地的震屏/音效。
        TurnManager.Instance?.TriggerPropUseKeyHintFor(pid);

        _picked = true;

        if (destroyOnPickup)
            Destroy(gameObject); // 让场上的拾取物消失
    }
}