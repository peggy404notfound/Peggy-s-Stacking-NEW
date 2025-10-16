using System.Collections;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public enum Player { P1 = 1, P2 = 2 }

    // ���� �������������ű����ô������� ���� //
    public static TurnManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ========== ��ѧ UI ���� ==========
    [Header("������ѧ���ȴ�����׶δ�����")]
    public TutorialHintOneShot p1PressHint;   // P1���� S ����
    public TutorialHintOneShot p2PressHint;   // P2���� �� ����

    [Header("���ߡ��״γ��֡���ѧ��P1/P2 ͨ�ã�")]
    public TutorialHintOneShot glueHint;      // ��ˮ����
    public TutorialHintOneShot weightHint;    // ���ȳ���
    public TutorialHintOneShot brickHint;     // שǽ����
    public TutorialHintOneShot iceHint;      // ��������
    public TutorialHintOneShot windHint;     // ���ȳ���

    [Header("שǽ���״�ʹ�á���ѧ��P1/P2 ͨ�ã�")]
    public TutorialHintOneShot brickUseHint;

    [Header("��ҡ���һ�λ���κε���ʱ�����߰�����")]
    public TutorialHintOneShot p1PropUseKeyHint; // P1 �״λ�õ���ʱ��ʾ
    public TutorialHintOneShot p2PropUseKeyHint; // P2 �״λ�õ���ʱ��ʾ

    [Header("���߳�����ʾ��P1/P2 ͨ�ã�")]
    public TutorialHintOneShot redLineHint; // ���ߵ�һ�γ���/����ʱ

    // ========== �غ���״̬ ==========
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

    // ========== ���ɲ��� ==========
    [Header("��ľԤ���壨���ٸ� P1 ��ֵ��")]
    public GameObject blockPrefabP1;
    public GameObject blockPrefabP2;

    [Header("���ɵ㣨���� Y��")]
    public Transform spawnPoint;

    [Header("���ұ߽磨���")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("�غ�����")]
    public bool infiniteTurns = true;
    public int piecesPerPlayer = 10;
    public float inputTimeout = 10f;
    public float settleSpeed = 0.05f;
    public float nextTurnDelay = 0.4f;
    public float fallBelowOffset = 10f;

    int _turnsDone;
    int TotalTurns => piecesPerPlayer * 2;

    // ========== ��ѧ�ſ���һ���Կ��� ==========
    // ����һ�غ��Ƿ�����ȫ�䶨������Ч/�������������������е���������
    bool _lastTurnSettled = true;

    // ����ֻ����һ�Σ������Ƿ��� OneShot �Լ�������
    bool _p1PressTried, _p2PressTried;
    bool _glueTried, _weightTried, _brickTried, _brickUseTried;
    bool _p1PropKeyTried, _p2PropKeyTried;
    bool _redLineTried;
    bool _iceTried;
    bool _pendIce;
    bool _windTried;
    bool _pendWind;


    // �������У����δ�䶨�����󵯴����ȹ��𣬵��䶨���ڡ��ȴ��׶Ρ�����
    bool _pendGlue, _pendWeight, _pendBrick, _pendBrickUse;
    bool _pendP1PropKey, _pendP2PropKey;
    bool _pendRedLine;

    void Start()
    {
        GamePause.Resume(); // ȷ��������ͣ
        StartCoroutine(RunTurns());
    }

    IEnumerator RunTurns()
    {
        if (!spawnPoint) { Debug.LogError("[TurnManager] ������ spawnPoint"); yield break; }
        if (!leftBound || !rightBound) { Debug.LogError("[TurnManager] ������ leftBound/rightBound"); yield break; }

        _turnsDone = 0;

        while ((infiniteTurns || _turnsDone < TotalTurns) && !RisingHandEndUI.IsGameOver)
        {
            _turnId++;
            if (propGen != null) propGen.OnTurnStart(_turnId);

            var current = (_turnsDone % 2 == 0) ? Player.P1 : Player.P2;

            // 1) ���ɲ���ʼ��ͣ
            var piece = Spawn(current);
            if (!piece || RisingHandEndUI.IsGameOver) yield break;

            // 2) �����루��ʱ��
            float t = 0f;
            bool dropped = false;
            while (piece && t < inputTimeout && !dropped)
            {
                if (RisingHandEndUI.IsGameOver) yield break;

                // ���� �������У����ڡ����䶨�ҵ�ǰδ��ͣ��ʱ��һ�� ���� //
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

                // ���� ������ѧ���ڵȴ��׶Ρ�����һ�غ����䶨ʱ����һ�� ���� //
                if (_lastTurnSettled && !GamePause.IsPaused)
                {
                    if (current == Player.P1 && !_p1PressTried) { _p1PressTried = true; p1PressHint?.TriggerIfNeeded(); }
                    else if (current == Player.P2 && !_p2PressTried) { _p2PressTried = true; p2PressHint?.TriggerIfNeeded(); }
                }

                // ��ͣ/����ռ��ʱ����ʱ��������
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
                    dropped = true;   // �Զ������ѷ�������ǰ�����ȴ�
                }

                t += Time.deltaTime;
                yield return null;
            }
            if (RisingHandEndUI.IsGameOver) yield break;

            // 3) ִ�����£�֧���ض��򵽡���ǰ�ƶ��顱��
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

                _lastTurnSettled = false; // ������ֱ�����Э�̽�������Ϊ��δ�䶨��
                StartCoroutine(MonitorPieceThenScoreAndEndProps(
                    dropTarget, _turnId, settleSpeed, fallBelowOffset));
            }

            // 5) �л���Ҳ����� UI
            currentPlayer = (currentPlayer == Player.P1) ? Player.P2 : Player.P1;
            _turnsDone++;
            RisingHandEndUI.ReportTurn(_turnsDone);

            // 6) �غϼ��
            if (RisingHandEndUI.IsGameOver) yield break;
            yield return new WaitForSeconds(nextTurnDelay);
        }

        if (!infiniteTurns && ScoreManager.Instance && !RisingHandEndUI.IsGameOver)
        {
            ScoreManager.Instance.RecountScores(out int _, out int _2);
        }
    }

    // ���� ����䶨/���磬�����䶨�󿪷ź�����ѧ ���� //
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

        _lastTurnSettled = true; // ��������������ѧ
    }

    // ���� ����һ�������ƶ��ķ��� ���� //
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
            if (!prefab) { Debug.LogError("[TurnManager] ���������� blockPrefabP1"); return null; }
        }

        // ����ǰ����� prefab ѡ���߼����ֲ���
        prefab = IceNextPieceSystem.Instance
                   ? IceNextPieceSystem.Instance.MaybeOverridePrefabFor(p, prefab)
                   : prefab;   // �� ������һ�У�������ǣ������һ���滻Ϊ����
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

    // ========== �ⲿ�ɵ��ã��Ǽǵ�ǰ�ƶ��� ==========
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

    // ========== �ⲿ�ɵ��ã����ߡ��״γ���/ʹ�á���ʾ ==========
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


    // ========== �ⲿ�ɵ��ã���ҵ�һ�λ�õ���ʱ������ʹ�á���ʾ ==========
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

    // ========== �ⲿ�ɵ��ã������״γ���/������ʾ ==========
    public void TriggerRedLineHintIfNeeded()
    {
        if (_redLineTried) return; _redLineTried = true;
        if (!_lastTurnSettled) { _pendRedLine = true; return; }
        redLineHint?.TriggerIfNeeded();
    }
}