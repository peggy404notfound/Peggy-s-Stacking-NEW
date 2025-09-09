using System.Collections;
using UnityEngine;

public class TurnManagerPlace : MonoBehaviour
{
    public enum Player { P1, P2 }

    [Header("Prefab & Bounds")]
    public GameObject blockPrefab;     // 预制体：含 SpriteRenderer + BoxCollider2D + Rigidbody2D + BlockMark + PlacementPreview + HorizontalSweeper
    public Transform leftBound;        // 左边界空物体
    public Transform rightBound;       // 右边界空物体

    [Header("Keys & Start")]
    public KeyCode p1Key = KeyCode.S;
    public KeyCode p2Key = KeyCode.DownArrow;
    public Player startPlayer = Player.P1;

    [Header("Stability Judge")]
    public float sleepVel = 0.05f;     // 认为“很慢”的速度阈值
    public float sleepHold = 0.75f;    // 需要保持的时间

    [Header("Fail Line")]
    public float bottomMargin = 0.5f;  // 失败线 = 屏幕底部 - bottomMargin

    // 当前回合对象
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
            // 1) 等当前玩家确认放置
            yield return StartCoroutine(WaitConfirmKey(currentPlayer));

            // 2) 等该块稳定或期间有人跌出底线
            yield return StartCoroutine(WaitStableOrFall(currentMark));
            if (gameEnded) yield break;

            // 3) 换人并生成下一块预览
            currentPlayer = (currentPlayer == Player.P1) ? Player.P2 : Player.P1;
            SpawnPreviewFor(currentPlayer);
        }
    }

    // ========== 生成：沿塔顶生成预览块，仅水平扫动 ==========
    private void SpawnPreviewFor(Player p)
    {
        if (!blockPrefab || !leftBound || !rightBound)
        {
            Debug.LogError("[TurnManagerPlace] Missing references.");
            return;
        }

        // 先放在中线，Y 待会儿贴塔顶
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

        // 边界与回合标记
        currentSweeper.leftBound = leftBound;
        currentSweeper.rightBound = rightBound;
        currentMark.isCurrentTurn = true;

        // 进入预览态（无物理、半透明、只水平扫动）
        currentPreview.EnterPreview();

        // —— 关键：把预览块“底边”对齐到当前塔顶 —— //
        float towerTopY = FindTowerTopY();              // 取所有 Stack&非触发 collider 的 maxY
        float halfH = currentPreview.GetHalfHeightWorld();
        float eps = 0.001f;
        var pos = go.transform.position;
        pos.y = towerTopY + halfH + eps;
        go.transform.position = pos;

        // 确保在 Stack 层（FindTowerTopY 不会把 isTrigger 的预览块算进塔顶）
        go.layer = LayerMask.NameToLayer("Stack");
    }

    // ========== 等待玩家按键：就地实化，不改 Y ==========
    private IEnumerator WaitConfirmKey(Player p)
    {
        while (!gameEnded)
        {
            if ((p == Player.P1 && Input.GetKeyDown(p1Key)) ||
                (p == Player.P2 && Input.GetKeyDown(p2Key)))
            {
                if (currentMark) currentMark.hasDropped = true; // 视作“确认放置”
                if (currentPreview) currentPreview.SolidifyHere(); // 就地实化（开启物理）
                yield break;
            }
            yield return null;
        }
    }

    // ========== 等待稳定 或 有方块跌出底线判负 ==========
    private IEnumerator WaitStableOrFall(BlockMark mark)
    {
        if (mark == null) yield break;
        var rb = mark.GetComponent<Rigidbody2D>();
        float acc = 0f;

        while (!gameEnded && rb != null)
        {
            // —— 失败线检测（无底部触发器）——
            float killY = GetCameraBottomY() - bottomMargin;
            var all = FindObjectsOfType<BlockMark>();
            foreach (var bm in all)
            {
                if (!bm) continue;
                if (bm.transform.position.y < killY)
                {
                    if (bm == mark && mark.hasDropped && !mark.touchedStack)
                        EndGameMiss(CurrentControllerName());     // 当前块、未触塔 → 没摞上
                    else
                        EndGameCollapse(CurrentControllerName());  // 否则 → 塔倒
                    yield break;
                }
            }

            // —— 稳定判定 ——（速度小并持续一段时间）
            if (rb.velocity.sqrMagnitude < sleepVel * sleepVel)
            {
                acc += Time.deltaTime;
                if (acc >= sleepHold) yield break; // 认为落稳
            }
            else acc = 0f;

            yield return null;
        }
    }

    // 读取当前塔顶（所有 Layer=Stack 且非触发的 Collider2D 的 max.y）
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