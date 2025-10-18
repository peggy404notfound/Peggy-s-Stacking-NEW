using UnityEngine;

/// <summary>
/// �����ҿ��� + ��ײ˲ʱ������޳�̼���
/// ���1��A/D�����2����/��
/// �ⲿ���� EnableControl(bool) / EnableControl() / DisableControl() ��������
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PushHand2D : MonoBehaviour
{
    [Header("����趨 1=P1(A/D)��2=P2(��/��)")]
    public int playerId = 1;

    [Header("�����ƶ�")]
    public float moveSpeed = 8f;
    public float xMin = -10f;
    public float xMax = 10f;

    [Header("�������ã�ײ�ϻ�ľʱ�ĳ����")]
    public LayerMask affectLayers;          // ֻ����Щ����Ч����ѡ��Ļ�ľ�㣩
    public float baseImpulse = 10f;         // ���������
    public float speedMultiplier = 2.0f;    // ��������ֵ�ǰ�ٶȵļӳɣ�push = base + |vx|*mult
    public float torqueAmount = 50f;        // �������Ť�أ������У�
    public float pushCooldown = 0.15f;      // ͬһֻ�������ƻ�����ȴ������һ֡���

    [Header("����ѡ��")]
    public bool useContinuousCollision = true;
    public float linearDrag = 0.05f;

    [Header("���ƿ��أ��ⲿ���ã�")]
    public bool controlEnabled = true;      // Ĭ��Ϊ�ɿ���

    // ���� �ڲ� ���� 
    private Rigidbody2D rb;
    private float nextPushTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.drag = linearDrag;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (useContinuousCollision)
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        // ���ⲿ�ؿأ��������룬ͣס�����ٶȣ������߽�ǯλ
        if (!controlEnabled)
        {
            if (rb != null)
            {
                rb.velocity = new Vector2(0f, 0f);
                Vector2 p = rb.position;
                p.x = Mathf.Clamp(p.x, xMin, xMax);
                rb.position = p;
            }
            return;
        }

        // ��ȡ���루P1: A/D��P2: ��/����
        float dir = 0f;
        if (playerId == 1)
        {
            if (Input.GetKey(KeyCode.A)) dir = -1f;
            if (Input.GetKey(KeyCode.D)) dir = 1f;
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftArrow)) dir = -1f;
            if (Input.GetKey(KeyCode.RightArrow)) dir = 1f;
        }

        // ���ٶ��ƶ������ж����У�
        rb.velocity = new Vector2(dir * moveSpeed, 0f);

        // �߽�����
        Vector2 pos = rb.position;
        pos.x = Mathf.Clamp(pos.x, xMin, xMax);
        rb.position = pos;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < nextPushTime) return;

        Rigidbody2D other = collision.rigidbody;
        if (other == null) return;

        // ֻ������ָ����
        if ((affectLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // ���ݵ�ǰ���֡����ٶȣ�����һ���Գ����
        float pushPower = baseImpulse + Mathf.Abs(rb.velocity.x) * speedMultiplier;

        // ������򣺰��ֵ�ǰ���ƶ�����
        float xSign = Mathf.Sign(rb.velocity.x);
        if (Mathf.Approximately(xSign, 0f))
        {
            // ���պ�ֹͣ��Ĭ�����ң�Ҳ���ýӴ�����������
            xSign = 1f;
        }
        Vector2 pushDir = Vector2.right * xSign;

        // һ���Ա���ʽ��� + Ť��
        other.AddForce(pushDir * pushPower, ForceMode2D.Impulse);
        other.AddTorque(Random.Range(-torqueAmount, torqueAmount), ForceMode2D.Impulse);

        nextPushTime = Time.time + pushCooldown;

        // ����ѡ��������Դ�����Ч/��ͷ��/������
        // AudioSource.PlayClipAtPoint(winPushSfx, Camera.main.transform.position, 1f);
        // StartCoroutine(CameraShake(0.15f, 0.2f));
        // StartCoroutine(SlowMo(0.5f, 0.6f));
    }

    // ===== �ⲿ���ݿ��Ʒ��� =====
    public void EnableControl(bool enable)
    {
        controlEnabled = enable;
        if (!enable && rb != null)
        {
            rb.velocity = Vector2.zero; // �ؿ�ʱͣס�֣���ֹ���Լ�����
        }
    }

    // �����޲ε���
    public void EnableControl() => EnableControl(true);

    // ���� DisableControl()
    public void DisableControl() => EnableControl(false);
}