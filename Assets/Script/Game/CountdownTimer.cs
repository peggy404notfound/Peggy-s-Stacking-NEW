using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class CountdownTimer : MonoBehaviour
{
    [Header("��������")]
    [Tooltip("����ʱ��ʱ�����룩��3����=180��")]
    public float durationSeconds = 180f;

    [Tooltip("����ʱ�Զ���ʼ����ʱ")]
    public bool autoStart = true;

    [Tooltip("ʹ������ʱ�䣨Time.deltaTime�����ǲ�����ʱ�䣨Time.unscaledDeltaTime����\n�������޸� Time.timeScale ����رմ�����ò�����ʱ�䣬�����ʱƯ�ơ�")]
    public bool useScaledTime = false;

    [Header("UI ��ʾ����ѡ��")]
    public TextMeshProUGUI timeLabel;

    [Tooltip("���ڸ��������뾯����ʾ")]
    public float warningThreshold = 10f;

    public bool enableWarningColor = true;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;

    [System.Serializable] public class FloatEvent : UnityEvent<float> { } // ����ʣ������
    [Header("�¼��ص�")]
    public FloatEvent onTick;      // ÿ֡����������=ʣ������
    public UnityEvent onTimesUp;   // ʱ�䵽����һ��

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

        int total = Mathf.CeilToInt(_remaining); // ����ȡ������ʣ 59.3 ��ʾ 01:00 -> 00:59
        int m = total / 60;
        int s = total % 60;
        timeLabel.text = $"{m:00}:{s:00}";

        if (enableWarningColor)
        {
            timeLabel.color = (_remaining <= warningThreshold) ? warningColor : normalColor;
        }
    }

    // ���� ������� API ����
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