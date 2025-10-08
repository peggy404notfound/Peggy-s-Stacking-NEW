using UnityEngine;
public class GluePlacer : MonoBehaviour
{
    [Header("��ճ�Ĳ㣨����/�����ȣ���Ĭ��ȫ����ճ")]
    public LayerMask stickableLayers = ~0;   // ~0 ��ʾ���в�

    /// <summary>�Ը���ҵ�ǰ����ʩ�ӡ�ճס��Ч����</summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentPiece(playerId);
        if (target == null)
        {
            Debug.LogWarning($"[GluePlacer] û�ҵ� P{playerId} �ĵ�ǰ��ľ���޷��Ͻ���");
            return;
        }

        // �Ѿ��Ϲ����Ͳ��ظ�
        if (target.GetComponent<_GlueAppliedFlag>() != null)
        {
            Debug.Log("[GluePlacer] �ÿ����Ϲ��������ԡ�");
            return;
        }

        var eff = target.gameObject.AddComponent<_GlueEffectSimple>();
        eff.Init(stickableLayers);
        target.gameObject.AddComponent<_GlueAppliedFlag>(); // ���

        Debug.Log($"[GluePlacer] �Ѷ� {target.name} ʩ�ӽ�ˮЧ����������ճ������������");
    }

    /// <summary>�ҵ�����ҡ���ǰ�غϵ��ǿ顱��owner=playerId��δ�������壬Y��ߡ�</summary>
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

            // �Ѿ���������Ĳ��㡰��ǰ�顱���������Ҳ���Ͻ�����ɾ�������У�
            var vz = mk.GetComponent<ValidZone>();
            if (vz != null && vz.isTowerMember) continue;

            float y = mk.transform.position.y;
            if (y > bestY) { bestY = y; best = mk.transform; }
        }
        return best;
    }

    /// <summary>�ڲ����򵥽�ˮЧ��������Ŀ���ľ�ϣ�������Ӵ���ճס���ؽ������۶ϡ�</summary>
    class _GlueEffectSimple : MonoBehaviour
    {
        LayerMask _stickable;
        bool _stuck = false;
        Rigidbody2D _rb;

        public void Init(LayerMask stickable) { _stickable = stickable; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) Debug.LogWarning("[GlueEffect] Ŀ��û�� Rigidbody2D��");
        }

        void OnCollisionEnter2D(Collision2D col) { TryStick(col); }
        void OnCollisionStay2D(Collision2D col) { TryStick(col); }

        void TryStick(Collision2D col)
        {
            if (_stuck || _rb == null) return;

            // ����ˣ�ֻ�� stickableLayers ճ
            if (((1 << col.collider.gameObject.layer) & _stickable) == 0) return;

            // ���� FixedJoint2D�������Է����壻��û�и�������������
            var fj = gameObject.AddComponent<FixedJoint2D>();
            fj.autoConfigureConnectedAnchor = true;
            fj.enableCollision = true;
            fj.breakForce = Mathf.Infinity;  // �����۶�
            fj.breakTorque = Mathf.Infinity;

            var otherRb = col.rigidbody ?? col.collider.GetComponentInParent<Rigidbody2D>();
            fj.connectedBody = otherRb;      // null = �������磨����û����Ҳ��ճס��

            _stuck = true;
            Debug.Log($"[GlueEffect] {name} ��ճס {(otherRb ? otherRb.name : "World")}��");

            // Ч����ɣ��Ƴ��˽ű��������ؽڣ�
            Destroy(this);
        }
    }

    /// <summary>��ǣ��ÿ��Ѿ�Ӧ�ù���ˮ�����ظ�����</summary>
    class _GlueAppliedFlag : MonoBehaviour { }
}