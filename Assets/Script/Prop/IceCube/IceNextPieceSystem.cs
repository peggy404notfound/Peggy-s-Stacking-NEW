using UnityEngine;
using TMPro;

public class IceNextPieceSystem : MonoBehaviour
{
    public static IceNextPieceSystem Instance { get; private set; }

    [Header("给 P1 / P2 生成的冰块 Prefab")]
    public GameObject iceBlockPrefabForP1;
    public GameObject iceBlockPrefabForP2;

    [Header("使用冰冻道具时音效（可选）")]
    public AudioClip useIceSfx;
    [Range(0f, 1f)] public float useSfxVolume = 1f;

    [Header("TMP 提示（直接拖已设置好的 TextMeshPro）")]
    public TextMeshProUGUI hintForP1;
    public TextMeshProUGUI hintForP2;

    // 状态标记
    bool nextPieceFrozenForP1 = false;
    bool nextPieceFrozenForP2 = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始隐藏提示
        SafeHide(hintForP1);
        SafeHide(hintForP2);
    }

    /// <summary>
    /// 使用冰块道具时调用。userPlayerId=1 表示P1使用（则P2下一块变冰）。
    /// </summary>
    public void ApplyToOpponentNext(int userPlayerId)
    {
        if (useIceSfx && Camera.main)
            AudioSource.PlayClipAtPoint(useIceSfx, Camera.main.transform.position, useSfxVolume);

        if (userPlayerId == 1)
        {
            nextPieceFrozenForP2 = true;
            ShowHint(hintForP2);
        }
        else
        {
            nextPieceFrozenForP1 = true;
            ShowHint(hintForP1);
        }

        TurnManager.Instance?.TriggerIceAppearIfNeeded();
    }

    /// <summary>
    /// 在TurnManager.Spawn里调用：确定是否替换为冰块并隐藏提示。
    /// </summary>
    public GameObject MaybeOverridePrefabFor(TurnManager.Player p, GameObject basePrefab)
    {
        if (p == TurnManager.Player.P1 && nextPieceFrozenForP1)
        {
            nextPieceFrozenForP1 = false;
            SafeHide(hintForP1);
            return iceBlockPrefabForP1 ? iceBlockPrefabForP1 : basePrefab;
        }
        if (p == TurnManager.Player.P2 && nextPieceFrozenForP2)
        {
            nextPieceFrozenForP2 = false;
            SafeHide(hintForP2);
            return iceBlockPrefabForP2 ? iceBlockPrefabForP2 : basePrefab;
        }
        return basePrefab;
    }

    // ===== 工具函数 =====
    void ShowHint(TextMeshProUGUI tmp)
    {
        if (tmp && !tmp.gameObject.activeSelf)
            tmp.gameObject.SetActive(true);
    }

    void SafeHide(TextMeshProUGUI tmp)
    {
        if (tmp && tmp.gameObject.activeSelf)
            tmp.gameObject.SetActive(false);
    }

    public void ResetAll()
    {
        nextPieceFrozenForP1 = false;
        nextPieceFrozenForP2 = false;
        SafeHide(hintForP1);
        SafeHide(hintForP2);
    }
}