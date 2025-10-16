using UnityEngine;

[DisallowMultipleComponent]
public class PropInventoryRouter : MonoBehaviour
{
    // —— 道具 ID（要与 PropItem.propId 保持一致）——
    private const string ID_WALL = "brickwall";
    private const string ID_WEIGHT = "weight";
    private const string ID_GLUE = "glue";
    private const string ID_ICE = "ice";
    private const string ID_WIND = "wind";


    private TurnManager _tm;   // 引用 TurnManager（用于：当前回合判断、触发教学）

    void Awake()
    {
        _tm = FindObjectOfType<TurnManager>();
    }

    // 检查是不是该玩家的回合（无 TM 时放行）
    bool IsPlayersTurnSafe(TurnManager.Player p)
    {
        if (_tm == null) return true;
        return _tm.IsPlayersTurn(p);
    }

    // 是否允许使用（暂停/道具占用时禁止）
    bool CanUseNow()
    {
        if (GamePause.IsPaused) return false;
        if (_tm != null && _tm.inputLocked) return false;
        return true;
    }

    /// <summary>
    /// 让指定玩家使用背包里的道具（按键路由调用）
    /// </summary>
    public void UseFor(int playerId)
    {
        if (!CanUseNow()) return;

        // 1) 取出该玩家的背包
        var inv = PlayerInventoryOneSlot.GetForPlayer(playerId);
        if (inv == null) return;

        // 2) 消耗槽位里的道具（如果需要“先预览后消费”，可以把 Consume 改为 Peek）
        var id = inv.Consume();
        if (string.IsNullOrEmpty(id)) return;

        // 3) 根据道具 ID 分发
        if (id == ID_WALL)
        {
            var placer = FindObjectOfType<BrickWallPlacer>();
            if (placer == null)
            {
                Debug.LogWarning("[Prop] 没找到 BrickWallPlacer，无法放置砖墙");
                return;
            }

            // 从 TurnManager 拿“当前左右移动的积木”
            var moving = _tm != null ? _tm.GetCurrentMovingBlock(playerId) : null;
            if (moving == null)
            {
                Debug.LogWarning($"[Prop] P{playerId} 当前没有左右移动的积木（也许还没生成/没登记）");
                return;
            }

            // 砖墙停住后：为“同一玩家”生成下一块左右移动积木
            placer.onNeedSpawnNext = (pid) =>
            {
                if (_tm != null) _tm.SpawnMovingBlockFor(pid);
            };

            // —— 关键：玩家第一次“按键使用砖墙”时就触发教学 —— //
            // TurnManager 内部会处理“本局只弹一次”和“等上一回合落定再弹”，所以这里直接调用即可。
            _tm?.TriggerBrickUseIfNeeded();

            // 进入砖墙放置流程
            placer.Begin(playerId, moving);
            return;
        }

        if (id == ID_WEIGHT)
        {
            var placer = FindObjectOfType<WeightPlacer>();
            if (placer != null) placer.Begin(playerId);
            else Debug.LogWarning("[Prop] 没找到 WeightPlacer，无法加重");
            return;
        }

        if (id == ID_GLUE)
        {
            var placer = FindObjectOfType<GluePlacer>();
            if (placer != null) placer.Begin(playerId);
            else Debug.LogWarning("[Prop] 没找到 GluePlacer，无法上胶");
            return;
        }

        if (id == ID_ICE)
        {
            // 当前玩家使用 → 对手的“下一块”标记为冰块
            IceNextPieceSystem.Instance?.ApplyToOpponentNext(playerId);

            // （可选）首用教学：如果你在 TurnManager 里实现了 TriggerIceAppearIfNeeded，就调用它
            _tm?.TriggerIceAppearIfNeeded();

            Debug.Log($"[Prop] P{playerId} used 'ice' -> mark opponent's next as ICE");
            return;
        }
        if (id == ID_WIND || id == "wind")
        {
            var tm = TurnManager.Instance;
            int user = (int)tm.currentPlayer;
            WindGustSystem.Instance?.PlayGustForOpponent(user);
            Debug.Log($"[Prop] P{user} used 'fan' -> wind {(user == 1 ? "L→R" : "R→L")}");
            return;
        }

        // 兜底日志（方便以后扩展新道具）
        Debug.Log($"[Prop] P{playerId} used '{id}'");
    }

    void Update()
    {
        // 只有轮到该玩家时才允许使用道具
        if (IsPlayersTurnSafe(TurnManager.Player.P1))
        {
            if (Input.GetKeyDown(KeyCode.Q))
                UseFor(1);   // P1 使用道具（Q）
        }

        if (IsPlayersTurnSafe(TurnManager.Player.P2))
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                UseFor(2);   // P2 使用道具（Enter / KeypadEnter）
        }
    }
}