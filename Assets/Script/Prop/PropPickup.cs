using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PropPickup : MonoBehaviour
{
    [Tooltip("拾取后是否立刻销毁这个道具实例")]
    public bool destroyOnPickup = true;

    [Header("拾取判定")]
    [Tooltip("要求的最小“实际重叠”穿透深度（米）。只有穿透达到该值才算拾取。")]
    [Min(0f)] public float minPenetration = 0.02f; // 视你的世界单位，0.01~0.03常见

    bool _picked = false;
    Collider2D _selfCol;

    void Reset()
    {
        // 确保碰撞体是触发器（保持你原设计）
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        _selfCol = GetComponent<Collider2D>();
        if (_selfCol && !_selfCol.isTrigger)
            _selfCol.isTrigger = true;
    }

    // 用统一入口，Enter/Stay 都尝试（只有满足“足够穿透”才会真正拾取）
    private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
    private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

    void TryPickup(Collider2D other)
    {
        if (_picked || !_selfCol || !other) return;

        // —— 新增：要求达到“最小穿透深度” —— //
        // Unity的 Collider2D.Distance：当重叠时 distance 为负值，其绝对值为穿透深度
        var d = other.Distance(_selfCol);
        if (!d.isOverlapped) return;                       // 没有重叠，直接返回
        if (-d.distance < minPenetration) return;          // 穿透深度不足，不算拾取

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

        // 首次获得道具提示
        TurnManager.Instance?.TriggerPropUseKeyHintFor(pid);

        _picked = true;

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}