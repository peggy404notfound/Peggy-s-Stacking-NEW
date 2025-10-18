using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrickWallPlacer : MonoBehaviour
{
    [Header("必填")]
    public Transform baseTransform;                 // 可留空，Awake里按Tag=Base寻找
    public GameObject wallPrefab;

    [Header("左右移动边界（墙上若无 HoverMover 将自动添加并使用这些边界）")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("碰撞设定")]
    public LayerMask solidLayers;
    [Tooltip("当任意接触点法线的 y ≥ 阈值，认为被下面托住；越接近1越严格")]
    public float groundNormalYThreshold = 0.2f;

    // ==== 最后30s 跟随平台 ====
    [Header("最后N秒：墙跟随平台X")]
    public CountdownTimer timer;
    public float followWhenRemainingSeconds = 30f;
    public float followLerp = 30f;                  // 越大越紧
    public bool keepKinematicAfterFinalize = true;  // 定点后保持Kinematic以便跟随

    // ==== 赢家推塔窗口 撤退/失效 ====
    [Header("赢家推塔窗口：撤退/失效")]
    public bool retractOnWinnerPush = true;
    public float retractDuration = 0.20f;
    public Vector2 retractOffset = new Vector2(0f, -1.0f);
    public float winnerPushWindowSeconds = 1.20f;
    public bool hideSpriteWhenRetract = false;

    // 定点后，通知外部“为同一玩家生成下一块左右移动积木”
    public Action<int> onNeedSpawnNext;

    // ========== 运行期 ==========
    GameObject _currentWall;          // 正在放置中的墙（未定点）
    Rigidbody2D _currentRb;
    Collider2D _currentCol;
    SpriteRenderer _currentSr;
    HoverMover _currentMover;
    GameObject _hiddenMoving;         // 被本回合替换掉的左右移动积木
    int _playerId;
    bool _active;                     // 正在放置流程
    bool _isFalling;                  // 已按键进入下落
    bool _finalizedCurrent;           // 已定点（针对当前墙）

    TurnManager _tm;

    // 跟踪“所有已定点墙”
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

    /// <summary>使用砖墙道具：把“当前左右移动积木”替换为砖墙（砖墙继续左右移动；按 S/↓ 开始重力下落）</summary>
    public void Begin(int playerId, GameObject currentMovingBlock)
    {
        if (playerId != 1 && playerId != 2) return;
        if (!wallPrefab || currentMovingBlock == null)
        {
            Debug.LogWarning("[BrickWallPlacer] 缺少 wallPrefab 或 currentMovingBlock");
            return;
        }

        _playerId = playerId;
        _hiddenMoving = currentMovingBlock;
        _hiddenMoving.SetActive(false);

        // 生成“正在放置中的”墙
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
        _currentRb.bodyType = RigidbodyType2D.Kinematic;     // 悬停阶段
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

        // 第一次按键：开始下落
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

        // 第二次按键：手动定点
        if (keyDown && _isFalling && !_finalizedCurrent)
        {
            TryFinalizeFromKey();
        }
    }

    void FixedUpdate()
    {
        // 遍历所有已定点墙进行“最后N秒跟随平台X”
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

    // 供碰撞代理调用
    public void TryFinalizeFromCollision()
    {
        if (_active && _isFalling && !_finalizedCurrent)
            FinalizePlacement();
    }

    void TryFinalizeFromKey() => FinalizePlacement();

    // 确认定点：把“当前墙”登记到 trackedWalls
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

        // 登记
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

        // 清理“当前流程”状态
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
            Debug.LogWarning("[BrickWallPlacer] 未设置 leftBound/rightBound，墙体 HoverMover 将使用默认边界。");
        }

        var fld = typeof(HoverMover).GetField("ownerPlayerId");
        if (fld != null) fld.SetValue(wallMover, _playerId);

        wallMover.StartHover();
        _currentMover = wallMover;
    }

    // ============= 外部调用：赢家推塔窗口，所有墙撤退/失效 =============
    public void BeginWinnerPushWindow() => BeginWinnerPushWindow(winnerPushWindowSeconds);

    public void BeginWinnerPushWindow(float seconds)
    {
        if (!retractOnWinnerPush) return;
        StartCoroutine(CoRetractAll(seconds));
    }

    IEnumerator CoRetractAll(float seconds)
    {
        // 对所有“已定点且未撤退”的墙执行撤退
        foreach (var w in trackedWalls)
        {
            if (w == null || w.retracted || w.go == null) continue;

            // 关闭碰撞
            if (w.col) w.col.enabled = false;

            // 可选：隐藏
            if (hideSpriteWhenRetract && w.sr) w.sr.enabled = false;

            // 视觉：轻微下沉
            if (retractDuration > 0f)
                StartCoroutine(CoLerpPos(w.go.transform, w.finalizePos, w.finalizePos + (Vector3)retractOffset, retractDuration));

            w.retracted = true;
        }

        // 持续整个推塔窗口（用不缩放时间）
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

// 把墙体的碰撞转发给 Placer，用来“触地即定点”
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