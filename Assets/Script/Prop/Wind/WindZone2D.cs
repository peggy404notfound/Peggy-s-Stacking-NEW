using UnityEngine;

/// <summary>
/// WindZone2D（简版+音效）
/// - 吹动目标层刚体（恒定风力）
/// - 持续 mainDuration 秒（由 WindGustSystem 控制）
/// - 可选择是否忽略“正在悬停”的那一块（HoverMover.IsHovering）
/// - 启动时播放一次风声，结束时自动停
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class WindZone2D : MonoBehaviour
{
    [Header("方向（单位向量）")]
    public Vector2 dir = Vector2.right;  // (1,0)=右吹；(-1,0)=左吹

    [Header("影响对象")]
    public LayerMask targetLayers;            // 要吹的层（积木）
    public bool onlyAffectWhenMoving = false; // 只吹动中的刚体

    [Header("风力参数")]
    public float duration = 3f;               // 持续时间
    public float baseStrength = 120f;         // 力度（按帧施加）

    [Header("音效")]
    public AudioSource audioSrc;              // 音源（可空）
    public AudioClip windClip;                // 风声音效

    [Header("过滤")]
    public bool ignoreHoveringPiece = true;   // 是否忽略“当前悬停”的那一块

    private float _timer;
    private BoxCollider2D _box;
    private Vector2 _aabbMin, _aabbMax;

    void Awake()
    {
        _box = GetComponent<BoxCollider2D>();
        _box.isTrigger = true;
        gameObject.SetActive(false);
    }

    /// <summary>启动风区</summary>
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

        // 播放风声
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
        // 停止音效
        if (audioSrc && audioSrc.isPlaying) audioSrc.Stop();

        enabled = false;
        gameObject.SetActive(false);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // 目标层过滤
        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;  // 先按层筛掉
        var rb = other.attachedRigidbody;
        if (!rb) return;
        if (rb.bodyType != RigidbodyType2D.Dynamic) return; // 只对 Dynamic 加力

        // 仅忽略“正在悬停”的那一个方块（不是所有带 HoverMover 的）
        // HoverMover 暴露了 IsHovering 可读属性
        if (ignoreHoveringPiece)
        {
            var hm = other.GetComponent<HoverMover>(); // 只查自身，不查父链以免误伤整塔
            if (hm != null && hm.enabled && hm.IsHovering) return;  // 仅当真的在悬停中才忽略
        }

        // 可选：只影响移动中的刚体
        if (onlyAffectWhenMoving && rb.velocity.sqrMagnitude < 0.0004f) return;

        // 简单 AABB 内判定（用碰撞体中心/位置）
        Vector2 p = rb.worldCenterOfMass;
        if (p.x < _aabbMin.x || p.x > _aabbMax.x || p.y < _aabbMin.y || p.y > _aabbMax.y) return;

        // 施加持续力（按帧）
        rb.AddForce(dir * baseStrength, ForceMode2D.Force);
    }
}