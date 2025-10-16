using UnityEngine;

// 放在类外，避免属性误用
public enum VisualScaleMode
{
    VisualChildOnly,
    WholeObject
}

public enum MassScalingMode
{
    None,
    FixedMultiplier,
    ByArea2D,
    ByVolume3D
}

public class WeightPlacer : MonoBehaviour
{
    [Header("视觉放大（会影响碰撞体）")]
    [Min(0.01f)] public float visualScaleMultiplier = 1.25f;

    [Tooltip("WholeObject：整体放大（碰撞体也变大）；VisualChildOnly：仅放大外观子物体（不影响碰撞体）")]
    public VisualScaleMode visualScaleMode = VisualScaleMode.WholeObject;

    [Header("质量放大模式")]
    [Tooltip("None：不修改质量；FixedMultiplier：用 massMultiplier；ByArea2D：质量随面积 s²；ByVolume3D：质量随体积 s³")]
    public MassScalingMode massScalingMode = MassScalingMode.ByArea2D;

    [Tooltip("仅当模式为 FixedMultiplier 时生效")]
    public float massMultiplier = 1.5f;

    [Header("是否同步放大重力")]
    public bool alsoScaleGravity = false;
    public float gravityMultiplier = 1.0f;

    [Header("仅放大外观时：外观子物体名称（不区分大小写）")]
    public string visualChildName = "Visual";

    /// <summary>
    /// 对指定玩家使用配重道具（整体放大 + 加重）
    /// </summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentPiece(playerId);
        if (!target)
        {
            Debug.LogWarning($"[WeightPlacer] 未找到 P{playerId} 的当前积木。");
            return;
        }

        var rb = target.GetComponent<Rigidbody2D>();
        if (!rb)
        {
            Debug.LogWarning("[WeightPlacer] 目标没有 Rigidbody2D。");
            return;
        }

        // 防重复
        if (target.GetComponent<_WeightAppliedFlag>())
        {
            Debug.Log("[WeightPlacer] 已对该块使用过配重，跳过。");
            return;
        }

        // 1) 放大
        Transform scaled = null;
        Vector3 oldScale = Vector3.one;

        if (visualScaleMode == VisualScaleMode.WholeObject)
        {
            scaled = target;
            oldScale = target.localScale;
            target.localScale = oldScale * visualScaleMultiplier; // 整体放大：碰撞体也变大
        }
        else
        {
            scaled = FindVisualChild(target, visualChildName);
            if (scaled)
            {
                oldScale = scaled.localScale;
                scaled.localScale = oldScale * visualScaleMultiplier; // 仅放大外观
            }
            else
            {
                Debug.LogWarning($"[WeightPlacer] 未找到外观子物体 {visualChildName}，跳过视觉放大。");
            }
        }

        // 强制刷新碰撞体几何
        var allCols = target.GetComponentsInChildren<Collider2D>(true);
        foreach (var col in allCols)
        {
            col.enabled = false;
            col.enabled = true;
        }

        // 2) 质量
        float oldMass = rb.mass;
        float finalMassMul = 1f;

        switch (massScalingMode)
        {
            case MassScalingMode.None:
                finalMassMul = 1f;
                break;
            case MassScalingMode.FixedMultiplier:
                finalMassMul = Mathf.Max(0.01f, massMultiplier);
                break;
            case MassScalingMode.ByArea2D:
                finalMassMul = Mathf.Max(0.01f, visualScaleMultiplier * visualScaleMultiplier);
                break;
            case MassScalingMode.ByVolume3D:
                finalMassMul = Mathf.Max(0.01f,
                    visualScaleMultiplier * visualScaleMultiplier * visualScaleMultiplier);
                break;
        }

        rb.mass = Mathf.Max(0.01f, rb.mass * finalMassMul);

        if (alsoScaleGravity)
            rb.gravityScale *= Mathf.Max(0.01f, gravityMultiplier);

        rb.WakeUp();

        var flag = target.gameObject.AddComponent<_WeightAppliedFlag>();
        flag.originalMass = oldMass;
        flag.originalScale = oldScale;
        flag.scaledTransform = scaled ? scaled : target;

        Debug.Log($"[WeightPlacer] {target.name} 放大 ×{visualScaleMultiplier}，质量 ×{finalMassMul}。");
    }

    // 查找当前玩家正在使用的积木
    Transform FindCurrentPiece(int playerId)
    {
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;

            var vz = mk.GetComponent<ValidZone>();
            if (vz && vz.isTowerMember) continue;

            float y = mk.transform.position.y;
            if (y > bestY)
            {
                bestY = y;
                best = mk.transform;
            }
        }
        return best;
    }

    // 在目标下寻找指定名称的子物体
    Transform FindVisualChild(Transform root, string name)
    {
        if (!root || string.IsNullOrEmpty(name)) return null;
        string low = name.ToLowerInvariant();

        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name.ToLowerInvariant() == low) return c;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root) continue;
            if (t.name.ToLowerInvariant() == low) return t;
        }
        return null;
    }

    // 标记该积木已被加重
    class _WeightAppliedFlag : MonoBehaviour
    {
        public float originalMass;
        public Vector3 originalScale;
        public Transform scaledTransform;
    }
}