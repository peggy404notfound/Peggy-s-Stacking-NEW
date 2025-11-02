using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class PostGameCounter : MonoBehaviour
{
    [Header("UI")]
    public GameObject timesUpPanel;
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI countALabel;
    public TextMeshProUGUI countBLabel;

    [Header("节奏（数数节奏）")]
    public float firstInterval = 0.08f;
    public float lastInterval = 0.35f;
    public int slowDownLastN = 6;
    public float betweenPlayersDelay = 0.5f;

    [Header("展示节奏")]
    [Tooltip("两边面板都滑入后，停留多少秒再进行赢家演出")]
    public float scoresHoldDuration = 3.0f;

    [Header("颜色")]
    public Color aTextColor = new Color(1f, 0.6f, 0.2f);
    public Color bTextColor = new Color(0.3f, 0.6f, 1f);

    [Header("旧字段（保留以兼容）")]
    public AudioSource sfx;            // 可留空；finishClip 会优先走 finishSource
    public AudioClip tickClip;         // 未在此脚本中使用
    public AudioClip finishClip;

    [Header("新增：结算音效 Clips")]
    public AudioClip countHighlightClip;   // 每高亮一个积木
    [Range(0f, 1f)] public float countHighlightVolume = 1f;
    public AudioClip panelSlideClip;       // 面板滑入时
    [Range(0f, 1f)] public float panelSlideVolume = 1f;
    public AudioClip winClip;              // 胜利选出时
    [Range(0f, 1f)] public float winVolume = 1f;

    [Header("新增：专用 AudioSources（建议在 Inspector 里赋值）")]
    public AudioSource countHighlightSource;
    public AudioSource panelSlideSource;
    public AudioSource winSource;
    public AudioSource finishSource;       // 播放 finishClip；可与 panelSlideSource 复用

    [Header("结果面板与皇冠")]
    public GameObject p1ResultRoot;
    public GameObject p2ResultRoot;
    public Animator p1ResultAnim;
    public Animator p2ResultAnim;
    public Animator crownAnim;

    [Header("动画名")]
    public string slideInTriggerName = "SlideIn";
    public string p1WinState = "P1Win";
    public string p2WinState = "P2Win";
    public string crownPopState = "CrownPop";

    // === Confetti 粒子 ===
    [Header("Winner Confetti")]
    public ParticleSystem confettiSystem;     // 拖入你买的彩带Prefab上的 ParticleSystem
    public bool playAfterCrown = true;        // 粒子在皇冠之后播放；关掉则在赢家动画后立刻播
    [Tooltip("从触发点到开始放彩带的延迟（非缩放时间）")]
    public float confettiDelay = 0.05f;

    [Header("时间点")]
    public float crownDelay = 0.15f;

    [Header("Winner Bonus 提示")]
    public GameObject bonusHintRoot;
    public TextMeshProUGUI bonusHintLabel;
    [TextArea] public string bonusHintText = "Press any key to get winner bonus!";

    [Header("赢家的“手”")]
    public GameObject handSpriteP1;
    public GameObject handSpriteP2;

    [Header("手的移动边界")]
    public float handMoveSpeed = 8f;
    public float handXMin = -10f;
    public float handXMax = 10f;

    [Header("可选：Bonus 时禁用的游戏根节点")]
    public GameObject gameplayRoot;

    private Coroutine _co;
    private bool _bonusActive = false;
    private int _winnerId = 0;

    void Awake()
    {
        // Animator 强制使用 UnscaledTime（双保险，Inspector 也已勾选）
        if (p1ResultAnim) p1ResultAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (p2ResultAnim) p2ResultAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (crownAnim) crownAnim.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 确保几个音源存在，且为 2D、不受全局暂停影响
        countHighlightSource = Ensure2DSfxSource(countHighlightSource, "PGC_CountHighlight_Audio");
        panelSlideSource = Ensure2DSfxSource(panelSlideSource, "PGC_PanelSlide_Audio");
        winSource = Ensure2DSfxSource(winSource, "PGC_Win_Audio");
        finishSource = Ensure2DSfxSource(finishSource, "PGC_Finish_Audio");
    }

    AudioSource Ensure2DSfxSource(AudioSource src, string goName)
    {
        if (!src)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
        }
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;          // 2D，避免距离衰减
        src.ignoreListenerPause = true; // 即使有全局暂停也能播放
        return src;
    }

    public void StartOnGameOver()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Start());
    }

    IEnumerator Co_Start()
    {
        if (timesUpPanel) timesUpPanel.SetActive(true);
        if (titleLabel) titleLabel.text = "Time's Up!";
        Debug.Log("[PGC] ===== 结算开始 =====");

        var allMarks = FindObjectsOfType<BlockMark>(includeInactive: false).ToList();
        List<BlockMark> eligible = allMarks.Where(IsEligibleForScore).ToList();

        var aList = eligible.Where(b => b.ownerPlayerId == 1)
                            .OrderBy(b => b.transform.position.y).ToList();
        var bList = eligible.Where(b => b.ownerPlayerId == 2)
                            .OrderBy(b => b.transform.position.y).ToList();

        int smA = 0, smB = 0;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RecountScores(out smA, out smB);
        else
        {
            smA = aList.Count; smB = bList.Count;
        }

        if (aList.Count > smA) aList = aList.Take(smA).ToList();
        if (bList.Count > smB) bList = bList.Take(smB).ToList();

        if (countALabel) { countALabel.color = aTextColor; countALabel.text = "0"; }
        if (countBLabel) { countBLabel.color = bTextColor; countBLabel.text = "0"; }

        // --- P1计数 ---
        yield return StartCoroutine(Co_CountOneSide(aList, countALabel, "A"));

        if (p1ResultAnim && !string.IsNullOrEmpty(slideInTriggerName))
        {
            p1ResultAnim.ResetTrigger(slideInTriggerName);
            p1ResultAnim.SetTrigger(slideInTriggerName);
        }
        if (panelSlideClip && panelSlideSource)
            panelSlideSource.PlayOneShot(panelSlideClip, panelSlideVolume);

        yield return new WaitForSecondsRealtime(betweenPlayersDelay);

        // --- P2计数 ---
        yield return StartCoroutine(Co_CountOneSide(bList, countBLabel, "B"));

        if (p2ResultAnim && !string.IsNullOrEmpty(slideInTriggerName))
        {
            p2ResultAnim.ResetTrigger(slideInTriggerName);
            p2ResultAnim.SetTrigger(slideInTriggerName);
        }
        if (panelSlideClip && panelSlideSource)
            panelSlideSource.PlayOneShot(panelSlideClip, panelSlideVolume);

        yield return new WaitForSecondsRealtime(scoresHoldDuration);

        int aScore = smA;
        int bScore = smB;
        if (aScore == bScore)
        {
            Debug.Log("[PGC] 平局，省略赢家演出。");
            yield break;
        }

        bool aWin = aScore > bScore;
        _winnerId = aWin ? 1 : 2;
        Debug.Log(aWin ? "[PGC] P1 胜出" : "[PGC] P2 胜出");

        if (aWin) { if (p2ResultRoot) p2ResultRoot.SetActive(false); }
        else { if (p1ResultRoot) p1ResultRoot.SetActive(false); }

        var winAnim = aWin ? p1ResultAnim : p2ResultAnim;
        if (winAnim) winAnim.Play(aWin ? p1WinState : p2WinState, 0, 0f);

        // 等赢家动画播完（确保不是过渡中）
        if (winAnim)
        {
            yield return new WaitUntil(() =>
                !winAnim.IsInTransition(0) &&
                winAnim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
        }

        // 若需要先播皇冠，再放粒子
        if (playAfterCrown)
        {
            // 原有 crownDelay
            if (!playAfterCrown)
            {
                yield return new WaitForSecondsRealtime(crownDelay);
                if (crownAnim)
                {
                    var go = crownAnim.gameObject;
                    if (!go.activeSelf) go.SetActive(true);
                    crownAnim.Rebind();
                    crownAnim.Play(crownPopState, 0, 0f);
                }
            }

            // 皇冠起跳后稍等一下再放彩带
            yield return new WaitForSecondsRealtime(confettiDelay);
            PlayConfetti();
        }
        else
        {
            // 不等皇冠，赢家动画后直接放彩带
            PlayConfetti();
        }


        if (winClip && winSource)
            winSource.PlayOneShot(winClip, winVolume);

        yield return new WaitForSecondsRealtime(crownDelay);

        if (crownAnim)
        {
            var go = crownAnim.gameObject;
            if (!go.activeSelf) go.SetActive(true);
            crownAnim.Rebind();
            crownAnim.Play(crownPopState, 0, 0f);
        }

        ShowBonusHint(true);
        // 注意：任何输入侦测在 Time.timeScale==0 时仍可用
        yield return new WaitUntil(() => Input.anyKeyDown);

        StopConfetti();

        if (p1ResultRoot) p1ResultRoot.SetActive(false);
        if (p2ResultRoot) p2ResultRoot.SetActive(false);
        if (timesUpPanel) timesUpPanel.SetActive(false);
        ShowBonusHint(false);
        if (crownAnim && crownAnim.gameObject.activeSelf)
            crownAnim.gameObject.SetActive(false);

        foreach (var wall in FindObjectsOfType<BrickWallPlacer>())
            wall.BeginWinnerPushWindow();

        EnterBonus(_winnerId);
        yield break;
    }

    IEnumerator Co_CountOneSide(List<BlockMark> list, TextMeshProUGUI label, string prefix)
    {
        if (label) label.text = "0";
        HighlightHalo last = null;
        int total = list.Count;

        for (int i = 0; i < total; i++)
        {
            if (last) last.SetHighlight(false);
            var bm = list[i];
            var hl = bm.GetComponent<HighlightHalo>();
            if (hl == null)
            {
                hl = bm.gameObject.AddComponent<HighlightHalo>();
                hl.scale = 1.12f;
                hl.alpha = 0.7f;
                hl.color = Color.white;
                hl.sortingOffset = +5;
            }
            last = hl;
            hl.SetHighlight(true);

            if (label) label.text = $"{i + 1}";

            // 每高亮一块播放音效（2D 源，不受距离影响）
            if (countHighlightClip && countHighlightSource)
                countHighlightSource.PlayOneShot(countHighlightClip, countHighlightVolume);

            // 统一使用非缩放时间的等待
            float t = (total <= 1) ? 1f : Mathf.InverseLerp(total - slowDownLastN, total - 1, i);
            t = Mathf.Clamp01(t);
            float wait = Mathf.Lerp(firstInterval, lastInterval, t);
            yield return new WaitForSecondsRealtime(wait);
        }

        if (last) last.SetHighlight(false);

        // 结束小音效
        if (finishClip)
        {
            if (finishSource) finishSource.PlayOneShot(finishClip);
            else if (sfx) sfx.PlayOneShot(finishClip);
        }
    }

    bool IsEligibleForScore(BlockMark bm)
    {
        foreach (var c in bm.gameObject.GetComponents<MonoBehaviour>())
        {
            var f = c.GetType().GetField("isTowerMember");
            if (f != null)
            {
                object val = f.GetValue(c);
                if (val is bool b) return b && (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2);
            }
        }
        return (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2) &&
               (bm.touchedStack || bm.hasDropped);
    }

    void ShowBonusHint(bool on)
    {
        if (bonusHintRoot) bonusHintRoot.SetActive(on);
        if (on && bonusHintLabel) bonusHintLabel.text = bonusHintText;
    }

    void EnterBonus(int winnerId)
    {
        _bonusActive = true;
        Time.timeScale = 1f;
        if (gameplayRoot) gameplayRoot.SetActive(false);

        GameObject go = (winnerId == 1) ? handSpriteP1 : handSpriteP2;
        GameObject other = (winnerId == 1) ? handSpriteP2 : handSpriteP1;

        if (other) other.SetActive(false);
        if (go)
        {
            go.SetActive(true);
            var ctrl = go.GetComponent<PushHand2D>();
            if (ctrl)
            {
                ctrl.playerId = (winnerId == 1) ? 1 : 2;
                ctrl.moveSpeed = handMoveSpeed;
                ctrl.xMin = handXMin;
                ctrl.xMax = handXMax;
                ctrl.EnableControl(true);
            }
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            var col = go.GetComponent<Collider2D>();
            if (col) col.isTrigger = false;
        }
    }

    void PlayConfetti()
    {
        if (!confettiSystem) return;
        confettiSystem.Clear(true);
        confettiSystem.Play(true);
    }

    void StopConfetti()
    {
        if (!confettiSystem) return;
        confettiSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }


    public void StopBonus()
    {
        if (!_bonusActive) return;
        _bonusActive = false;

        if (handSpriteP1)
        {
            var c = handSpriteP1.GetComponent<PushHand2D>();
            if (c) c.EnableControl(false);
            handSpriteP1.SetActive(false);
        }
        if (handSpriteP2)
        {
            var c = handSpriteP2.GetComponent<PushHand2D>();
            if (c) c.EnableControl(false);
            handSpriteP2.SetActive(false);
        }
        if (gameplayRoot) gameplayRoot.SetActive(true);
    }
}