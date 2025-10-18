using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class PostGameCounter : MonoBehaviour
{
    [Header("UI")]
    public GameObject timesUpPanel;
    public TextMeshProUGUI titleLabel;   // "Time's Up!"
    public TextMeshProUGUI countALabel;  // 只显示数字
    public TextMeshProUGUI countBLabel;  // 只显示数字

    [Header("节奏（数数节奏）")]
    public float firstInterval = 0.08f;
    public float lastInterval = 0.35f;
    public int slowDownLastN = 6;
    public float betweenPlayersDelay = 0.5f;

    [Header("展示节奏（新增）")]
    [Tooltip("两边面板都滑入后，停留多少秒再进行赢家演出")]
    public float scoresHoldDuration = 3.0f;   // <- 你要的“停 3 秒”

    [Header("颜色")]
    public Color aTextColor = new Color(1f, 0.6f, 0.2f);
    public Color bTextColor = new Color(0.3f, 0.6f, 1f);

    [Header("音效（可选）")]
    public AudioSource sfx;
    public AudioClip tickClip;
    public AudioClip finishClip;

    // —— 面板 & 皇冠 ——
    [Header("结果面板与皇冠")]
    public GameObject p1ResultRoot;    // 用于“输家立刻消失”
    public GameObject p2ResultRoot;
    public Animator p1ResultAnim;     // 含 SlideIn / P1Win
    public Animator p2ResultAnim;     // 含 SlideIn / P2Win
    public Animator crownAnim;        // 共享皇冠 Animator（默认态 Idle，含 CrownPop）

    [Header("动画名")]
    public string slideInTriggerName = "SlideIn";
    public string p1WinState = "P1Win";
    public string p2WinState = "P2Win";
    public string crownPopState = "CrownPop";

    [Header("时间点")]
    public float crownDelay = 0.15f;   // 赢家Win后皇冠延迟

    // === 新增：Bonus 提示与手 ===
    [Header("Winner Bonus 提示（可选）")]
    public GameObject bonusHintRoot;              // 放一行 "Press any key to get winner bonus!"
    public TextMeshProUGUI bonusHintLabel;
    [TextArea] public string bonusHintText = "Press any key to get winner bonus!";

    [Header("赢家的“手”（场景里预放，默认隐藏）")]
    public GameObject handSpriteP1;   // 赢家= P1 时启用
    public GameObject handSpriteP2;   // 赢家= P2 时启用

    [Header("手的移动边界（传给 PushHand2D，或留它默认）")]
    public float handMoveSpeed = 8f;
    public float handXMin = -10f;
    public float handXMax = 10f;

    [Header("可选：Bonus 时禁用的游戏根节点")]
    public GameObject gameplayRoot;   // 拖 Game 节点进来

    private Coroutine _co;
    private bool _bonusActive = false; // === 新增
    private int _winnerId = 0;         // === 新增

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

        // 1) 收集场景里“可参与计分”的 BlockMark
        var allMarks = FindObjectsOfType<BlockMark>(includeInactive: false).ToList();

        // 过滤：只要 P1/P2，且排除悬停（未触地/未入塔）与掉出场外的
        // 规则：优先以 isTowerMember 为准；否则要求 (touchedStack || hasDropped)
        List<BlockMark> eligible = allMarks.Where(IsEligibleForScore).ToList();

        // 分边 + 视觉排序（从低到高，便于“由慢到快”高亮）
        var aList = eligible.Where(b => b.ownerPlayerId == 1)
                            .OrderBy(b => b.transform.position.y).ToList();
        var bList = eligible.Where(b => b.ownerPlayerId == 2)
                            .OrderBy(b => b.transform.position.y).ToList();

        Debug.Log($"[PGC] 预筛选完成：A候选={aList.Count}，B候选={bList.Count}");

        // 2) 用 ScoreManager 作为“权威分数”，并以它裁剪列表长度，确保显示=真实分
        int smA = 0, smB = 0;
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RecountScores(out smA, out smB);   // 权威统计
            Debug.Log($"[PGC] ScoreManager：A={smA}，B={smB}（作为最终分数）");
        }
        else
        {
            // 如果没有 ScoreManager，就退化到当前列表数量
            smA = aList.Count; smB = bList.Count;
            Debug.LogWarning("[PGC] 未找到 ScoreManager，使用预筛选数量作为分数。");
        }

        // 把用于“逐个高亮”的列表长度裁剪到权威分（避免把悬停/出界算进去）
        if (aList.Count > smA) aList = aList.Take(smA).ToList();
        if (bList.Count > smB) bList = bList.Take(smB).ToList();

        // 初始化 UI 文本（只数字）
        if (countALabel) { countALabel.color = aTextColor; countALabel.text = "0"; }
        if (countBLabel) { countBLabel.color = bTextColor; countBLabel.text = "0"; }

        // 3) —— P1：先计数，再滑入 ——
        Debug.Log("[PGC] 开始计数 Player A ...");
        yield return StartCoroutine(Co_CountOneSide(aList, countALabel, "A"));
        Debug.Log("[PGC] Player A 计数完成，触发面板滑入。");

        if (p1ResultAnim)
        {
            p1ResultAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (!string.IsNullOrEmpty(slideInTriggerName))
            {
                p1ResultAnim.ResetTrigger(slideInTriggerName);
                p1ResultAnim.SetTrigger(slideInTriggerName);
            }
        }

        yield return new WaitForSecondsRealtime(betweenPlayersDelay);

        // 4) —— P2：先计数，再滑入 ——
        Debug.Log("[PGC] 开始计数 Player B ...");
        yield return StartCoroutine(Co_CountOneSide(bList, countBLabel, "B"));
        Debug.Log("[PGC] Player B 计数完成，触发面板滑入。");

        if (p2ResultAnim)
        {
            p2ResultAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (!string.IsNullOrEmpty(slideInTriggerName))
            {
                p2ResultAnim.ResetTrigger(slideInTriggerName);
                p2ResultAnim.SetTrigger(slideInTriggerName);
            }
        }

        // 5) —— 停留让玩家看清分数（3s） ——
        Debug.Log($"[PGC] 停留显示分数 {scoresHoldDuration:F2}s ...");
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, scoresHoldDuration));

        // 6) —— 判胜 → 输家消失 → 赢家 Win → 皇冠 ——
        int aScore = smA;
        int bScore = smB;
        Debug.Log($"[PGC] 最终计分：A={aScore}, B={bScore}");

        if (aScore == bScore)
        {
            Debug.Log("[PGC] 平局，省略赢家演出。");
            yield break;
        }

        bool aWin = aScore > bScore;
        _winnerId = aWin ? 1 : 2;               // === 新增：记录赢家
        Debug.Log(aWin ? "[PGC] P1 胜出" : "[PGC] P2 胜出");

        // 输家面板立刻消失
        if (aWin) { if (p2ResultRoot) p2ResultRoot.SetActive(false); }
        else { if (p1ResultRoot) p1ResultRoot.SetActive(false); }

        // 赢家 Win 动画（各自不同）
        var winAnim = aWin ? p1ResultAnim : p2ResultAnim;
        if (winAnim)
        {
            winAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            winAnim.Play(aWin ? p1WinState : p2WinState, 0, 0f);
            Debug.Log("[PGC] 播放赢家面板 Win 动画。");
        }

        // 皇冠：稍等半拍 → 从头播放
        yield return new WaitForSecondsRealtime(crownDelay);
        if (crownAnim)
        {
            var go = crownAnim.gameObject;
            if (!go.activeSelf) go.SetActive(true);
            crownAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            crownAnim.Rebind();
            crownAnim.Play(crownPopState, 0, 0f);
            Debug.Log("[PGC] 播放皇冠 CrownPop 动画。");
        }

        // === 新增：Bonus 入口（按任意键直接进入） ===
        ShowBonusHint(true);                               // 出现 "Press any key ..."
        yield return new WaitUntil(() => Input.anyKeyDown);// 等任意键

        // 直接关闭所有结算UI（不淡出）
        if (p1ResultRoot) p1ResultRoot.SetActive(false);
        if (p2ResultRoot) p2ResultRoot.SetActive(false);
        if (timesUpPanel) timesUpPanel.SetActive(false);
        ShowBonusHint(false);
        
        // 新增：把皇冠也一起隐藏
        if (crownAnim && crownAnim.gameObject.activeSelf)
        {
            crownAnim.gameObject.SetActive(false);
        }

        // 在赢家推塔前让所有墙撤退 / 失效（避免挡手或顶塌）
        foreach (var wall in FindObjectsOfType<BrickWallPlacer>())
        {
            wall.BeginWinnerPushWindow(); // 也可以传持续秒数 wall.BeginWinnerPushWindow(1.4f);
        }

        // 打开赢家的“手”（Sprite），由物理碰撞去推倒积木
        EnterBonus(_winnerId);

        Debug.Log("[PGC] ===== 进入 Bonus：赢家可用手撞倒所有积木 =====");
        yield break;
    }

    // —— 数一侧：逐个高亮 + 文本递增 + tick —— 
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
            if (sfx && tickClip) sfx.PlayOneShot(tickClip);

            float t = (total <= 1) ? 1f : Mathf.InverseLerp(total - slowDownLastN, total - 1, i);
            t = Mathf.Clamp01(t);
            float wait = Mathf.Lerp(firstInterval, lastInterval, t);
            yield return new WaitForSecondsRealtime(wait);

            Debug.Log($"[PGC] {prefix} highlight #{i + 1}: {bm.name}");
        }

        if (last) last.SetHighlight(false);
        if (sfx && finishClip) sfx.PlayOneShot(finishClip);
    }

    // —— 是否计分：与 ScoreManager 的“isTowerMember”为准；否则用触地/已落下兜底 —— 
    bool IsEligibleForScore(BlockMark bm)
    {
        // 先看 isTowerMember（与 ScoreManager 的权威逻辑保持一致）
        foreach (var c in bm.gameObject.GetComponents<MonoBehaviour>())
        {
            var f = c.GetType().GetField("isTowerMember");
            if (f != null)
            {
                object val = f.GetValue(c);
                if (val is bool b) return b && (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2);
            }
        }

        // 没有 isTowerMember 字段：退化到“触地/已落下”
        return (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2) &&
               (bm.touchedStack || bm.hasDropped);
    }

    // === 新增：Bonus 相关的极简方法 ===
    void ShowBonusHint(bool on)
    {
        if (bonusHintRoot) bonusHintRoot.SetActive(on);
        if (on && bonusHintLabel) bonusHintLabel.text = bonusHintText;
    }

    void EnterBonus(int winnerId)
    {
        _bonusActive = true;

        // 新增：恢复时间流逝，避免 PushHand2D 用 deltaTime 时不动
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

            // 保险：确保 Rigidbody2D 设置（Dynamic + 无重力）
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            // 新增：确保是实体碰撞，用物理去“撞倒”积木
            var col = go.GetComponent<Collider2D>();
            if (col) col.isTrigger = false;
        }
        else
        {
            Debug.LogWarning("[PGC] 未指定赢家的手 Sprite。");
        }
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
        Debug.Log("[PGC] Bonus 已关闭。");
    }
}