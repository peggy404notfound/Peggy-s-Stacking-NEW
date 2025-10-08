using UnityEngine;
using UnityEngine.Events;

public class RisingHandEndUI : MonoBehaviour
{
    [Header("����")]
    public RectTransform handRect;          // �ֵ� RectTransform��Anchor �ڵױ�����λ�ã�Pivot.y = 0��
    public Collider2D baseCollider;         // Base �� BoxCollider2D������ȷ�����ָ߶ȣ�

    [Header("�������ƶ�")]
    public int startAfterDropped = 6;         // ���� N ����������/����������֣���غ����ﵽ N��
    public float baseRiseSpeedPx = 160f;      // �����ٶȣ�px/s��
    public float speedGainPerTurnPx = 0f;     // ÿ�غ��ٶ�������������پ��� 0��
    public float maxRiseSpeedPx = 700f;       // �ٶ����ޣ�px/s��
    public float startBottomPaddingPx = 24f;  // �ֳ���ʱ�� Base ����������ƫ��

    [Header("�ж����������߼�ʱ�ж���")]
    public float gracePx = 0f;                  // ���ݶȣ����賬�� ����+grace ���и�
    public float considerAsSettledSpeed = 0.05f;// ��Ϊ�����ȡ����ٶ���ֵ�����絥λ/�룩

    [Header("�����ص�")]
    public bool freezeTimeOnEnd = true;
    public UnityEvent onGameOver;

    [Header("��ѧ UI ����")]
    [Tooltip("���ߵ�һ�γ���ʱ���� TurnManager �ĺ��߽�ѧ��ʾ")]
    public bool triggerRedLineHintOnStart = true;

    // TurnManager ���ף����ڳ���/���ٵļ�����
    public static int turnCountFromTM = 0;
    public static void ReportTurn(int totalTurns) => turnCountFromTM = totalTurns;

    public static bool IsGameOver = false;

    Camera cam;
    RectTransform canvasRect;
    bool started, ended;

    // ��ֹ��δ�����ѧ
    bool _redLineHintFired = false;

    void Awake()
    {
        cam = Camera.main;
        if (!handRect || !baseCollider)
        {
            Debug.LogWarning("[RisingHandEndUI] ���� Inspector ���� Hand Rect �� Base Collider��");
            enabled = false; return;
        }
        canvasRect = handRect.root as RectTransform;
        handRect.gameObject.SetActive(false);

        // ÿ������/���ýű�ʱ���ý�����ǣ������г�������Ϊ true��
        IsGameOver = false;
    }

    void Start()
    {
        // �ֵĳ�ʼλ�ã�Base �� + padding
        var p = handRect.anchoredPosition;
        p.y = GetBaseTopCanvasY() + startBottomPaddingPx;
        handRect.anchoredPosition = p;
    }

    void Update()
    {
        if (ended) return;

        // 1) ���㣺��ǰ��������/������������ & ��߶��ߣ�Canvas Y��
        int settledCount;
        float highestTopY = GetHighestTopCanvasY(out settledCount);

        // 2) �������֣�ʹ�ã��غ϶��� vs ��������ȡ��һ�����֣�����ÿ֡�ƶ����ж�
        int count = Mathf.Max(turnCountFromTM, settledCount);
        if (!started && count >= startAfterDropped)
        {
            started = true;
            handRect.gameObject.SetActive(true);

            // ���� �����״γ��֣��������߽�ѧ��TurnManager �Ḻ���䶨�ſ�/һ���ԡ�������
            if (triggerRedLineHintOnStart && !_redLineHintFired)
            {
                _redLineHintFired = true;
                TurnManager.Instance?.TriggerRedLineHintIfNeeded();
            }
        }
        if (!started) return;

        // 3) �ƶ���ÿ֡��������������ٶȣ��� speedGainPerTurnPx > 0��
        int extraTurns = Mathf.Max(0, count - startAfterDropped);
        float v = Mathf.Min(maxRiseSpeedPx, baseRiseSpeedPx + extraTurns * speedGainPerTurnPx);

        var p = handRect.anchoredPosition;
        p.y += v * Time.unscaledDeltaTime;   // ʼ������������ Time.timeScale Ӱ�죩
        handRect.anchoredPosition = p;

        // 4) ��ʱ�и������ȴ���һ�غϣ����ֳ�������������ȶ��� + gracePx�����̽���
        if (p.y > highestTopY + gracePx)
            EndGame();
    }

    // ���� Base ��������Y -> Canvas�ֲ�Y��Overlay���� null������
    float GetBaseTopCanvasY()
    {
        float baseTopWorldY = baseCollider.bounds.max.y;
        Vector2 screen = (Vector2)cam.WorldToScreenPoint(new Vector3(0f, baseTopWorldY, 0f));
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        return local.y;
    }

    // ���� ֻͳ�ơ�������/�������Ŀ飬���ء���߶��ߡ��� CanvasY ���� 
    float GetHighestTopCanvasY(out int settledCount)
    {
        settledCount = 0;
        float bestTopWorld = baseCollider ? baseCollider.bounds.max.y : float.NegativeInfinity;
        float v2Threshold = considerAsSettledSpeed * considerAsSettledSpeed;

        var blocks = Object.FindObjectsOfType<BlockMark>();
        foreach (var b in blocks)
        {
            if (!b) continue;

            // ֻ�ơ�������/�������ģ�ValidZone.isTowerMember Ϊ�棬�����˯��/�ٶȺܵ�
            bool settled = false;

            var vz = b.GetComponent<ValidZone>();
            if (vz && vz.isTowerMember) settled = true;

            if (!settled)
            {
                var rb = b.GetComponent<Rigidbody2D>();
                if (rb && (!rb.IsAwake() || rb.velocity.sqrMagnitude <= v2Threshold))
                    settled = true;
            }

            if (!settled) continue;

            settledCount++;

            Bounds bd;
            var col = b.GetComponentInChildren<Collider2D>();
            if (col) bd = col.bounds;
            else
            {
                var r = b.GetComponentInChildren<Renderer>();
                bd = r ? r.bounds : new Bounds(b.transform.position, Vector3.zero);
            }

            if (bd.max.y > bestTopWorld) bestTopWorld = bd.max.y; // ����
        }

        Vector2 screen = (Vector2)cam.WorldToScreenPoint(new Vector3(0f, bestTopWorld, 0f));
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        return local.y;
    }

    void EndGame()
    {
        IsGameOver = true;
        if (ended) return;
        ended = true;
        if (freezeTimeOnEnd) Time.timeScale = 0f;
        onGameOver?.Invoke();
        Debug.Log("[RisingHandEndUI] Game Over");
    }
}