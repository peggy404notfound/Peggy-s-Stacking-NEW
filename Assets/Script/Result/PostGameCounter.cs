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
    public TextMeshProUGUI countALabel;  // "A: n"
    public TextMeshProUGUI countBLabel;  // "B: n"

    [Header("节奏")]
    public float firstInterval = 0.08f;
    public float lastInterval = 0.35f;
    public int slowDownLastN = 6;
    public float betweenPlayersDelay = 0.5f;

    [Header("颜色")]
    public Color aTextColor = new Color(1f, 0.6f, 0.2f); // 橙
    public Color bTextColor = new Color(0.3f, 0.6f, 1f); // 蓝

    [Header("音效（可选）")]
    public AudioSource sfx;
    public AudioClip tickClip;
    public AudioClip finishClip;

    Coroutine _co;

    // —— 绑定到 GameEndManager.onGameOver —— //
    public void StartOnGameOver()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Start());
    }

    IEnumerator Co_Start()
    {
        if (timesUpPanel) timesUpPanel.SetActive(true);
        if (titleLabel) titleLabel.text = "Time's Up!";

        // 收集 BlockMark（这行可换成你更严格的过滤）
        var allMarks = FindObjectsOfType<BlockMark>(includeInactive: false).ToList();
        var valid = allMarks.Where(bm => bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2).ToList();

        // —— 仅用于自检 —— 
        Debug.Log($"[PGC] total:{allMarks.Count} valid:{valid.Count} A:{valid.Count(v => v.ownerPlayerId == 1)} B:{valid.Count(v => v.ownerPlayerId == 2)}");

        // 分开两边，并各自排序（低→高）
        var aList = valid.Where(b => b.ownerPlayerId == 1)
                         .OrderBy(b => b.transform.position.y).ToList();
        var bList = valid.Where(b => b.ownerPlayerId == 2)
                         .OrderBy(b => b.transform.position.y).ToList();

        // UI 初始化
        if (countALabel) { countALabel.color = aTextColor; countALabel.text = "A: 0"; }
        if (countBLabel) { countBLabel.color = bTextColor; countBLabel.text = "B: 0"; }

        // —— 关键：先数 P1（A），再数 P2（B），中间有停顿 —— 
        yield return StartCoroutine(Co_CountOneSide(aList, countALabel, "A"));

        yield return new WaitForSecondsRealtime(betweenPlayersDelay);

        yield return StartCoroutine(Co_CountOneSide(bList, countBLabel, "B"));

        // 可选：核对最终分
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RecountScores(out int p1, out int p2);
            Debug.Log($"[PGC] Final -> A:{p1} B:{p2}");
        }
    }


    IEnumerator Co_CountOneSide(List<BlockMark> list, TextMeshProUGUI label, string prefix)
    {
        if (label) label.text = $"{prefix}: 0";

        HighlightHalo last = null;
        int total = list.Count;

        for (int i = 0; i < total; i++)
        {
            // 上一个取消
            if (last) last.SetHighlight(false);

            var bm = list[i];
            var hl = bm.GetComponent<HighlightHalo>();
            if (hl == null)
            {
                hl = bm.gameObject.AddComponent<HighlightHalo>();
                hl.scale = 1.12f;          // 稍微大一点
                hl.alpha = 0.7f;           // 更亮
                hl.color = Color.white;
                hl.sortingOffset = +5;     // 放到上层，避免被本体挡住
            }
            last = hl;

            // 当前高亮
            hl.SetHighlight(true);

            // 数字与音效
            if (label) label.text = $"{prefix}: {i + 1}";
            if (sfx && tickClip) sfx.PlayOneShot(tickClip);

            // 越靠后越慢
            float t = (total <= 1) ? 1f : Mathf.InverseLerp(total - slowDownLastN, total - 1, i);
            t = Mathf.Clamp01(t);
            float wait = Mathf.Lerp(firstInterval, lastInterval, t);
            yield return new WaitForSecondsRealtime(wait);
            Debug.Log($"[PGC] {prefix} highlight #{i + 1}: {bm.name}");
        }

        if (last) last.SetHighlight(false);
        if (sfx && finishClip) sfx.PlayOneShot(finishClip);
    }

    // —— 与 ScoreManager 的“成员判定”尽量保持一致 —— //
    bool IsTowerMemberOrTouched(BlockMark bm)
    {
        // 1) 先按 ScoreManager 的反射习惯找 isTowerMember=true
        foreach (var c in bm.gameObject.GetComponents<MonoBehaviour>())
        {
            var f = c.GetType().GetField("isTowerMember");
            if (f != null)
            {
                object val = f.GetValue(c);
                if (val is bool b && b) return bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2;
            }
        }
        // 2) 兜底：用你 BlockMark 自己的状态（已触塔/已落下）
        return (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2) &&
               (bm.touchedStack || bm.hasDropped);
    }
}