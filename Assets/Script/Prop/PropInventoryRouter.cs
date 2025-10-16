using UnityEngine;

[DisallowMultipleComponent]
public class PropInventoryRouter : MonoBehaviour
{
    // ���� ���� ID��Ҫ�� PropItem.propId ����һ�£�����
    private const string ID_WALL = "brickwall";
    private const string ID_WEIGHT = "weight";
    private const string ID_GLUE = "glue";
    private const string ID_ICE = "ice";
    private const string ID_WIND = "wind";


    private TurnManager _tm;   // ���� TurnManager�����ڣ���ǰ�غ��жϡ�������ѧ��

    void Awake()
    {
        _tm = FindObjectOfType<TurnManager>();
    }

    // ����ǲ��Ǹ���ҵĻغϣ��� TM ʱ���У�
    bool IsPlayersTurnSafe(TurnManager.Player p)
    {
        if (_tm == null) return true;
        return _tm.IsPlayersTurn(p);
    }

    // �Ƿ�����ʹ�ã���ͣ/����ռ��ʱ��ֹ��
    bool CanUseNow()
    {
        if (GamePause.IsPaused) return false;
        if (_tm != null && _tm.inputLocked) return false;
        return true;
    }

    /// <summary>
    /// ��ָ�����ʹ�ñ�����ĵ��ߣ�����·�ɵ��ã�
    /// </summary>
    public void UseFor(int playerId)
    {
        if (!CanUseNow()) return;

        // 1) ȡ������ҵı���
        var inv = PlayerInventoryOneSlot.GetForPlayer(playerId);
        if (inv == null) return;

        // 2) ���Ĳ�λ��ĵ��ߣ������Ҫ����Ԥ�������ѡ������԰� Consume ��Ϊ Peek��
        var id = inv.Consume();
        if (string.IsNullOrEmpty(id)) return;

        // 3) ���ݵ��� ID �ַ�
        if (id == ID_WALL)
        {
            var placer = FindObjectOfType<BrickWallPlacer>();
            if (placer == null)
            {
                Debug.LogWarning("[Prop] û�ҵ� BrickWallPlacer���޷�����שǽ");
                return;
            }

            // �� TurnManager �á���ǰ�����ƶ��Ļ�ľ��
            var moving = _tm != null ? _tm.GetCurrentMovingBlock(playerId) : null;
            if (moving == null)
            {
                Debug.LogWarning($"[Prop] P{playerId} ��ǰû�������ƶ��Ļ�ľ��Ҳ��û����/û�Ǽǣ�");
                return;
            }

            // שǽͣס��Ϊ��ͬһ��ҡ�������һ�������ƶ���ľ
            placer.onNeedSpawnNext = (pid) =>
            {
                if (_tm != null) _tm.SpawnMovingBlockFor(pid);
            };

            // ���� �ؼ�����ҵ�һ�Ρ�����ʹ��שǽ��ʱ�ʹ�����ѧ ���� //
            // TurnManager �ڲ��ᴦ������ֻ��һ�Ρ��͡�����һ�غ��䶨�ٵ�������������ֱ�ӵ��ü��ɡ�
            _tm?.TriggerBrickUseIfNeeded();

            // ����שǽ��������
            placer.Begin(playerId, moving);
            return;
        }

        if (id == ID_WEIGHT)
        {
            var placer = FindObjectOfType<WeightPlacer>();
            if (placer != null) placer.Begin(playerId);
            else Debug.LogWarning("[Prop] û�ҵ� WeightPlacer���޷�����");
            return;
        }

        if (id == ID_GLUE)
        {
            var placer = FindObjectOfType<GluePlacer>();
            if (placer != null) placer.Begin(playerId);
            else Debug.LogWarning("[Prop] û�ҵ� GluePlacer���޷��Ͻ�");
            return;
        }

        if (id == ID_ICE)
        {
            // ��ǰ���ʹ�� �� ���ֵġ���һ�顱���Ϊ����
            IceNextPieceSystem.Instance?.ApplyToOpponentNext(playerId);

            // ����ѡ�����ý�ѧ��������� TurnManager ��ʵ���� TriggerIceAppearIfNeeded���͵�����
            _tm?.TriggerIceAppearIfNeeded();

            Debug.Log($"[Prop] P{playerId} used 'ice' -> mark opponent's next as ICE");
            return;
        }
        if (id == ID_WIND || id == "wind")
        {
            var tm = TurnManager.Instance;
            int user = (int)tm.currentPlayer;
            WindGustSystem.Instance?.PlayGustForOpponent(user);
            Debug.Log($"[Prop] P{user} used 'fan' -> wind {(user == 1 ? "L��R" : "R��L")}");
            return;
        }

        // ������־�������Ժ���չ�µ��ߣ�
        Debug.Log($"[Prop] P{playerId} used '{id}'");
    }

    void Update()
    {
        // ֻ���ֵ������ʱ������ʹ�õ���
        if (IsPlayersTurnSafe(TurnManager.Player.P1))
        {
            if (Input.GetKeyDown(KeyCode.Q))
                UseFor(1);   // P1 ʹ�õ��ߣ�Q��
        }

        if (IsPlayersTurnSafe(TurnManager.Player.P2))
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                UseFor(2);   // P2 ʹ�õ��ߣ�Enter / KeypadEnter��
        }
    }
}