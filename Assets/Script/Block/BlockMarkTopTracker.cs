using UnityEngine;

public class BlockMarkTopTracker : MonoBehaviour
{
    private bool hasLanded = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasLanded) return; // 避免重复触发
        // 判断是否碰到地基或其他方块
        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Base") ||
            collision.collider.CompareTag("Block"))
        {
            hasLanded = true;
            // 通知管理器更新最高点
            float topY = GetComponent<Collider2D>().bounds.max.y;
            SpawnerDynamicY.UpdateTopY(topY);
        }
    }
}