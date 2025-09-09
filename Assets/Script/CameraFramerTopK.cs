using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFramerTopK : MonoBehaviour
{
    [Header("Targets")]
    public int topK = 4;                  // ��֤��������ʾ�����K�飨Ĭ��4��
    public LayerMask stackLayer;          // ��Ϊ������ "Stack" ��
    public Collider2D baseCollider;       // ��ѡ����Base��Collider�Ͻ�������ֹ��ͷѹ����������

    [Header("Framing")]
    public float padding = 0.6f;          // ���ף����絥λ��
    public float minOrtho = 3f;           // ��С�������
    public float maxOrtho = 12f;          // ����������
    public float followLerp = 8f;         // ƽ��ϵ����Խ��Խ���֣�

    [Header("Clamp Y by Base (optional)")]
    public bool clampByBase = true;       // ��ѡ�����������ڵ��棨��ҪbaseCollider��

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic)
            Debug.LogWarning("[CameraFramerTopK] Camera is not orthographic.");
        if (stackLayer.value == 0)
            stackLayer = 1 << LayerMask.NameToLayer("Stack");
    }

    private void LateUpdate()
    {
        // 1) �ҵ����С��ѷ��á��ķ��飨�� BlockMark���������ã���ײ��Ч������ Stack �㣩
        var marks = FindObjectsOfType<BlockMark>();
        var placed = new List<Collider2D>();

        foreach (var m in marks)
        {
            if (!m) continue;
            var go = m.gameObject;
            if (((1 << go.layer) & stackLayer) == 0) continue;

            var col = go.GetComponent<Collider2D>();
            var rb = go.GetComponent<Rigidbody2D>();
            if (!col || !rb) continue;
            if (col.isTrigger) continue;        // Ԥ��̬���� trigger���ų�
            if (!col.enabled) continue;
            if (!rb.simulated) continue;        // Ԥ��̬�ر������ų�

            placed.Add(col);
        }

        if (placed.Count == 0) return;

        // 2) ѡ�����߶���ߵ�ǰK����
        var top = placed
            .OrderByDescending(c => c.bounds.max.y)
            .Take(Mathf.Max(1, topK))
            .ToList();

        // 3) ����Ŀ���Χ�� + ��ѡ�ذѵ���Ҳ�����
        Bounds? maybe = null;
        foreach (var c in top)
        {
            maybe = maybe.HasValue ? Encapsulate(maybe.Value, c.bounds) : (Bounds?)c.bounds;
        }
        Bounds box = maybe.Value;

        if (clampByBase && baseCollider)
        {
            // �� Base ������Ҳ��������Χ�У���ֹ��ͷ̫�Ͳõ�����
            box = Encapsulate(box, baseCollider.bounds);
        }

        // 4) ����Ŀ�����ģ���׼����ǿ飬ͬʱ���ڰ�Χ�����ģ�
        Vector3 center = box.center;
        center.z = transform.position.z;

        // 5) ��Ŀ�� orthoSize���ð�Χ�У���padding����ȫ���뻭��
        float halfW = box.extents.x + padding;
        float halfH = box.extents.y + padding;

        float targetOrtho = Mathf.Max(halfH, halfW / Mathf.Max(0.0001f, cam.aspect));
        targetOrtho = Mathf.Clamp(targetOrtho, minOrtho, maxOrtho);

        // 6) ƽ���ƶ�/����
        float t = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, center, t);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrtho, t);

        // 7) ����Y��н�������������ڵ��棨��ѡ��
        if (clampByBase && baseCollider)
        {
            float baseTop = baseCollider.bounds.max.y;
            // ����ɿ������������Y = camPosY - orthoSize
            float minVisibleY = transform.position.y - cam.orthographicSize;
            if (minVisibleY < baseTop)
            {
                float dy = baseTop - minVisibleY;
                var p = transform.position;
                p.y += dy;
                transform.position = p;
            }
        }
    }

    private Bounds Encapsulate(Bounds a, Bounds b)
    {
        a.Encapsulate(b.min);
        a.Encapsulate(b.max);
        return a;
    }
}