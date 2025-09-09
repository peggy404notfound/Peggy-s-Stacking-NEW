using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFramerTopK : MonoBehaviour
{
    [Header("Targets")]
    public int topK = 4;                  // 保证画面里显示的最高K块（默认4）
    public LayerMask stackLayer;          // 设为仅包含 "Stack" 层
    public Collider2D baseCollider;       // 可选：把Base的Collider拖进来，防止镜头压到地面以下

    [Header("Framing")]
    public float padding = 0.6f;          // 留白（世界单位）
    public float minOrtho = 3f;           // 最小正交半高
    public float maxOrtho = 12f;          // 最大正交半高
    public float followLerp = 8f;         // 平滑系数（越大越跟手）

    [Header("Clamp Y by Base (optional)")]
    public bool clampByBase = true;       // 勾选后相机不会低于地面（需要baseCollider）

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
        // 1) 找到所有“已放置”的方块（有 BlockMark，刚体启用，碰撞有效，且在 Stack 层）
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
            if (col.isTrigger) continue;        // 预览态会是 trigger，排除
            if (!col.enabled) continue;
            if (!rb.simulated) continue;        // 预览态关闭物理，排除

            placed.Add(col);
        }

        if (placed.Count == 0) return;

        // 2) 选“按高度最高的前K个”
        var top = placed
            .OrderByDescending(c => c.bounds.max.y)
            .Take(Mathf.Max(1, topK))
            .ToList();

        // 3) 计算目标包围盒 + 可选地把地面也算进来
        Bounds? maybe = null;
        foreach (var c in top)
        {
            maybe = maybe.HasValue ? Encapsulate(maybe.Value, c.bounds) : (Bounds?)c.bounds;
        }
        Bounds box = maybe.Value;

        if (clampByBase && baseCollider)
        {
            // 把 Base 的上沿也包含进包围盒，防止镜头太低裁掉地面
            box = Encapsulate(box, baseCollider.bounds);
        }

        // 4) 计算目标中心（对准最高那块，同时等于包围盒中心）
        Vector3 center = box.center;
        center.z = transform.position.z;

        // 5) 算目标 orthoSize：让包围盒（含padding）完全进入画面
        float halfW = box.extents.x + padding;
        float halfH = box.extents.y + padding;

        float targetOrtho = Mathf.Max(halfH, halfW / Mathf.Max(0.0001f, cam.aspect));
        targetOrtho = Mathf.Clamp(targetOrtho, minOrtho, maxOrtho);

        // 6) 平滑移动/缩放
        float t = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, center, t);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrtho, t);

        // 7) 额外Y向夹紧：不让相机低于地面（可选）
        if (clampByBase && baseCollider)
        {
            float baseTop = baseCollider.bounds.max.y;
            // 相机可看到的最低世界Y = camPosY - orthoSize
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