using UnityEngine;

public class BlockMarkTopTracker : MonoBehaviour
{
    private bool hasLanded = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasLanded) return; // �����ظ�����
        // �ж��Ƿ������ػ�����������
        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Base") ||
            collision.collider.CompareTag("Block"))
        {
            hasLanded = true;
            // ֪ͨ������������ߵ�
            float topY = GetComponent<Collider2D>().bounds.max.y;
            SpawnerDynamicY.UpdateTopY(topY);
        }
    }
}