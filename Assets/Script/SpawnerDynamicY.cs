using UnityEngine;

public class SpawnerDynamicY : MonoBehaviour
{
    public Transform spawnPoint;
    public float safeOffset = 2f;

    private static float currentTopY = 0f; // ȫ����ߵ��¼

    void Awake()
    {
        if (!spawnPoint) spawnPoint = transform;
        currentTopY = spawnPoint.position.y; // ��ʼ
    }

    void LateUpdate()
    {
        Vector3 pos = spawnPoint.position;
        pos.y = currentTopY + safeOffset;
        spawnPoint.position = pos;
    }

    // ��������ã�������ߵ�
    public static void UpdateTopY(float newTopY)
    {
        if (newTopY > currentTopY)
            currentTopY = newTopY;
    }
}