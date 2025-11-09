using System.Collections.Generic;
using UnityEngine;

public class PropGeneration : MonoBehaviour
{
    [System.Serializable]
    public class WeightedItem
    {
        public GameObject prefab;

        [Header("权重 & 节律")]
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int turnsCooldown = 0;

        [Header("解锁回合（该回合及之后才允许出现）")]
        [Min(0)] public int availableFromTurn = 0;

        [HideInInspector] public int lastSpawnTurn = -999999;
    }

    [Header("带权重的道具表（在元素里设置 availableFromTurn / turnsCooldown）")]
    public List<WeightedItem> weightedItems = new List<WeightedItem>();

    [Header("出现几率")]
    [Range(0f, 1f)] public float spawnChancePerWave = 1f;

    [Header("引用")]
    public Transform baseTransform;
    [Tooltip("仅用于与“积木（blocks）”的重叠检测")]
    public LayerMask solidLayers;
    public Transform itemsParent;

    [Header("移动范围限制（务必填写）")]
    public Transform leftBound;
    public Transform rightBound;
    public Transform spawnPointY;   // 仅用其 Y 来限制最高生成高度

    [Header("回合控制")]
    public int firstSpawnAfterRounds = 1;
    public int spawnEveryNRounds = 1;
    public int minItemsPerSpawn = 1;
    public int maxItemsPerSpawn = 1;

    [Header("位置权重（仅两类：平台内 / 平台外）")]
    [Range(0f, 1f)] public float weightInsideBase = 0.90f;
    [Range(0f, 1f)] public float weightOutsideBase = 0.10f;

    [Header("通用")]
    [Tooltip("与积木/道具的最小清空半径")]
    public float minClearRadius = 0.25f;
    public int maxTriesPerItem = 50;
    [Tooltip("平台内：相对平台顶面的高度随机范围（世界单位）")]
    public Vector2 insideAboveBaseRange = new Vector2(0.20f, 0.80f);

    [Header("高度限制")]
    [Tooltip("道具最高高度 = 生成高度 - 本值")]
    public float yTopMarginBelowSpawn = 0.5f;
    [Tooltip("道具生成的最低世界坐标Y；低于该值的候选点会被丢弃")]
    public float minSpawnHeight = -9999f;

    [Header("稳产设置（已取消兜底，不再强制）")]
    public bool forceSpawnWhenEligible = false;
    public bool debugLogs = false;

    [Header("防卡位额外判定（仅保留圆形避让）")]
    [Tooltip("圆形避让半径，用于远离带 BlockMark 的积木群")]
    public float extraBlockAvoidRadius = 0.6f;

    // —— 按你的要求：已移除“避免 HoverMover 横向移动带”的全部字段与逻辑 —— //

    [Header("与其他道具防重叠")]
    [Tooltip("道具自身的Layer，用于与已生成道具防重叠")]
    public LayerMask itemLayers;

    [Header("倒计时末段不再产生新道具")]
    public bool blockSpawnsInLastSeconds = true;
    public float noSpawnLastSeconds = 5f;
    [Tooltip("倒计时脚本（可不拖，Awake中会自动寻找）")]
    public CountdownTimer countdown;

    [Header("平台内 X 轴网格化（让中间也常出现离散点）")]
    [Tooltip("X 轴对齐步长；=0 关闭。例如设为 1，可得到 …,-3,-2,-1,0,1,2,3,… 之类的点")]
    public float insideSnapStep = 1f;

    // ===== 常量/状态 =====
    const float VIEWPORT_PADDING = 0.06f;

    int _currentTurnId = 0;
    readonly Dictionary<int, List<GameObject>> _activeByTurn = new();

    float MoveXMin => leftBound ? leftBound.position.x : float.NegativeInfinity;
    float MoveXMax => rightBound ? rightBound.position.x : float.PositiveInfinity;
    float MoveYCap => spawnPointY ? (spawnPointY.position.y - Mathf.Max(0f, yTopMarginBelowSpawn)) : float.PositiveInfinity;

    void Awake()
    {
        if (!countdown)
            countdown = FindObjectOfType<CountdownTimer>();
    }

    // ========== TurnManager 调用口 ==========
    public void OnTurnStart(int turnId)
    {
        _currentTurnId = turnId;

        if (blockSpawnsInLastSeconds && countdown != null && countdown.RemainingSeconds <= Mathf.Max(0f, noSpawnLastSeconds))
        {
            if (debugLogs) Debug.Log("[PropGen] blocked by countdown (last seconds)");
            return;
        }

        if (turnId < firstSpawnAfterRounds) return;
        int sinceFirst = turnId - firstSpawnAfterRounds;
        if (sinceFirst % Mathf.Max(1, spawnEveryNRounds) != 0) return;

        if (Random.value > Mathf.Clamp01(spawnChancePerWave)) return;

        int want = Mathf.Clamp(Random.Range(minItemsPerSpawn, maxItemsPerSpawn + 1), 1, 999);
        int made = SpawnWave(want);

        if (debugLogs) Debug.Log($"[PropGen] turn={turnId} want={want} made={made}");
    }

