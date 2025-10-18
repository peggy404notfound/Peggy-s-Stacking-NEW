using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WindGustSystem : MonoBehaviour
{
    public static WindGustSystem Instance { get; private set; }

    [Header("����Ԥ����ê��")]
    public WindZone2D windZonePrefab;
    public Transform leftAnchor;
    public Transform rightAnchor;

    [Header("�̶��� + ��������")]
    public float mainTime = 3.0f;
    public Vector2 zoneSize = new Vector2(16f, 8.5f);

    [Header("Ŀ���/����")]
    public LayerMask targetLayers;
    public bool onlyAffectWhenMoving = false;
    public float baseStrength = 250f;

    [Header("�綯�������ף����������ڷ�/����������")]
    public GameObject windVisualRight; // �� �������ҹ�ʱ���ţ��������/��ڵ��Ҵ��汾��
    public GameObject windVisualLeft;  // �� ���������ʱ���ţ������Ҳ�/��ڵ��󴵰汾��

    // ���� �̶��������������/��ľ/���ɵ�仯�� ����
    [Header("�̶�����")]
    [Tooltip("���Ϻ�������ĺ㶨Ϊ Fixed Center���������꣩")]
    public bool useFixedCenter = false;
    [Tooltip("�������ģ��������꣩����ѡ Use Fixed Center ����Ч")]
    public Vector2 fixedCenter;

#if UNITY_EDITOR
    [ContextMenu("Bake Fixed Center From Current")]
    private void BakeFixedCenterFromCurrent()
    {
        fixedCenter = ComputeDynamicCenter();
        EditorUtility.SetDirty(this);
    }
#endif

    // ���� ����ʱ������ͣ��ľ ����
    [Header("����ʱ������ͣ��ľ")]
    [Tooltip("�����󣺴��翪ʼʱ����ǰ��ͣ��ľ������ SetActive(false)����ͣ���ٻָ� SetActive(true)")]
    public bool hideHoveringPieceDuringGust = true;

    private WindZone2D _zone;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>�������ĸ����򴵣�userPlayerId=1�����ң�=2������</summary>
    public void PlayGustForOpponent(int userPlayerId) => StartCoroutine(Co_Play(userPlayerId));

    private IEnumerator Co_Play(int userPlayerId)
    {
        var timer = FindObjectOfType<CountdownTimer>();
        TurnManager.Instance?.LockTurnInput();
        timer?.Pause();

        // ���� �ҡ���ͣ�顱������ʽ�� ����
        GameObject hoverGO = null;
        Behaviour hoverMover = null;
        var tm = TurnManager.Instance;
        if (tm != null)
        {
            try { hoverGO = tm.GetCurrentMovingBlock((int)tm.currentPlayer); } catch { hoverGO = null; }
        }
        if (hoverGO == null)
        {
            foreach (var b in FindObjectsOfType<Behaviour>(includeInactive: false))
            {
                if (b && b.enabled && b.GetType().Name == "HoverMover")
                {
                    hoverGO = b.gameObject;
                    hoverMover = b;
                    break;
                }
            }
        }
        if (hoverGO && !hoverMover) hoverMover = hoverGO.GetComponent("HoverMover") as Behaviour;

        // ���� ���緽�������� ����
        Vector2 dir = (userPlayerId == 1) ? Vector2.right : Vector2.left;
        Vector2 center = GetCenter();

        // ���� ��¼�봦����ͣ�� ����
        bool didHideHoverGO = false;
        Rigidbody2D rb = null;
        RigidbodyType2D oldType = RigidbodyType2D.Dynamic;

        if (hoverGO)
        {
            var hm = hoverGO.GetComponent<HoverMover>();
            var rrb = hoverGO.GetComponent<Rigidbody2D>();

            if (hideHoveringPieceDuringGust)
            {
                didHideHoverGO = true;
                hoverGO.SetActive(false);
            }
            else
            {
                if (rrb)
                {
                    oldType = rrb.bodyType;
                    rrb.bodyType = RigidbodyType2D.Dynamic;
                    rrb.WakeUp();
                }
                if (hm) hm.enabled = false;
            }
        }

        try
        {
            // ���� ����������վɣ� ����
            if (_zone == null) _zone = Instantiate(windZonePrefab);
            _zone.gameObject.SetActive(true);
            _zone.targetLayers = targetLayers;
            _zone.onlyAffectWhenMoving = onlyAffectWhenMoving;
            _zone.baseStrength = baseStrength;
            _zone.ignoreHoveringPiece = false;
            _zone.Play(center, zoneSize, dir, mainTime);

            // ���� ���Ӿ������������ö�Ӧ��һ�׶�������һ�����أ� ����
            bool blowRight = (dir.x > 0f);
            PlayVisual(blowRight ? windVisualRight : windVisualLeft);
            StopVisual(blowRight ? windVisualLeft : windVisualRight);

            yield return new WaitForSecondsRealtime(mainTime);
        }
        finally
        {
            // ���� ��β���ָ�һ�� ����
            timer?.Resume();
            if (_zone) _zone.gameObject.SetActive(false);

            StopVisual(windVisualRight);
            StopVisual(windVisualLeft);

            if (hoverGO)
            {
                var hm = hoverGO.GetComponent<HoverMover>();
                var rrb = hoverGO.GetComponent<Rigidbody2D>();

                // ͳһ�ص�����ͣ�С����������Զ������ʱ
                hoverGO.SetActive(true);
                if (rrb) rrb.bodyType = RigidbodyType2D.Kinematic;
                if (hm && !hm.enabled) hm.enabled = true;
                if (hm) hm.StartHover();
            }

            TurnManager.Instance?.UnlockTurnInput();
        }
    }

    // ���� �������ƣ���ͷ���� / �رգ� ����
    private void PlayVisual(GameObject go)
    {
        if (!go) return;
        // �ȴ򿪣��ٰ����� Animator ��ͷ���ţ������ϴβ�������
        go.SetActive(true);
        var anims = go.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            a.Rebind();     // ���õ�Ĭ����̬
            a.Update(0f);   // ����Ӧ��
            // ������ Animator ֻ��һ��Ĭ�� state���������ͻ��ͷ��Ĭ�� state
            a.Play(0, -1, 0f);
        }
    }

    private void StopVisual(GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
    }

    // ���� ���ĵ�ͳһ��� ����
    private Vector2 GetCenter() => useFixedCenter ? fixedCenter : ComputeDynamicCenter();

    /// <summary>��̬���ģ�x Ϊ����ê���е㣻y ȡ spawnPoint.y - 0.5f�������� transform.y��</summary>
    private Vector2 ComputeDynamicCenter()
    {
        float cx = (leftAnchor && rightAnchor)
            ? 0.5f * (leftAnchor.position.x + rightAnchor.position.x)
            : transform.position.x;

        float cy = (TurnManager.Instance && TurnManager.Instance.spawnPoint)
            ? TurnManager.Instance.spawnPoint.position.y - 0.5f
            : transform.position.y;

        return new Vector2(cx, cy);
    }

    // ���� Gizmo Ԥ�� ����
    void OnDrawGizmos()
    {
        if (!useFixedCenter && (!leftAnchor || !rightAnchor)) return;

        Vector2 c = GetCenter();
        Vector3 center3 = new Vector3(c.x, c.y, 0f);
        Vector3 size3 = new Vector3(zoneSize.x, zoneSize.y, 0.1f);

        var fill = new Color(0.2f, 0.7f, 1f, 0.10f);
        var line = new Color(0.2f, 0.7f, 1f, 0.90f);

        Gizmos.color = fill; Gizmos.DrawCube(center3, size3);
        Gizmos.color = line; Gizmos.DrawWireCube(center3, size3);
    }
}