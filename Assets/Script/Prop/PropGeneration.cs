using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PropGeneration : MonoBehaviour
{
    [System.Serializable]
    public class WeightedItem
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int turnsCooldown = 0;
        [HideInInspector] public int lastSpawnTurn = -999999;
    }

    [Header("��Ȩ�صĵ��߱�")]
    public List<WeightedItem> weightedItems = new List<WeightedItem>();

    [Header("���ּ���")]
    [Range(0f, 1f)] public float spawnChancePerWave = 1f;

    [Header("����")]
    public Transform baseTransform;
    public LayerMask stackLayers;   // ��/��ľ�����ڡ���Ե��������
    public LayerMask solidLayers;   // ���ص������飺���� Base/Stack�����ų� Prop �Լ���
    public Transform itemsParent;

    [Header("�ƶ���Χ���ƣ������д��")]
    public Transform leftBound;
    public Transform rightBound;
    public Transform spawnPointY;   // ֻ�� Y

    [Header("�غϿ���")]
    public int firstSpawnAfterRounds = 1;
    public int spawnEveryNRounds = 1;
    public int minItemsPerSpawn = 1;
    public int maxItemsPerSpawn = 1;

    [Header("λ��Ȩ�أ�������=1-ǰ���")]
    [Range(0f, 1f)] public float weightInsideBase = 0.70f;
    [Range(0f, 1f)] public float weightOutsideBase = 0.15f;

    [Header("ͨ��")]
    public float minClearRadius = 0.25f;
    public int maxTriesPerItem = 80;               // �� ��߳��Դ���
    public Vector2 insideAboveBaseRange = new Vector2(0.20f, 0.80f);

    [Header("�߶�����")]
    [Tooltip("������߸߶� = ���ɸ߶� - ��ֵ")]
    public float yTopMarginBelowSpawn = 0.5f;

    [Header("�Ȳ�����")]
    public bool forceSpawnWhenEligible = true;        // ���С��ûغ�Ӧˢ��ʱǿ������ˢ 1 ��
    public bool debugLogs = false;                    // ��Ҫ�Ų�ʱ�ٹ���

    const float VIEWPORT_PADDING = 0.06f;
    const float COLUMN_EDGE_OUT_OFFSET = 0.30f;
    const float EDGE_VERTICAL_JITTER = 0.18f;

    int _currentTurnId = 0;
    readonly Dictionary<int, List<GameObject>> _activeByTurn = new();

    // ���� ��ݷ��� ���� //
    float MoveXMin => leftBound ? leftBound.position.x : float.NegativeInfinity;
    float MoveXMax => rightBound ? rightBound.position.x : float.PositiveInfinity;
    float MoveYCap => spawnPointY ? (spawnPointY.position.y - Mathf.Max(0f, yTopMarginBelowSpawn)) : float.PositiveInfinity;

    // ========== TurnManager ���� ==========
    public void OnTurnStart(int turnId)
    {
        _currentTurnId = turnId;

        // �غϽ���
        if (turnId < firstSpawnAfterRounds) return;
        int sinceFirst = turnId - firstSpawnAfterRounds;
        if (sinceFirst % Mathf.Max(1, spawnEveryNRounds) != 0) return;

        // ����
        if (Random.value > Mathf.Clamp01(spawnChancePerWave)) return;

        int want = Mathf.Clamp(Random.Range(minItemsPerSpawn, maxItemsPerSpawn + 1), 1, 999);
        int made = SpawnWave(want);

        if (forceSpawnWhenEligible && made == 0)
        {
            // ���ף���֤����ˢһ��
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

    // ========== ���� ==========
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

        // 1) �����������
        for (int t = 0; t < maxTriesPerItem; t++)
        {
            if (!TryPickPosition(out var pos)) continue;

            var prefab = PickPrefabWeighted(_currentTurnId);
            if (!prefab) continue; // �����ˣ���������

            var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity, itemsParent ? itemsParent : transform);
            Register(go);
            MaybeTriggerFirstAppearHint(go);
            return true;
        }

        // 2) ���ף�forceFallback ��ȫ�ֿ��أ�
        if (forceFallback || forceSpawnWhenEligible)
        {
            if (TryFallbackPosition(out var pos))
            {
                var prefab = PickPrefabWeighted(_currentTurnId);
                if (prefab)
                {
                    var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity, itemsParent ? itemsParent : transform);
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

    void Register(GameObject go)
    {
        if (!_activeByTurn.TryGetValue(_currentTurnId, out var list))
        {
            list = new List<GameObject>();
            _activeByTurn[_currentTurnId] = list;
        }
        list.Add(go);
    }

    bool TryPickPosition(out Vector2 pos)
    {
        // λ�ò�����Inside / Outside / ColumnEdge��
        float r = Random.value;
        if (r < weightInsideBase) pos = SampleInsideBase();
        else if (r < weightInsideBase + weightOutsideBase) pos = SampleOutsideBase();
        else if (!TrySampleOnTowerColumnEdge(out pos)) pos = SampleInsideBase();

        // Լ���ж�
        if (!IsWithinMoveBounds(pos)) return false;
        if (!IsInsideCamera(pos)) return false;

        // ����ж���ֻ�� solidLayers ���أ����鲻Ҫ�ѡ�Prop�������Ž���
        if (!IsClear(pos, minClearRadius)) return false;

        return true;
    }

    // ���� fallback�����׷�һ�� ���� //
    bool TryFallbackPosition(out Vector2 pos)
    {
        var rect = GetBaseRect();

        float xMin = Mathf.Max(rect.xMin, MoveXMin);
        float xMax = Mathf.Min(rect.xMax, MoveXMax);
        float yMin = rect.yMax + Mathf.Min(insideAboveBaseRange.x, insideAboveBaseRange.y);
        float yMax = Mathf.Min(MoveYCap, rect.yMax + Mathf.Max(insideAboveBaseRange.x, insideAboveBaseRange.y));

        // �ȳ���һ���ϡ��ȡ��ĵ㣨��������ƫ�ϣ�
        pos = new Vector2(Mathf.Lerp(xMin, xMax, 0.5f), Mathf.Lerp(yMin, yMax, 0.7f));

        // ֻ�ܿ� Base/Stack ��Ӳ��ͻ���� Prop �� solidLayers ���ų���
        if (!IsClearHard(pos, minClearRadius * 0.7f))
            pos = new Vector2(Mathf.Lerp(xMin, xMax, 0.5f), Mathf.Lerp(yMin, yMax, 0.9f));

        // �Բ��о���ɹ���������������΢��/��΢�ص���
        return true;
    }

    // ========== ��Ȩ��ѡ���� ==========
    private GameObject PickPrefabWeighted(int turnId)
    {
        var pool = new List<WeightedItem>();
        foreach (var wi in weightedItems)
        {
            if (!wi.prefab) continue;
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

    // ========== ���� ==========
    private Vector2 SampleInsideBase()
    {
        var rect = GetBaseRect();
        float xMin = Mathf.Max(rect.xMin, MoveXMin);
        float xMax = Mathf.Min(rect.xMax, MoveXMax);
        if (xMax <= xMin) { xMin = rect.center.x - 0.01f; xMax = rect.center.x + 0.01f; }

        float yMin = rect.yMax + Mathf.Min(insideAboveBaseRange.x, insideAboveBaseRange.y);
        float yMax = Mathf.Min(MoveYCap, rect.yMax + Mathf.Max(insideAboveBaseRange.x, insideAboveBaseRange.y));
        return new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
    }

    private Vector2 SampleOutsideBase()
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

        float areaLeft = hasLeft ? (leftMaxX - leftMinX) * h : 0f;
        float areaRight = hasRight ? (rightMaxX - rightMinX) * h : 0f;
        float sum = areaLeft + areaRight;
        if (sum <= 0f) return SampleInsideBase();

        float pick = Random.value * sum;
        if (pick < areaLeft) return new Vector2(Random.Range(leftMinX, leftMaxX), Random.Range(yMin, yMax));
        else return new Vector2(Random.Range(rightMinX, rightMaxX), Random.Range(yMin, yMax));
    }

    private bool TrySampleOnTowerColumnEdge(out Vector2 pos)
    {
        pos = Vector2.zero;
        var blocks = Physics2D.OverlapCircleAll(baseTransform.position, 60f, stackLayers);
        if (blocks == null || blocks.Length == 0) return false;

        float medianWidth = 1f;
        var widths = new List<float>();
        foreach (var b in blocks) widths.Add(b.bounds.size.x);
        widths.Sort();
        if (widths.Count > 0) medianWidth = widths[widths.Count / 2] * 1.05f;

        var columns = new Dictionary<int, List<Collider2D>>();
        foreach (var c in blocks)
        {
            int key = Mathf.RoundToInt(c.bounds.center.x / medianWidth);
            if (!columns.TryGetValue(key, out var list)) { list = new List<Collider2D>(); columns[key] = list; }
            list.Add(c);
        }
        if (columns.Count == 0) return false;

        var col = columns.ElementAt(Random.Range(0, columns.Count)).Value;
        float colMinX = float.MaxValue, colMaxX = float.MinValue;
        foreach (var c in col)
        {
            colMinX = Mathf.Min(colMinX, c.bounds.min.x);
            colMaxX = Mathf.Max(colMaxX, c.bounds.max.x);
        }

        var pick = col[Random.Range(0, col.Count)];
        var bnd = pick.bounds;
        float y = Mathf.Clamp(
            Random.Range(bnd.min.y, bnd.max.y) + Random.Range(-EDGE_VERTICAL_JITTER, EDGE_VERTICAL_JITTER),
            bnd.min.y - EDGE_VERTICAL_JITTER, bnd.max.y + EDGE_VERTICAL_JITTER
        );
        y = Mathf.Min(y, MoveYCap);

        bool rightSide = Random.value > 0.5f;
        float x = rightSide ? (colMaxX + COLUMN_EDGE_OUT_OFFSET) : (colMinX - COLUMN_EDGE_OUT_OFFSET);
        if (x < MoveXMin || x > MoveXMax) return false;

        pos = new Vector2(x, y);
        return true;
    }

    // ========== ���� ==========
    private Rect GetBaseRect()
    {
        var bc2d = baseTransform.GetComponent<BoxCollider2D>();
        if (bc2d)
        {
            var c = (Vector2)baseTransform.TransformPoint(bc2d.offset);
            var s = Vector2.Scale(bc2d.size, baseTransform.lossyScale);
            return new Rect(c - s * 0.5f, s);
        }
        var r = baseTransform.GetComponent<Renderer>();
        if (r != null)
        {
            var b = r.bounds;
            return new Rect(new Vector2(b.min.x, b.min.y), new Vector2(b.size.x, b.size.y));
        }
        var p = baseTransform.position;
        return new Rect(new Vector2(p.x - 1f, p.y - 0.5f), new Vector2(2f, 1f));
    }

    private bool IsClear(Vector2 worldPos, float radius)
    {
        return Physics2D.OverlapCircle(worldPos, radius, solidLayers) == null;
    }

    // ��Ӳ���á���ֻ�ܿ� Base/Stack ��Ӳ�壻��� Prop �Լ��� Layer ��Ҫ�ŵ���� Mask��
    private bool IsClearHard(Vector2 worldPos, float radius)
    {
        return Physics2D.OverlapCircle(worldPos, radius, solidLayers) == null;
    }

    private bool IsInsideCamera(Vector2 worldPos)
    {
        var cam = Camera.main;
        if (!cam) return true;
        var v = cam.WorldToViewportPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        return v.z >= 0f &&
               v.x > VIEWPORT_PADDING && v.x < 1f - VIEWPORT_PADDING &&
               v.y > VIEWPORT_PADDING && v.y < 1f - VIEWPORT_PADDING;
    }

    private Rect GetCameraWorldRect(float padding01)
    {
        var cam = Camera.main;
        if (!cam) return new Rect(-1000f, -1000f, 2000f, 2000f);
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(padding01, padding01, 0f));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f - padding01, 1f - padding01, 0f));
        float xMin = Mathf.Min(bl.x, tr.x), xMax = Mathf.Max(bl.x, tr.x);
        float yMin = Mathf.Min(bl.y, tr.y), yMax = Mathf.Max(bl.y, tr.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private bool IsWithinMoveBounds(Vector2 p)
    {
        return p.x >= MoveXMin && p.x <= MoveXMax && p.y <= MoveYCap;
    }

    // ========== ���ݱ�Ǵ������״γ��֡��̳� ==========
    private void MaybeTriggerFirstAppearHint(GameObject go)
    {
        if (go.GetComponentInChildren<PropGlueHint>(true) != null) { TurnManager.Instance?.TriggerGlueAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWeightHint>(true) != null) { TurnManager.Instance?.TriggerWeightAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWallHint>(true) != null) { TurnManager.Instance?.TriggerBrickAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropIceHint>(true) != null) { TurnManager.Instance?.TriggerIceAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWindHint>(true) != null) { TurnManager.Instance?.TriggerWindAppearIfNeeded(); return; }
    }
}