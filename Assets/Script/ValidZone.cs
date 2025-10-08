using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ValidZone : MonoBehaviour
{
    // ScoreManager 扫描的唯一信号
    public bool isTowerMember = false;

    // 若你的层名不是 Base/Stack，可在 Inspector 改名
    [Header("Layer names")]
    public string baseLayerName = "Base";
    public string blockLayerName = "Stack";

    // 缓存
    private Collider2D selfCol;
    private LayerMask baseMask;
    private ContactFilter2D filterBlocks;
    private readonly Collider2D[] contacts = new Collider2D[12]; // 小缓冲区足够

    void Awake()
    {
        selfCol = GetComponent<Collider2D>();

        // 通过名字取 LayerMask，避免手动拖拽
        baseMask = LayerMask.GetMask(baseLayerName);

        filterBlocks = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask(blockLayerName),
            useTriggers = false
        };
    }

    void FixedUpdate()
    {
        // 1) 是否接触 Base 层（最快）
        bool onBase = selfCol.IsTouchingLayers(baseMask);

        // 2) 没踩 Base 再看是否被塔成员支撑（贴边接触也算）
        bool onMember = false;
        if (!onBase)
        {
            int n = selfCol.GetContacts(filterBlocks, contacts); // 接触，不是重叠
            for (int i = 0; i < n; i++)
            {
                var otherCol = contacts[i];
                if (!otherCol) continue;

                var otherVZ = otherCol.GetComponentInParent<ValidZone>();
                if (otherVZ && otherVZ != this && otherVZ.isTowerMember)
                {
                    onMember = true;
                    break; // 够了，提前结束
                }
            }
        }

        bool newVal = onBase || onMember;
        if (newVal != isTowerMember)
            isTowerMember = newVal;
    }
}