using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HoverMover : MonoBehaviour
{
    [Header("����ʱʵ���ٶȣ�ֻ����ʾ��")]
    [SerializeField] private float moveSpeed = 3f;
    public float MoveSpeed => moveSpeed;

    [Header("�Զ����䣺���δ����ʱ�ĳ�ʱʱ�䣨�룬<=0 �رգ�")]
    public float autoDropSeconds = 4f;

    [HideInInspector] public Transform leftBound;
    [HideInInspector] public Transform rightBound;

    public bool IsHovering => hovering;

    private Rigidbody2D rb;
    private bool hovering = false;
    private float dir = 1f;
    private float originalGravity;

    // ��ֵ�߽磨��ѡ��
    private bool useNumericBounds = false;
    private float leftX, rightX;

    // ��ͣ��ʼʱ�䣬�����Զ������ʱ
    private float hoverStartTime = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravity = rb.gravityScale;
    }

    /// <summary>��ʼ������ͣ��������ǰ�ٶȣ�����ʼ�Զ������ʱ��</summary>
    public void StartHover()
    {
        hovering = true;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;

        // ��ȫ�ֹ�������ȡ��ǰ�ٶ�
        if (GameSpeedManager.Instance != null)
            moveSpeed = GameSpeedManager.Instance.GetCurrentMoveSpeed();
        else
            moveSpeed = 3f; // ����

        // ��ʼ��ʱ��<=0 ����Ϊ�ر��Զ����䣩
        hoverStartTime = Time.time;
    }

    /// <summary>������ͣ����ʼ���䣨������һ�ʱ��������</summary>
    public void Drop()
    {
        if (!hovering) return;
        hovering = false;

        // �лض�̬���壬����������
        if (rb)
        {
            rb.velocity = Vector2.zero;
            rb.gravityScale = originalGravity;
            rb.bodyType = RigidbodyType2D.Dynamic;   // �ؼ����Ļ� Dynamic
            rb.WakeUp();
            if (rb.velocity.sqrMagnitude < 0.0001f)  // ����һ�£���ֹճס
                rb.velocity = Vector2.down * 0.01f;
        }

        hoverStartTime = -1f;
    }

    /// <summary>����ֵ��ʽ�������ұ߽磨���ɹ��������ã���</summary>
    public void SetBounds(float left, float right)
    {
        useNumericBounds = true;
        leftX = Mathf.Min(left, right);
        rightX = Mathf.Max(left, right);
    }

    void Update()
    {
        if (!hovering) return;

        // ˮƽ�ƶ�
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
                // ���ᡰ�Զ����䡱�ļ�ʱ���� Hover �ڼ䲻����Ϊ UI ����ʱ
                hoverStartTime += Time.deltaTime;
            }
            else if (Time.time - hoverStartTime >= autoDropSeconds)
            {
                Drop();
            }
        }
    }
}