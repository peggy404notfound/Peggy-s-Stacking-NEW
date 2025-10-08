using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HoverMover : MonoBehaviour
{
    [Header("水平移动")]
    public float moveSpeed = 3f;
    [HideInInspector] public Transform leftBound;
    [HideInInspector] public Transform rightBound;

    private Rigidbody2D rb;
    private bool hovering = false;
    private float dir = 1f;
    private float originalGravity;

    // —— 新增：数值形式的边界 —— //
    private bool useNumericBounds = false;
    private float leftX, rightX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravity = rb.gravityScale;
    }

    public void StartHover()
    {
        hovering = true;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
    }

    public void Drop()
    {
        if (!hovering) return;
        hovering = false;
        rb.velocity = Vector2.zero;         // 避免侧滑
        rb.gravityScale = originalGravity;  // 开始下落
    }

    // —— 新增：支持 TurnManager 调用 —— //
    public void SetBounds(float left, float right)
    {
        useNumericBounds = true;
        leftX = Mathf.Min(left, right);
        rightX = Mathf.Max(left, right);
    }

    private void Update()
    {
        if (!hovering) return;

        transform.position += Vector3.right * dir * moveSpeed * Time.deltaTime;

        if (useNumericBounds)
        {
            // 用 float 边界
            if (transform.position.x <= leftX) dir = 1f;
            if (transform.position.x >= rightX) dir = -1f;
        }
        else
        {
            // 用 Transform 边界
            if (leftBound && transform.position.x <= leftBound.position.x) dir = 1f;
            if (rightBound && transform.position.x >= rightBound.position.x) dir = -1f;
        }
    }
}