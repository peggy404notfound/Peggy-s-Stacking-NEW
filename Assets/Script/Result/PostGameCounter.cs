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

    [Header("����")]
    public float firstInterval = 0.08f;
    public float lastInterval = 0.35f;
    public int slowDownLastN = 6;
    public float betweenPlayersDelay = 0.5f;

    [Header("��ɫ")]
    public Color aTextColor = new Color(1f, 0.6f, 0.2f); // ��
    public Color bTextColor = new Color(0.3f, 0.6f, 1f); // ��

    [Header("��Ч����ѡ��")]
    public AudioSource sfx;
    public AudioClip tickClip;
    public AudioClip finishClip;

    Coroutine _co;

    // ���� �󶨵� GameEndManager.onGameOver ���� //
    public void StartOnGameOver()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Start());
    }

    IEnumerator Co_Start()
    {
        if (timesUpPanel) timesUpPanel.SetActive(true);
        if (titleLabel) titleLabel.text = "Time's Up!";

        // �ռ� BlockMark�����пɻ�������ϸ�Ĺ��ˣ�
        var allMarks = FindObjectsOfType<BlockMark>(includeInactive: false).ToList();
        var valid = allMarks.Where(bm => bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2).ToList();

        // ���� �������Լ� ���� 
        Debug.Log($"[PGC] total:{allMarks.Count} valid:{valid.Count} A:{valid.Count(v => v.ownerPlayerId == 1)} B:{valid.Count(v => v.ownerPlayerId == 2)}");

        // �ֿ����ߣ����������򣨵͡��ߣ�
        var aList = valid.Where(b => b.ownerPlayerId == 1)
                         .OrderBy(b => b.transform.position.y).ToList();
        var bList = valid.Where(b => b.ownerPlayerId == 2)
                         .OrderBy(b => b.transform.position.y).ToList();

        // UI ��ʼ��
        if (countALabel) { countALabel.color = aTextColor; countALabel.text = "A: 0"; }
        if (countBLabel) { countBLabel.color = bTextColor; countBLabel.text = "B: 0"; }

        // ���� �ؼ������� P1��A�������� P2��B�����м���ͣ�� ���� 
        yield return StartCoroutine(Co_CountOneSide(aList, countALabel, "A"));

        yield return new WaitForSecondsRealtime(betweenPlayersDelay);

        yield return StartCoroutine(Co_CountOneSide(bList, countBLabel, "B"));

        // ��ѡ���˶����շ�
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
            // ��һ��ȡ��
            if (last) last.SetHighlight(false);

            var bm = list[i];
            var hl = bm.GetComponent<HighlightHalo>();
            if (hl == null)
            {
                hl = bm.gameObject.AddComponent<HighlightHalo>();
                hl.scale = 1.12f;          // ��΢��һ��
                hl.alpha = 0.7f;           // ����
                hl.color = Color.white;
                hl.sortingOffset = +5;     // �ŵ��ϲ㣬���ⱻ���嵲ס
            }
            last = hl;

            // ��ǰ����
            hl.SetHighlight(true);

            // ��������Ч
            if (label) label.text = $"{prefix}: {i + 1}";
            if (sfx && tickClip) sfx.PlayOneShot(tickClip);

            // Խ����Խ��
            float t = (total <= 1) ? 1f : Mathf.InverseLerp(total - slowDownLastN, total - 1, i);
            t = Mathf.Clamp01(t);
            float wait = Mathf.Lerp(firstInterval, lastInterval, t);
            yield return new WaitForSecondsRealtime(wait);
            Debug.Log($"[PGC] {prefix} highlight #{i + 1}: {bm.name}");
        }

        if (last) last.SetHighlight(false);
        if (sfx && finishClip) sfx.PlayOneShot(finishClip);
    }

    // ���� �� ScoreManager �ġ���Ա�ж�����������һ�� ���� //
    bool IsTowerMemberOrTouched(BlockMark bm)
    {
        // 1) �Ȱ� ScoreManager �ķ���ϰ���� isTowerMember=true
        foreach (var c in bm.gameObject.GetComponents<MonoBehaviour>())
        {
            var f = c.GetType().GetField("isTowerMember");
            if (f != null)
            {
                object val = f.GetValue(c);
                if (val is bool b && b) return bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2;
            }
        }
        // 2) ���ף����� BlockMark �Լ���״̬���Ѵ���/�����£�
        return (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2) &&
               (bm.touchedStack || bm.hasDropped);
    }
}