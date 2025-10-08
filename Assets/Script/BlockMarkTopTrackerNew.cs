using UnityEngine;
using System.Collections;

public class BlockMarkTopTrackerNew : MonoBehaviour
{
    [Header("��Ϊ�����ȡ�����ֵ")]
    public float settleSpeed = 0.05f;   // �ٶȵ��ڸ�ֵ��Ϊ��������
    public float settleTime = 0.12f;   // �������ȶ���ô�ò�������

    private bool reported = false;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (reported) return;

        // ֻ�������ػ�(Base��) �� ��������(Tag=Block) ʱ��ʼ�۲��Ƿ�����
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
            // �������ټ�ʱ���ڼ�������������
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
        // �ؼ��������µ��������ű�������Ҳ���£�
        DownSpawnerDynamicY.UpdateTopY(topY);
        // ������Կɴ򿪣�
        // Debug.Log($"[BlockMarkTopTrackerNew] Top reported: {topY}");
    }
}