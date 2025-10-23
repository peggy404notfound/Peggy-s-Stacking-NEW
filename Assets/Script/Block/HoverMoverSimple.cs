using UnityEngine;

/// <summary>
/// ����� HoverMoverSimple��ֻ�������������ƶ��������Զ����䡢��ʱ���ⲿ������
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class HoverMoverSimple : MonoBehaviour
{
    [Header("�����ƶ��ٶ�")]
    public float moveSpeed = 3f;

    private Rigidbody2D rb;
    private float dir = 1f;
    private bool hovering = false;

    // ��ѡ��ֵ�߽�
    private bool useNumericBounds = false;
    private float leftX, rightX;

    public bool IsHovering => hovering;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>��ʼ�����ƶ�����ͣ����</summary>
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

    /// <summary>�������ұ߽硣</summary>
    public void SetBounds(float left, float right)
    {
        useNumericBounds = true;
        leftX = Mathf.Min(left, right);
        rightX = Mathf.Max(left, right);
    }

    /// <summary>�������䣺������ͣ���ָ�������</summary>
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

        // �ƶ�
        transform.position += Vector3.right * dir * moveSpeed * Time.deltaTime;

        // ������
        if (useNumericBounds)
        {
            if (transform.position.x <= leftX) dir = 1f;
            if (transform.position.x >= rightX) dir = -1f;
        }
    }
}