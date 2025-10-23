using System.Collections;
using UnityEngine;

public class TurnManagerSimple : MonoBehaviour
{
    public enum Player { P1 = 1, P2 = 2 }

    [Header("基本参数")]
    public Transform spawnPoint;
    public Transform leftBound;
    public Transform rightBound;

    [Header("预制体（至少给 P1 赋值）")]
    public GameObject blockPrefabP1;
    public GameObject blockPrefabP2;

    [Header("回合设置")]
    public bool infiniteTurns = true;
    public int piecesPerPlayer = 10;
    public float inputTimeout = 10f;
    public float nextTurnDelay = 0.3f;

    [Header("按键设置")]
    public KeyCode p1DropKey = KeyCode.S;
    public KeyCode p2DropKey = KeyCode.DownArrow;

    private int _turnsDone = 0;
    private Player _current = Player.P1;

    void Start()
    {
        if (!spawnPoint) { Debug.LogError("[TurnManagerSimple] 请拖入 spawnPoint"); enabled = false; return; }
        if (!leftBound || !rightBound) { Debug.LogError("[TurnManagerSimple] 请拖入 leftBound/rightBound"); enabled = false; return; }
        if (!blockPrefabP1 && !blockPrefabP2) { Debug.LogError("[TurnManagerSimple] 至少设置 blockPrefabP1"); enabled = false; return; }
        StartCoroutine(RunTurns());
    }

    IEnumerator RunTurns()
    {
        _turnsDone = 0;
        _current = Player.P1;
        int totalTurns = piecesPerPlayer * 2;

        while (infiniteTurns || _turnsDone < totalTurns)
        {
            GameObject piece = Spawn(_current);
            if (!piece) yield break;

            float t = 0f; bool dropped = false;
            while (!dropped && piece)
            {
                if (_current == Player.P1 ? Input.GetKeyDown(p1DropKey) : Input.GetKeyDown(p2DropKey))
                    dropped = true;

                t += Time.deltaTime;
                if (t >= inputTimeout) dropped = true;

                if (!dropped) { yield return null; continue; }

                // 执行下落
                var hm = piece.GetComponent<HoverMoverSimple>();
                if (hm != null) hm.Drop();
                else
                {
                    var rb = piece.GetComponent<Rigidbody2D>();
                    if (rb)
                    {
                        rb.bodyType = RigidbodyType2D.Dynamic;
                        rb.gravityScale = 1f;
                        rb.velocity = Vector2.down * 0.01f;
                        rb.WakeUp();
                    }
                }
            }

            yield return new WaitForSeconds(nextTurnDelay);
            _current = (_current == Player.P1) ? Player.P2 : Player.P1;
            _turnsDone++;
        }
    }

    GameObject Spawn(Player p)
    {
        GameObject prefab = (p == Player.P1)
            ? (blockPrefabP1 ? blockPrefabP1 : blockPrefabP2)
            : (blockPrefabP2 ? blockPrefabP2 : blockPrefabP1);
        if (!prefab) { Debug.LogError("[TurnManagerSimple] 请至少设置一个预制体"); return null; }

        float lx = Mathf.Min(leftBound.position.x, rightBound.position.x);
        float rx = Mathf.Max(leftBound.position.x, rightBound.position.x);
        Vector3 pos = new Vector3((lx + rx) * 0.5f, spawnPoint.position.y, 0f);

        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.name = $"Block_{p}_{_turnsDone + 1}";

        // 确保有刚体
        var rb = go.GetComponent<Rigidbody2D>();
        if (!rb) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;

        // 确保有 HoverMoverSimple，并启动
        var hover = go.GetComponent<HoverMoverSimple>();
        if (!hover) hover = go.AddComponent<HoverMoverSimple>();
        hover.moveSpeed = Mathf.Max(hover.moveSpeed, 0.1f);
        hover.SetBounds(lx, rx);
        hover.StartHover();

        return go;
    }
}