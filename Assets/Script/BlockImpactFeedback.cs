using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BlockImpactFeedback : MonoBehaviour
{
    [Header("触发层（底座/积木等）")]
    public LayerMask impactLayers;

    [Header("音效设置")]
    public AudioClip impactClipFirst;        // 首次接触音效
    [Range(0f, 1f)] public float firstVolume = 0.6f;
    public AudioClip impactClipSubsequent;   // 未落稳时的再次碰撞音效
    [Range(0f, 1f)] public float subsequentVolume = 0.5f;

    [Header("抖动（仅首次接触）")]
    public float shakeAmplitude = 0.03f;
    public float shakeDuration = 0.08f;

    [Header("判定参数")]
    public bool alwaysPlayFirstHit = true;  // 首次接触必播音（不看速度）
    public float minImpactSpeed = 0.6f;  // 仅用于“再次碰撞”的过滤
    public float settleSpeed = 0.15f; // 落稳判定：速度阈值
    public float settleTime = 0.25f; // 落稳判定：持续时间

    [Header("未落稳期间的二次碰撞冷却")]
    public float secondaryCooldown = 0.10f;

    [Header("调试")]
    public bool debugLog = false;

    private AudioSource _as;
    private Rigidbody2D _rb;

    private bool _firstHitDone = false;   // 是否发生过首次有效接触
    private bool _settled = false;   // 是否已落稳
    private float _settleTimer = 0f;
    private float _lastSubHitTime = -999f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        // 自动创建并配置 AudioSource（无需手动挂）
        _as = GetComponent<AudioSource>();
        if (_as == null)
        {
            _as = gameObject.AddComponent<AudioSource>();
            _as.hideFlags = HideFlags.HideInInspector;
        }
        _as.playOnAwake = false;
        _as.loop = false;
        _as.spatialBlend = 0f; // 2D
        _as.volume = 1f; // 实际音量由 PlayOneShot 控制
    }

    void Update()
    {
        // 只有“发生过首次接触”以后，才开始落稳计时
        if (_settled || !_firstHitDone) return;

        if (_rb.velocity.magnitude < settleSpeed)
        {
            _settleTimer += Time.deltaTime;
            if (_settleTimer >= settleTime)
            {
                _settled = true;
                if (debugLog) Debug.Log($"{name}: 已落稳，后续不再播放普通落地音。");
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

    void OnTriggerEnter2D(Collider2D other) // 兼容 isTrigger 的底座/墙
    {
        float approxSpeed = _rb ? _rb.velocity.magnitude : 0f;
        HandleImpact(other, approxSpeed, "Trigger");
    }

    void HandleImpact(Collider2D other, float speed, string kind)
    {
        if (_settled) return;

        // 层过滤：只对指定层触发
        if ((impactLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            if (debugLog) Debug.Log($"{name}: {kind} 命中层 {LayerMask.LayerToName(other.gameObject.layer)} 不在 impactLayers，忽略。");
            return;
        }

        if (!_firstHitDone)
        {
            // 首次接触：无视速度阈值，必播音 + 抖动
            if (impactClipFirst) _as.PlayOneShot(impactClipFirst, firstVolume);
            if (CameraShake2D.I) CameraShake2D.I.Shake(shakeAmplitude, shakeDuration);
            _firstHitDone = true;
            if (debugLog) Debug.Log($"{name}: 首次{kind}（speed={speed:F2}），播放首碰音效+抖动。");
        }
        else
        {
            // 未落稳期间的再次碰撞：用速度阈值过滤，只播二次音效（无抖动）
            if (speed < minImpactSpeed)
            {
                if (debugLog) Debug.Log($"{name}: {kind} 再次撞击 speed={speed:F2} < {minImpactSpeed:F2}，忽略。");
                return;
            }
            if (Time.time - _lastSubHitTime >= secondaryCooldown)
            {
                if (impactClipSubsequent) _as.PlayOneShot(impactClipSubsequent, subsequentVolume);
                _lastSubHitTime = Time.time;
                if (debugLog) Debug.Log($"{name}: {kind} 再次撞击（speed={speed:F2}），播放二次音效。");
            }
        }
    }
}