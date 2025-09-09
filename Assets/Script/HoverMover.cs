using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HoverMover : MonoBehaviour
{
    [Header("ˮƽ�ƶ�")]
    public float moveSpeed = 3f;
    [HideInInspector] public Transform leftBound;
    [HideInInspector] public Transform rightBound;

    private Rigidbody2D rb;
    private bool hovering = false;
    private float dir = 1f;
    private float originalGravity;

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
        rb.velocity = Vector2.zero;         // ����໬
        rb.gravityScale = originalGravity;  // ��ʼ����
    }

    private void Update()
    {
        if (!hovering) return;

        transform.position += Vector3.right * dir * moveSpeed * Time.deltaTime;

        if (leftBound && transform.position.x <= leftBound.position.x) dir = 1f;
        if (rightBound && transform.position.x >= rightBound.position.x) dir = -1f;
    }
}