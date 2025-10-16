using System;
using UnityEngine;

public class BrickWallPlacer : MonoBehaviour
{
    [Header("必填")]
    public Transform baseTransform;                 // 可留空，仅保持一致
    public GameObject wallPrefab;                   // 砖墙预制体（SpriteRenderer + BoxCollider2D）

    [Header("左右移动边界（墙上若无 HoverMover 将自动添加并使用这些边界）")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("碰撞设定")]
    public LayerMask solidLayers;                   // 能托住墙的层（Base/Stack/Wall 等）
    [Tooltip("当任意接触点法线的 y ≥ 阈值，认为被下面托住；越接近1越严格")]
    public float groundNormalYThreshold = 0.2f;

    // 砖墙定点后，通知外部为“同一玩家”生成新的左右移动积木
    public Action<int> onNeedSpawnNext;

    // 运行期
    GameObject _wall;               // 当前放置的砖墙
    Rigidbody2D _rb;                // 墙刚体
    HoverMover _wallMover;          // 墙上的 HoverMover（按键时禁用）
    GameObject _hiddenMoving;       // 被替换的原移动积木
    int _playerId;                  // 1/2
    bool _active;                   // 是否在放置流程中
    bool _isFalling;                // 是否已开始重力下落
    bool _finalized;                // 是否已定点

    TurnManager _tm;                // 用于上锁/解锁输入 & 标记“把下一次落下指向当前登记的移动块”

    void Awake()
    {
        _tm = FindObjectOfType<TurnManager>();
    }

    /// <summary>
    /// 使用砖墙道具：把“当前左右移动积木”替换为砖墙（砖墙继续左右移动；按 S/↓ 开始重力下落）
    /// </summary>
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
        _hiddenMoving.SetActive(false); // 隐藏本回合原始块（不要销毁）

        // 在原位置生成实体砖墙
        _wall = Instantiate(wallPrefab, currentMovingBlock.transform.position, Quaternion.identity);
        _wall.name = $"Wall_P{playerId}";

        // 确保不透明 & collider 实体
        var sr = _wall.GetComponent<SpriteRenderer>();
        if (sr) { var c = sr.color; c.a = 1f; sr.color = c; }

        var bc = _wall.GetComponent<BoxCollider2D>();
        if (!bc) bc = _wall.AddComponent<BoxCollider2D>();
        bc.isTrigger = false; // 必须是实体碰撞体

        // 刚体：先 Kinematic（不受重力），按键后切 Dynamic
        _rb = _wall.GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = _wall.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = Mathf.Max(1f, _rb.gravityScale); // 切 Dynamic 后生效
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.simulated = true;

        // 让砖墙左右移动（如果 prefab 无 HoverMover 就自动加一个）
        SetupHoverMoverOnWall(currentMovingBlock);

        // 给墙体挂碰撞代理 → 把墙身上的碰撞回调到本脚本
        var proxy = _wall.AddComponent<BrickWallCollisionProxy>();
        proxy.Init(this, solidLayers, groundNormalYThreshold);

        // 进入砖墙流程：锁住 TurnManager 的 S/↓ 输入
        _tm?.LockTurnInput();

        _active = true;
        _isFalling = false;
        _finalized = false;
    }

    void Update()
    {
        if (!_active || _finalized || _wall == null) return;

        bool keyDown = (_playerId == 1) ? Input.GetKeyDown(KeyCode.S)
                                        : Input.GetKeyDown(KeyCode.DownArrow);

        // 第一次按键：开始下落
        if (keyDown && !_isFalling)
        {
            _isFalling = true;

            // 停掉左右悬停（不改 HoverMover 本体，只在这里关闭）
            if (_wallMover != null)
            {
                try { _wallMover.Drop(); } catch { }
                _wallMover.enabled = false;
            }

            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;      // 交给物理重力
                if (_rb.gravityScale <= 0f) _rb.gravityScale = 1f;

                // 轻推一下，避免 v=0 卡住
                if (_rb.velocity.sqrMagnitude < 0.0001f)
                    _rb.AddForce(Vector2.down * 0.1f, ForceMode2D.Impulse);

                _rb.WakeUp();
            }
            return;
        }

        // 第二次按键：手动定点（空中也可定点）
        if (keyDown && _isFalling && !_finalized)
        {
            TryFinalizeFromKey();
        }
    }

    // 供碰撞代理调用：若满足条件则完成放置
    public void TryFinalizeFromCollision()
    {
        if (_active && _isFalling && !_finalized)
            FinalizePlacement();
    }

    // 供按键调用的定点（与碰撞定点一致）
    void TryFinalizeFromKey()
    {
        FinalizePlacement();
    }

    // —— 确认定点：停止流程，并让同一玩家生成下一块左右移动积木 —— //
    void FinalizePlacement()
    {
        if (_finalized) return;
        _finalized = true;

        // 彻底停住墙体
        if (_rb)
        {
            _rb.velocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.bodyType = RigidbodyType2D.Static; // 锁定当前位置
        }

        _active = false;
        _isFalling = false;

        int pid = _playerId;
        _playerId = 0;

        // ★ 告诉 TurnManager：下一次 S/↓ 要“重定向”到当前登记的移动块（而不是之前被隐藏的那块）
        _tm?.ArmRedirectDrop();

        // 生成同一玩家的新“左右移动积木”
        onNeedSpawnNext?.Invoke(pid);

        // 解锁 TurnManager 输入（回到主循环，等待这位玩家对“新积木”按 S/↓）
        StartCoroutine(UnlockAfterKeyRelease(pid));   // ← 延迟到按键抬起再解锁

        // 旧移动块的引用交由外部新的生成流程自然覆盖
        _hiddenMoving = null;

        System.Collections.IEnumerator UnlockAfterKeyRelease(int pid)
        {
            // 先等 1 帧，避免同一帧里被 TurnManager 看到这次按键
            yield return null;

            // 选对本玩家使用的那个键
            KeyCode key = (pid == 1) ? KeyCode.S : KeyCode.DownArrow;

            // 等到本次按键完全抬起
            while (Input.GetKey(key))
                yield return null;

            // 再多等一帧更保险（避免边沿抖动）
            yield return null;

            // 现在才解锁；下一次新的按键才会被 TurnManager 消费
            _tm?.UnlockTurnInput();
        }

    }

    // —— 在“墙”上准备 HoverMover（必须在墙身上） —— //
    void SetupHoverMoverOnWall(GameObject sourceMovingBlock)
    {
        var wallMover = _wall.GetComponent<HoverMover>();
        if (wallMover == null) wallMover = _wall.AddComponent<HoverMover>();

        // 设置边界：优先使用本脚本拖的左右边界
        if (leftBound && rightBound)
        {
            float min = Mathf.Min(leftBound.position.x, rightBound.position.x);
            float max = Mathf.Max(leftBound.position.x, rightBound.position.x);
            wallMover.SetBounds(min, max);
        }
        else
        {
            Debug.LogWarning("[BrickWallPlacer] 未设置 leftBound/rightBound，墙体 HoverMover 将使用其默认边界。");
        }

        // 设置所有权（若 HoverMover 有该字段则赋值；没有就忽略）
        var fld = typeof(HoverMover).GetField("ownerPlayerId");
        if (fld != null) fld.SetValue(wallMover, _playerId);

        wallMover.StartHover();
        _wallMover = wallMover; // 保存引用，按键时禁用
    }
}

// —— 附着在“墙体”上的小组件：把 OnCollisionEnter2D 转发给 BrickWallPlacer —— //
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

        // 至少一个接触点法线朝上 → 认为被托住
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