using UnityEngine;
public class GluePlacer : MonoBehaviour
{
    [Header("可粘的层（塔块/底座等）。默认全部可粘")]
    public LayerMask stickableLayers = ~0;   // ~0 表示所有层

    /// <summary>对该玩家当前方块施加“粘住”效果。</summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentPiece(playerId);
        if (target == null)
        {
            Debug.LogWarning($"[GluePlacer] 没找到 P{playerId} 的当前积木，无法上胶。");
            return;
        }

        // 已经上过胶就不重复
        if (target.GetComponent<_GlueAppliedFlag>() != null)
        {
            Debug.Log("[GluePlacer] 该块已上过胶，忽略。");
            return;
        }

        var eff = target.gameObject.AddComponent<_GlueEffectSimple>();
        eff.Init(stickableLayers);
        target.gameObject.AddComponent<_GlueAppliedFlag>(); // 标记

        Debug.Log($"[GluePlacer] 已对 {target.name} 施加胶水效果（碰到就粘，永不掉）。");
    }

    /// <summary>找到该玩家“当前回合的那块”：owner=playerId，未并入塔体，Y最高。</summary>
    Transform FindCurrentPiece(int playerId)
    {
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;

            var rb = mk.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            // 已经并入塔体的不算“当前块”（如果你想也能上胶，可删除这两行）
            var vz = mk.GetComponent<ValidZone>();
            if (vz != null && vz.isTowerMember) continue;

            float y = mk.transform.position.y;
            if (y > bestY) { bestY = y; best = mk.transform; }
        }
        return best;
    }

    /// <summary>内部：简单胶水效果（挂在目标积木上）。任意接触即粘住，关节永不折断。</summary>
    class _GlueEffectSimple : MonoBehaviour
    {
        LayerMask _stickable;
        bool _stuck = false;
        Rigidbody2D _rb;

        public void Init(LayerMask stickable) { _stickable = stickable; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) Debug.LogWarning("[GlueEffect] 目标没有 Rigidbody2D。");
        }

        void OnCollisionEnter2D(Collision2D col) { TryStick(col); }
        void OnCollisionStay2D(Collision2D col) { TryStick(col); }

        void TryStick(Collision2D col)
        {
            if (_stuck || _rb == null) return;

            // 层过滤：只和 stickableLayers 粘
            if (((1 << col.collider.gameObject.layer) & _stickable) == 0) return;

            // 创建 FixedJoint2D，连到对方刚体；若没有刚体则连到世界
            var fj = gameObject.AddComponent<FixedJoint2D>();
            fj.autoConfigureConnectedAnchor = true;
            fj.enableCollision = true;
            fj.breakForce = Mathf.Infinity;  // 永不折断
            fj.breakTorque = Mathf.Infinity;

            var otherRb = col.rigidbody ?? col.collider.GetComponentInParent<Rigidbody2D>();
            fj.connectedBody = otherRb;      // null = 连到世界（底座没刚体也能粘住）

            _stuck = true;
            Debug.Log($"[GlueEffect] {name} 已粘住 {(otherRb ? otherRb.name : "World")}。");

            // 效果完成，移除此脚本（保留关节）
            Destroy(this);
        }
    }

    /// <summary>标记：该块已经应用过胶水（防重复）。</summary>
    class _GlueAppliedFlag : MonoBehaviour { }
}