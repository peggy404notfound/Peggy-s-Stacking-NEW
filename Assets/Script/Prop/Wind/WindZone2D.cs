using UnityEngine;

/// <summary>
/// 风区触发器（2D）：在区域内持续对命中的刚体施加随时间与横向位置衰减的力
/// 依赖：BoxCollider2D（IsTrigger=true）
/// 可选：ParticleSystem、AudioSource 放到同物体上
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class WindZone2D : MonoBehaviour
{
    [Header("方向（单位向量）")]
    public Vector2 dir = Vector2.right;

    [Header("影响对象")]
    public LayerMask targetLayers;
    public bool onlyAffectWhenMoving = true;    // 近似“只吹不稳/在动的块”

    [Header("强度与时长")]
    public float duration = 1.1f;               // 强风时长
    public float baseStrength = 35f;            // 主强度（按质量/重力调）
    public AnimationCurve strengthOverTime = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("横向距离衰减（0=左边界，1=右边界）")]
    public AnimationCurve horizontalFalloff = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("可选视觉/音效")]
    public ParticleSystem windFx;
    public AudioSource audioSrc;

    // WindZone2D.cs 顶部参数区新增
    [Header("过滤")]
    public bool ignoreHoveringPiece = true;  // 不影响 HoverMover 正在操控的块
    public LayerMask excludeLayers;          // （可选）用 LayerMask 额外排除


    // 运行态
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
    /// 播放一阵风：设置中心、尺寸、方向、时长并启动
    /// </summary>
    public void Play(Vector2 worldCenter, Vector2 size, Vector2 windDir, float mainDuration)
    {
        transform.position = worldCenter;
        dir = windDir.sqrMagnitude > 0.0001f ? windDir.normalized : Vector2.right;
        duration = Mathf.Max(0.01f, mainDuration);

        // 设置触发器大小
        _box.size = size;

        // 计算AABB用于横向归一化
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

        // 到时自动停
        if (_timer >= duration)
        {
            if (windFx && windFx.isPlaying && windFx.main.loop == false)
                windFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (audioSrc && audioSrc.isPlaying)
                audioSrc.Stop();

            enabled = false;
            // 不销毁，由系统回收/复用
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // 先排除 layer
        if (((1 << other.gameObject.layer) & excludeLayers.value) != 0) return;

        // 只作用到目标层
        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        var rb = other.attachedRigidbody;
        if (!rb) return;

        // ★ 关键：跳过悬停中的积木（有 HoverMover 组件的都忽略）
        if (ignoreHoveringPiece)
        {
            if (other.GetComponentInParent<HoverMover>() != null)
                return;
        }

        // 时间衰减
        float t = Mathf.Clamp01(_timer / Mathf.Max(0.0001f, duration));
        float kTime = strengthOverTime != null ? strengthOverTime.Evaluate(t) : 1f;

        // 横向位置衰减：根据刚体质心在风区AABB的相对x位置
        float nx = Mathf.InverseLerp(_aabbMin.x, _aabbMax.x, rb.worldCenterOfMass.x);
        float kDist = horizontalFalloff != null ? horizontalFalloff.Evaluate(nx) : 1f;

        Vector2 force = dir * baseStrength * kTime * kDist;
        rb.AddForce(force, ForceMode2D.Force);
    }
}