using UnityEngine;

public class DownSpawnerDynamicY : MonoBehaviour
{
    public Transform spawnPoint;          // SpawnY �ڵ�
    public LayerMask stackLayers;         // ֻ�������顱�� Layer����Ҫ�� Base��

    // ���ɸ߶ȣ����������� = extraGap
    public float blockHalfHeight = 0.5f;  // �����ߣ�1u �߾��� 0.5��
    public float extraGap = 0.2f;   // �����������Ķ����϶

    // ����ʱ���»���
    public float downSmooth = 20f;        // Խ�����Խ��
    public float snapThreshold = 1.5f;    // ���ú���ʱ������ٵ���ֵ

    static float currentTopY;

    void Awake()
    {
        if (!spawnPoint) spawnPoint = transform;
        currentTopY = spawnPoint.position.y; // ��ʼ
    }

    void LateUpdate()
    {
        // ʵʱ��ȡ��ʵ����������ɨ�����з����� Collider2D��
        float scannedTop = ScanTopYAll();
        if (!float.IsNegativeInfinity(scannedTop) && scannedTop < currentTopY)
        {
            float drop = currentTopY - scannedTop;
            float k = (drop > snapThreshold) ? downSmooth * 2f : downSmooth; // ����µ�����
            float t = 1f - Mathf.Exp(-k * Time.deltaTime);
            currentTopY = Mathf.Lerp(currentTopY, scannedTop, t);             // ֻ��������
        }

        // Ӧ�õ� SpawnY�������� UpdateTopY ��ߣ�����ֻ������� currentTopY��
        float centerOffset = blockHalfHeight + extraGap; // ���ɵ�=����+���+��϶
        Vector3 p = spawnPoint.position;
        p.y = currentTopY + centerOffset;
        spawnPoint.position = p;
    }

    // ���顰���ȡ�ʱ���ã�̧����ߵ㣨����ֻ�ǲ�����
    public static void UpdateTopY(float newTopY)
    {
        if (newTopY > currentTopY) currentTopY = newTopY;
    }

    // ɨ�賡�������� Collider2D��ȡ���� stackLayers ����� y
    float ScanTopYAll()
    {
        Collider2D[] all = Object.FindObjectsOfType<Collider2D>();
        float top = float.NegativeInfinity;
        int mask = stackLayers.value;

        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (!c) continue;
            if ((mask & (1 << c.gameObject.layer)) == 0) continue; // ���˵������
            float y = c.bounds.max.y;
            if (y > top) top = y;
        }
        return top;
    }
}