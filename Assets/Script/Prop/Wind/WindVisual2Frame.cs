using System.Collections;
using UnityEngine;

/// <summary>
/// 用两帧图片做一个简易风动画（不含物理）
/// - 自动在 warn/main/tail 三段时间内：淡入→循环切帧→淡出
/// - 会根据风向左右翻转或旋转
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WindVisual2Frame : MonoBehaviour
{
    [Header("两帧贴图")]
    public Sprite frameA;
    public Sprite frameB;

    [Header("动画参数")]
    public float fps = 10f;             // 切帧速度
    public bool rotateByDirection = false; // false=水平+左右翻转；true=按方向旋转

    [Header("淡入淡出")]
    public float fadeIn = 0.15f;
    public float fadeOut = 0.2f;

    [Header("轻微位移（营造流动感，可为0）")]
    public float driftSpeed = 2.5f;     // 世界坐标移动速度（单位/秒）
    public float driftAmplitudeY = 0.15f;

    SpriteRenderer _sr;
    Coroutine _co;
    Vector3 _startPos;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.color = new Color(1, 1, 1, 0); // 初始透明
    }

    public void Play(Vector2 center, Vector2 dir, float warnTime, float mainTime, float tailTime)
    {
        if (_co != null) StopCoroutine(_co);
        transform.position = center;
        _startPos = transform.position;

        // 朝向
        if (rotateByDirection)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, ang);
            _sr.flipX = false;
        }
        else
        {
            transform.rotation = Quaternion.identity;
            _sr.flipX = (dir.x < 0f); // 右→左时水平翻转
        }

        _co = StartCoroutine(Co_Run(warnTime, mainTime, tailTime));
    }

    IEnumerator Co_Run(float warnTime, float mainTime, float tailTime)
    {
        // 淡入（在 warn 段执行）
        float t = 0f;
        while (t < fadeIn + Mathf.Max(0f, warnTime - fadeIn))
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeIn));
            SetAlpha(a);
            DoDrift();
            yield return null;
        }

        // 主循环（mainTime 内以 fps 交替切帧）
        float timeMain = 0f;
        float frameTimer = 0f;
        bool toggle = false;
        while (timeMain < mainTime)
        {
            timeMain += Time.deltaTime;
            frameTimer += Time.deltaTime;

            if (frameTimer >= 1f / Mathf.Max(1f, fps))
            {
                frameTimer = 0f;
                toggle = !toggle;
                _sr.sprite = toggle ? frameA : frameB;
            }

            DoDrift();
            yield return null;
        }

        // 淡出（tail 段）
        float t2 = 0f;
        Color c = _sr.color;
        while (t2 < Mathf.Max(fadeOut, tailTime))
        {
            t2 += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t2 / Mathf.Max(0.0001f, fadeOut));
            SetAlpha(a);
            DoDrift();
            yield return null;
        }

        gameObject.SetActive(false);
    }

    void DoDrift()
    {
        if (driftSpeed == 0f && driftAmplitudeY == 0f) return;
        var p = _startPos;
        p.x += Time.time * driftSpeed;
        p.y += Mathf.Sin(Time.time * 4f) * driftAmplitudeY;
        transform.position = p;
    }

    void SetAlpha(float a)
    {
        var c = _sr.color; c.a = a; _sr.color = c;
    }
}