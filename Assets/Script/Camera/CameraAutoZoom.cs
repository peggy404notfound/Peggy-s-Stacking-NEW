using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class CameraAutoZoom : MonoBehaviour
{
    [Header("��������ͬһ�� spawnPoint����Y������+safeOffset��")]
    public Transform spawnPoint;
    public float safeOffset = 2f;

    [Header("�ػ� Collider�����ڻ�ȡ�ػ��ױ� Y��")]
    public Collider2D baseCollider;   // �ϵػ��� Collider2D

    [Header("�������")]
    public float baseSize = 6f;         // ��ʼ�����ߴ�
    public float spawnTopBuffer = 1.5f; // ���ɵ㵽��Ļ������С����
    public float topMargin = 0f;        // ������������
    public float smoothSpeed = 6f;      // ƽ���ٶ�

    private Camera cam;
    private float baseBottomY;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        if (!baseCollider)
            Debug.LogError("[CameraAutoZoom] ������ػ��� Collider2D��");

        baseBottomY = baseCollider ? baseCollider.bounds.min.y : 0f;

        // �������
        cam.orthographicSize = baseSize;
        Vector3 p = cam.transform.position;
        // ������� Y = �ػ��ױ� + ������
        p.y = baseBottomY + cam.orthographicSize;
        cam.transform.position = p;
    }

    void LateUpdate()
    {
        if (!spawnPoint) return;

        // ���� �� ���ɵ�Y - safeOffset
        float towerTopY = spawnPoint.position.y - safeOffset;

        // Լ����spawnPoint ��������Ļ�� >= spawnTopBuffer
        // ��Ļ�ϱ߽� = �������Y + ������
        float desiredHalfBySpawn = (spawnPoint.position.y + spawnTopBuffer - baseBottomY) * 0.5f;

        // Լ���������������ף���ѡ��
        float desiredHalfByTower = (towerTopY + topMargin - baseBottomY) * 0.5f;

        // Ŀ����
        float targetSize = Mathf.Max(baseSize, desiredHalfBySpawn, desiredHalfByTower);

        // ƽ������
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetSize,
            1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
        );

        // ���Y��ʼ�ձ��ֵػ��ױ�������ױ�
        float desiredY = baseBottomY + cam.orthographicSize;
        Vector3 pos = cam.transform.position;
        pos.y = Mathf.Lerp(pos.y, desiredY, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
        cam.transform.position = pos;
    }
}