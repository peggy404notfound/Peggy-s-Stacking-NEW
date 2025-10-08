using UnityEngine;

/// <summary>
/// ����ǰ��ҡ����غϵ��ǿ��ľ����ʱ���أ�ֱ�ӳ�������ϵ����
/// ʹ�ã�WeightPlacer.Begin(playerId);
/// ����
/// - �ӳ������ҵ� owner=playerId �����л�ľ��
/// - ɸ���Ѿ���������(ValidZone.isTowerMember==true)�ģ�
/// - ѡ Y ��ߵ�һ�飬���� rb.mass *= massMultiplier��
/// - ���أ�ͬһ��ֻ��Ӧ��һ�Σ��Ҹ���������
/// </summary>
public class WeightPlacer : MonoBehaviour
{
    [Header("�������ʣ�Ĭ��1.5����")]
    public float massMultiplier = 1.5f;

    [Header("��ѡ���Ƿ�ͬʱ�Ŵ�����")]
    public bool alsoScaleGravity = false;
    public float gravityMultiplier = 1.0f;

    /// <summary>��ָ�����ʹ�ü��ص���</summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentPiece(playerId);
        if (target == null)
        {
            Debug.LogWarning($"[WeightPlacer] û�ҵ� P{playerId} �ĵ�ǰ��ľ���޷����ء�");
            return;
        }

        var rb = target.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning($"[WeightPlacer] Ŀ���ľ�� Rigidbody2D��");
            return;
        }

        // ���ظ���ͬһ��ֻ����һ��
        var flag = target.GetComponent<_WeightAppliedFlag>();
        if (flag != null)
        {
            Debug.Log($"[WeightPlacer] �ѶԸÿ���ع������ԡ�");
            return;
        }

        // Ӧ��
        float oldMass = rb.mass;
        rb.mass = Mathf.Max(0.01f, oldMass * Mathf.Max(0.01f, massMultiplier));

        if (alsoScaleGravity)
            rb.gravityScale *= Mathf.Max(0.01f, gravityMultiplier);

        target.gameObject.AddComponent<_WeightAppliedFlag>().Init(oldMass);

        Debug.Log($"[WeightPlacer] �� {target.name} ���أ�mass {oldMass:F2} �� {rb.mass:F2}");
    }

    /// <summary>�ҵ�����ҡ���ǰ�غϵ��ǿ顱��owner=playerId��δ�������壬Y���</summary>
    Transform FindCurrentPiece(int playerId)
    {
        // ������ BlockMark
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;

            // �и������
            var rb = mk.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            // �Ѿ���������Ĳ�Ҫ�����û�� ValidZone ����ͺ������������
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

    /// <summary>��ǣ���������Ѿ������ع���˳����ԭʼ������Ŀǰ�������ˣ�</summary>
    class _WeightAppliedFlag : MonoBehaviour
    {
        public float originalMass;
        public void Init(float m) => originalMass = m;
    }
}