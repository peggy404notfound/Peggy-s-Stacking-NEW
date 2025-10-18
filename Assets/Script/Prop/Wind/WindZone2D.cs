using UnityEngine;

/// <summary>
/// WindZone2D�����+��Ч��
/// - ����Ŀ�����壨�㶨������
/// - ���� mainDuration �루�� WindGustSystem ���ƣ�
/// - ��ѡ���Ƿ���ԡ�������ͣ������һ�飨HoverMover.IsHovering��
/// - ����ʱ����һ�η���������ʱ�Զ�ͣ
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class WindZone2D : MonoBehaviour
{
    [Header("���򣨵�λ������")]
    public Vector2 dir = Vector2.right;  // (1,0)=�Ҵ���(-1,0)=��

    [Header("Ӱ�����")]
    public LayerMask targetLayers;            // Ҫ���Ĳ㣨��ľ��
    public bool onlyAffectWhenMoving = false; // ֻ�����еĸ���

    [Header("��������")]
    public float duration = 3f;               // ����ʱ��
    public float baseStrength = 120f;         // ���ȣ���֡ʩ�ӣ�

    [Header("��Ч")]
    public AudioSource audioSrc;              // ��Դ���ɿգ�
    public AudioClip windClip;                // ������Ч

    [Header("����")]
    public bool ignoreHoveringPiece = true;   // �Ƿ���ԡ���ǰ��ͣ������һ��

    private float _timer;
    private BoxCollider2D _box;
    private Vector2 _aabbMin, _aabbMax;

    void Awake()
    {
        _box = GetComponent<BoxCollider2D>();
        _box.isTrigger = true;
        gameObject.SetActive(false);
    }

    /// <summary>��������</summary>
    public void Play(Vector2 worldCenter, Vector2 size, Vector2 windDir, float mainDuration)
    {
        transform.position = worldCenter;
        dir = windDir.normalized;
        duration = mainDuration;

        _box.size = size;

        Vector2 half = size * 0.5f;
        _aabbMin = worldCenter - half;
        _aabbMax = worldCenter + half;

        _timer = 0f;
        enabled = true;
        gameObject.SetActive(true);

        // ���ŷ���
        if (audioSrc && windClip)
        {
            audioSrc.clip = windClip;
            audioSrc.loop = true;
            audioSrc.Play();
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= duration)
        {
            StopWind();
        }
    }

    private void StopWind()
    {
        // ֹͣ��Ч
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();

        enabled = false;
        gameObject.SetActive(false);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // Ŀ������
        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;  // �Ȱ���ɸ��
        var rb = other.attachedRigidbody;
        if (!rb) return;
        if (rb.bodyType != RigidbodyType2D.Dynamic) return; // ֻ�� Dynamic ����

        // �����ԡ�������ͣ������һ�����飨�������д� HoverMover �ģ�
        // HoverMover ��¶�� IsHovering �ɶ�����
        if (ignoreHoveringPiece)
        {
            var hm = other.GetComponent<HoverMover>(); // ֻ���������鸸��������������
            if (hm != null && hm.enabled && hm.IsHovering) return;  // �����������ͣ�вź���
        }

        // ��ѡ��ֻӰ���ƶ��еĸ���
        if (onlyAffectWhenMoving && rb.velocity.sqrMagnitude < 0.0004f) return;

        // �� AABB ���ж�������ײ������/λ�ã�
        Vector2 p = rb.worldCenterOfMass;
        if (p.x < _aabbMin.x || p.x > _aabbMax.x || p.y < _aabbMin.y || p.y > _aabbMax.y) return;

        // ʩ�ӳ���������֡��
        rb.AddForce(dir * baseStrength, ForceMode2D.Force);
    }
}