    public void OnTurnEnd(int turnId)
    {
        if (_activeByTurn.TryGetValue(turnId, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i]) Destroy(list[i]);
            _activeByTurn.Remove(turnId);
        }
    }

    // ========== 生成 ==========
    private int SpawnWave(int count)
    {
        int made = 0;
        for (int i = 0; i < count; i++)
            if (TrySpawnOne()) made++;
        return made;
    }

    private bool TrySpawnOne(bool forceFallback = false)
    {
        if (!baseTransform) return false;

        for (int t = 0; t < maxTriesPerItem; t++)
        {
            if (!TryPickPositionSimple(out var pos)) continue;

            if (IsUnsafeBecauseOfBlocks(pos)) continue;

            var prefab = PickPrefabWeighted(_currentTurnId);
            if (!prefab) continue;

            var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f),
                                 Quaternion.identity, itemsParent ? itemsParent : transform);

            Register(go);
            MaybeTriggerFirstAppearHint(go);
            return true;
        }

        if (debugLogs) Debug.Log("[PropGen] no spawn (sampling failed)");
        return false;
    }

    bool IsUnsafeBecauseOfBlocks(Vector2 pos)
    {
        var hits = Physics2D.OverlapCircleAll(pos, extraBlockAvoidRadius);
        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.GetComponentInParent<BlockMark>() != null)
                return true;
        }
        return false;
    }

    void Register(GameObject go)
    {
        if (!_activeByTurn.TryGetValue(_currentTurnId, out var list))
        {
            list = new List<GameObject>();
            _activeByTurn[_currentTurnId] = list;
        }
        list.Add(go);
    }

    // ========= 选位置（平台内 / 平台外） =========
    bool TryPickPositionSimple(out Vector2 pos)
    {
        pos = Vector2.zero;

        float wInside = Mathf.Max(0f, weightInsideBase);
        float wOutside = Mathf.Max(0f, weightOutsideBase);
        float sum = wInside + wOutside;
        if (sum <= 0f) wInside = 1f;
        float r = Random.value * (sum <= 0 ? 1f : sum);

        if (r <= wInside) pos = SampleInsideBase();
        else pos = SampleOutsideBase();

        if (!IsWithinMoveBounds(pos)) return false;
        if (!IsInsideCamera(pos)) return false;

        if (!IsClear(pos, minClearRadius)) return false;

        if (pos.y < minSpawnHeight) return false;

        if (!IsClearFromItems(pos, minClearRadius)) return false;

        return true;
    }

    Vector2 SampleInsideBase()
    {
        var rect = GetBaseRect();

        float xMin = Mathf.Max(rect.xMin, MoveXMin);
        float xMax = Mathf.Min(rect.xMax, MoveXMax);
        if (xMax <= xMin) { xMin = rect.center.x - 0.01f; xMax = rect.center.x + 0.01f; }

        float yMin = rect.yMax + Mathf.Min(insideAboveBaseRange.x, insideAboveBaseRange.y);
        float yMax = Mathf.Min(MoveYCap, rect.yMax + Mathf.Max(insideAboveBaseRange.x, insideAboveBaseRange.y));

        // 先在平台内均匀采样，再按步长网格对齐
        float x = Random.Range(xMin, xMax);
        if (insideSnapStep > 0f)
        {
            x = Mathf.Round(x / insideSnapStep) * insideSnapStep;
            x = Mathf.Clamp(x, xMin, xMax);
        }

        return new Vector2(x, Random.Range(yMin, yMax));
    }

    Vector2 SampleOutsideBase()
    {
        var baseRect = GetBaseRect();
        var camRect = GetCameraWorldRect(VIEWPORT_PADDING);

        float yMin = Mathf.Max(baseRect.yMax, camRect.yMin);
        float yMax = Mathf.Min(MoveYCap, camRect.yMax);
        float h = Mathf.Max(0f, yMax - yMin);

        float leftMinX = Mathf.Max(camRect.xMin, MoveXMin);
        float leftMaxX = Mathf.Min(baseRect.xMin, MoveXMax);

        float rightMinX = Mathf.Max(baseRect.xMax, MoveXMin);
        float rightMaxX = Mathf.Min(camRect.xMax, MoveXMax);

        bool hasLeft = leftMaxX > leftMinX && h > 0f;
        bool hasRight = rightMaxX > rightMinX && h > 0f;

        if (!hasLeft && !hasRight)
            return SampleInsideBase();

        float areaLeft = hasLeft ? (leftMaxX - leftMinX) * h : 0f;
        float areaRight = hasRight ? (rightMaxX - rightMinX) * h : 0f;
        float sum = areaLeft + areaRight;

        float pick = Random.value * sum;
        float x = (pick < areaLeft)
            ? Random.Range(leftMinX, leftMaxX)
            : Random.Range(rightMinX, rightMaxX);

        if (insideSnapStep > 0f)
        {
            x = Mathf.Round(x / insideSnapStep) * insideSnapStep;
        }

        return new Vector2(x, Random.Range(yMin, yMax));
    }

    // ========== 工具 ==========
    Rect GetBaseRect()
    {
        var bc2d = baseTransform ? baseTransform.GetComponent<BoxCollider2D>() : null;
        if (bc2d)
        {
            var c = (Vector2)baseTransform.TransformPoint(bc2d.offset);
            var s = Vector2.Scale(bc2d.size, baseTransform.lossyScale);
            return new Rect(c - s * 0.5f, s);
        }
        var r = baseTransform ? baseTransform.GetComponent<Renderer>() : null;
        if (r != null)
        {
            var b = r.bounds;
            return new Rect(new Vector2(b.min.x, b.min.y), new Vector2(b.size.x, b.size.y));
        }
        var p = baseTransform ? baseTransform.position : Vector3.zero;
        return new Rect(new Vector2(p.x - 1f, p.y - 0.5f), new Vector2(2f, 1f));
    }

    bool IsClear(Vector2 worldPos, float radius)
    {
        return Physics2D.OverlapCircle(worldPos, radius, solidLayers) == null;
    }

    bool IsClearFromItems(Vector2 worldPos, float radius)
    {
        if (itemLayers.value == 0) return true;
        return Physics2D.OverlapCircle(worldPos, radius, itemLayers) == null;
    }

    bool IsInsideCamera(Vector2 worldPos)
    {
        var cam = Camera.main;
        if (!cam) return true;
        var v = cam.WorldToViewportPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        return v.z >= 0f &&
               v.x > VIEWPORT_PADDING && v.x < 1f - VIEWPORT_PADDING &&
               v.y > VIEWPORT_PADDING && v.y < 1f - VIEWPORT_PADDING;
    }

    Rect GetCameraWorldRect(float padding01)
    {
        var cam = Camera.main;
        if (!cam) return new Rect(-1000f, -1000f, 2000f, 2000f);
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(padding01, padding01, 0f));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f - padding01, 1f - padding01, 0f));
        float xMin = Mathf.Min(bl.x, tr.x), xMax = Mathf.Max(bl.x, tr.x);
        float yMin = Mathf.Min(bl.y, tr.y), yMax = Mathf.Max(bl.y, tr.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    bool IsWithinMoveBounds(Vector2 p)
    {
        return p.x >= MoveXMin && p.x <= MoveXMax && p.y <= MoveYCap;
    }

    // ========== 选道具 ==========
    GameObject PickPrefabWeighted(int turnId)
    {
        var pool = new List<WeightedItem>();
        foreach (var wi in weightedItems)
        {
            if (!wi.prefab) continue;
            if (turnId < wi.availableFromTurn) continue;
            if (wi.turnsCooldown > 0 && (turnId - wi.lastSpawnTurn) <= wi.turnsCooldown) continue;
            if (wi.weight <= 0f) continue;
            pool.Add(wi);
        }
        if (pool.Count == 0) return null;

        float sum = 0f;
        for (int i = 0; i < pool.Count; i++) sum += Mathf.Max(0f, pool[i].weight);
        if (sum <= 0f) return null;

        float pick = Random.value * sum;
        float acc = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            acc += Mathf.Max(0f, pool[i].weight);
            if (pick <= acc)
            {
                pool[i].lastSpawnTurn = turnId;
                return pool[i].prefab;
            }
        }
        var last = pool[pool.Count - 1];
        last.lastSpawnTurn = turnId;
        return last.prefab;
    }

    void MaybeTriggerFirstAppearHint(GameObject go)
    {
        if (go.GetComponentInChildren<PropGlueHint>(true) != null) { TurnManager.Instance?.TriggerGlueAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWeightHint>(true) != null) { TurnManager.Instance?.TriggerWeightAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWallHint>(true) != null) { TurnManager.Instance?.TriggerBrickAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropIceHint>(true) != null) { TurnManager.Instance?.TriggerIceAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWindHint>(true) != null) { TurnManager.Instance?.TriggerWindAppearIfNeeded(); return; }
    }
}