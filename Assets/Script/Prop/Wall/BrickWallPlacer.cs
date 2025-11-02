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
    public float followLerp = 30f;
    public bool keepKinematicAfterFinalize = true;

    // ==== 赢家推塔窗口 撤退/失效 ====
    [Header("赢家推塔窗口：撤退/失效")]
    public bool retractOnWinnerPush = true;
    public float retractDuration = 0.20f;
    public Vector2 retractOffset = new Vector2(0f, -1.0f);
    public float winnerPushWindowSeconds = 1.20f;
    public bool hideSpriteWhenRetract = false;

    [Header("音效（新增）")]
    public AudioClip useWallSfx;            // 使用砖墙道具时音效
    [Range(0f, 1f)] public float useSfxVolume = 1f;
    public AudioClip finalizeWallSfx;       // 砖墙定点时音效
    [Range(0f, 1f)] public float finalizeSfxVolume = 1f;

    public Action<int> onNeedSpawnNext;

    // ========== 运行期 ==========
    GameObject _currentWall;
    Rigidbody2D _currentRb;
    Collider2D _currentCol;
    SpriteRenderer _currentSr;
    HoverMover _currentMover;
    GameObject _hiddenMoving;
    int _playerId;
    bool _active;
    bool _isFalling;
    bool _finalizedCurrent;

    TurnManager _tm;

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

    /// <summary>使用砖墙道具：把“当前左右移动积木”替换为砖墙</summary>
    public void Begin(int playerId, GameObject currentMovingBlock)
    {
        // --- 新增：播放使用音效 ---
        if (useWallSfx != null)
        {
            AudioSource.PlayClipAtPoint(useWallSfx, Camera.main.transform.position, useSfxVolume);
        }

        if (playerId != 1 && playerId != 2) return;
        if (!wallPrefab || currentMovingBlock == null)
        {
            Debug.LogWarning("[BrickWallPlacer] 缺少 wallPrefab 或 currentMovingBlock");
            return;
        }

        _playerId = playerId;
        _hiddenMoving = currentMovingBlock;
        _hiddenMoving.SetActive(false);

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
        _currentRb.bodyType = RigidbodyType2D.Kinematic;
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

        if (keyDown && _isFalling && !_finalizedCurrent)
        {
            TryFinalizeFromKey();
        }
    }

    void FixedUpdate()
    {
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

    public void TryFinalizeFromCollision()
    {
        if (_active && _isFalling && !_finalizedCurrent)
            FinalizePlacement();
    }

    void TryFinalizeFromKey() => FinalizePlacement();

    void FinalizePlacement()
    {
        if (_finalizedCurrent) return;
        _finalizedCurrent = true;

        // --- 新增：播放定点音效 ---
        if (finalizeWallSfx != null)
        {
            AudioSource.PlayClipAtPoint(finalizeWallSfx, Camera.main.transform.position, finalizeSfxVolume);
        }

        float xOffset = 0f;
        if (baseTransform)
            xOffset = _currentWall.transform.position.x - baseTransform.position.x;

        if (_currentRb)
        {
            _currentRb.velocity = Vector2.zero;
            _currentRb.angularVelocity = 0f;
            _currentRb.bodyType = keepKinematicAfterFinalize ? RigidbodyType2D.Kinematic : RigidbodyType2D.Static;
        }

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
            Debug.LogWarning("[BrickWallPlacer] 未设置 leftBound/rightBound。");
        }

        var fld = typeof(HoverMover).GetField("ownerPlayerId");
        if (fld != null) fld.SetValue(wallMover, _playerId);

        wallMover.StartHover();
        _currentMover = wallMover;
    }

    public void BeginWinnerPushWindow() => BeginWinnerPushWindow(winnerPushWindowSeconds);

    public void BeginWinnerPushWindow(float seconds)
    {
        if (!retractOnWinnerPush) return;
        StartCoroutine(CoRetractAll(seconds));
    }

    IEnumerator CoRetractAll(float seconds)
    {
        foreach (var w in trackedWalls)
        {
            if (w == null || w.retracted || w.go == null) continue;
            if (w.col) w.col.enabled = false;
            if (hideSpriteWhenRetract && w.sr) w.sr.enabled = false;
            if (retractDuration > 0f)
                StartCoroutine(CoLerpPos(w.go.transform, w.finalizePos, w.finalizePos + (Vector3)retractOffset, retractDuration));
            w.retracted = true;
        }

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