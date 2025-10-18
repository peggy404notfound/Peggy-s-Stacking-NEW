using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrickWallPlacer : MonoBehaviour
{
    [Header("����")]
    public Transform baseTransform;                 // �����գ�Awake�ﰴTag=BaseѰ��
    public GameObject wallPrefab;

    [Header("�����ƶ��߽磨ǽ������ HoverMover ���Զ���Ӳ�ʹ����Щ�߽磩")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("��ײ�趨")]
    public LayerMask solidLayers;
    [Tooltip("������Ӵ��㷨�ߵ� y �� ��ֵ����Ϊ��������ס��Խ�ӽ�1Խ�ϸ�")]
    public float groundNormalYThreshold = 0.2f;

    // ==== ���30s ����ƽ̨ ====
    [Header("���N�룺ǽ����ƽ̨X")]
    public CountdownTimer timer;
    public float followWhenRemainingSeconds = 30f;
    public float followLerp = 30f;                  // Խ��Խ��
    public bool keepKinematicAfterFinalize = true;  // ����󱣳�Kinematic�Ա����

    // ==== Ӯ���������� ����/ʧЧ ====
    [Header("Ӯ���������ڣ�����/ʧЧ")]
    public bool retractOnWinnerPush = true;
    public float retractDuration = 0.20f;
    public Vector2 retractOffset = new Vector2(0f, -1.0f);
    public float winnerPushWindowSeconds = 1.20f;
    public bool hideSpriteWhenRetract = false;

    // �����֪ͨ�ⲿ��Ϊͬһ���������һ�������ƶ���ľ��
    public Action<int> onNeedSpawnNext;

    // ========== ������ ==========
    GameObject _currentWall;          // ���ڷ����е�ǽ��δ���㣩
    Rigidbody2D _currentRb;
    Collider2D _currentCol;
    SpriteRenderer _currentSr;
    HoverMover _currentMover;
    GameObject _hiddenMoving;         // �����غ��滻���������ƶ���ľ
    int _playerId;
    bool _active;                     // ���ڷ�������
    bool _isFalling;                  // �Ѱ�����������
    bool _finalizedCurrent;           // �Ѷ��㣨��Ե�ǰǽ��

    TurnManager _tm;

    // ���١������Ѷ���ǽ��
    class TrackedWall
    {
        public GameObject go;
        public Rigidbody2D rb;
        public Collider2D col;
        public SpriteRenderer sr;
        public float xOffsetFromBase;
        public Vector3 finalizePos;
        public bool retracted;
    }
    readonly List<TrackedWall> trackedWalls = new List<TrackedWall>();

    void Awake()
    {
        _tm = FindObjectOfType<TurnManager>();
        if (!baseTransform)
        {
            var baseGo = GameObject.FindWithTag("Base");
            if (baseGo) baseTransform = baseGo.transform;
        }
    }

    /// <summary>ʹ��שǽ���ߣ��ѡ���ǰ�����ƶ���ľ���滻Ϊשǽ��שǽ���������ƶ����� S/�� ��ʼ�������䣩</summary>
    public void Begin(int playerId, GameObject currentMovingBlock)
    {
        if (playerId != 1 && playerId != 2) return;
        if (!wallPrefab || currentMovingBlock == null)
        {
            Debug.LogWarning("[BrickWallPlacer] ȱ�� wallPrefab �� currentMovingBlock");
            return;
        }

        _playerId = playerId;
        _hiddenMoving = currentMovingBlock;
        _hiddenMoving.SetActive(false);

        // ���ɡ����ڷ����еġ�ǽ
        _currentWall = Instantiate(wallPrefab, currentMovingBlock.transform.position, Quaternion.identity);
        _currentWall.name = $"Wall_P{playerId}";

        _currentSr = _currentWall.GetComponent<SpriteRenderer>();
        if (_currentSr) { var c = _currentSr.color; c.a = 1f; _currentSr.color = c; }

        var bc = _currentWall.GetComponent<BoxCollider2D>();
        if (!bc) bc = _currentWall.AddComponent<BoxCollider2D>();
        bc.isTrigger = false;
        _currentCol = bc;

        _currentRb = _currentWall.GetComponent<Rigidbody2D>();
        if (_currentRb == null) _currentRb = _currentWall.AddComponent<Rigidbody2D>();
        _currentRb.bodyType = RigidbodyType2D.Kinematic;     // ��ͣ�׶�
        _currentRb.gravityScale = Mathf.Max(1f, _currentRb.gravityScale);
        _currentRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _currentRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _currentRb.simulated = true;

        SetupHoverMoverOnWall(currentMovingBlock);

        var proxy = _currentWall.AddComponent<BrickWallCollisionProxy>();
        proxy.Init(this, solidLayers, groundNormalYThreshold);

        _tm?.LockTurnInput();

        _active = true;
        _isFalling = false;
        _finalizedCurrent = false;
    }

    void Update()
    {
        if (!_active || _finalizedCurrent || _currentWall == null) return;

        bool keyDown = (_playerId == 1) ? Input.GetKeyDown(KeyCode.S)
                                        : Input.GetKeyDown(KeyCode.DownArrow);

        // ��һ�ΰ�������ʼ����
        if (keyDown && !_isFalling)
        {
            _isFalling = true;

            if (_currentMover != null)
            {
                try { _currentMover.Drop(); } catch { }
                _currentMover.enabled = false;
            }

            if (_currentRb != null)
            {
                _currentRb.bodyType = RigidbodyType2D.Dynamic;
                if (_currentRb.gravityScale <= 0f) _currentRb.gravityScale = 1f;
                if (_currentRb.velocity.sqrMagnitude < 0.0001f)
                    _currentRb.AddForce(Vector2.down * 0.1f, ForceMode2D.Impulse);
                _currentRb.WakeUp();
            }
            return;
        }

        // �ڶ��ΰ������ֶ�����
        if (keyDown && _isFalling && !_finalizedCurrent)
        {
            TryFinalizeFromKey();
        }
    }

    void FixedUpdate()
    {
        // ���������Ѷ���ǽ���С����N�����ƽ̨X��
        if (baseTransform && timer != null && timer.RemainingSeconds > 0f &&
            timer.RemainingSeconds <= followWhenRemainingSeconds)
        {
            float alpha = 1f - Mathf.Exp(-followLerp * Time.fixedDeltaTime);
            for (int i = 0; i < trackedWalls.Count; i++)
            {
                var w = trackedWalls[i];
                if (w == null || w.retracted || w.rb == null) continue;

                Vector2 cur = w.rb.position;
                float targetX = baseTransform.position.x + w.xOffsetFromBase;
                float newX = Mathf.Lerp(cur.x, targetX, alpha);
                w.rb.MovePosition(new Vector2(newX, cur.y));
            }
        }
    }

    // ����ײ�������
    public void TryFinalizeFromCollision()
    {
        if (_active && _isFalling && !_finalizedCurrent)
            FinalizePlacement();
    }

    void TryFinalizeFromKey() => FinalizePlacement();

    // ȷ�϶��㣺�ѡ���ǰǽ���Ǽǵ� trackedWalls
    void FinalizePlacement()
    {
        if (_finalizedCurrent) return;
        _finalizedCurrent = true;

        float xOffset = 0f;
        if (baseTransform)
            xOffset = _currentWall.transform.position.x - baseTransform.position.x;

        if (_currentRb)
        {
            _currentRb.velocity = Vector2.zero;
            _currentRb.angularVelocity = 0f;
            _currentRb.bodyType = keepKinematicAfterFinalize ? RigidbodyType2D.Kinematic : RigidbodyType2D.Static;
        }

        // �Ǽ�
        trackedWalls.Add(new TrackedWall
        {
            go = _currentWall,
            rb = _currentRb,
            col = _currentCol,
            sr = _currentSr,
            xOffsetFromBase = xOffset,
            finalizePos = _currentWall.transform.position,
            retracted = false
        });

        // ������ǰ���̡�״̬
        _active = false;
        _isFalling = false;

        int pid = _playerId;
        _playerId = 0;

        _tm?.ArmRedirectDrop();
        onNeedSpawnNext?.Invoke(pid);
        StartCoroutine(UnlockAfterKeyRelease(pid));

        _hiddenMoving = null;
        _currentWall = null;
        _currentRb = null;
        _currentCol = null;
        _currentSr = null;
        _currentMover = null;
    }

    IEnumerator UnlockAfterKeyRelease(int pid)
    {
        yield return null;
        KeyCode key = (pid == 1) ? KeyCode.S : KeyCode.DownArrow;
        while (Input.GetKey(key)) yield return null;
        yield return null;
        _tm?.UnlockTurnInput();
        yield break;
    }

    void SetupHoverMoverOnWall(GameObject sourceMovingBlock)
    {
        var wallMover = _currentWall.GetComponent<HoverMover>();
        if (wallMover == null) wallMover = _currentWall.AddComponent<HoverMover>();

        if (leftBound && rightBound)
        {
            float min = Mathf.Min(leftBound.position.x, rightBound.position.x);
            float max = Mathf.Max(leftBound.position.x, rightBound.position.x);
            wallMover.SetBounds(min, max);
        }
        else
        {
            Debug.LogWarning("[BrickWallPlacer] δ���� leftBound/rightBound��ǽ�� HoverMover ��ʹ��Ĭ�ϱ߽硣");
        }

        var fld = typeof(HoverMover).GetField("ownerPlayerId");
        if (fld != null) fld.SetValue(wallMover, _playerId);

        wallMover.StartHover();
        _currentMover = wallMover;
    }

    // ============= �ⲿ���ã�Ӯ���������ڣ�����ǽ����/ʧЧ =============
    public void BeginWinnerPushWindow() => BeginWinnerPushWindow(winnerPushWindowSeconds);

    public void BeginWinnerPushWindow(float seconds)
    {
        if (!retractOnWinnerPush) return;
        StartCoroutine(CoRetractAll(seconds));
    }

    IEnumerator CoRetractAll(float seconds)
    {
        // �����С��Ѷ�����δ���ˡ���ǽִ�г���
        foreach (var w in trackedWalls)
        {
            if (w == null || w.retracted || w.go == null) continue;

            // �ر���ײ
            if (w.col) w.col.enabled = false;

            // ��ѡ������
            if (hideSpriteWhenRetract && w.sr) w.sr.enabled = false;

            // �Ӿ�����΢�³�
            if (retractDuration > 0f)
                StartCoroutine(CoLerpPos(w.go.transform, w.finalizePos, w.finalizePos + (Vector3)retractOffset, retractDuration));

            w.retracted = true;
        }

        // ���������������ڣ��ò�����ʱ�䣩
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator CoLerpPos(Transform tr, Vector3 from, Vector3 to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            tr.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        tr.position = to;
    }
}

// ��ǽ�����ײת���� Placer�����������ؼ����㡱
public class BrickWallCollisionProxy : MonoBehaviour
{
    BrickWallPlacer owner;
    LayerMask solid;
    float yThreshold;

    public void Init(BrickWallPlacer owner, LayerMask solidLayers, float groundNormalYThreshold)
    {
        this.owner = owner;
        solid = solidLayers;
        yThreshold = groundNormalYThreshold;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (owner == null) return;
        if ((solid.value & (1 << c.collider.gameObject.layer)) == 0) return;

        for (int i = 0; i < c.contactCount; i++)
        {
            if (c.GetContact(i).normal.y >= yThreshold)
            {
                owner.TryFinalizeFromCollision();
                break;
            }
        }
    }
}