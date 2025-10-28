using UnityEngine;
using TMPro;
using System.Collections;

public class CountdownTimer : MonoBehaviour
{
    [Header("Basic")]
    public float durationSeconds = 180f;
    public bool autoStart = true;
    public bool useScaledTime = false;

    [Header("UI")]
    public TextMeshProUGUI timeLabel;
    public float warningThreshold = 10f;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;
    public float normalScale = 1f;

    [Header("Per-Second Double Pulse (Big -> Small)")]
    public float bigMax = 1.40f;
    public float bigUp = 0.10f, bigDown = 0.10f;
    public float smallMax = 1.18f;
    public float smallUp = 0.06f, smallDown = 0.06f;

    [Header("Audio (Looped Tick in Warning)")]
    public AudioSource audioSource;
    public AudioClip tickLoopClip;

    [Header("BGM Control")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;


    // --- state ---
    float _remain;
    bool _running;
    bool _inWarningPrev;
    bool _loopPlaying;
    int _lastWhole = -1;
    Vector3 _baseScale = Vector3.one;
    Coroutine _pulseCo;
    float _pulseScale = 1f;

    // --- 兼容旧接口：供其他脚本读取 ---
    public float RemainingSeconds => _remain;
    public float ElapsedSeconds => Mathf.Max(0f, durationSeconds - _remain);
    public float Progress01 => durationSeconds > 0f ? 1f - (_remain / durationSeconds) : 1f;

    void Awake()
    {
        _remain = Mathf.Max(0f, durationSeconds);
        if (timeLabel) _baseScale = timeLabel.rectTransform.localScale;
        UpdateLabel(false);
        StopTickLoop();
    }

    void Start() { if (autoStart) StartTimer(); }

    void Update()
    {
        if (!_running) return;

        float dt = useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
        _remain -= dt;

        if (_remain <= 0f)
        {
            _remain = 0f;
            _running = false;
            UpdateLabel(false);
            StopTickLoop();
            StopPulse();
            ApplyScale(1f);

            // 仅在彻底结束时停 BGM
            if (bgmSource != null && bgmSource.isPlaying)
                bgmSource.Stop();

            // 新增：时间到 → 触发结算
            var mgr = FindObjectOfType<GameEndManager>();
            if (mgr != null) mgr.GameOver();
            else FindObjectOfType<PostGameCounter>()?.StartOnGameOver();
            return;
        }

        bool inWarning = _remain <= warningThreshold;
        HandleWarningTransition(inWarning);

        int whole = Mathf.CeilToInt(_remain); // 显示的整秒
        if (inWarning && whole != _lastWhole)
        {
            _lastWhole = whole;
            StartDoublePulse(); // 每秒触发：大→小
        }
        else if (!inWarning)
        {
            _lastWhole = -1;
        }

        UpdateLabel(inWarning);
    }

    // --- UI ---
    void UpdateLabel(bool inWarning)
    {
        if (!timeLabel) return;
        int t = Mathf.CeilToInt(_remain);
        timeLabel.text = $"{t / 60:00}:{t % 60:00}";
        timeLabel.color = inWarning ? warningColor : normalColor;
    }

    // --- Scale ---
    void ApplyScale(float scaleMul)
    {
        if (!timeLabel) return;
        timeLabel.rectTransform.localScale = _baseScale * (normalScale * scaleMul);
    }

    // --- Pulse (Big -> Small) ---
    void StartDoublePulse()
    {
        StopPulse();
        _pulseCo = StartCoroutine(CoDoublePulse());
    }
    void StopPulse()
    {
        if (_pulseCo != null) StopCoroutine(_pulseCo);
        _pulseCo = null;
        _pulseScale = 1f;
        ApplyScale(1f);
    }

    IEnumerator CoDoublePulse()
    {
        yield return CoOnePulse(bigMax, bigUp, bigDown);
        yield return CoOnePulse(smallMax, smallUp, smallDown);
        _pulseScale = 1f;
        ApplyScale(1f);
    }

    IEnumerator CoOnePulse(float max, float up, float down)
    {
        float t = 0f;
        // 放大
        while (t < up && up > 0f)
        {
            t += useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / up);
            _pulseScale = Mathf.Lerp(1f, max, SmoothStep(p));
            ApplyScale(_pulseScale);
            yield return null;
        }
        // 缩回
        t = 0f;
        while (t < down && down > 0f)
        {
            t += useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / down);
            _pulseScale = Mathf.Lerp(max, 1f, SmoothStep(p));
            ApplyScale(_pulseScale);
            yield return null;
        }
        _pulseScale = 1f;
        ApplyScale(1f);
    }

    static float SmoothStep(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // --- Warning audio loop ---
    void HandleWarningTransition(bool inWarningNow)
    {
        if (inWarningNow && !_inWarningPrev) StartTickLoop();
        if (_inWarningPrev && !inWarningNow)
        {
            StopTickLoop();
            StopPulse();
        }
        _inWarningPrev = inWarningNow;
    }

    void StartTickLoop()
    {
        if (_loopPlaying || audioSource == null || tickLoopClip == null) return;
        audioSource.loop = true;
        audioSource.clip = tickLoopClip;
        audioSource.Play();
        _loopPlaying = true;
    }
    void StopTickLoop()
    {
        if (audioSource != null && _loopPlaying) audioSource.Stop();
        _loopPlaying = false;
    }

    // --- Controls ---
    public void StartTimer()
    {
        _remain = Mathf.Max(0f, durationSeconds);
        _running = true;
        _lastWhole = -1;
        StopTickLoop();
        StopPulse();
        UpdateLabel(false);
        ApplyScale(1f);
        // 只要开始计时，就播放 BGM（不要在别处暂停它）
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            if (!bgmSource.isPlaying) bgmSource.Play();
        }
    }
    public void ResetTo(float seconds)
    {
        durationSeconds = Mathf.Max(0f, seconds);
        _remain = durationSeconds;
        _running = false;
        _lastWhole = -1;
        StopTickLoop();
        StopPulse();
        UpdateLabel(false);
        ApplyScale(1f);
    }
    public void Pause()
    {
        _running = false;
        // 仍然建议停掉 warning 的 tick 循环
        StopTickLoop();
        // 不要 Pause/Stop bgmSource
    }

    public void Resume()
    {
        if (_remain > 0f)
        {
            _running = true;
            if (_remain <= warningThreshold) StartTickLoop();
            // 不要 UnPause/Play bgmSource（它一直在播）
        }
    }

    void OnDisable() { StopTickLoop(); StopPulse(); }
    void OnDestroy() { StopTickLoop(); StopPulse(); }
}
