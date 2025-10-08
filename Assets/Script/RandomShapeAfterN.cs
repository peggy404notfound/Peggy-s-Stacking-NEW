using UnityEngine;


public class RandomShapeAfterN : MonoBehaviour
{
    // —— 基本设置 ——
    [Tooltip("从第几回合开始进入随机形状（例如 6 = 前5回合固定方块，第6回合才开始随机）")]
    public int startRandomTurn = 6;

    [Header("P1 默认正方形 / 随机池")]
    public GameObject P1DefaultSquare;
    public GameObject[] P1Shapes;       // 第 startRandomTurn 回合后，从这里随机
    [Tooltip("可选：与 P1Shapes 等长的权重（不填=均匀随机）")]
    public float[] P1Weights;

    [Header("P2 默认正方形 / 随机池")]
    public GameObject P2DefaultSquare;
    public GameObject[] P2Shapes;
    [Tooltip("可选：与 P2Shapes 等长的权重（不填=均匀随机）")]
    public float[] P2Weights;

    // —— 单例（方便任何脚本调用） ——
    public static RandomShapeAfterN Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 可选：如果希望跨场景存在，取消注释
        // DontDestroyOnLoad(gameObject);
    }

    public GameObject GetPrefabFor(TurnManager.Player owner, int turnCount)
    {
        // 前期固定正方形
        if (turnCount < startRandomTurn)
        {
            return owner == TurnManager.Player.P1 ? P1DefaultSquare : P2DefaultSquare;
        }

        // 进入随机阶段
        if (owner == TurnManager.Player.P1)
        {
            return PickFromPool(P1Shapes, P1Weights, fallback: P1DefaultSquare);
        }
        else
        {
            return PickFromPool(P2Shapes, P2Weights, fallback: P2DefaultSquare);
        }
    }

    // —— 工具函数：从池中按权重/均匀随机挑选 ——
    GameObject PickFromPool(GameObject[] pool, float[] weights, GameObject fallback)
    {
        if (pool == null || pool.Length == 0)
            return fallback;

        // 如果没填权重或长度不匹配 → 均匀随机
        if (weights == null || weights.Length != pool.Length)
        {
            int idx = Random.Range(0, pool.Length);
            return pool[idx] != null ? pool[idx] : fallback;
        }

        // 有权重：做一次加权随机
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
            total += Mathf.Max(0f, weights[i]);

        if (total <= 0f)
        {
            int idx = Random.Range(0, pool.Length);
            return pool[idx] != null ? pool[idx] : fallback;
        }

        float r = Random.value * total;
        float cum = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            cum += Mathf.Max(0f, weights[i]);
            if (r <= cum)
                return pool[i] != null ? pool[i] : fallback;
        }
        return fallback;
    }
}