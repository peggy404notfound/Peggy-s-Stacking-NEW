using UnityEngine;

public class SpawnerDynamicY : MonoBehaviour
{
    public Transform spawnPoint;
    public float safeOffset = 2f;

    private static float currentTopY = 0f; // 全局最高点记录

    void Awake()
    {
        if (!spawnPoint) spawnPoint = transform;
        currentTopY = spawnPoint.position.y; // 初始
    }

    void LateUpdate()
    {
        Vector3 pos = spawnPoint.position;
        pos.y = currentTopY + safeOffset;
        spawnPoint.position = pos;
    }

    // 给方块调用，更新最高点
    public static void UpdateTopY(float newTopY)
    {
        if (newTopY > currentTopY)
            currentTopY = newTopY;
    }
}