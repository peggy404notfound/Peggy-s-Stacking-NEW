using UnityEngine;

public class GluePlacer : MonoBehaviour
{
    [Header("只允许粘到这些层（底座/其它积木）")]
    public LayerMask stickableLayers = ~0;

    [Header("底部接触判定：与 Vector2.up 的点积阈值")]
    [Range(0f, 1f)] public float bottomDotThreshold = 0.5f;

    [Header("落地反馈")]
    public AudioClip glueStickSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    public GameObject glueSplashPrefab;

    [Header("Prefab 内子物体控制")]
    [Tooltip("在每个积木 prefab 里放一个同名子物体，并默认 inactive")]
    public string glueChildName = "Glue";
    [Tooltip("若存在多个同名物体是否全部激活")]
    public bool activateAllMatches = true;

    /// <summary>
    /// 玩家使用胶水：为该玩家“当前待落下”的方块添加 BottomGlueEffect，
    /// 并激活 prefab 中的 Glue 子物体。
    /// </summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentFallingPiece(playerId);
        if (!target)
        {
            Debug.LogWarning("[GluePlacer] 没找到当前待落下的方块。");
            return;
        }

        // ① 加入物理胶水逻辑
        var eff = target.GetComponent<BottomGlueEffect>() ?? target.gameObject.AddComponent<BottomGlueEffect>();
        eff.Init(stickableLayers, bottomDotThreshold, glueStickSfx, sfxVolume, glueSplashPrefab);

        // ② 激活 Glue 子物体
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
            Debug.LogWarning($"[GluePlacer] 未在 {target.name} 下找到名为 \"{glueChildName}\" 的子物体。");
        else
            Debug.Log($"[GluePlacer] 已在 {target.name} 激活 {count} 个 \"{glueChildName}\" 子物体。");
    }

    // ========== 寻找当前“待落下”的那块 ==========
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