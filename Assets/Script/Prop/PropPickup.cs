using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PropPickup : MonoBehaviour
{
    [Header("拾取后处理")]
    [Tooltip("拾取后是否立刻销毁这个道具实例")]
    public bool destroyOnPickup = true;

    [Header("重叠判定")]
    [Tooltip("要求的最小“实际重叠”穿透深度（米）。只有穿透达到该值才算拾取。")]
    [Min(0f)] public float minPenetration = 0.02f; // 0.01~0.03 常见，按你世界单位调

    [Header("仅允许玩家主动丢下后的短窗口拾取")]
    [Tooltip("只在 HoverMover.Drop() 之后的短时间窗口内允许拾取")]
    public bool onlyDuringDropWindow = true;

    [Tooltip("判定为下落的最小向下速度（米/秒），用来排除平台水平移动带来的误拾取")]
    [Min(0f)] public float minDownSpeed = 0.15f; // 0.1~0.3 可调

    private bool _picked = false;
    private Collider2D _selfCol;

    void Reset()
    {
        // 保证自身是触发器
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        _selfCol = GetComponent<Collider2D>();
        if (_selfCol && !_selfCol.isTrigger)
            _selfCol.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);
    private void OnTriggerStay2D(Collider2D other) => TryPickup(other);

    void TryPickup(Collider2D other)
    {
        if (_picked || !_selfCol || !other) return;

        // —— 新增：仅在Drop窗口 + 有足够向下速度时允许拾取 ——
        if (onlyDuringDropWindow)
        {
            // 必须是刚被玩家Drop的那块积木（HoverMover在Drop时登记的Collider）
            if (!HoverMover.IsColliderInDropWindow(other))
                return;

            // 必须确实在向下运动，避免平台水平移动导致误拾取
            var rb = other.attachedRigidbody;
            if (rb == null || rb.velocity.y > -minDownSpeed)
                return;
        }

        // —— 要求达到最小穿透深度，避免擦边触发 ——
        var d = other.Distance(_selfCol);
        if (!d.isOverlapped) return;              // 没重叠
        if (-d.distance < minPenetration) return; // 穿透不够深

        // —— 只响应玩家积木（自身或父对象有 BlockMark，并拿到玩家ID）——
        var mark = other.GetComponent<BlockMark>() ?? other.GetComponentInParent<BlockMark>();
        if (mark == null) return;

        int pid = mark.ownerPlayerId;
        if (pid != 1 && pid != 2) return;

        // —— 放入对应玩家的单槽背包 —— 
        var inv = PlayerInventoryOneSlot.GetForPlayer(pid);
        if (inv == null) return;

        var item = GetComponent<PropItem>();
        string propId = item ? item.propId : "unknown";
        inv.Set(propId);

        // —— 首次获得道具时的键位提示（可选）——
        TurnManager.Instance?.TriggerPropUseKeyHintFor(pid);

        _picked = true;
        if (destroyOnPickup)
            Destroy(gameObject);
    }
}