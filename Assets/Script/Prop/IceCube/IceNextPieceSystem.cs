using UnityEngine;

public class IceNextPieceSystem : MonoBehaviour
{
    public static IceNextPieceSystem Instance { get; private set; }

    [Header("给 P1 / P2 生成的冰块 Prefab（请分别拖入 IceCubeP1 / IceCubeP2）")]
    public GameObject iceBlockPrefabForP1;   // 当“P1 的下一块”需要变成冰时使用
    public GameObject iceBlockPrefabForP2;   // 当“P2 的下一块”需要变成冰时使用

    [Header("使用冰冻道具时音效（新增）")]
    public AudioClip useIceSfx;              // 使用冰冻道具的音效
    [Range(0f, 1f)] public float useSfxVolume = 1f;

    // 标记：某方“下一块”为冰（一次性）
    bool nextPieceFrozenForP1 = false;
    bool nextPieceFrozenForP2 = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// 由“冰冻道具”使用时调用：userPlayerId 使用后，让“对手”的下一块结冰
    /// userPlayerId: 1 表示使用者为 P1（则 P2 下一块结冰）；2 表示使用者为 P2（则 P1 下一块结冰）
    /// </summary>
    public void ApplyToOpponentNext(int userPlayerId)
    {
        // --- 新增：播放使用音效 ---
        if (useIceSfx != null)
        {
            AudioSource.PlayClipAtPoint(useIceSfx, Camera.main.transform.position, useSfxVolume);
        }

        if (userPlayerId == 1) nextPieceFrozenForP2 = true;
        else nextPieceFrozenForP1 = true;

        Debug.Log($"[ICE] user={userPlayerId} -> mark next frozen: P1={nextPieceFrozenForP1}, P2={nextPieceFrozenForP2}");

        // 首次出现教学（如果你在 TurnManager 里加了 iceHint / TriggerIceAppearIfNeeded）
        TurnManager.Instance?.TriggerIceAppearIfNeeded();
    }

    /// <summary>
    /// 供 TurnManager.Spawn(p) 在“确定最终 prefab”时调用。
    /// 如果 p 的下一块被标记为冰，则返回对应阵营的冰块 prefab，并清除标记；否则返回 basePrefab。
    /// </summary>
    public GameObject MaybeOverridePrefabFor(TurnManager.Player p, GameObject basePrefab)
    {
        if (p == TurnManager.Player.P1 && nextPieceFrozenForP1)
        {
            Debug.Log("[ICE] Override -> P1 use ice prefab");
            nextPieceFrozenForP1 = false;
            return iceBlockPrefabForP1 ? iceBlockPrefabForP1 : basePrefab;
        }
        if (p == TurnManager.Player.P2 && nextPieceFrozenForP2)
        {
            Debug.Log("[ICE] Override -> P2 use ice prefab");
            nextPieceFrozenForP2 = false;
            return iceBlockPrefabForP2 ? iceBlockPrefabForP2 : basePrefab;
        }
        return basePrefab;
    }
}