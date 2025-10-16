using System.Collections;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public enum Player { P1 = 1, P2 = 2 }

    // —— 单例：给其他脚本调用触发方法 —— //
    public static TurnManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ========== 教学 UI 引用 ==========
    [Header("操作教学（等待输入阶段触发）")]
    public TutorialHintOneShot p1PressHint;   // P1：按 S 落下
    public TutorialHintOneShot p2PressHint;   // P2：按 ↓ 落下

    [Header("道具“首次出现”教学（P1/P2 通用）")]
    public TutorialHintOneShot glueHint;      // 胶水出现
    public TutorialHintOneShot weightHint;    // 秤砣出现
    public TutorialHintOneShot brickHint;     // 砖墙出现
    public TutorialHintOneShot iceHint;      // 冰冻出现
    public TutorialHintOneShot windHint;     // 风扇出现

    [Header("砖墙“首次使用”教学（P1/P2 通用）")]
    public TutorialHintOneShot brickUseHint;

    [Header("玩家“第一次获得任何道具时，告诉按键”")]
    public TutorialHintOneShot p1PropUseKeyHint; // P1 首次获得道具时提示
    public TutorialHintOneShot p2PropUseKeyHint; // P2 首次获得道具时提示

    [Header("红线出现提示（P1/P2 通用）")]
    public TutorialHintOneShot redLineHint; // 红线第一次出现/启动时

    // ========== 回合内状态 ==========
    public GameObject currentMovingBlockP1;
    public GameObject currentMovingBlockP2;

    [HideInInspector] public bool inputLocked = false;
    public void LockTurnInput() { inputLocked = true; }
    public void UnlockTurnInput() { inputLocked = false; }

    [HideInInspector] public bool redirectDropToCurrentMoving = false;
    public void ArmRedirectDrop() { redirectDropToCurrentMoving = true; }

    public Player currentPlayer = Player.P1;
    public bool IsPlayersTurn(Player p) => currentPlayer == p;
    public PropGeneration propGen;
    int _turnId = 0;

    // ========== 生成参数 ==========
    [Header("积木预制体（至少给 P1 赋值）")]
    public GameObject blockPrefabP1;
    public GameObject blockPrefabP2;

    [Header("生成点（仅用 Y）")]
    public Transform spawnPoint;

    [Header("左右边界（必填）")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("回合设置")]
    public bool infiniteTurns = true;
    public int piecesPerPlayer = 10;
    public float inputTimeout = 10f;
    public float settleSpeed = 0.05f;
    public float nextTurnDelay = 0.4f;
    public float fallBelowOffset = 10f;

    int _turnsDone;
    int TotalTurns => piecesPerPlayer * 2;

    // ========== 教学门控与一次性开关 ==========
    // “上一回合是否已完全落定”（音效/震屏结束），控制所有弹窗不会打断
    bool _lastTurnSettled = true;

    // 本局只尝试一次（真正是否弹由 OneShot 自己决定）
    bool _p1PressTried, _p2PressTried;
    bool _glueTried, _weightTried, _brickTried, _brickUseTried;
    bool _p1PropKeyTried, _p2PropKeyTried;
    bool _redLineTried;
    bool _iceTried;
    bool _pendIce;
    bool _windTried;
    bool _pendWind;


    // 待弹队列（如果未落定就请求弹窗，先挂起，等落定后在“等待阶段”弹）
    bool _pendGlue, _pendWeight, _pendBrick, _pendBrickUse;
    bool _pendP1PropKey, _pendP2PropKey;
    bool _pendRedLine;

    void Start()
    {
        GamePause.Resume(); // 确保不是暂停
        StartCoroutine(RunTurns());
    }

    IEnumerator RunTurns()
    {
        if (!spawnPoint) { Debug.LogError("[TurnManager] 请拖入 spawnPoint"); yield break; }
        if (!leftBound || !rightBound) { Debug.LogError("[TurnManager] 请拖入 leftBound/rightBound"); yield break; }

        _turnsDone = 0;

        while ((infiniteTurns || _turnsDone < TotalTurns) && !RisingHandEndUI.IsGameOver)
        {
            _turnId++;
            if (propGen != null) propGen.OnTurnStart(_turnId);

            var current = (_turnsDone % 2 == 0) ? Player.P1 : Player.P2;

            // 1) 生成并开始悬停
            var piece = Spawn(current);
            if (!piece || RisingHandEndUI.IsGameOver) yield break;

            // 2) 等输入（或超时）
            float t = 0f;
            bool dropped = false;
            while (piece && t < inputTimeout && !dropped)
            {
                if (RisingHandEndUI.IsGameOver) yield break;

                // —— 待弹队列：仅在“已落定且当前未暂停”时出一个 —— //
                if (_lastTurnSettled && !GamePause.IsPaused)
                {
                    if (_pendBrickUse) { _pendBrickUse = false; brickUseHint?.TriggerIfNeeded(); }
                    else if (_pendBrick) { _pendBrick = false; brickHint?.TriggerIfNeeded(); }
                    else if (_pendRedLine) { _pendRedLine = false; redLineHint?.TriggerIfNeeded(); }
                    else if (_pendGlue) { _pendGlue = false; glueHint?.TriggerIfNeeded(); }
                    else if (_pendWeight) { _pendWeight = false; weightHint?.TriggerIfNeeded(); }
                    else if (_pendP1PropKey) { _pendP1PropKey = false; p1PropUseKeyHint?.TriggerIfNeeded(); }
                    else if (_pendP2PropKey) { _pendP2PropKey = false; p2PropUseKeyHint?.TriggerIfNeeded(); }
                    else if (_pendIce) { _pendIce = false; iceHint?.TriggerIfNeeded(); }
                    else if (_pendWind) { _pendWind = false; windHint?.TriggerIfNeeded(); }
                }

                // —— 操作教学：在等待阶段、且上一回合已落定时触发一次 —— //
                if (_lastTurnSettled && !GamePause.IsPaused)
                {
                    if (current == Player.P1 && !_p1PressTried) { _p1PressTried = true; p1PressHint?.TriggerIfNeeded(); }
                    else if (current == Player.P2 && !_p2PressTried) { _p2PressTried = true; p2PressHint?.TriggerIfNeeded(); }
                }

                // 暂停/道具占用时不计时不吃输入
                if (GamePause.IsPaused || inputLocked)
                {
                    yield return null;
                    continue;
                }

                if (current == Player.P1 && Input.GetKeyDown(KeyCode.S)) dropped = true;
                if (current == Player.P2 && Input.GetKeyDown(KeyCode.DownArrow)) dropped = true;
                
                var mover = piece ? piece.GetComponent<HoverMover>() : null;
                if (mover && !mover.IsHovering)
                {
                    dropped = true;   // 自动落下已发生，提前结束等待
                }

                t += Time.deltaTime;
                yield return null;
            }
            if (RisingHandEndUI.IsGameOver) yield break;

            // 3) 执行落下（支持重定向到“当前移动块”）
            GameObject dropTarget = piece;
            if (redirectDropToCurrentMoving)
            {
                var reg = GetCurrentMovingBlock((int)current);
                if (reg) dropTarget = reg;
                redirectDropToCurrentMoving = false;
            }

            if (dropTarget)
            {
                var trb = dropTarget.GetComponent<Rigidbody2D>();
                var tmover = dropTarget.GetComponent<HoverMover>();

                if (tmover) tmover.Drop();
                if (trb)
                {
                    trb.bodyType = RigidbodyType2D.Dynamic;
                    trb.WakeUp();
                    if (trb.velocity.sqrMagnitude < 0.0001f)
                        trb.velocity = Vector2.down * 0.01f;
                }

                _lastTurnSettled = false; // 从现在直到监控协程结束都视为“未落定”
                StartCoroutine(MonitorPieceThenScoreAndEndProps(
                    dropTarget, _turnId, settleSpeed, fallBelowOffset));
            }

            // 5) 切换玩家并更新 UI
            currentPlayer = (currentPlayer == Player.P1) ? Player.P2 : Player.P1;
            _turnsDone++;
            RisingHandEndUI.ReportTurn(_turnsDone);

            // 6) 回合间隔
            if (RisingHandEndUI.IsGameOver) yield break;
            yield return new WaitForSeconds(nextTurnDelay);
        }

        if (!infiniteTurns && ScoreManager.Instance && !RisingHandEndUI.IsGameOver)
        {
            ScoreManager.Instance.RecountScores(out int _, out int _2);
        }
    }

    // —— 监控落定/出界，并在落定后开放后续教学 —— //
    IEnumerator MonitorPieceThenScoreAndEndProps(GameObject piece, int thisTurnId, float settleSpeedRef, float fallBelowOffsetRef)
    {
        if (!piece) { _lastTurnSettled = true; yield break; }

        float stableTimer = 0f;

        var rb = piece ? piece.GetComponent<Rigidbody2D>() : null;
        var vz = piece ? piece.GetComponent<ValidZone>() : null;
        var mark = piece ? piece.GetComponent<BlockMark>() : null;
        var cam = Camera.main;

        while (piece && !RisingHandEndUI.IsGameOver)
        {
            if (!rb) break;

            bool fallen = false;
            if (cam)
            {
                float bottom = cam.transform.position.y - cam.orthographicSize;
                if (rb.position.y < bottom - fallBelowOffsetRef) fallen = true;
            }
            if (fallen) break;

            bool touched = (vz && vz.isTowerMember) || (mark && mark.touchedStack);
            bool slowNow = rb.velocity.sqrMagnitude <= settleSpeedRef * settleSpeedRef;

            if (touched && slowNow)
            {
                stableTimer += Time.deltaTime;
                if (stableTimer >= 0.20f) break;
            }
            else stableTimer = 0f;

            yield return null;
        }

        if (RisingHandEndUI.IsGameOver) { _lastTurnSettled = true; yield break; }

        if (ScoreManager.Instance)
            ScoreManager.Instance.RecountScores(out int _, out int _2);

        if (propGen != null) propGen.OnTurnEnd(thisTurnId);

        _lastTurnSettled = true; // 现在允许弹后续教学
    }

    // —— 生成一个左右移动的方块 —— //
    GameObject Spawn(Player p)
    {
        GameObject prefab = null;

        if (RandomShapeAfterN.Instance != null)
            prefab = RandomShapeAfterN.Instance.GetPrefabFor(p, _turnsDone + 1);
        else
            prefab = (p == Player.P1 ? blockPrefabP1 : blockPrefabP2);

        if (!prefab)
        {
            prefab = blockPrefabP1;
            if (!prefab) { Debug.LogError("[TurnManager] 请至少设置 blockPrefabP1"); return null; }
        }

        // ……前面你的 prefab 选择逻辑保持不变
        prefab = IceNextPieceSystem.Instance
                   ? IceNextPieceSystem.Instance.MaybeOverridePrefabFor(p, prefab)
                   : prefab;   // ← 新增这一行：若被标记，则把下一块替换为冰块
        Debug.Log($"[Spawn] player={p}, finalPrefab={prefab.name}");

        float lx = leftBound.position.x, rx = rightBound.position.x;
        float cx = (lx + rx) * 0.5f;
        float sy = spawnPoint.position.y;

        var go = Instantiate(prefab, new Vector3(cx, sy, 0f), Quaternion.identity);
        go.name = $"Block_{p}_{_turnsDone + 1}";

        var mark = go.GetComponent<BlockMark>();
        if (!mark) mark = go.AddComponent<BlockMark>();
        mark.ownerPlayerId = (int)p;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb) rb.bodyType = RigidbodyType2D.Kinematic;

        var mover = go.GetComponent<HoverMover>();
        if (mover)
        {
            float min = Mathf.Min(lx, rx);
            float max = Mathf.Max(lx, rx);
            mover.SetBounds(min, max);
            mover.StartHover();
        }

        SetOwnerIfExists(go, (int)p);
        SetCurrentMovingBlock((int)p, go);
        return go;
    }

    void SetOwnerIfExists(GameObject go, int ownerId)
    {
        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            var f = c.GetType().GetField("ownerPlayerId");
            if (f != null) { f.SetValue(c, ownerId); break; }
        }
    }

    // ========== 外部可调用：登记当前移动块 ==========
    public void SetCurrentMovingBlock(int playerId, GameObject go)
    {
        if (playerId == 1) currentMovingBlockP1 = go;
        else currentMovingBlockP2 = go;
    }
    public GameObject GetCurrentMovingBlock(int playerId)
    {
        return (playerId == 1) ? currentMovingBlockP1 : currentMovingBlockP2;
    }
    public GameObject SpawnMovingBlockFor(int playerId) => Spawn((Player)playerId);

    // ========== 外部可调用：道具“首次出现/使用”提示 ==========
    public void TriggerGlueAppearIfNeeded()
    {
        if (_glueTried) return; _glueTried = true;
        if (!_lastTurnSettled) { _pendGlue = true; return; }
        glueHint?.TriggerIfNeeded();
    }
    public void TriggerWeightAppearIfNeeded()
    {
        if (_weightTried) return; _weightTried = true;
        if (!_lastTurnSettled) { _pendWeight = true; return; }
        weightHint?.TriggerIfNeeded();
    }
    public void TriggerBrickAppearIfNeeded()
    {
        if (_brickTried) return; _brickTried = true;
        if (!_lastTurnSettled) { _pendBrick = true; return; }
        brickHint?.TriggerIfNeeded();
    }
    public void TriggerBrickUseIfNeeded()
    {
        if (_brickUseTried) return; _brickUseTried = true;
        if (!_lastTurnSettled) { _pendBrickUse = true; return; }
        brickUseHint?.TriggerIfNeeded();
    }
    public void TriggerIceAppearIfNeeded()
    {
        if (_iceTried) return; _iceTried = true;
        if (!_lastTurnSettled) { _pendIce = true; return; }
        iceHint?.TriggerIfNeeded();
    }
    public void TriggerWindAppearIfNeeded()
    {
        if (_windTried) return; _windTried = true;
        if (!_lastTurnSettled) { _pendWind = true; return; }
        windHint?.TriggerIfNeeded();
    }


    // ========== 外部可调用：玩家第一次获得道具时“按键使用”提示 ==========
    public void TriggerPropUseKeyHintFor(int playerId)
    {
        if (playerId == 1)
        {
            if (_p1PropKeyTried) return; _p1PropKeyTried = true;
            if (!_lastTurnSettled) { _pendP1PropKey = true; return; }
            p1PropUseKeyHint?.TriggerIfNeeded();
        }
        else
        {
            if (_p2PropKeyTried) return; _p2PropKeyTried = true;
            if (!_lastTurnSettled) { _pendP2PropKey = true; return; }
            p2PropUseKeyHint?.TriggerIfNeeded();
        }
    }

    // ========== 外部可调用：红线首次出现/启动提示 ==========
    public void TriggerRedLineHintIfNeeded()
    {
        if (_redLineTried) return; _redLineTried = true;
        if (!_lastTurnSettled) { _pendRedLine = true; return; }
        redLineHint?.TriggerIfNeeded();
    }
}