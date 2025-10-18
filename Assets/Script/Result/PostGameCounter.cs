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
    public TextMeshProUGUI countALabel;  // ֻ��ʾ����
    public TextMeshProUGUI countBLabel;  // ֻ��ʾ����

    [Header("���ࣨ�������ࣩ")]
    public float firstInterval = 0.08f;
    public float lastInterval = 0.35f;
    public int slowDownLastN = 6;
    public float betweenPlayersDelay = 0.5f;

    [Header("չʾ���ࣨ������")]
    [Tooltip("������嶼�����ͣ���������ٽ���Ӯ���ݳ�")]
    public float scoresHoldDuration = 3.0f;   // <- ��Ҫ�ġ�ͣ 3 �롱

    [Header("��ɫ")]
    public Color aTextColor = new Color(1f, 0.6f, 0.2f);
    public Color bTextColor = new Color(0.3f, 0.6f, 1f);

    [Header("��Ч����ѡ��")]
    public AudioSource sfx;
    public AudioClip tickClip;
    public AudioClip finishClip;

    // ���� ��� & �ʹ� ����
    [Header("��������ʹ�")]
    public GameObject p1ResultRoot;    // ���ڡ����������ʧ��
    public GameObject p2ResultRoot;
    public Animator p1ResultAnim;     // �� SlideIn / P1Win
    public Animator p2ResultAnim;     // �� SlideIn / P2Win
    public Animator crownAnim;        // ����ʹ� Animator��Ĭ��̬ Idle���� CrownPop��

    [Header("������")]
    public string slideInTriggerName = "SlideIn";
    public string p1WinState = "P1Win";
    public string p2WinState = "P2Win";
    public string crownPopState = "CrownPop";

    [Header("ʱ���")]
    public float crownDelay = 0.15f;   // Ӯ��Win��ʹ��ӳ�

    // === ������Bonus ��ʾ���� ===
    [Header("Winner Bonus ��ʾ����ѡ��")]
    public GameObject bonusHintRoot;              // ��һ�� "Press any key to get winner bonus!"
    public TextMeshProUGUI bonusHintLabel;
    [TextArea] public string bonusHintText = "Press any key to get winner bonus!";

    [Header("Ӯ�ҵġ��֡���������Ԥ�ţ�Ĭ�����أ�")]
    public GameObject handSpriteP1;   // Ӯ��= P1 ʱ����
    public GameObject handSpriteP2;   // Ӯ��= P2 ʱ����

    [Header("�ֵ��ƶ��߽磨���� PushHand2D��������Ĭ�ϣ�")]
    public float handMoveSpeed = 8f;
    public float handXMin = -10f;
    public float handXMax = 10f;

    [Header("��ѡ��Bonus ʱ���õ���Ϸ���ڵ�")]
    public GameObject gameplayRoot;   // �� Game �ڵ����

    private Coroutine _co;
    private bool _bonusActive = false; // === ����
    private int _winnerId = 0;         // === ����

    public void StartOnGameOver()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Start());
    }

    IEnumerator Co_Start()
    {
        if (timesUpPanel) timesUpPanel.SetActive(true);
        if (titleLabel) titleLabel.text = "Time's Up!";
        Debug.Log("[PGC] ===== ���㿪ʼ =====");

        // 1) �ռ�������ɲ���Ʒ֡��� BlockMark
        var allMarks = FindObjectsOfType<BlockMark>(includeInactive: false).ToList();

        // ���ˣ�ֻҪ P1/P2�����ų���ͣ��δ����/δ����������������
        // ���������� isTowerMember Ϊ׼������Ҫ�� (touchedStack || hasDropped)
        List<BlockMark> eligible = allMarks.Where(IsEligibleForScore).ToList();

        // �ֱ� + �Ӿ����򣨴ӵ͵��ߣ����ڡ��������족������
        var aList = eligible.Where(b => b.ownerPlayerId == 1)
                            .OrderBy(b => b.transform.position.y).ToList();
        var bList = eligible.Where(b => b.ownerPlayerId == 2)
                            .OrderBy(b => b.transform.position.y).ToList();

        Debug.Log($"[PGC] Ԥɸѡ��ɣ�A��ѡ={aList.Count}��B��ѡ={bList.Count}");

        // 2) �� ScoreManager ��Ϊ��Ȩ�����������������ü��б��ȣ�ȷ����ʾ=��ʵ��
        int smA = 0, smB = 0;
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RecountScores(out smA, out smB);   // Ȩ��ͳ��
            Debug.Log($"[PGC] ScoreManager��A={smA}��B={smB}����Ϊ���շ�����");
        }
        else
        {
            // ���û�� ScoreManager�����˻�����ǰ�б�����
            smA = aList.Count; smB = bList.Count;
            Debug.LogWarning("[PGC] δ�ҵ� ScoreManager��ʹ��Ԥɸѡ������Ϊ������");
        }

        // �����ڡ�������������б��Ȳü���Ȩ���֣��������ͣ/�������ȥ��
        if (aList.Count > smA) aList = aList.Take(smA).ToList();
        if (bList.Count > smB) bList = bList.Take(smB).ToList();

        // ��ʼ�� UI �ı���ֻ���֣�
        if (countALabel) { countALabel.color = aTextColor; countALabel.text = "0"; }
        if (countBLabel) { countBLabel.color = bTextColor; countBLabel.text = "0"; }

        // 3) ���� P1���ȼ������ٻ��� ����
        Debug.Log("[PGC] ��ʼ���� Player A ...");
        yield return StartCoroutine(Co_CountOneSide(aList, countALabel, "A"));
        Debug.Log("[PGC] Player A ������ɣ�������廬�롣");

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

        // 4) ���� P2���ȼ������ٻ��� ����
        Debug.Log("[PGC] ��ʼ���� Player B ...");
        yield return StartCoroutine(Co_CountOneSide(bList, countBLabel, "B"));
        Debug.Log("[PGC] Player B ������ɣ�������廬�롣");

        if (p2ResultAnim)
        {
            p2ResultAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (!string.IsNullOrEmpty(slideInTriggerName))
            {
                p2ResultAnim.ResetTrigger(slideInTriggerName);
                p2ResultAnim.SetTrigger(slideInTriggerName);
            }
        }

        // 5) ���� ͣ������ҿ��������3s�� ����
        Debug.Log($"[PGC] ͣ����ʾ���� {scoresHoldDuration:F2}s ...");
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, scoresHoldDuration));

        // 6) ���� ��ʤ �� �����ʧ �� Ӯ�� Win �� �ʹ� ����
        int aScore = smA;
        int bScore = smB;
        Debug.Log($"[PGC] ���ռƷ֣�A={aScore}, B={bScore}");

        if (aScore == bScore)
        {
            Debug.Log("[PGC] ƽ�֣�ʡ��Ӯ���ݳ���");
            yield break;
        }

        bool aWin = aScore > bScore;
        _winnerId = aWin ? 1 : 2;               // === ��������¼Ӯ��
        Debug.Log(aWin ? "[PGC] P1 ʤ��" : "[PGC] P2 ʤ��");

        // ������������ʧ
        if (aWin) { if (p2ResultRoot) p2ResultRoot.SetActive(false); }
        else { if (p1ResultRoot) p1ResultRoot.SetActive(false); }

        // Ӯ�� Win ���������Բ�ͬ��
        var winAnim = aWin ? p1ResultAnim : p2ResultAnim;
        if (winAnim)
        {
            winAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            winAnim.Play(aWin ? p1WinState : p2WinState, 0, 0f);
            Debug.Log("[PGC] ����Ӯ����� Win ������");
        }

        // �ʹڣ��ԵȰ��� �� ��ͷ����
        yield return new WaitForSecondsRealtime(crownDelay);
        if (crownAnim)
        {
            var go = crownAnim.gameObject;
            if (!go.activeSelf) go.SetActive(true);
            crownAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
            crownAnim.Rebind();
            crownAnim.Play(crownPopState, 0, 0f);
            Debug.Log("[PGC] ���Żʹ� CrownPop ������");
        }

        // === ������Bonus ��ڣ��������ֱ�ӽ��룩 ===
        ShowBonusHint(true);                               // ���� "Press any key ..."
        yield return new WaitUntil(() => Input.anyKeyDown);// �������

        // ֱ�ӹر����н���UI����������
        if (p1ResultRoot) p1ResultRoot.SetActive(false);
        if (p2ResultRoot) p2ResultRoot.SetActive(false);
        if (timesUpPanel) timesUpPanel.SetActive(false);
        ShowBonusHint(false);
        
        // �������ѻʹ�Ҳһ������
        if (crownAnim && crownAnim.gameObject.activeSelf)
        {
            crownAnim.gameObject.SetActive(false);
        }

        // ��Ӯ������ǰ������ǽ���� / ʧЧ�����⵲�ֻ�����
        foreach (var wall in FindObjectsOfType<BrickWallPlacer>())
        {
            wall.BeginWinnerPushWindow(); // Ҳ���Դ��������� wall.BeginWinnerPushWindow(1.4f);
        }

        // ��Ӯ�ҵġ��֡���Sprite������������ײȥ�Ƶ���ľ
        EnterBonus(_winnerId);

        Debug.Log("[PGC] ===== ���� Bonus��Ӯ�ҿ�����ײ�����л�ľ =====");
        yield break;
    }

    // ���� ��һ�ࣺ������� + �ı����� + tick ���� 
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

    // ���� �Ƿ�Ʒ֣��� ScoreManager �ġ�isTowerMember��Ϊ׼�������ô���/�����¶��� ���� 
    bool IsEligibleForScore(BlockMark bm)
    {
        // �ȿ� isTowerMember���� ScoreManager ��Ȩ���߼�����һ�£�
        foreach (var c in bm.gameObject.GetComponents<MonoBehaviour>())
        {
            var f = c.GetType().GetField("isTowerMember");
            if (f != null)
            {
                object val = f.GetValue(c);
                if (val is bool b) return b && (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2);
            }
        }

        // û�� isTowerMember �ֶΣ��˻���������/�����¡�
        return (bm.ownerPlayerId == 1 || bm.ownerPlayerId == 2) &&
               (bm.touchedStack || bm.hasDropped);
    }

    // === ������Bonus ��صļ��򷽷� ===
    void ShowBonusHint(bool on)
    {
        if (bonusHintRoot) bonusHintRoot.SetActive(on);
        if (on && bonusHintLabel) bonusHintLabel.text = bonusHintText;
    }

    void EnterBonus(int winnerId)
    {
        _bonusActive = true;

        // �������ָ�ʱ�����ţ����� PushHand2D �� deltaTime ʱ����
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

            // ���գ�ȷ�� Rigidbody2D ���ã�Dynamic + ��������
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            // ������ȷ����ʵ����ײ��������ȥ��ײ������ľ
            var col = go.GetComponent<Collider2D>();
            if (col) col.isTrigger = false;
        }
        else
        {
            Debug.LogWarning("[PGC] δָ��Ӯ�ҵ��� Sprite��");
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
        Debug.Log("[PGC] Bonus �ѹرա�");
    }
}