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
    public float bigMax = 1.15f;
    public float bigUp = 0.2f, bigDown = 0.2f;
    public float smallMax = 1.05f;
    public float smallUp = 0.06f, smallDown = 0.06f;

    [Header("Audio (Looped Tick in Warning)")]
    public AudioSource audioSource;
    public AudioClip tickLoopClip;

    [Header("BGM Control")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;

    // === New: Inspector-adjustable BGM volume ===
    [Range(0f, 1f)]
    public float bgmVolume = 1f;

    // --- state ---
    float _remain;
    bool _running;
    bool _inWarningPrev;
    bool _loopPlaying;
    int _lastWhole = -1;
    Vector3 _baseScale = Vector3.one;
    Coroutine _pulseCo;
    float _pulseScale = 1f;

    // === Back-compat for other scripts ===
    public float RemainingSeconds => _remain;

    void Awake()
    {
        if (bgmSource != null)
        {
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        // 若误把 tick 和 bgm 指到同一个 AudioSource，自动为 tick 创建专用 Source
        if (audioSource != null && audioSource == bgmSource)
        {
            Debug.LogWarning("CountdownTimer: 'audioSource' (tick) 与 'bgmSource' 相同，已为 tick 创建专用 AudioSource。");
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        _remain = Mathf.Max(0f, durationSeconds);
        if (timeLabel) _baseScale = timeLabel.rectTransform.localScale;
        UpdateLabel(false);
        StopTickLoop();

        // apply initial bgm volume
        ApplyBgmVolume();
    }

    void Start()
    {
        if (autoStart) StartTimer();
    }

    void Update()
    {
        if (_running)
        {
            // reflect runtime volume tweaks from Inspector
            ApplyBgmVolume();

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

                if (bgmSource != null && bgmSource.isPlaying)
                    bgmSource.Stop();

                var mgr = FindObjectOfType<GameEndManager>();
                if (mgr != null) mgr.GameOver();
                else FindObjectOfType<PostGameCounter>()?.StartOnGameOver();
                return;
            }

            bool inWarning = _remain <= warningThreshold;
            HandleWarningTransition(inWarning);

            int whole = Mathf.CeilToInt(_remain);
            if (inWarning && whole != _lastWhole)
            {
                _lastWhole = whole;
                StartDoublePulse();
            }
            else if (!inWarning)
            {
                _lastWhole = -1;
            }

            UpdateLabel(inWarning);
        }
        else
        {
            // still apply volume when paused so slider is always responsive
            ApplyBgmVolume();
        }
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
        // enlarge
        while (t < up && up > 0f)
        {
            t += useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / up);
            _pulseScale = Mathf.Lerp(1f, max, SmoothStep(p));
            ApplyScale(_pulseScale);
            yield return null;
        }
        // shrink back
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

        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            ApplyBgmVolume();
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
        StopTickLoop();
    }

    public void Resume()
    {
        if (_remain > 0f)
        {
            _running = true;
            if (_remain <= warningThreshold) StartTickLoop();
        }
    }

    void OnDisable() { StopTickLoop(); StopPulse(); }
    void OnDestroy() { StopTickLoop(); StopPulse(); }

    // === volume helper ===
    void ApplyBgmVolume()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = Mathf.Clamp01(bgmVolume);
        }
    }
}