using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class HoverMover : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 3f;
    public float MoveSpeed => moveSpeed;

    [Tooltip("Seconds before auto drop after StartHover()")]
    public float autoDropSeconds = 4f;

    [Header("Bounds by Transform (default)")]
    [HideInInspector] public Transform leftBound;
    [HideInInspector] public Transform rightBound;

    [Header("Bounds by Numeric (optional)")]
    public bool useNumericBounds = false;
    public float leftX = -5f;
    public float rightX = 5f;

    public bool IsHovering => hovering;

    private Rigidbody2D rb;
    private bool hovering = false;
    private float dir = 1f;
    private float originalGravity = 1f;

    // 用不受 timeScale 影响的时间做计时
    private float hoverStartUnscaled = -1f;

    // ================== 新增：投放窗口注册 ==================
    [Header("Drop → Prop Pickup Window")]
    [SerializeField, Tooltip("玩家触发 Drop() 后，在这段时间内允许道具拾取（秒）")]
    private float dropPickupWindow = 1.0f; // 可按手感调 0.6~1.2 秒

    private Collider2D[] allCols;

    // 静态注册表：Collider InstanceID -> 允许拾取的截止时间（unscaledTime）
    private static readonly Dictionary<int, float> s_dropEligibleUntil =
        new Dictionary<int, float>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb) originalGravity = rb.gravityScale;

        // 缓存自身及子物体的 Collider
        allCols = GetComponentsInChildren<Collider2D>(includeInactive: false);
    }

    /// <summary>
    /// 查询某个 Collider 是否处于“玩家刚丢下”的时间窗口内。
    /// </summary>
    public static bool IsColliderInDropWindow(Collider2D col)
    {
        if (!col) return false;
        if (s_dropEligibleUntil.TryGetValue(col.GetInstanceID(), out float until))
            return Time.unscaledTime <= until;
        return false;
    }

    /// <summary>
    /// 启动悬停（左右移动），并重置自动下落计时。
    /// </summary>
    public void StartHover()
    {
        hovering = true;

        if (rb)
        {
            rb.gravityScale = 0f;
            rb.velocity = Vector2.zero;
        }

        // 若有全局速度管理器，可在这里覆盖 moveSpeed（可选）
        // if (GameSpeedManager.Instance != null)
        //     moveSpeed = GameSpeedManager.Instance.GetCurrentMoveSpeed();

        hoverStartUnscaled = Time.unscaledTime;
    }

    /// <summary>
    /// 触发下落，恢复重力，并开启“可拾取窗口”。
    /// </summary>
    public void Drop()
    {
        if (!hovering) return;
        hovering = false;

        if (rb)
        {
            rb.velocity = Vector2.zero;
            rb.gravityScale = originalGravity <= 0f ? 1f : originalGravity;
        }

        // ===== 关键：注册这块积木的所有 Collider 在短时间内可拾取 =====
        float until = Time.unscaledTime + Mathf.Max(0.05f, dropPickupWindow);
        if (allCols == null || allCols.Length == 0)
            allCols = GetComponentsInChildren<Collider2D>(includeInactive: false);

        for (int i = 0; i < allCols.Length; i++)
        {
            var c = allCols[i];
            if (!c || !c.enabled) continue;
            s_dropEligibleUntil[c.GetInstanceID()] = until;
        }
    }

    void Update()
    {
        if (!hovering) return;

        // 用 unscaledDeltaTime 驱动左右移动，避免任何 timeScale 干扰
        float dt = Time.unscaledDeltaTime;
        transform.position += Vector3.right * dir * moveSpeed * dt;

        // 处理左右边界
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

        // 自动下落计时：UI 期间（软暂停）冻结计时
        if (autoDropSeconds > 0f && hoverStartUnscaled >= 0f)
        {
            if (GamePause.IsPaused)
            {
                // 通过补回本帧 dt 的方式冻结经过时间
                hoverStartUnscaled += dt;
            }
            else if (Time.unscaledTime - hoverStartUnscaled >= autoDropSeconds)
            {
                Drop();
            }
        }
    }

    // =======================
    // Backward-compatible API
    // =======================

    /// <summary>
    /// 与旧代码兼容：使用 Transform 作为左右边界。
    /// </summary>
    public void SetBounds(Transform left, Transform right)
    {
        leftBound = left;
        rightBound = right;
        useNumericBounds = false;
    }

    /// <summary>
    /// 与旧代码兼容：使用数值 x 作为左右边界。
    /// </summary>
    public void SetBounds(float leftX, float rightX)
    {
        this.leftX = leftX;
        this.rightX = rightX;
        useNumericBounds = true;
    }
}