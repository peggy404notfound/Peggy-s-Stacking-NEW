using UnityEngine;

/// <summary>
/// 极简版 HoverMoverSimple：只负责左右来回移动，不含自动下落、计时或外部依赖。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class HoverMoverSimple : MonoBehaviour
{
    [Header("左右移动速度")]
    public float moveSpeed = 3f;

    private Rigidbody2D rb;
    private float dir = 1f;
    private bool hovering = false;

    // 可选数值边界
    private bool useNumericBounds = false;
    private float leftX, rightX;

    public bool IsHovering => hovering;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>开始左右移动（悬停）。</summary>
    public void StartHover()
    {
        hovering = true;
        if (rb)
        {
            rb.gravityScale = 0f;
            rb.velocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    /// <summary>设置左右边界。</summary>
    public void SetBounds(float left, float right)
    {
        useNumericBounds = true;
        leftX = Mathf.Min(left, right);
        rightX = Mathf.Max(left, right);
    }

    /// <summary>立即下落：结束悬停并恢复重力。</summary>
    public void Drop()
    {
        hovering = false;
        if (rb)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
            rb.velocity = Vector2.zero;
            rb.WakeUp();
        }
    }

    void Update()
    {
        if (!hovering) return;

        // 移动
        transform.position += Vector3.right * dir * moveSpeed * Time.deltaTime;

        // 反向检测
        if (useNumericBounds)
        {
            if (transform.position.x <= leftX) dir = 1f;
            if (transform.position.x >= rightX) dir = -1f;
        }
    }
}