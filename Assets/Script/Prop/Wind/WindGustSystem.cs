using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WindGustSystem : MonoBehaviour
{
    public static WindGustSystem Instance { get; private set; }

    [Header("风区预制与锚点")]
    public WindZone2D windZonePrefab;
    public Transform leftAnchor;
    public Transform rightAnchor;

    [Header("固定风 + 覆盖区域")]
    public float mainTime = 3.0f;
    public Vector2 zoneSize = new Vector2(16f, 8.5f);

    [Header("目标层/力度")]
    public LayerMask targetLayers;
    public bool onlyAffectWhenMoving = false;
    public float baseStrength = 250f;

    [Header("风动画（两套，美术独立摆放/独立动画）")]
    public GameObject windVisualRight; // → 从左向右刮时播放（画面左侧/你摆的右吹版本）
    public GameObject windVisualLeft;  // ← 从右向左刮时播放（画面右侧/你摆的左吹版本）

    // —— 固定风区（不随相机/积木/生成点变化） ——
    [Header("固定风区")]
    [Tooltip("勾上后风区中心恒定为 Fixed Center（世界坐标）")]
    public bool useFixedCenter = false;
    [Tooltip("风区中心（世界坐标）。勾选 Use Fixed Center 后生效")]
    public Vector2 fixedCenter;

#if UNITY_EDITOR
    [ContextMenu("Bake Fixed Center From Current")]
    private void BakeFixedCenterFromCurrent()
    {
        fixedCenter = ComputeDynamicCenter();
        EditorUtility.SetDirty(this);
    }
#endif

    // —— 吹风时隐藏悬停积木 ——
    [Header("吹风时处理悬停积木")]
    [Tooltip("开启后：吹风开始时将当前悬停积木整物体 SetActive(false)，风停后再恢复 SetActive(true)")]
    public bool hideHoveringPieceDuringGust = true;

    private WindZone2D _zone;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>对手向哪个方向吹：userPlayerId=1→向右；=2→向左</summary>
    public void PlayGustForOpponent(int userPlayerId) => StartCoroutine(Co_Play(userPlayerId));

    private IEnumerator Co_Play(int userPlayerId)
    {
        var timer = FindObjectOfType<CountdownTimer>();
        TurnManager.Instance?.LockTurnInput();
        timer?.Pause();

        // —— 找“悬停块”（兼容式） ——
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

        // —— 吹风方向与中心 ——
        Vector2 dir = (userPlayerId == 1) ? Vector2.right : Vector2.left;
        Vector2 center = GetCenter();

        // —— 记录与处理悬停块 ——
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
            // —— 物理风区（照旧） ——
            if (_zone == null) _zone = Instantiate(windZonePrefab);
            _zone.gameObject.SetActive(true);
            _zone.targetLayers = targetLayers;
            _zone.onlyAffectWhenMoving = onlyAffectWhenMoving;
            _zone.baseStrength = baseStrength;
            _zone.ignoreHoveringPiece = false;
            _zone.Play(center, zoneSize, dir, mainTime);

            // —— 风视觉：按方向启用对应那一套动画（另一个隐藏） ——
            bool blowRight = (dir.x > 0f);
            PlayVisual(blowRight ? windVisualRight : windVisualLeft);
            StopVisual(blowRight ? windVisualLeft : windVisualRight);

            yield return new WaitForSecondsRealtime(mainTime);
        }
        finally
        {
            // —— 收尾：恢复一切 ——
            timer?.Resume();
            if (_zone) _zone.gameObject.SetActive(false);

            StopVisual(windVisualRight);
            StopVisual(windVisualLeft);

            if (hoverGO)
            {
                var hm = hoverGO.GetComponent<HoverMover>();
                var rrb = hoverGO.GetComponent<Rigidbody2D>();

                // 统一回到“悬停中”，并重置自动下落计时
                hoverGO.SetActive(true);
                if (rrb) rrb.bodyType = RigidbodyType2D.Kinematic;
                if (hm && !hm.enabled) hm.enabled = true;
                if (hm) hm.StartHover();
            }

            TurnManager.Instance?.UnlockTurnInput();
        }
    }

    // —— 动画控制（从头播放 / 关闭） ——
    private void PlayVisual(GameObject go)
    {
        if (!go) return;
        // 先打开，再把所有 Animator 从头播放，避免上次残留进度
        go.SetActive(true);
        var anims = go.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            a.Rebind();     // 重置到默认姿态
            a.Update(0f);   // 立即应用
            // 如果你的 Animator 只有一个默认 state，下面这句就会从头播默认 state
            a.Play(0, -1, 0f);
        }
    }

    private void StopVisual(GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
    }

    // —— 中心点统一入口 ——
    private Vector2 GetCenter() => useFixedCenter ? fixedCenter : ComputeDynamicCenter();

    /// <summary>动态中心：x 为左右锚点中点；y 取 spawnPoint.y - 0.5f（无则退 transform.y）</summary>
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

    // —— Gizmo 预览 ——
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