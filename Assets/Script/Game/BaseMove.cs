using UnityEngine;

/// <summary>
/// 在游戏最后30秒让平台左右移动（物理友好版）
/// 挂在 Base 上，要求 Base 有 Rigidbody2D（Kinematic）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BaseMove : MonoBehaviour
{
    [Header("引用")]
    public CountdownTimer timer;         // 拖入你的倒计时脚本
    [Header("触发设置")]
    public float triggerSeconds = 30f;   // 最后30秒开始移动
    public float amplitude = 1.2f;       // 左右摆动幅度（单位：世界坐标）
    public float speedHz = 0.6f;         // 摆动频率（每秒往返次数）
    public float fadeInSeconds = 2f;     // 从静止到全幅度的过渡时间
    [Header("时间源（与计时保持一致）")]
    public bool useScaledTime = false;   // 如果计时器不随 TimeScale 缩放，这里也设为 false

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