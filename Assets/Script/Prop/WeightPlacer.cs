using UnityEngine;

/// <summary>
/// 给当前玩家“本回合的那块积木”临时加重（直接乘以质量系数）
/// 使用：WeightPlacer.Begin(playerId);
/// 规则：
/// - 从场景中找到 owner=playerId 的所有积木；
/// - 筛掉已经并入塔体(ValidZone.isTowerMember==true)的；
/// - 选 Y 最高的一块，给它 rb.mass *= massMultiplier；
/// - 防重：同一块只会应用一次（挂个标记组件）
/// </summary>
public class WeightPlacer : MonoBehaviour
{
    [Header("质量倍率（默认1.5×）")]
    public float massMultiplier = 1.5f;

    [Header("可选：是否同时放大重力")]
    public bool alsoScaleGravity = false;
    public float gravityMultiplier = 1.0f;

    /// <summary>对指定玩家使用加重道具</summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentPiece(playerId);
        if (target == null)
        {
            Debug.LogWarning($"[WeightPlacer] 没找到 P{playerId} 的当前积木，无法加重。");
            return;
        }

        var rb = target.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning($"[WeightPlacer] 目标积木无 Rigidbody2D。");
            return;
        }

        // 防重复：同一块只加重一次
        var flag = target.GetComponent<_WeightAppliedFlag>();
        if (flag != null)
        {
            Debug.Log($"[WeightPlacer] 已对该块加重过，忽略。");
            return;
        }

        // 应用
        float oldMass = rb.mass;
        rb.mass = Mathf.Max(0.01f, oldMass * Mathf.Max(0.01f, massMultiplier));

        if (alsoScaleGravity)
            rb.gravityScale *= Mathf.Max(0.01f, gravityMultiplier);

        target.gameObject.AddComponent<_WeightAppliedFlag>().Init(oldMass);

        Debug.Log($"[WeightPlacer] 对 {target.name} 加重：mass {oldMass:F2} → {rb.mass:F2}");
    }

    /// <summary>找到该玩家“当前回合的那块”：owner=playerId，未并入塔体，Y最高</summary>
    Transform FindCurrentPiece(int playerId)
    {
        // 找所有 BlockMark
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;

            // 有刚体才算
            var rb = mk.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            // 已经并入塔体的不要（如果没有 ValidZone 组件就忽略这个条件）
            var vz = mk.GetComponent<ValidZone>();
            if (vz != null && vz.isTowerMember) continue;

            float y = mk.transform.position.y;
            if (y > bestY)
            {
                bestY = y;
                best = mk.transform;
            }
        }

        return best;
    }

    /// <summary>标记：表明这块已经被加重过，顺便存个原始质量（目前不做回退）</summary>
    class _WeightAppliedFlag : MonoBehaviour
    {
        public float originalMass;
        public void Init(float m) => originalMass = m;
    }
}