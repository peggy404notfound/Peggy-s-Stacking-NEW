using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class CameraAutoZoom : MonoBehaviour
{
    [Header("与生成器同一个 spawnPoint（其Y≈塔顶+safeOffset）")]
    public Transform spawnPoint;
    public float safeOffset = 2f;

    [Header("地基 Collider（用于获取地基底边 Y）")]
    public Collider2D baseCollider;   // 拖地基的 Collider2D

    [Header("相机参数")]
    public float baseSize = 6f;         // 初始正交尺寸
    public float spawnTopBuffer = 1.5f; // 生成点到屏幕顶的最小距离
    public float topMargin = 0f;        // 塔顶额外留白
    public float smoothSpeed = 6f;      // 平滑速度

    private Camera cam;
    private float baseBottomY;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        if (!baseCollider)
            Debug.LogError("[CameraAutoZoom] 请拖入地基的 Collider2D！");

        baseBottomY = baseCollider ? baseCollider.bounds.min.y : 0f;

        // 开局相机
        cam.orthographicSize = baseSize;
        Vector3 p = cam.transform.position;
        // 相机中心 Y = 地基底边 + 相机半高
        p.y = baseBottomY + cam.orthographicSize;
        cam.transform.position = p;
    }

    void LateUpdate()
    {
        if (!spawnPoint) return;

        // 塔顶 ≈ 生成点Y - safeOffset
        float towerTopY = spawnPoint.position.y - safeOffset;

        // 约束：spawnPoint 必须离屏幕顶 >= spawnTopBuffer
        // 屏幕上边界 = 相机中心Y + 相机半高
        float desiredHalfBySpawn = (spawnPoint.position.y + spawnTopBuffer - baseBottomY) * 0.5f;

        // 约束：塔顶额外留白（可选）
        float desiredHalfByTower = (towerTopY + topMargin - baseBottomY) * 0.5f;

        // 目标半高
        float targetSize = Mathf.Max(baseSize, desiredHalfBySpawn, desiredHalfByTower);

        // 平滑缩放
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetSize,
            1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
        );

        // 相机Y：始终保持地基底边贴画面底边
        float desiredY = baseBottomY + cam.orthographicSize;
        Vector3 pos = cam.transform.position;
        pos.y = Mathf.Lerp(pos.y, desiredY, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
        cam.transform.position = pos;
    }
}