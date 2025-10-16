using UnityEngine;

public class IceNextPieceSystem : MonoBehaviour
{
    public static IceNextPieceSystem Instance { get; private set; }

    [Header("�� P1 / P2 ���ɵı��� Prefab����ֱ����� IceCubeP1 / IceCubeP2��")]
    public GameObject iceBlockPrefabForP1;   // ����P1 ����һ�顱��Ҫ��ɱ�ʱʹ��
    public GameObject iceBlockPrefabForP2;   // ����P2 ����һ�顱��Ҫ��ɱ�ʱʹ��

    // ��ǣ�ĳ������һ�顱Ϊ����һ���ԣ�
    bool nextPieceFrozenForP1 = false;
    bool nextPieceFrozenForP2 = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// �ɡ��������ߡ�ʹ��ʱ���ã�userPlayerId ʹ�ú��á����֡�����һ����
    /// userPlayerId: 1 ��ʾʹ����Ϊ P1���� P2 ��һ��������2 ��ʾʹ����Ϊ P2���� P1 ��һ������
    /// </summary>
    public void ApplyToOpponentNext(int userPlayerId)
    {
        if (userPlayerId == 1) nextPieceFrozenForP2 = true;
        else nextPieceFrozenForP1 = true;

        Debug.Log($"[ICE] user={userPlayerId} -> mark next frozen: P1={nextPieceFrozenForP1}, P2={nextPieceFrozenForP2}");
        TurnManager.Instance?.TriggerIceAppearIfNeeded();

        // �״γ��ֽ�ѧ��������� TurnManager ����� iceHint / TriggerIceAppearIfNeeded��
        TurnManager.Instance?.TriggerIceAppearIfNeeded();
    }

    /// <summary>
    /// �� TurnManager.Spawn(p) �ڡ�ȷ������ prefab��ʱ���á�
    /// ��� p ����һ�鱻���Ϊ�����򷵻ض�Ӧ��Ӫ�ı��� prefab���������ǣ����򷵻� basePrefab��
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
