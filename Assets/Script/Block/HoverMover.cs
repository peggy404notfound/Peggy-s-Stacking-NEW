using UnityEngine;

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

    // �ò��� timeScale Ӱ���ʱ������ʱ
    private float hoverStartUnscaled = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb) originalGravity = rb.gravityScale;
    }

    /// <summary>
    /// ������ͣ�������ƶ������������Զ������ʱ��
    /// </summary>
    public void StartHover()
    {
        hovering = true;

        if (rb)
        {
            rb.gravityScale = 0f;
            rb.velocity = Vector2.zero;
        }

        // ����ȫ���ٶȹ��������������︲�� moveSpeed����ѡ��
        // if (GameSpeedManager.Instance != null)
        //     moveSpeed = GameSpeedManager.Instance.GetCurrentMoveSpeed();

        hoverStartUnscaled = Time.unscaledTime;
    }

    /// <summary>
    /// �������䣬�ָ�������
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
    }

    void Update()
    {
        if (!hovering) return;

        // �� unscaledDeltaTime ���������ƶ��������κ� timeScale ����
        float dt = Time.unscaledDeltaTime;
        transform.position += Vector3.right * dir * moveSpeed * dt;

        // �������ұ߽�
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

        // �Զ������ʱ��UI �ڼ䣨����ͣ�������ʱ
        if (autoDropSeconds > 0f && hoverStartUnscaled >= 0f)
        {
            if (GamePause.IsPaused)
            {
                // ͨ�����ر�֡ dt �ķ�ʽ���ᾭ��ʱ��
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
    /// ��ɴ�����ݣ�ʹ�� Transform ��Ϊ���ұ߽硣
    /// </summary>
    public void SetBounds(Transform left, Transform right)
    {
        leftBound = left;
        rightBound = right;
        useNumericBounds = false;
    }

    /// <summary>
    /// ��ɴ�����ݣ�ʹ����ֵ x ��Ϊ���ұ߽硣
    /// </summary>
    public void SetBounds(float leftX, float rightX)
    {
        this.leftX = leftX;
        this.rightX = rightX;
        useNumericBounds = true;
    }
}