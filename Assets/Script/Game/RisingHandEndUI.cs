using UnityEngine;
using UnityEngine.Events;

public class RisingHandEndUI : MonoBehaviour
{
    [Header("引用")]
    public RectTransform handRect;          // 手的 RectTransform（Anchor 在底边任意位置，Pivot.y = 0）
    public Collider2D baseCollider;         // Base 的 BoxCollider2D（用于确定出现高度）

    [Header("出现与移动")]
    public int startAfterDropped = 6;         // 至少 N 个“已落稳/入塔”后出现（或回合数达到 N）
    public float baseRiseSpeedPx = 160f;      // 基础速度（px/s）
    public float speedGainPerTurnPx = 0f;     // 每回合速度增量（不想加速就留 0）
    public float maxRiseSpeedPx = 700f;       // 速度上限（px/s）
    public float startBottomPaddingPx = 24f;  // 手出现时距 Base 顶部的像素偏移

    [Header("判定参数（顶边即时判定）")]
    public float gracePx = 0f;                  // 宽容度：手需超过 顶边+grace 才判负
    public float considerAsSettledSpeed = 0.05f;// 认为“落稳”的速度阈值（世界单位/秒）

    [Header("结束回调")]
    public bool freezeTimeOnEnd = true;
    public UnityEvent onGameOver;

    [Header("教学 UI 触发")]
    [Tooltip("红线第一次出现时触发 TurnManager 的红线教学提示")]
    public bool triggerRedLineHintOnStart = true;

    // TurnManager 兜底（用于出现/加速的计数）
    public static int turnCountFromTM = 0;
    public static void ReportTurn(int totalTurns) => turnCountFromTM = totalTurns;

    public static bool IsGameOver = false;

    Camera cam;
    RectTransform canvasRect;
    bool started, ended;

    // 防止多次触发教学
    bool _redLineHintFired = false;

    void Awake()
    {
        cam = Camera.main;
        if (!handRect || !baseCollider)
        {
            Debug.LogWarning("[RisingHandEndUI] 请在 Inspector 拖入 Hand Rect 与 Base Collider。");
            enabled = false; return;
        }
        canvasRect = handRect.root as RectTransform;
        handRect.gameObject.SetActive(false);

        // 每次载入/启用脚本时重置结束标记（避免切场景后仍为 true）
        IsGameOver = false;
    }

    void Start()
    {
        // 手的初始位置：Base 顶 + padding
        var p = handRect.anchoredPosition;
        p.y = GetBaseTopCanvasY() + startBottomPaddingPx;
        handRect.anchoredPosition = p;
    }

    void Update()
    {
        if (ended) return;

        // 1) 计算：当前“已落稳/入塔”的数量 & 最高顶边（Canvas Y）
        int settledCount;
        float highestTopY = GetHighestTopCanvasY(out settledCount);

        // 2) 触发出现：使用（回合兜底 vs 落稳数）取大；一旦出现，后续每帧移动与判定
        int count = Mathf.Max(turnCountFromTM, settledCount);
        if (!started && count >= startAfterDropped)
        {
            started = true;
            handRect.gameObject.SetActive(true);

            // —— 红线首次出现：触发红线教学（TurnManager 会负责“落定门控/一次性”）——
            if (triggerRedLineHintOnStart && !_redLineHintFired)
            {
                _redLineHintFired = true;
                TurnManager.Instance?.TriggerRedLineHintIfNeeded();
            }
        }
        if (!started) return;

        // 3) 移动（每帧上升；如需递增速度，把 speedGainPerTurnPx > 0）
        int extraTurns = Mathf.Max(0, count - startAfterDropped);
        float v = Mathf.Min(maxRiseSpeedPx, baseRiseSpeedPx + extraTurns * speedGainPerTurnPx);

        var p = handRect.anchoredPosition;
        p.y += v * Time.unscaledDeltaTime;   // 始终上升（不受 Time.timeScale 影响）
        handRect.anchoredPosition = p;

        // 4) 即时判负（不等待下一回合）：手超过“最高已落稳顶边 + gracePx”立刻结束
        if (p.y > highestTopY + gracePx)
            EndGame();
    }

    // —— Base 顶部世界Y -> Canvas局部Y（Overlay：传 null）——
    float GetBaseTopCanvasY()
    {
        float baseTopWorldY = baseCollider.bounds.max.y;
        Vector2 screen = (Vector2)cam.WorldToScreenPoint(new Vector3(0f, baseTopWorldY, 0f));
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local);
        return local.y;
    }

    // —— 只统计“已落稳/入塔”的块，返回“最高顶边”的 CanvasY —— 
    float GetHighestTopCanvasY(out int settledCount)
    {
        settledCount = 0;
        float bestTopWorld = baseCollider ? baseCollider.bounds.max.y : float.NegativeInfinity;
        float v2Threshold = considerAsSettledSpeed * considerAsSettledSpeed;

        var blocks = Object.FindObjectsOfType<BlockMark>();
        foreach (var b in blocks)
        {
            if (!b) continue;

            // 只计“已落稳/入塔”的：ValidZone.isTowerMember 为真，或刚体睡眠/速度很低
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

            if (bd.max.y > bestTopWorld) bestTopWorld = bd.max.y; // 顶边
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