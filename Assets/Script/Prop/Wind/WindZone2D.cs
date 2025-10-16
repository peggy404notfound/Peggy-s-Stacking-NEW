using UnityEngine;

/// <summary>
/// ������������2D�����������ڳ��������еĸ���ʩ����ʱ�������λ��˥������
/// ������BoxCollider2D��IsTrigger=true��
/// ��ѡ��ParticleSystem��AudioSource �ŵ�ͬ������
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class WindZone2D : MonoBehaviour
{
    [Header("���򣨵�λ������")]
    public Vector2 dir = Vector2.right;

    [Header("Ӱ�����")]
    public LayerMask targetLayers;
    public bool onlyAffectWhenMoving = true;    // ���ơ�ֻ������/�ڶ��Ŀ顱

    [Header("ǿ����ʱ��")]
    public float duration = 1.1f;               // ǿ��ʱ��
    public float baseStrength = 35f;            // ��ǿ�ȣ�������/��������
    public AnimationCurve strengthOverTime = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("�������˥����0=��߽磬1=�ұ߽磩")]
    public AnimationCurve horizontalFalloff = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("��ѡ�Ӿ�/��Ч")]
    public ParticleSystem windFx;
    public AudioSource audioSrc;

    // WindZone2D.cs ��������������
    [Header("����")]
    public bool ignoreHoveringPiece = true;  // ��Ӱ�� HoverMover ���ڲٿصĿ�
    public LayerMask excludeLayers;          // ����ѡ���� LayerMask �����ų�


    // ����̬
    float _timer;
    Vector2 _aabbMin, _aabbMax;
    BoxCollider2D _box;

    void Awake()
    {
        _box = GetComponent<BoxCollider2D>();
        _box.isTrigger = true;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// ����һ��磺�������ġ��ߴ硢����ʱ��������
    /// </summary>
    public void Play(Vector2 worldCenter, Vector2 size, Vector2 windDir, float mainDuration)
    {
        transform.position = worldCenter;
        dir = windDir.sqrMagnitude > 0.0001f ? windDir.normalized : Vector2.right;
        duration = Mathf.Max(0.01f, mainDuration);

        // ���ô�������С
        _box.size = size;

        // ����AABB���ں����һ��
        Vector2 half = size * 0.5f;
        _aabbMin = worldCenter - half;
        _aabbMax = worldCenter + half;

        _timer = 0f;

        if (windFx) windFx.Play(true);
        if (audioSrc) audioSrc.Play();

        enabled = true;
        gameObject.SetActive(true);
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // ��ʱ�Զ�ͣ
        if (_timer >= duration)
        {
            if (windFx && windFx.isPlaying && windFx.main.loop == false)
                windFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (audioSrc && audioSrc.isPlaying)
                audioSrc.Stop();

            enabled = false;
            // �����٣���ϵͳ����/����
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // ���ų� layer
        if (((1 << other.gameObject.layer) & excludeLayers.value) != 0) return;

        // ֻ���õ�Ŀ���
        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        var rb = other.attachedRigidbody;
        if (!rb) return;

        // �� �ؼ���������ͣ�еĻ�ľ���� HoverMover ����Ķ����ԣ�
        if (ignoreHoveringPiece)
        {
            if (other.GetComponentInParent<HoverMover>() != null)
                return;
        }

        // ʱ��˥��
        float t = Mathf.Clamp01(_timer / Mathf.Max(0.0001f, duration));
        float kTime = strengthOverTime != null ? strengthOverTime.Evaluate(t) : 1f;

        // ����λ��˥�������ݸ��������ڷ���AABB�����xλ��
        float nx = Mathf.InverseLerp(_aabbMin.x, _aabbMax.x, rb.worldCenterOfMass.x);
        float kDist = horizontalFalloff != null ? horizontalFalloff.Evaluate(nx) : 1f;

        Vector2 force = dir * baseStrength * kTime * kDist;
        rb.AddForce(force, ForceMode2D.Force);
    }
}