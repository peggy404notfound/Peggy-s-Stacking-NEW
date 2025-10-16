using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class CountdownTimer : MonoBehaviour
{
    [Header("基础设置")]
    [Tooltip("倒计时总时长（秒）。3分钟=180秒")]
    public float durationSeconds = 180f;

    [Tooltip("启动时自动开始倒计时")]
    public bool autoStart = true;

    [Tooltip("使用缩放时间（Time.deltaTime）还是不缩放时间（Time.unscaledDeltaTime）。\n如果你会修改 Time.timeScale 建议关闭此项，改用不缩放时间，避免计时漂移。")]
    public bool useScaledTime = false;

    [Header("UI 显示（可选）")]
    public TextMeshProUGUI timeLabel;

    [Tooltip("低于该秒数进入警告显示")]
    public float warningThreshold = 10f;

    public bool enableWarningColor = true;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;

    [System.Serializable] public class FloatEvent : UnityEvent<float> { } // 传递剩余秒数
    [Header("事件回调")]
    public FloatEvent onTick;      // 每帧触发，参数=剩余秒数
    public UnityEvent onTimesUp;   // 时间到触发一次

    public float RemainingSeconds => _remaining;
    public float ElapsedSeconds => Mathf.Clamp(durationSeconds - _remaining, 0f, durationSeconds);
    public float Progress01 => durationSeconds > 0f ? 1f - (_remaining / durationSeconds) : 1f;

    float _remaining;
    bool _running;

    void Awake()
    {
        _remaining = Mathf.Max(0f, durationSeconds);
        UpdateLabel();
    }

    void Start()
    {
        if (autoStart) StartTimer();
    }

    void Update()
    {
        if (!_running || _remaining <= 0f) return;

        float dt = useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
        _remaining -= dt;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            UpdateLabel();
            onTick?.Invoke(_remaining);
            _running = false;
            onTimesUp?.Invoke();
            return;
        }

        UpdateLabel();
        onTick?.Invoke(_remaining);
    }

    void UpdateLabel()
    {
        if (timeLabel == null) return;

        int total = Mathf.CeilToInt(_remaining); // 向上取整：还剩 59.3 显示 01:00 -> 00:59
        int m = total / 60;
        int s = total % 60;
        timeLabel.text = $"{m:00}:{s:00}";

        if (enableWarningColor)
        {
            timeLabel.color = (_remaining <= warningThreshold) ? warningColor : normalColor;
        }
    }

    // —— 对外控制 API ——
    public void StartTimer()
    {
        _remaining = Mathf.Max(0f, durationSeconds);
        _running = true;
        UpdateLabel();
    }

    public void ResetTo(float seconds)
    {
        durationSeconds = Mathf.Max(0f, seconds);
        _remaining = durationSeconds;
        UpdateLabel();
    }

    public void Pause() => _running = false;
    public void Resume() { if (_remaining > 0f) _running = true; }
    public void StopAndClear() { _running = false; _remaining = 0f; UpdateLabel(); }

    public void AddSeconds(float seconds)
    {
        _remaining = Mathf.Max(0f, _remaining + seconds);
        if (_remaining > 0f) UpdateLabel();
    }
}