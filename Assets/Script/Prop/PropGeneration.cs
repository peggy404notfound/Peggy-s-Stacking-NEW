using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PropGeneration : MonoBehaviour
{
    [System.Serializable]
    public class WeightedItem
    {
        public GameObject prefab;

        [Header("Ȩ�� & ����")]
        [Min(0f)] public float weight = 1f;       // ���и���Ȩ��
        [Min(0)] public int turnsCooldown = 0;    // ͬһ�ֵ������γ���֮�����С�غϼ�������ظ���

        [Header("�����غϣ��ûغϼ�֮���������֣�")]
        [Min(0)] public int availableFromTurn = 0; // ������������ 5 ��ʾ��5�غϼ�֮��ſ��ܳ���

        [HideInInspector] public int lastSpawnTurn = -999999;
    }

    [Header("��Ȩ�صĵ��߱���Ԫ�������� availableFromTurn / turnsCooldown��")]
    public List<WeightedItem> weightedItems = new List<WeightedItem>();

    [Header("���ּ���")]
    [Range(0f, 1f)] public float spawnChancePerWave = 1f;

    [Header("����")]
    public Transform baseTransform;
    public LayerMask solidLayers;      // �����ڡ����ص�����⣨������� Base/Stack���ų� Prop �Լ���
    public Transform itemsParent;

    [Header("�ƶ���Χ���ƣ������д��")]
    public Transform leftBound;
    public Transform rightBound;
    public Transform spawnPointY;      // ���� Y ������������ɸ߶�

    [Header("�غϿ���")]
    public int firstSpawnAfterRounds = 1;   // �ڼ��غϿ�ʼˢ��ȫ�֣�
    public int spawnEveryNRounds = 1;       // ÿ N �غ�ˢһ��
    public int minItemsPerSpawn = 1;
    public int maxItemsPerSpawn = 1;

    [Header("λ��Ȩ�أ������ࣺƽ̨�� / ƽ̨�⣩")]
    [Tooltip("������ƽ̨�Ϸ���Base ��ˮƽ��Χ֮�ڣ����ɵĸ���")]
    [Range(0f, 1f)] public float weightInsideBase = 0.70f;
    [Tooltip("������ƽ̨ˮƽ��Χ֮�⣨��Ļ�����ࣩ���ɵĸ���")]
    [Range(0f, 1f)] public float weightOutsideBase = 0.30f;

    [Header("ͨ��")]
    public float minClearRadius = 0.25f;             // ���ɵ��� Base/Stack ��Ӳ�����С��հ뾶
    public int maxTriesPerItem = 50;
    public Vector2 insideAboveBaseRange = new Vector2(0.20f, 0.80f); // ƽ̨���ڡ�ʱ���� Base ���������߶ȷ�Χ�����絥λ��

    [Header("�߶�����")]
    [Tooltip("������߸߶� = ���ɸ߶� - ��ֵ")]
    public float yTopMarginBelowSpawn = 0.5f;

    [Header("�Ȳ�����")]
    public bool forceSpawnWhenEligible = true; // ���С��ûغ�Ӧˢ��ʱ����ʧ���򶵵�����ˢ 1 ��
    public bool debugLogs = false;

    [Header("����λ�����ж�")]
    [Tooltip("ˢ����ʱ�ܿ���ľ�İ뾶���ף�������ֵ���ֱ����")]
    public float extraBlockAvoidRadius = 0.6f;

    [Tooltip("�Ӻ�ѡ������Ԥ��һ����ȫ�����߶ȣ����������ľ��ˢ���ף�")]
    public float verticalSafeBand = 0.8f;

    [Tooltip("�ж�Ϊ���������䡱���ٶ���ֵ����/�룬Y< -threshold��")]
    public float fallingSpeedThreshold = 0.2f;

    // ===== ����/״̬ =====
    const float VIEWPORT_PADDING = 0.06f;

    int _currentTurnId = 0;
    readonly Dictionary<int, List<GameObject>> _activeByTurn = new();

    // ��ݷ���
    float MoveXMin => leftBound ? leftBound.position.x : float.NegativeInfinity;
    float MoveXMax => rightBound ? rightBound.position.x : float.PositiveInfinity;
    float MoveYCap => spawnPointY ? (spawnPointY.position.y - Mathf.Max(0f, yTopMarginBelowSpawn)) : float.PositiveInfinity;

    // ========== TurnManager ���ÿ� ==========
    public void OnTurnStart(int turnId)
    {
        _currentTurnId = turnId;

        // �غϽ��ɣ�ȫ�֣�
        if (turnId < firstSpawnAfterRounds) return;
        int sinceFirst = turnId - firstSpawnAfterRounds;
        if (sinceFirst % Mathf.Max(1, spawnEveryNRounds) != 0) return;

        // ���ʴ���
        if (Random.value > Mathf.Clamp01(spawnChancePerWave)) return;

        int want = Mathf.Clamp(Random.Range(minItemsPerSpawn, maxItemsPerSpawn + 1), 1, 999);
        int made = SpawnWave(want);

        // ���ף�����ˢ 1 ��
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

        // 1) �������������ֻ�ڡ�ƽ̨��/ƽ̨�⡱������ѡ��
        for (int t = 0; t < maxTriesPerItem; t++)
        {
            if (!TryPickPositionSimple(out var pos)) continue;

            // [FIX #1] ���ܱ��ã����ľ���� �� ���Ϸ���ȫ�����������ľ �� �����õ�
            if (IsUnsafeBecauseOfBlocks(pos)) continue;

            var prefab = PickPrefabWeighted(_currentTurnId);
            if (!prefab) continue; // �����ˣ���������

            var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f),
                                 Quaternion.identity, itemsParent ? itemsParent : transform);

            // [FIX #2] ���� ���ֱ����ڣ�Э�̽���/�ָ� Collider��
            Register(go);
            MaybeTriggerFirstAppearHint(go);
            return true;
        }

        // 2) ����
        if (forceFallback || forceSpawnWhenEligible)
        {
            if (TryFallbackPosition(out var pos))
            {
                var prefab = PickPrefabWeighted(_currentTurnId);
                if (prefab)
                {
                    var go = Instantiate(prefab, new Vector3(pos.x, pos.y, 0f),
                                         Quaternion.identity, itemsParent ? itemsParent : transform);

                    // [FIX #2 - fallback ͬ���ӱ�����]
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
        // 1) Բ����ã������� BlockMark ̫���Ͳ�ˢ
        var hits = Physics2D.OverlapCircleAll(pos, extraBlockAvoidRadius);
        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.GetComponentInParent<BlockMark>() != null)
                return true;
        }

        // 2) ����ȫ����pos ���� verticalSafeBand �߶��ڣ��С��������¡��Ļ�ľ�Ͳ�ˢ
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
                return true; // �������´���������
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

    // ========= ѡλ�ã��򻯰棬����ƽ̨�� / ƽ̨�⡱�� =========
    bool TryPickPositionSimple(out Vector2 pos)
    {
        pos = Vector2.zero;

        // ��Ȩ�ؾ������⣨ֻ���ࣩ
        float wInside = Mathf.Max(0f, weightInsideBase);
        float wOutside = Mathf.Max(0f, weightOutsideBase);
        float sum = wInside + wOutside;
        if (sum <= 0f) wInside = 1f; // ��ֹȫ 0 ���¿���
        float r = Random.value * (sum <= 0 ? 1f : sum);

        if (r <= wInside) pos = SampleInsideBase();
        else pos = SampleOutsideBase();

        // ����Լ��
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

        // ����Χ��Base �� �� �����Ͻ�
        float yMin = Mathf.Max(baseRect.yMax, camRect.yMin);
        float yMax = Mathf.Min(MoveYCap, camRect.yMax);
        float h = Mathf.Max(0f, yMax - yMin);

        // ���������Ļ�� & ���� Base ˮƽ�غϣ�
        float leftMinX = Mathf.Max(camRect.xMin, MoveXMin);
        float leftMaxX = Mathf.Min(baseRect.xMin, MoveXMax);

        // �Ҳ�����
        float rightMinX = Mathf.Max(baseRect.xMax, MoveXMin);
        float rightMaxX = Mathf.Min(camRect.xMax, MoveXMax);

        bool hasLeft = leftMaxX > leftMinX && h > 0f;
        bool hasRight = rightMaxX > rightMinX && h > 0f;

        if (!hasLeft && !hasRight)
            return SampleInsideBase(); // û�����ռ����˻ء��ڲࡱ

        // ���������������Ҳ�
        float areaLeft = hasLeft ? (leftMaxX - leftMinX) * h : 0f;
        float areaRight = hasRight ? (rightMaxX - rightMinX) * h : 0f;
        float sum = areaLeft + areaRight;

        float pick = Random.value * sum;
        if (pick < areaLeft)
            return new Vector2(Random.Range(leftMinX, leftMaxX), Random.Range(yMin, yMax));
        else
            return new Vector2(Random.Range(rightMinX, rightMaxX), Random.Range(yMin, yMax));
    }

    // �D�D fallback�����׷�һ����ƽ̨������ƫ�ϣ� �D�D
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

    // ========== ѡ���ߣ����롰�����غϡ����ˣ� ==========
    GameObject PickPrefabWeighted(int turnId)
    {
        var pool = new List<WeightedItem>();
        foreach (var wi in weightedItems)
        {
            if (!wi.prefab) continue;

            // �����غ����ƣ��ﵽ availableFromTurn �ſɽ����ѡ
            if (turnId < wi.availableFromTurn) continue;

            // ��ȴ������ͬһ��Ʒ����Ƶ��
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

    // ========== �״γ�����ʾ��������ԭ�߼��� ==========
    void MaybeTriggerFirstAppearHint(GameObject go)
    {
        if (go.GetComponentInChildren<PropGlueHint>(true) != null) { TurnManager.Instance?.TriggerGlueAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWeightHint>(true) != null) { TurnManager.Instance?.TriggerWeightAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWallHint>(true) != null) { TurnManager.Instance?.TriggerBrickAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropIceHint>(true) != null) { TurnManager.Instance?.TriggerIceAppearIfNeeded(); return; }
        if (go.GetComponentInChildren<PropWindHint>(true) != null) { TurnManager.Instance?.TriggerWindAppearIfNeeded(); return; }
    }
}