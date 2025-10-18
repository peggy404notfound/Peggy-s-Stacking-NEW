using UnityEngine;

/// <summary>
/// 手左右控制 + 碰撞瞬时冲击（无冲刺键）
/// 玩家1：A/D；玩家2：←/→
/// 外部可用 EnableControl(bool) / EnableControl() / DisableControl() 开关输入
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PushHand2D : MonoBehaviour
{
    [Header("玩家设定 1=P1(A/D)，2=P2(←/→)")]
    public int playerId = 1;

    [Header("基础移动")]
    public float moveSpeed = 8f;
    public float xMin = -10f;
    public float xMax = 10f;

    [Header("推力设置（撞上积木时的冲击）")]
    public LayerMask affectLayers;          // 只对这些层生效（勾选你的积木层）
    public float baseImpulse = 10f;         // 基础冲击力
    public float speedMultiplier = 2.0f;    // 冲击力随手当前速度的加成：push = base + |vx|*mult
    public float torqueAmount = 50f;        // 随机附加扭矩（翻滚感）
    public float pushCooldown = 0.15f;      // 同一只手连续推击的冷却，避免一帧多次

    [Header("物理选项")]
    public bool useContinuousCollision = true;
    public float linearDrag = 0.05f;

    [Header("控制开关（外部调用）")]
    public bool controlEnabled = true;      // 默认为可控制

    // —— 内部 —— 
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
        // 被外部关控：不读输入，停住横向速度，仅做边界钳位
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

        // 读取输入（P1: A/D；P2: ←/→）
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

        // 用速度移动（更有动量感）
        rb.velocity = new Vector2(dir * moveSpeed, 0f);

        // 边界限制
        Vector2 pos = rb.position;
        pos.x = Mathf.Clamp(pos.x, xMin, xMax);
        rb.position = pos;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < nextPushTime) return;

        Rigidbody2D other = collision.rigidbody;
        if (other == null) return;

        // 只作用于指定层
        if ((affectLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // 根据当前“手”的速度，计算一次性冲击力
        float pushPower = baseImpulse + Mathf.Abs(rb.velocity.x) * speedMultiplier;

        // 冲击方向：按手当前的移动方向
        float xSign = Mathf.Sign(rb.velocity.x);
        if (Mathf.Approximately(xSign, 0f))
        {
            // 若刚好停止，默认向右（也可用接触法线做方向）
            xSign = 1f;
        }
        Vector2 pushDir = Vector2.right * xSign;

        // 一次性爆发式冲击 + 扭矩
        other.AddForce(pushDir * pushPower, ForceMode2D.Impulse);
        other.AddTorque(Random.Range(-torqueAmount, torqueAmount), ForceMode2D.Impulse);

        nextPushTime = Time.time + pushCooldown;

        // （可选）这里可以触发音效/镜头震动/慢动作
        // AudioSource.PlayClipAtPoint(winPushSfx, Camera.main.transform.position, 1f);
        // StartCoroutine(CameraShake(0.15f, 0.2f));
        // StartCoroutine(SlowMo(0.5f, 0.6f));
    }

    // ===== 外部兼容控制方法 =====
    public void EnableControl(bool enable)
    {
        controlEnabled = enable;
        if (!enable && rb != null)
        {
            rb.velocity = Vector2.zero; // 关控时停住手，防止惯性继续顶
        }
    }

    // 兼容无参调用
    public void EnableControl() => EnableControl(true);

    // 兼容 DisableControl()
    public void DisableControl() => EnableControl(false);
}