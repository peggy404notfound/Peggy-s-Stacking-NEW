using System;
using UnityEngine;

public class BrickWallPlacer : MonoBehaviour
{
    [Header("����")]
    public Transform baseTransform;                 // �����գ�������һ��
    public GameObject wallPrefab;                   // שǽԤ���壨SpriteRenderer + BoxCollider2D��

    [Header("�����ƶ��߽磨ǽ������ HoverMover ���Զ���Ӳ�ʹ����Щ�߽磩")]
    public Transform leftBound;
    public Transform rightBound;

    [Header("��ײ�趨")]
    public LayerMask solidLayers;                   // ����סǽ�Ĳ㣨Base/Stack/Wall �ȣ�
    [Tooltip("������Ӵ��㷨�ߵ� y �� ��ֵ����Ϊ��������ס��Խ�ӽ�1Խ�ϸ�")]
    public float groundNormalYThreshold = 0.2f;

    // שǽ�����֪ͨ�ⲿΪ��ͬһ��ҡ������µ������ƶ���ľ
    public Action<int> onNeedSpawnNext;

    // ������
    GameObject _wall;               // ��ǰ���õ�שǽ
    Rigidbody2D _rb;                // ǽ����
    HoverMover _wallMover;          // ǽ�ϵ� HoverMover������ʱ���ã�
    GameObject _hiddenMoving;       // ���滻��ԭ�ƶ���ľ
    int _playerId;                  // 1/2
    bool _active;                   // �Ƿ��ڷ���������
    bool _isFalling;                // �Ƿ��ѿ�ʼ��������
    bool _finalized;                // �Ƿ��Ѷ���

    TurnManager _tm;                // ��������/�������� & ��ǡ�����һ������ָ��ǰ�Ǽǵ��ƶ��顱

    void Awake()
    {
        _tm = FindObjectOfType<TurnManager>();
    }

    /// <summary>
    /// ʹ��שǽ���ߣ��ѡ���ǰ�����ƶ���ľ���滻Ϊשǽ��שǽ���������ƶ����� S/�� ��ʼ�������䣩
    /// </summary>
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
        _hiddenMoving.SetActive(false); // ���ر��غ�ԭʼ�飨��Ҫ���٣�

        // ��ԭλ������ʵ��שǽ
        _wall = Instantiate(wallPrefab, currentMovingBlock.transform.position, Quaternion.identity);
        _wall.name = $"Wall_P{playerId}";

        // ȷ����͸�� & collider ʵ��
        var sr = _wall.GetComponent<SpriteRenderer>();
        if (sr) { var c = sr.color; c.a = 1f; sr.color = c; }

        var bc = _wall.GetComponent<BoxCollider2D>();
        if (!bc) bc = _wall.AddComponent<BoxCollider2D>();
        bc.isTrigger = false; // ������ʵ����ײ��

        // ���壺�� Kinematic���������������������� Dynamic
        _rb = _wall.GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = _wall.AddComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = Mathf.Max(1f, _rb.gravityScale); // �� Dynamic ����Ч
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.simulated = true;

        // ��שǽ�����ƶ������ prefab �� HoverMover ���Զ���һ����
        SetupHoverMoverOnWall(currentMovingBlock);

        // ��ǽ�����ײ���� �� ��ǽ���ϵ���ײ�ص������ű�
        var proxy = _wall.AddComponent<BrickWallCollisionProxy>();
        proxy.Init(this, solidLayers, groundNormalYThreshold);

        // ����שǽ���̣���ס TurnManager �� S/�� ����
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

