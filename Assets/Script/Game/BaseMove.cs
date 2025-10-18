using UnityEngine;

/// <summary>
/// ����Ϸ���30����ƽ̨�����ƶ��������Ѻð棩
/// ���� Base �ϣ�Ҫ�� Base �� Rigidbody2D��Kinematic��
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BaseMove : MonoBehaviour
{
    [Header("����")]
    public CountdownTimer timer;         // ������ĵ���ʱ�ű�
    [Header("��������")]
    public float triggerSeconds = 30f;   // ���30�뿪ʼ�ƶ�
    public float amplitude = 1.2f;       // ���Ұڶ����ȣ���λ���������꣩
    public float speedHz = 0.6f;         // �ڶ�Ƶ�ʣ�ÿ������������
    public float fadeInSeconds = 2f;     // �Ӿ�ֹ��ȫ���ȵĹ���ʱ��
    [Header("ʱ��Դ�����ʱ����һ�£�")]
    public bool useScaledTime = false;   // �����ʱ������ TimeScale ���ţ�����Ҳ��Ϊ false

    private Rigidbody2D rb;
    private Vector2 startPos;
    private bool wasActive;
    private float startTime;

    private float Now => useScaledTime ? Time.time : Time.unscaledTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        startPos = rb.position;
        startTime = Now;
    }

    void FixedUpdate()
    {
        if (timer == null || timer.RemainingSeconds <= 0f)
        {
            if (wasActive) rb.MovePosition(startPos);
            wasActive = false;
            return;
        }

        bool active = timer.RemainingSeconds <= triggerSeconds;
        if (active)
        {
            if (!wasActive) startTime = Now;

            float fade = fadeInSeconds > 0 ? Mathf.Clamp01((Now - startTime) / fadeInSeconds) : 1f;
            float omega = 2f * Mathf.PI * speedHz;
            float x = startPos.x + Mathf.Sin(Now * omega) * (amplitude * fade);

            rb.MovePosition(new Vector2(x, startPos.y));
        }
        else
        {
            if (wasActive) rb.MovePosition(startPos);
        }

        wasActive = active;
    }
}