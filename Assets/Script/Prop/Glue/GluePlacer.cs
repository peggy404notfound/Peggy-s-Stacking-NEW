using UnityEngine;

public class GluePlacer : MonoBehaviour
{
    [Header("ֻ����ճ����Щ�㣨����/������ľ��")]
    public LayerMask stickableLayers = ~0;

    [Header("�ײ��Ӵ��ж����� Vector2.up �ĵ����ֵ")]
    [Range(0f, 1f)] public float bottomDotThreshold = 0.5f;

    [Header("��ط���")]
    public AudioClip glueStickSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    public GameObject glueSplashPrefab;

    [Header("Prefab �����������")]
    [Tooltip("��ÿ����ľ prefab ���һ��ͬ�������壬��Ĭ�� inactive")]
    public string glueChildName = "Glue";
    [Tooltip("�����ڶ��ͬ�������Ƿ�ȫ������")]
    public bool activateAllMatches = true;

    /// <summary>
    /// ���ʹ�ý�ˮ��Ϊ����ҡ���ǰ�����¡��ķ������ BottomGlueEffect��
    /// ������ prefab �е� Glue �����塣
    /// </summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentFallingPiece(playerId);
        if (!target)
        {
            Debug.LogWarning("[GluePlacer] û�ҵ���ǰ�����µķ��顣");
            return;
        }

        // �� ��������ˮ�߼�
        var eff = target.GetComponent<BottomGlueEffect>() ?? target.gameObject.AddComponent<BottomGlueEffect>();
        eff.Init(stickableLayers, bottomDotThreshold, glueStickSfx, sfxVolume, glueSplashPrefab);

        // �� ���� Glue ������
        int count = 0;
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == glueChildName)
            {
                child.gameObject.SetActive(true);
                count++;
                if (!activateAllMatches) break;
            }
        }

        if (count == 0)
            Debug.LogWarning($"[GluePlacer] δ�� {target.name} ���ҵ���Ϊ \"{glueChildName}\" �������塣");
        else
            Debug.Log($"[GluePlacer] ���� {target.name} ���� {count} �� \"{glueChildName}\" �����塣");
    }

    // ========== Ѱ�ҵ�ǰ�������¡����ǿ� ==========
    Transform FindCurrentFallingPiece(int playerId)
    {
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;
            var rb = mk.GetComponent<Rigidbody2D>();
            if (!rb) continue;

            var vz = mk.GetComponent<ValidZone>();
            if (vz != null && vz.isTowerMember) continue;

            if (mk.transform.position.y > bestY)
            {
                bestY = mk.transform.position.y;
                best = mk.transform;
            }
        }
        return best;
    }
}