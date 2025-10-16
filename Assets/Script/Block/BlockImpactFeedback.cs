using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BlockImpactFeedback : MonoBehaviour
{
    [Header("�����㣨����/��ľ�ȣ�")]
    public LayerMask impactLayers;

    [Header("��Ч����")]
    public AudioClip impactClipFirst;        // �״νӴ���Ч
    [Range(0f, 1f)] public float firstVolume = 0.6f;
    public AudioClip impactClipSubsequent;   // δ����ʱ���ٴ���ײ��Ч
    [Range(0f, 1f)] public float subsequentVolume = 0.5f;

    [Header("���������״νӴ���")]
    public float shakeAmplitude = 0.03f;
    public float shakeDuration = 0.08f;

    [Header("�ж�����")]
    public bool alwaysPlayFirstHit = true;  // �״νӴ��ز����������ٶȣ�
    public float minImpactSpeed = 0.6f;  // �����ڡ��ٴ���ײ���Ĺ���
    public float settleSpeed = 0.15f; // �����ж����ٶ���ֵ
    public float settleTime = 0.25f; // �����ж�������ʱ��

    [Header("δ�����ڼ�Ķ�����ײ��ȴ")]
    public float secondaryCooldown = 0.10f;

    [Header("����")]
    public bool debugLog = false;

    private AudioSource _as;
    private Rigidbody2D _rb;

    private bool _firstHitDone = false;   // �Ƿ������״���Ч�Ӵ�
    private bool _settled = false;   // �Ƿ�������
    private float _settleTimer = 0f;
    private float _lastSubHitTime = -999f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        // �Զ����������� AudioSource�������ֶ��ң�
        _as = GetComponent<AudioSource>();
        if (_as == null)
        {
            _as = gameObject.AddComponent<AudioSource>();
            _as.hideFlags = HideFlags.HideInInspector;
        }
        _as.playOnAwake = false;
        _as.loop = false;
        _as.spatialBlend = 0f; // 2D
        _as.volume = 1f; // ʵ�������� PlayOneShot ����
    }

    void Update()
    {
        // ֻ�С��������״νӴ����Ժ󣬲ſ�ʼ���ȼ�ʱ
        if (_settled || !_firstHitDone) return;

        if (_rb.velocity.magnitude < settleSpeed)
        {
            _settleTimer += Time.deltaTime;
            if (_settleTimer >= settleTime)
            {
                _settled = true;
                if (debugLog) Debug.Log($"{name}: �����ȣ��������ٲ�����ͨ�������");
            }
        }
        else
        {
            _settleTimer = 0f;
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        HandleImpact(c.collider, c.relativeVelocity.magnitude, "Collision");
    }

    void OnTriggerEnter2D(Collider2D other) // ���� isTrigger �ĵ���/ǽ
    {
        float approxSpeed = _rb ? _rb.velocity.magnitude : 0f;
        HandleImpact(other, approxSpeed, "Trigger");
    }

    void HandleImpact(Collider2D other, float speed, string kind)
    {
        if (_settled) return;

        // ����ˣ�ֻ��ָ���㴥��
        if ((impactLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            if (debugLog) Debug.Log($"{name}: {kind} ���в� {LayerMask.LayerToName(other.gameObject.layer)} ���� impactLayers�����ԡ�");
            return;
        }

        if (!_firstHitDone)
        {
            // �״νӴ��������ٶ���ֵ���ز��� + ����
            if (impactClipFirst) _as.PlayOneShot(impactClipFirst, firstVolume);
            if (CameraShake2D.I) CameraShake2D.I.Shake(shakeAmplitude, shakeDuration);
            _firstHitDone = true;
            if (debugLog) Debug.Log($"{name}: �״�{kind}��speed={speed:F2}��������������Ч+������");
        }
        else
        {
            // δ�����ڼ���ٴ���ײ�����ٶ���ֵ���ˣ�ֻ��������Ч���޶�����
            if (speed < minImpactSpeed)
            {
                if (debugLog) Debug.Log($"{name}: {kind} �ٴ�ײ�� speed={speed:F2} < {minImpactSpeed:F2}�����ԡ�");
                return;
            }
            if (Time.time - _lastSubHitTime >= secondaryCooldown)
            {
                if (impactClipSubsequent) _as.PlayOneShot(impactClipSubsequent, subsequentVolume);
                _lastSubHitTime = Time.time;
                if (debugLog) Debug.Log($"{name}: {kind} �ٴ�ײ����speed={speed:F2}�������Ŷ�����Ч��");
            }
        }
    }
}