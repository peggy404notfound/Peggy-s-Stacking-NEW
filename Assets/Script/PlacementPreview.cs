using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlacementPreview : MonoBehaviour
{
    private SpriteRenderer sr;
    private BoxCollider2D col;
    private Rigidbody2D rb;
    private HorizontalSweeper sweeper;
    private float originalAlpha = 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        sweeper = GetComponent<HorizontalSweeper>();
        if (sr) originalAlpha = sr.color.a;
    }

    public void EnterPreview()
    {
        if (sr)
        {
            var c = sr.color; c.a = 0.4f; sr.color = c;   // 半透明
        }
        if (col) col.isTrigger = true;                   // 不参与碰撞
        if (rb) rb.simulated = false;                   // 关闭物理
        if (sweeper) sweeper.isActive = true;            // 只水平扫动
    }

    public void SolidifyHere()
    {
        if (sr)
        {
            var c = sr.color; c.a = (originalAlpha <= 0f ? 1f : originalAlpha);
            sr.color = c;                                // 还原不透明
        }
        if (col) col.isTrigger = false;
        if (rb)
        {
            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 1f;                        // 交给物理维持真实性
        }
        if (sweeper) sweeper.isActive = false;
        // 确保最终层是 Stack（防止预制体层设置不一致）
        gameObject.layer = LayerMask.NameToLayer("Stack");
    }

    public float GetHalfHeightWorld()
    {
        if (col) return col.bounds.extents.y;
        if (sr) return sr.bounds.extents.y;
        return 0.5f;
    }
}