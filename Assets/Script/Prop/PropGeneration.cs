using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PropGeneration : MonoBehaviour
{
    [System.Serializable]
    public class WeightedItem
    {
        public GameObject prefab;

        [Header("权重 & 节律")]
        [Min(0f)] public float weight = 1f;       // 抽中概率权重
        [Min(0)] public int turnsCooldown = 0;    // 同一种道具两次出现之间的最小回合间隔（防重复）

        [Header("解锁回合（该回合及之后才允许出现）")]
        [Min(0)] public int availableFromTurn = 0; // 新增：例如填 5 表示第5回合及之后才可能出现

        [HideInInspector] public int lastSpawnTurn = -999999;
    }

    [Header("带权重的道具表（在元素里设置 availableFromTurn / turnsCooldown）")]
    public List<WeightedItem> weightedItems = new List<WeightedItem>();

    [Header("出现几率")]
    [Range(0f, 1f)] public float spawnChancePerWave = 1f;

    [Header("引用")]
    public Transform baseTransform;
    public LayerMask solidLayers;      // 仅用于“防重叠”检测（建议包含 Base/Stack，排除 Prop 自己）
    public Transform itemsParent;

    [Header("移动范围限制（务必填写）")]
    public Transform leftBound;
    public Transform rightBound;
    public Transform spawnPointY;      // 仅用 Y 来限制最高生成高度

    [Header("回合控制")]
    public int firstSpawnAfterRounds = 1;   // 第几回合开始刷（全局）
    public int spawnEveryNRounds = 1;       // 每 N 回合刷一次
    public int minItemsPerSpawn = 1;
    public int maxItemsPerSpawn = 1;

    [Header("位置权重（仅两类：平台内 / 平台外）")]
    [Tooltip("道具在平台上方（Base 的水平范围之内）生成的概率")]
    [Range(0f, 1f)] public float weightInsideBase = 0.70f;
    [Tooltip("道具在平台水平范围之外（屏幕内两侧）生成的概率")]
    [Range(0f, 1f)] public float weightOutsideBase = 0.30f;

    [Header("通用")]
    public float minClearRadius = 0.25f;             // 生成点与 Base/Stack 等硬体的最小清空半径
    public int maxTriesPerItem = 50;
    public Vector2 insideAboveBaseRange = new Vector2(0.20f, 0.80f); // 平台“内”时，距 Base 顶面的随机高度范围（世界单位）

    [Header("高度限制")]
    [Tooltip("道具最高高度 = 生成高度 - 本值")]
    public float yTopMarginBelowSpawn = 0.5f;

    [Header("稳产设置")]
    public bool forceSpawnWhenEligible = true; // 命中“该回合应刷”时，若失败则兜底至少刷 1 个
    public bool debugLogs = false;

    [Header("防卡位额外判定")]
    [Tooltip("刷道具时避开积木的半径（米）。建议≈道具直径）")]
    public float extraBlockAvoidRadius = 0.6f;

    [Tooltip("从候选点往上预留一条安全竖带高度，内有下落积木则不刷（米）")]
    public float verticalSafeBand = 0.8f;

    [Tooltip("判定为“在向下落”的速度阈值（米/秒，Y< -threshold）")]
    public float fallingSpeedThreshold = 0.2f;

    // ===== 常量/状态 =====
    const float VIEWPORT_PADDING = 0.06f;

    int _currentTurnId = 0;
    readonly Dictionary<int, List<GameObject>> _activeByTurn = new();

    // 便捷访问
    float MoveXMin => leftBound ? leftBound.position.x : float.NegativeInfinity;
    float MoveXMax => rightBound ? rightBound.position.x : float.PositiveInfinity;
    float MoveYCap => spawnPointY ? (spawnPointY.position.y - Mathf.Max(0f, yTopMarginBelowSpawn)) : float.PositiveInfinity;

    // ========== TurnManager 调用口 ==========
    public void OnTurnStart(int turnId)
    {
        _currentTurnId = turnId;

        // 回合节律（全局）
        if (turnId < firstSpawnAfterRounds) return;
        int sinceFirst = turnId - firstSpawnAfterRounds;
        if (sinceFirst % Mathf.Max(1, spawnEveryNRounds) != 0) return;

        // 概率触发
        if (Random.value > Mathf.Clamp01(spawnChancePerWave)) return;

        int want = Mathf.Clamp(Random.Range(minItemsPerSpawn, maxItemsPerSpawn + 1), 1, 999);
        int made = SpawnWave(want);

        // 兜底：至少刷 1 个
        if (forceSpawnWhenEligible && made == 0)
        {
            if (TrySpawnOne(forceFallback: true)) made = 1;
        }

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

        // 1) 正常随机采样（只在“平台内/平台外”两类里选）
        for (int t = 0; t < maxTriesPerItem; t++)
        {
            if (!TryPickPositionSimple(out var pos)) continue;

            // [FIX #1] 智能避让：离积木过近 或 其上方安全带内有下落积木 → 放弃该点
            if (IsUnsafeBecauseOfBlocks(pos)) continue;

            var prefab = PickPrefabWeighted(_currentTurnId);
            if (!prefab) continue; // 不早退，继续尝试

            var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f),
                                 Quaternion.identity, itemsParent ? itemsParent : transform);

            // [FIX #2] —— 显现保护期（协程禁用/恢复 Collider）
            Register(go);
            MaybeTriggerFirstAppearHint(go);
            return true;
        }

        // 2) 兜底
        if (forceFallback || forceSpawnWhenEligible)
        {
            if (TryFallbackPosition(out var pos))
            {
                var prefab = PickPrefabWeighted(_currentTurnId);
                if (prefab)
                {
                    var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f),
                                         Quaternion.identity, itemsParent ? itemsParent : transform);

                    // [FIX #2 - fallback 同样加保护期]
                    Register(go);
                    MaybeTriggerFirstAppearHint(go);
                    if (debugLogs) Debug.Log("[PropGen] fallback spawn");
                    return true;
                }
            }
        }

        if (debugLogs) Debug.Log("[PropGen] no spawn (sampling failed)");
        return false;
    }

    bool IsUnsafeBecauseOfBlocks(Vector2 pos)
    {
        // 1) 圆域避让：离任意 BlockMark 太近就不刷
        var hits = Physics2D.OverlapCircleAll(pos, extraBlockAvoidRadius);
        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.GetComponentInParent<BlockMark>() != null)
                return true;
        }

        // 2) 竖向安全带：pos 往上 verticalSafeBand 高度内，有“正在向下”的积木就不刷
        Vector2 boxCenter = pos + Vector2.up * (verticalSafeBand * 0.5f);
        Vector2 boxSize = new Vector2(extraBlockAvoidRadius * 2f, verticalSafeBand);
        var area = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f);
        foreach (var a in area)
        {
            if (!a) continue;
            var mark = a.GetComponentInParent<BlockMark>();
            if (!mark) continue;

            var rb = mark.GetComponent<Rigidbody2D>();
            if (rb && rb.velocity.y < -Mathf.Abs(fallingSpeedThreshold))
                return true; // 正在向下穿过该竖带
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

    // ========= 选位置（简化版，仅“平台内 / 平台外”） =========
    bool TryPickPositionSimple(out Vector2 pos)
    {
        pos = Vector2.zero;

        // 按权重决定内外（只两类）
        float wInside = Mathf.Max(0f, weightInsideBase);
        float wOutside = Mathf.Max(0f, weightOutsideBase);
        float sum = wInside + wOutside;
        if (sum <= 0f) wInside = 1f; // 防止全 0 导致卡死
        float r = Random.value * (sum <= 0 ? 1f : sum);

        if (r <= wInside) pos = SampleInsideBase();
        else pos = SampleOutsideBase();

        // 基本约束
        if (!IsWithinMoveBounds(pos)) return false;
        if (!IsInsideCamera(pos)) return false;
        if (!IsClear(pos, minClearRadius)) return false;

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

        return new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
    }

    Vector2 SampleOutsideBase()
    {
        var baseRect = GetBaseRect();
        var camRect = GetCameraWorldRect(VIEWPORT_PADDING);

        // 纵向范围：Base 顶 到 可用上界
        float yMin = Mathf.Max(baseRect.yMax, camRect.yMin);
        float yMax = Mathf.Min(MoveYCap, camRect.yMax);
        float h = Mathf.Max(0f, yMax - yMin);

        // 左侧区域（屏幕内 & 不与 Base 水平重合）
        float leftMinX = Mathf.Max(camRect.xMin, MoveXMin);
        float leftMaxX = Mathf.Min(baseRect.xMin, MoveXMax);

        // 右侧区域
        float rightMinX = Mathf.Max(baseRect.xMax, MoveXMin);
        float rightMaxX = Mathf.Min(camRect.xMax, MoveXMax);

        bool hasLeft = leftMaxX > leftMinX && h > 0f;
        bool hasRight = rightMaxX > rightMinX && h > 0f;

        if (!hasLeft && !hasRight)
            return SampleInsideBase(); // 没有外侧空间则退回“内侧”

        // 面积按比例随机左右侧
        float areaLeft = hasLeft ? (leftMaxX - leftMinX) * h : 0f;
        float areaRight = hasRight ? (rightMaxX - rightMinX) * h : 0f;
        float sum = areaLeft + areaRight;

        float pick = Random.value * sum;
        if (pick < areaLeft)
            return new Vector2(Random.Range(leftMinX, leftMaxX), Random.Range(yMin, yMax));
        else
            return new Vector2(Random.Range(rightMinX, rightMaxX), Random.Range(yMin, yMax));
    }

    // ―― fallback：保底放一个（平台内中央偏上） ――
    bool TryFallbackPosition(out Vector2 pos)
    {
        var rect = GetBaseRect();

        float xMin = Mathf.Max(rect.xMin, MoveXMin);
        float xMax = Mathf.Min(rect.xMax, MoveXMax);
        float yMin = rect.yMax + Mathf.Min(insideAboveBaseRange.x, insideAboveBaseRange.y);
        float yMax = Mathf.Min(MoveYCap, rect.yMax + Mathf.Max(insideAboveBaseRange.x, insideAboveBaseRange.y));

        pos = new Vector2(Mathf.Lerp(xMin, xMax, 0.5f), Mathf.Lerp(yMin, yMax, 0.75f));
        if (!IsClear(pos, minClearRadius * 0.7f))
        {
            pos = new Vector2(Mathf.Lerp(xMin, xMax, 0.5f), Mathf.Lerp(yMin, yMax, 0.9f));
        }
        return true;
    }

    // ========== 选道具（加入“解锁回合”过滤） ==========
    GameObject PickPrefabWeighted(int turnId)
    {
        var pool = new List<WeightedItem>();
        foreach (var wi in weightedItems)
        {
            if (!wi.prefab) continue;

            // 解锁回合限制：达到 availableFromTurn 才可进入候选
            if (turnId < wi.availableFromTurn) continue;

            // 冷却：避免同一物品过于频繁
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

    // ========== 首次出现提示（保留你原逻辑） ==========
    void MaybeTriggerFirstAppearHint(GameObject go)
    {
        if (go.GetComponentInChildren<PropGlueHint>(true) != null) { TurnManager.Instance?.TriggerGlueAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWeightHint>(true) != null) { TurnManager.Instance?.TriggerWeightAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWallHint>(true) != null) { TurnManager.Instance?.TriggerBrickAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropIceHint>(true) != null) { TurnManager.Instance?.TriggerIceAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWindHint>(true) != null) { TurnManager.Instance?.TriggerWindAppearIfNeeded(); return; }
    }
}