using System.Collections;
using UnityEngine;

public class TurnManagerStrict : MonoBehaviour
{
    public static TurnManagerStrict Instance { get; private set; }

    public enum Player { P1, P2 }

    [Header("Prefab & Bounds")]
    public GameObject blockPrefab;
    public Transform leftBound;
    public Transform rightBound;

    [Header("Fixed Spawn Height")]
    public Transform spawnY; // 固定生成高度

    [Header("Keys & Start Player")]
    public KeyCode p1Key = KeyCode.S;
    public KeyCode p2Key = KeyCode.DownArrow;
    public Player startPlayer = Player.P1;

    [Header("Stability Judge")]
    public float sleepVel = 0.05f;   // 速度阈值
    public float sleepHold = 0.75f;  // 持续安稳时间

    [Header("Fail Line")]
    public float bottomMargin = 0.5f; // 屏幕底部再往下多少作为失败线

    private Player currentPlayer;
    private BlockMark currentMark;
    private HoverMover currentMover;
    private bool gameEnded = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        currentPlayer = startPlayer;
        Debug.Log("[TurnManager] Start with " + (currentPlayer == Player.P1 ? "P1" : "P2")
                  + ". Keys: P1=" + p1Key + ", P2=" + p2Key);

        SpawnAtFixedHeight(currentPlayer);
        StartCoroutine(RunTurnLoop());
    }

    private IEnumerator RunTurnLoop()
    {
        while (!gameEnded)
        {
            yield return StartCoroutine(WaitForDropKey(currentPlayer));
            yield return StartCoroutine(WaitStableOrFall(currentMark));
            if (gameEnded) yield break;

            currentPlayer = (currentPlayer == Player.P1) ? Player.P2 : Player.P1;
            SpawnAtFixedHeight(currentPlayer);
        }
    }

    private IEnumerator WaitForDropKey(Player p)
    {
        Debug.Log("[TurnManager] Waiting for " + (p == Player.P1 ? "P1 [S]" : "P2 [Down]") + " to drop...");
        while (!gameEnded)
        {
            if ((p == Player.P1 && Input.GetKeyDown(p1Key)) ||
                (p == Player.P2 && Input.GetKeyDown(p2Key)))
            {
                Debug.Log("[TurnManager] Drop pressed.");
                if (currentMover != null) currentMover.Drop();
                if (currentMark != null) currentMark.hasDropped = true; // 标记已放下
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator WaitStableOrFall(BlockMark mark)
    {
        if (mark == null) yield break;
        var rb = mark.GetComponent<Rigidbody2D>();
        float acc = 0f;

        while (!gameEnded && rb != null)
        {
            // 1) 失败线检测（无任何触发器）
            float killY = GetCameraBottomY() - bottomMargin;

            // 任意方块越过失败线 → 塔倒；但如果是“本回合方块且未触塔且已放下” → Miss
            var all = FindObjectsOfType<BlockMark>();
            foreach (var bm in all)
            {
                if (bm == null) continue;
                if (bm.transform.position.y < killY)
                {
                    if (bm == mark && mark.hasDropped && !mark.touchedStack)
                        EndGameMiss(CurrentControllerName());     // 没摞上
                    else
                        EndGameCollapse(CurrentControllerName());  // 塔倒
                    yield break;
                }
            }

            // 2) 落稳检测
            if (rb.velocity.sqrMagnitude < sleepVel * sleepVel)
            {
                acc += Time.deltaTime;
                if (acc >= sleepHold) yield break; // 认为落稳
            }
            else acc = 0f;

            yield return null;
        }
    }

    private float GetCameraBottomY()
    {
        var cam = Camera.main;
        if (cam == null) return -10f; // 兜底
        // 视口 (0,0) 是屏幕左下角
        return cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).y;
    }

    private void SpawnAtFixedHeight(Player forPlayer)
    {
        float xMid = (leftBound.position.x + rightBound.position.x) * 0.5f;
        float y = (spawnY != null) ? spawnY.position.y : 3f;

        var go = Instantiate(blockPrefab, new Vector3(xMid, y, 0f), Quaternion.identity);
        currentMover = go.GetComponent<HoverMover>();
        currentMark = go.GetComponent<BlockMark>();

        if (currentMover == null) { Debug.LogError("[TurnManager] HoverMover missing on Block prefab."); return; }
        if (currentMark == null) { Debug.LogError("[TurnManager] BlockMark missing on Block prefab."); return; }

        currentMover.leftBound = leftBound;
        currentMover.rightBound = rightBound;

        currentMark.isCurrentTurn = true;
        currentMover.StartHover(); // 重力关闭 + 左右移动
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