        // ��һ�ΰ�������ʼ����
        if (keyDown && !_isFalling)
        {
            _isFalling = true;

            // ͣ��������ͣ������ HoverMover ���壬ֻ������رգ�
            if (_wallMover != null)
            {
                try { _wallMover.Drop(); } catch { }
                _wallMover.enabled = false;
            }

            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;      // ������������
                if (_rb.gravityScale <= 0f) _rb.gravityScale = 1f;

                // ����һ�£����� v=0 ��ס
                if (_rb.velocity.sqrMagnitude < 0.0001f)
                    _rb.AddForce(Vector2.down * 0.1f, ForceMode2D.Impulse);

                _rb.WakeUp();
            }
            return;
        }

        // �ڶ��ΰ������ֶ����㣨����Ҳ�ɶ��㣩
        if (keyDown && _isFalling && !_finalized)
        {
            TryFinalizeFromKey();
        }
    }

    // ����ײ������ã���������������ɷ���
    public void TryFinalizeFromCollision()
    {
        if (_active && _isFalling && !_finalized)
            FinalizePlacement();
    }

    // ���������õĶ��㣨����ײ����һ�£�
    void TryFinalizeFromKey()
    {
        FinalizePlacement();
    }

    // ���� ȷ�϶��㣺ֹͣ���̣�����ͬһ���������һ�������ƶ���ľ ���� //
    void FinalizePlacement()
    {
        if (_finalized) return;
        _finalized = true;

        // ����ͣסǽ��
        if (_rb)
        {
            _rb.velocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.bodyType = RigidbodyType2D.Static; // ������ǰλ��
        }

        _active = false;
        _isFalling = false;

        int pid = _playerId;
        _playerId = 0;

        // �� ���� TurnManager����һ�� S/�� Ҫ���ض��򡱵���ǰ�Ǽǵ��ƶ��飨������֮ǰ�����ص��ǿ飩
        _tm?.ArmRedirectDrop();

        // ����ͬһ��ҵ��¡������ƶ���ľ��
        onNeedSpawnNext?.Invoke(pid);

        // ���� TurnManager ���루�ص���ѭ�����ȴ���λ��Ҷԡ��»�ľ���� S/����
        StartCoroutine(UnlockAfterKeyRelease(pid));   // �� �ӳٵ�����̧���ٽ���

        // ���ƶ�������ý����ⲿ�µ�����������Ȼ����
        _hiddenMoving = null;

        System.Collections.IEnumerator UnlockAfterKeyRelease(int pid)
        {
            // �ȵ� 1 ֡������ͬһ֡�ﱻ TurnManager ������ΰ���
            yield return null;

            // ѡ�Ա����ʹ�õ��Ǹ���
            KeyCode key = (pid == 1) ? KeyCode.S : KeyCode.DownArrow;

            // �ȵ����ΰ�����ȫ̧��
            while (Input.GetKey(key))
                yield return null;

            // �ٶ��һ֡�����գ�������ض�����
            yield return null;

            // ���ڲŽ�������һ���µİ����Żᱻ TurnManager ����
            _tm?.UnlockTurnInput();
        }

    }

    // ���� �ڡ�ǽ����׼�� HoverMover��������ǽ���ϣ� ���� //
    void SetupHoverMoverOnWall(GameObject sourceMovingBlock)
    {
        var wallMover = _wall.GetComponent<HoverMover>();
        if (wallMover == null) wallMover = _wall.AddComponent<HoverMover>();

        // ���ñ߽磺����ʹ�ñ��ű��ϵ����ұ߽�
        if (leftBound && rightBound)
        {
            float min = Mathf.Min(leftBound.position.x, rightBound.position.x);
            float max = Mathf.Max(leftBound.position.x, rightBound.position.x);
            wallMover.SetBounds(min, max);
        }
        else
        {
            Debug.LogWarning("[BrickWallPlacer] δ���� leftBound/rightBound��ǽ�� HoverMover ��ʹ����Ĭ�ϱ߽硣");
        }

        // ��������Ȩ���� HoverMover �и��ֶ���ֵ��û�оͺ��ԣ�
        var fld = typeof(HoverMover).GetField("ownerPlayerId");
        if (fld != null) fld.SetValue(wallMover, _playerId);

        wallMover.StartHover();
        _wallMover = wallMover; // �������ã�����ʱ����
    }
}

// ���� �����ڡ�ǽ�塱�ϵ�С������� OnCollisionEnter2D ת���� BrickWallPlacer ���� //
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

        // ����һ���Ӵ��㷨�߳��� �� ��Ϊ����ס
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