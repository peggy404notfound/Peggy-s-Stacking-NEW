using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class CameraFollowTopY : MonoBehaviour
{
    [Header("Spawning / baseline")]
    public Transform spawnPoint;        // ͬ���������Ǹ�
    public float safeOffset = 2f;       // ��������һ��
    public Collider2D baseCollider;     // �ػ� Collider2D�����ڡ��ױ����ס���

    [Header("Top constraints")]
    public float spawnTopBuffer = 1.5f; // spawnPoint ����Ļ������С����
    public float topMargin = 0f;        // ����ѡ��������������

    [Header("Follow speed")]
    public float upSmooth = 5f;         // ���ϸ���ƽ����
    public float downSmooth = 12f;      // ���»���ƽ���ȣ�����ʱ���죩

    [Header("Lightweight top scan (for collapse)")]
    public LayerMask stackLayers;       // ֻ��ѡ�����顱�Ĳ㣨��Ҫ�� Base��
    public float scanRadius = 100f;     // ɨ��뾶
    public Transform scanCenter;        // �����������Լ�

    private Camera cam;
    private float baseBottomY;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        if (!baseCollider)
        {
            Debug.LogError("[CameraFollowTopY] Please assign baseCollider.");
            enabled = false;
            return;
        }

        baseBottomY = baseCollider.bounds.min.y;

        // ���֣��ױ�����
        Vector3 p = transform.position;
        p.y = baseBottomY + cam.orthographicSize;
        transform.position = p;

        if (!scanCenter) scanCenter = transform;
    }

    void LateUpdate()
    {
        if (!spawnPoint) return;

        // 1) ʵʱ��ȡ��ʵ��������������ʱ���ͣ�
        float actualTopY = GetActualTopY();

        // 2) ����Լ�� -> ���Ŀ���������Y
        //   a) ��Ļ�ϱ� >= spawnPoint + spawnTopBuffer
        float needBySpawn = spawnPoint.position.y + spawnTopBuffer - cam.orthographicSize;
        //   b) ��Ļ�ϱ� >= actualTopY + topMargin
        float needByActualTop = actualTopY + topMargin - cam.orthographicSize;
        //   c) ��Ļ�±� >= baseBottomY  ���ױ����ף�
        float needByBottom = baseBottomY + cam.orthographicSize;

        float targetY = Mathf.Max(needBySpawn, needByActualTop, needByBottom);

        // 3) ���²�ͬƽ����������죩
        float curY = transform.position.y;
        float k = (targetY < curY) ? downSmooth : upSmooth;
        float t = 1f - Mathf.Exp(-k * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(curY, targetY, t);
        transform.position = pos;
    }

    float GetActualTopY()
    {
        Vector2 c = scanCenter ? (Vector2)scanCenter.position : (Vector2)transform.position;
        var hits = Physics2D.OverlapCircleAll(c, scanRadius, stackLayers);
        if (hits == null || hits.Length == 0)
            return baseBottomY; // û��⵽���飬���˻ص��ױ�

        float top = float.NegativeInfinity;
        foreach (var h in hits)
            if (h && h.bounds.max.y > top) top = h.bounds.max.y;

        return float.IsNegativeInfinity(top) ? baseBottomY : top;
    }
}