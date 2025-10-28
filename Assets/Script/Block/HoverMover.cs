using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HoverMover : MonoBehaviour
{
    [Header("运行时实际速度（只读显示）")]
    [SerializeField] private float moveSpeed = 3f;
    public float MoveSpeed => moveSpeed;

    [Header("自动下落：玩家未操作时的超时时间（秒，<=0 关闭）")]
    public float autoDropSeconds = 4f;

    [HideInInspector] public Transform leftBound;
    [HideInInspector] public Transform rightBound;

    public bool IsHovering => hovering;

    private Rigidbody2D rb;
    private bool hovering = false;
    private float dir = 1f;
    private float originalGravity;

    // 数值边界（可选）
    private bool useNumericBounds = false;
    private float leftX, rightX;

    // 悬停开始时间，用于自动下落计时
    private float hoverStartTime = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravity = rb.gravityScale;
    }

    /// <summary>开始左右悬停：锁定当前速度，并开始自动下落计时。</summary>
    public void StartHover()
    {
        hovering = true;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;

        // 从全局管理器获取当前速度
        if (GameSpeedManager.Instance != null)
            moveSpeed = GameSpeedManager.Instance.GetCurrentMoveSpeed();
        else
            moveSpeed = 3f; // 兜底

        // 开始计时（<=0 则视为关闭自动下落）
        hoverStartTime = Time.time;
    }

    /// <summary>结束悬停，开始下落（可由玩家或超时触发）。</summary>
    public void Drop()
    {
        if (!hovering) return;
        hovering = false;

        // 切回动态刚体，让它受重力
        if (rb)
        {
            rb.velocity = Vector2.zero;
            rb.gravityScale = originalGravity;
            rb.bodyType = RigidbodyType2D.Dynamic;   // 关键：改回 Dynamic
            rb.WakeUp();
            if (rb.velocity.sqrMagnitude < 0.0001f)  // 轻推一下，防止粘住
                rb.velocity = Vector2.down * 0.01f;
        }

        hoverStartTime = -1f;
    }

    /// <summary>以数值形式设置左右边界（可由管理器调用）。</summary>
    public void SetBounds(float left, float right)
    {
        useNumericBounds = true;
        leftX = Mathf.Min(left, right);
        rightX = Mathf.Max(left, right);
    }

    void Update()
    {
        if (!hovering) return;

        // 水平移动
        transform.position += Vector3.right * dir * moveSpeed * Time.deltaTime;

        if (useNumericBounds)
        {
            if (transform.position.x <= leftX) dir = 1f;
            if (transform.position.x >= rightX) dir = -1f;
        }
        else
        {
            if (leftBound && transform.position.x <= leftBound.position.x) dir = 1f;
            if (rightBound && transform.position.x >= rightBound.position.x) dir = -1f;
        }

        if (autoDropSeconds > 0f && hoverStartTime >= 0f)
        {
            if (GamePause.IsPaused)
            {
                // 冻结“自动下落”的计时，让 Hover 期间不会因为 UI 而超时
                hoverStartTime += Time.deltaTime;
            }
            else if (Time.time - hoverStartTime >= autoDropSeconds)
            {
                Drop();
            }
        }
    }
}