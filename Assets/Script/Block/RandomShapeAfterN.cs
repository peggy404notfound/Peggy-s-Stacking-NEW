using UnityEngine;


public class RandomShapeAfterN : MonoBehaviour
{
    // ���� �������� ����
    [Tooltip("�ӵڼ��غϿ�ʼ���������״������ 6 = ǰ5�غϹ̶����飬��6�غϲſ�ʼ�����")]
    public int startRandomTurn = 6;

    [Header("P1 Ĭ�������� / �����")]
    public GameObject P1DefaultSquare;
    public GameObject[] P1Shapes;       // �� startRandomTurn �غϺ󣬴��������
    [Tooltip("��ѡ���� P1Shapes �ȳ���Ȩ�أ�����=���������")]
    public float[] P1Weights;

    [Header("P2 Ĭ�������� / �����")]
    public GameObject P2DefaultSquare;
    public GameObject[] P2Shapes;
    [Tooltip("��ѡ���� P2Shapes �ȳ���Ȩ�أ�����=���������")]
    public float[] P2Weights;

    // ���� �����������κνű����ã� ����
    public static RandomShapeAfterN Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // ��ѡ�����ϣ���糡�����ڣ�ȡ��ע��
        // DontDestroyOnLoad(gameObject);
    }

    public GameObject GetPrefabFor(TurnManager.Player owner, int turnCount)
    {
        // ǰ�ڹ̶�������
        if (turnCount < startRandomTurn)
        {
            return owner == TurnManager.Player.P1 ? P1DefaultSquare : P2DefaultSquare;
        }

        // ��������׶�
        if (owner == TurnManager.Player.P1)
        {
            return PickFromPool(P1Shapes, P1Weights, fallback: P1DefaultSquare);
        }
        else
        {
            return PickFromPool(P2Shapes, P2Weights, fallback: P2DefaultSquare);
        }
    }

    // ���� ���ߺ������ӳ��а�Ȩ��/���������ѡ ����
    GameObject PickFromPool(GameObject[] pool, float[] weights, GameObject fallback)
    {
        if (pool == null || pool.Length == 0)
            return fallback;

        // ���û��Ȩ�ػ򳤶Ȳ�ƥ�� �� �������
        if (weights == null || weights.Length != pool.Length)
        {
            int idx = Random.Range(0, pool.Length);
            return pool[idx] != null ? pool[idx] : fallback;
        }

        // ��Ȩ�أ���һ�μ�Ȩ���
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