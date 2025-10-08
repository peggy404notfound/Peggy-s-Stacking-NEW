using UnityEngine;
using System.Collections;

public class BlockMarkTopTrackerNew : MonoBehaviour
{
    [Header("认为“落稳”的阈值")]
    public float settleSpeed = 0.05f;   // 速度低于该值视为基本不动
    public float settleTime = 0.12f;   // 需连续稳定这么久才算落稳

    private bool reported = false;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (reported) return;

        // 只在碰到地基(Base层) 或 其他方块(Tag=Block) 时开始观察是否落稳
        int baseLayer = LayerMask.NameToLayer("Base");
        bool hitBase = (collision.collider.gameObject.layer == baseLayer);
        bool hitBlock = collision.collider.CompareTag("Block");
        if (hitBase || hitBlock)
        {
            StartCoroutine(ReportWhenStable());
        }
    }

    IEnumerator ReportWhenStable()
    {
        var rb = GetComponent<Rigidbody2D>();
        var col = GetComponent<Collider2D>();
        if (!col) yield break;

        float okFor = 0f;
        float v2 = settleSpeed * settleSpeed;

        while (rb && col)
        {
            // 连续低速计时，期间若加速则清零
            if (!rb.IsAwake() || rb.velocity.sqrMagnitude <= v2)
                okFor += Time.deltaTime;
            else
                okFor = 0f;

            if (okFor >= settleTime) break;
            yield return null;
        }

        if (reported || col == null) yield break;
        reported = true;

        float topY = col.bounds.max.y;
        // 关键：调用新的生成器脚本（能上也能下）
        DownSpawnerDynamicY.UpdateTopY(topY);
        // 如需调试可打开：
        // Debug.Log($"[BlockMarkTopTrackerNew] Top reported: {topY}");
    }
}