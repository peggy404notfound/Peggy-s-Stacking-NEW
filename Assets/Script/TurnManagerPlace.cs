using System.Collections;
using UnityEngine;

public class TurnManagerPlace : MonoBehaviour
{
    public enum Player { P1, P2 }

    [Header("Prefab & Bounds")]
    public GameObject blockPrefab;     // Ԥ���壺�� SpriteRenderer + BoxCollider2D + Rigidbody2D + BlockMark + PlacementPreview + HorizontalSweeper
    public Transform leftBound;        // ��߽������
    public Transform rightBound;       // �ұ߽������

    [Header("Keys & Start")]
    public KeyCode p1Key = KeyCode.S;
    public KeyCode p2Key = KeyCode.DownArrow;
    public Player startPlayer = Player.P1;

    [Header("Stability Judge")]
    public float sleepVel = 0.05f;     // ��Ϊ�����������ٶ���ֵ
    public float sleepHold = 0.75f;    // ��Ҫ���ֵ�ʱ��

    [Header("Fail Line")]
    public float bottomMargin = 0.5f;  // ʧ���� = ��Ļ�ײ� - bottomMargin

    // ��ǰ�غ϶���
    private Player currentPlayer;
    private BlockMark currentMark;
    private PlacementPreview currentPreview;
    private HorizontalSweeper currentSweeper;

    private bool gameEnded = false;

    private void Start()
    {
        currentPlayer = startPlayer;
        Debug.Log("[TurnManagerPlace] Start with " + (currentPlayer == Player.P1 ? "P1" : "P2") +
                  ". Keys: P1=" + p1Key + ", P2=" + p2Key);

        SpawnPreviewFor(currentPlayer);
        StartCoroutine(MainLoop());
    }

    private IEnumerator MainLoop()
    {
        while (!gameEnded)
        {
            // 1) �ȵ�ǰ���ȷ�Ϸ���
            yield return StartCoroutine(WaitConfirmKey(currentPlayer));

            // 2) �ȸÿ��ȶ����ڼ����˵�������
            yield return StartCoroutine(WaitStableOrFall(currentMark));
            if (gameEnded) yield break;

            // 3) ���˲�������һ��Ԥ��
            currentPlayer = (currentPlayer == Player.P1) ? Player.P2 : Player.P1;
            SpawnPreviewFor(currentPlayer);
        }
    }

    // ========== ���ɣ�����������Ԥ���飬��ˮƽɨ�� ==========
    private void SpawnPreviewFor(Player p)
    {
        if (!blockPrefab || !leftBound || !rightBound)
        {
            Debug.LogError("[TurnManagerPlace] Missing references.");
            return;
        }

        // �ȷ������ߣ�Y �����������
        float xMid = (leftBound.position.x + rightBound.position.x) * 0.5f;
        var go = Instantiate(blockPrefab, new Vector3(xMid, 0f, 0f), Quaternion.identity);

        currentPreview = go.GetComponent<PlacementPreview>();
        currentSweeper = go.GetComponent<HorizontalSweeper>();
        currentMark = go.GetComponent<BlockMark>();

        if (!currentPreview || !currentSweeper || !currentMark)
        {
            Debug.LogError("[TurnManagerPlace] Block prefab missing required components.");
            return;
        }

        // �߽���غϱ��
        currentSweeper.leftBound = leftBound;
        currentSweeper.rightBound = rightBound;
        currentMark.isCurrentTurn = true;

        // ����Ԥ��̬����������͸����ֻˮƽɨ����
        currentPreview.EnterPreview();

        // ���� �ؼ�����Ԥ���顰�ױߡ����뵽��ǰ���� ���� //
        float towerTopY = FindTowerTopY();              // ȡ���� Stack&�Ǵ��� collider �� maxY
        float halfH = currentPreview.GetHalfHeightWorld();
        float eps = 0.001f;
        var pos = go.transform.position;
        pos.y = towerTopY + halfH + eps;
        go.transform.position = pos;

        // ȷ���� Stack �㣨FindTowerTopY ����� isTrigger ��Ԥ�������������
        go.layer = LayerMask.NameToLayer("Stack");
    }

    // ========== �ȴ���Ұ������͵�ʵ�������� Y ==========
    private IEnumerator WaitConfirmKey(Player p)
    {
        while (!gameEnded)
        {
            if ((p == Player.P1 && Input.GetKeyDown(p1Key)) ||
                (p == Player.P2 && Input.GetKeyDown(p2Key)))
            {
                if (currentMark) currentMark.hasDropped = true; // ������ȷ�Ϸ��á�
                if (currentPreview) currentPreview.SolidifyHere(); // �͵�ʵ������������
                yield break;
            }
            yield return null;
        }
    }

    // ========== �ȴ��ȶ� �� �з�����������и� ==========
    private IEnumerator WaitStableOrFall(BlockMark mark)
    {
        if (mark == null) yield break;
        var rb = mark.GetComponent<Rigidbody2D>();
        float acc = 0f;

        while (!gameEnded && rb != null)
        {
            // ���� ʧ���߼�⣨�޵ײ�������������
            float killY = GetCameraBottomY() - bottomMargin;
            var all = FindObjectsOfType<BlockMark>();
            foreach (var bm in all)
            {
                if (!bm) continue;
                if (bm.transform.position.y < killY)
                {
                    if (bm == mark && mark.hasDropped && !mark.touchedStack)
                        EndGameMiss(CurrentControllerName());     // ��ǰ�顢δ���� �� û����
                    else
                        EndGameCollapse(CurrentControllerName());  // ���� �� ����
                    yield break;
                }
            }

            // ���� �ȶ��ж� �������ٶ�С������һ��ʱ�䣩
            if (rb.velocity.sqrMagnitude < sleepVel * sleepVel)
            {
                acc += Time.deltaTime;
                if (acc >= sleepHold) yield break; // ��Ϊ����
            }
            else acc = 0f;

            yield return null;
        }
    }

    // ��ȡ��ǰ���������� Layer=Stack �ҷǴ����� Collider2D �� max.y��
    private float FindTowerTopY()
    {
        float top = float.NegativeInfinity;
        var colliders = FindObjectsOfType<Collider2D>();
        foreach (var c in colliders)
        {
            if (c == null || c.isTrigger) continue;
            if (c.gameObject.layer != LayerMask.NameToLayer("Stack")) continue;
            float y = c.bounds.max.y;
            if (y > top) top = y;
        }
        if (float.IsNegativeInfinity(top)) top = 0f;
        return top;
    }

    private float GetCameraBottomY()
    {
        var cam = Camera.main;
        if (!cam) return -10f;
        return cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).y;
    }

    private string CurrentControllerName()
    {
        return (currentPlayer == Player.P1) ? "P1" : "P2";
    }

    private void EndGameMiss(string loser)
    {
        gameEnded = true;
        Debug.Log("[Result] " + loser + " did not stack on the tower. Other player wins.");
    }

    private void EndGameCollapse(string culprit)
    {
        gameEnded = true;
        Debug.Log("[Result] " + culprit + " caused a collapse. Other player wins.");
    }
}