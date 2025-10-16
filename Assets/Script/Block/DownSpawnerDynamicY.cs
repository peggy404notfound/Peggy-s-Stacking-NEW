using UnityEngine;

public class DownSpawnerDynamicY : MonoBehaviour
{
    public Transform spawnPoint;          // SpawnY 节点
    public LayerMask stackLayers;         // 只勾“方块”的 Layer（不要勾 Base）

    // 生成高度：底面离塔顶 = extraGap
    public float blockHalfHeight = 0.5f;  // 方块半高（1u 高就填 0.5）
    public float extraGap = 0.2f;   // 底面与塔顶的额外缝隙

    // 倒塌时向下回落
    public float downSmooth = 20f;        // 越大回落越快
    public float snapThreshold = 1.5f;    // 掉得很猛时额外加速的阈值

    static float currentTopY;

    void Awake()
    {
        if (!spawnPoint) spawnPoint = transform;
        currentTopY = spawnPoint.position.y; // 初始
    }

    void LateUpdate()
    {
        // 实时读取“实际塔顶”（扫描所有方块层的 Collider2D）
        float scannedTop = ScanTopYAll();
        if (!float.IsNegativeInfinity(scannedTop) && scannedTop < currentTopY)
        {
            float drop = currentTopY - scannedTop;
            float k = (drop > snapThreshold) ? downSmooth * 2f : downSmooth; // 大幅下跌更快
            float t = 1f - Mathf.Exp(-k * Time.deltaTime);
            currentTopY = Mathf.Lerp(currentTopY, scannedTop, t);             // 只向下修正
        }

        // 应用到 SpawnY（上升靠 UpdateTopY 提高；这里只负责跟随 currentTopY）
        float centerOffset = blockHalfHeight + extraGap; // 生成点=塔顶+半高+缝隙
        Vector3 p = spawnPoint.position;
        p.y = currentTopY + centerOffset;
        spawnPoint.position = p;
    }

    // 方块“落稳”时调用：抬高最高点（向上只涨不跌）
    public static void UpdateTopY(float newTopY)
    {
        if (newTopY > currentTopY) currentTopY = newTopY;
    }

    // 扫描场景里所有 Collider2D，取属于 stackLayers 的最高 y
    float ScanTopYAll()
    {
        Collider2D[] all = Object.FindObjectsOfType<Collider2D>();
        float top = float.NegativeInfinity;
        int mask = stackLayers.value;

        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (!c) continue;
            if ((mask & (1 << c.gameObject.layer)) == 0) continue; // 过滤到方块层
            float y = c.bounds.max.y;
            if (y > top) top = y;
        }
        return top;
    }
}