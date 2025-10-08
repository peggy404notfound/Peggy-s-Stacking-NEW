using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class CameraFollowTopY : MonoBehaviour
{
    [Header("Spawning / baseline")]
    public Transform spawnPoint;        // 同生成器的那个
    public float safeOffset = 2f;       // 与生成器一致
    public Collider2D baseCollider;     // 地基 Collider2D（用于“底边贴底”）

    [Header("Top constraints")]
    public float spawnTopBuffer = 1.5f; // spawnPoint 到屏幕顶的最小距离
    public float topMargin = 0f;        // （可选）塔顶额外留白

    [Header("Follow speed")]
    public float upSmooth = 5f;         // 向上跟随平滑度
    public float downSmooth = 12f;      // 向下回落平滑度（倒塌时更快）

    [Header("Lightweight top scan (for collapse)")]
    public LayerMask stackLayers;       // 只勾选“方块”的层（不要勾 Base）
    public float scanRadius = 100f;     // 扫描半径
    public Transform scanCenter;        // 不填就用相机自己

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

        // 开局：底边贴底
        Vector3 p = transform.position;
        p.y = baseBottomY + cam.orthographicSize;
        transform.position = p;

        if (!scanCenter) scanCenter = transform;
    }

    void LateUpdate()
    {
        if (!spawnPoint) return;

        // 1) 实时获取“实际塔顶”（倒塌时会变低）
        float actualTopY = GetActualTopY();

        // 2) 三个约束 -> 求出目标相机中心Y
        //   a) 屏幕上边 >= spawnPoint + spawnTopBuffer
        float needBySpawn = spawnPoint.position.y + spawnTopBuffer - cam.orthographicSize;
        //   b) 屏幕上边 >= actualTopY + topMargin
        float needByActualTop = actualTopY + topMargin - cam.orthographicSize;
        //   c) 屏幕下边 >= baseBottomY  （底边贴底）
        float needByBottom = baseBottomY + cam.orthographicSize;

        float targetY = Mathf.Max(needBySpawn, needByActualTop, needByBottom);

        // 3) 上下不同平滑（下落更快）
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
            return baseBottomY; // 没检测到方块，就退回到底边

        float top = float.NegativeInfinity;
        foreach (var h in hits)
            if (h && h.bounds.max.y > top) top = h.bounds.max.y;

        return float.IsNegativeInfinity(top) ? baseBottomY : top;
    }
}