using UnityEngine;

public class GluePlacer : MonoBehaviour
{
    [Header("只允许粘到这些层（底座/其它积木）")]
    public LayerMask stickableLayers = ~0;

    [Header("底部接触判定：与 Vector2.up 的点积阈值")]
    [Range(0f, 1f)] public float bottomDotThreshold = 0.5f;

    [Header("落地反馈")]
    public AudioClip glueStickSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    public GameObject glueSplashPrefab;

    [Header("可视化：通用胶水底边贴图（透明PNG）")]
    public Sprite glueOverlaySprite;
    [Tooltip("胶水相对方块宽度的比例（1=与方块等宽）")]
    public float overlayWidthScale = 1.0f;
    [Tooltip("胶水相对自身原始高度的比例（厚薄）")]
    public float overlayHeightScale = 1.0f;
    [Tooltip("往上/下微调，正数=向上挪一点（单位：世界单位）")]
    public float overlayYOffset = 0.0f;
    [Tooltip("把胶水放在方块上面还是下面渲染")]
    public bool overlayRenderOnTop = true;
    [Tooltip("渲染顺序相对方块的偏移")]
    public int overlaySortingDelta = 1;

    /// <summary>对该玩家“当前待落下”的方块启用胶水：添加 BottomGlueEffect + 叠加底部胶水贴图</summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentFallingPiece(playerId);
        if (!target) return;

        var eff = target.GetComponent<BottomGlueEffect>() ?? target.gameObject.AddComponent<BottomGlueEffect>();
        eff.Init(stickableLayers, bottomDotThreshold, glueStickSfx, sfxVolume, glueSplashPrefab);

        // 读取覆写组件（可为空）
        var ov = target.GetComponent<GlueOverlayOverride>();
        TryCreateOrUpdateOverlay(target, ov);
        Debug.Log($"[GluePlacer] 已为 {target.name} 启用胶水（底面判定 + 视觉覆盖）。");
    }


    // ========== 寻找当前“待落下”的那块 ==========
    Transform FindCurrentFallingPiece(int playerId)
    {
        // 这里用你项目里的标记组件名；保持和你工程一致
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;

            var rb = mk.GetComponent<Rigidbody2D>();
            if (!rb) continue;

            // 若你有“已并入塔体”的标志，就排除
            var vz = mk.GetComponent<ValidZone>();
            if (vz != null && vz.isTowerMember) continue;

            if (mk.transform.position.y > bestY)
            {
                bestY = mk.transform.position.y;
                best = mk.transform;
            }
        }
        return best;
    }

    // ========== 生成/更新 胶水叠加层（支持可选覆写） ==========
    void TryCreateOrUpdateOverlay(Transform target, GlueOverlayOverride ov)
    {
        if (!glueOverlaySprite)
        {
            Debug.LogWarning("[GluePlacer] glueOverlaySprite 未设置，只会有粘连逻辑，无可视化。");
            return;
        }

        // 1) 收集所有 SpriteRenderer；用于支持拼块（L/T/长方形）
        var renderers = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("[GluePlacer] 目标方块层级里没有 SpriteRenderer，无法放置胶水贴图。");
            return;
        }

        // 找“最底行”的子块（允许微小容差）
        float minY = float.PositiveInfinity;
        foreach (var r in renderers) minY = Mathf.Min(minY, r.bounds.min.y);
        const float tol = 0.02f;
        var bottomRow = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var r in renderers)
            if (Mathf.Abs(r.bounds.min.y - minY) <= tol) bottomRow.Add(r);
        if (bottomRow.Count == 0) return;

        // 底行联合宽度 & 中心
        float left = float.PositiveInfinity, right = float.NegativeInfinity, rowBottomY = minY;
        foreach (var r in bottomRow)
        {
            left = Mathf.Min(left, r.bounds.min.x);
            right = Mathf.Max(right, r.bounds.max.x);
            rowBottomY = Mathf.Min(rowBottomY, r.bounds.min.y);
        }
        float unionWidth = Mathf.Max(0.0001f, right - left);
        float centerX = 0.5f * (left + right);

        // 参考单格尺寸（取底行第一块）
        var refR = bottomRow[0];
        float oneCellW = refR.bounds.size.x;
        float oneCellH = refR.bounds.size.y;

        // 2) 读取覆写或用全局默认
        bool useOv = ov != null && ov.enableOverride;
        bool onTop = useOv ? ov.renderOnTop : overlayRenderOnTop;
        int orderDelta = useOv ? ov.sortingDelta : overlaySortingDelta;
        float xScaleLocal = useOv ? ov.xScale : overlayWidthScale;
        float yScaleLocal = useOv ? ov.yScale : overlayHeightScale;
        float yOffsetLocal = useOv ? ov.yOffset : overlayYOffset;
        int cellsLocal = useOv ? ov.bottomRowCells : 0;

        // 目标宽度（优先按“格数”，否则按比例）
        float targetWidthWorld = (cellsLocal > 0)
            ? oneCellW * cellsLocal
            : unionWidth * Mathf.Clamp(xScaleLocal, 0.2f, 2.5f);

        // 目标厚度（以“一格高度的10%”为基准）
        float desiredThicknessWorld = oneCellH * 0.10f * Mathf.Clamp(yScaleLocal, 0.3f, 2f);

        // 3) 生成/复用 Overlay
        var existing = target.Find("GlueOverlay");
        GameObject glueObj;
        SpriteRenderer glueSr;
        if (existing)
        {
            glueObj = existing.gameObject;
            glueSr = existing.GetComponent<SpriteRenderer>() ?? glueObj.AddComponent<SpriteRenderer>();
        }
        else
        {
            glueObj = new GameObject("GlueOverlay");
            glueObj.transform.SetParent(target, worldPositionStays: true);
            glueSr = glueObj.AddComponent<SpriteRenderer>();
        }

        // 排序层沿用底行
        glueSr.sprite = glueOverlaySprite;
        glueSr.sortingLayerID = refR.sortingLayerID;
        glueSr.sortingOrder = refR.sortingOrder + (onTop ? Mathf.Abs(orderDelta) : -Mathf.Abs(orderDelta));

        // 4) 优先用 Sliced + size（端头不变形；Sprite 需设置 Border）
        glueSr.drawMode = SpriteDrawMode.Sliced;
        glueSr.size = new Vector2(targetWidthWorld, desiredThicknessWorld);
        glueObj.transform.localScale = Vector3.one;

        // 兜底：若没设 Border 导致 Sliced 无效，则回退为按缩放
        if (glueSr.drawMode != SpriteDrawMode.Sliced && glueSr.drawMode != SpriteDrawMode.Tiled)
        {
            Vector2 native = glueOverlaySprite.bounds.size;
            float scaleX = targetWidthWorld / Mathf.Max(native.x, 1e-5f);
            float scaleY = desiredThicknessWorld / Mathf.Max(native.y, 1e-5f);
            glueObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            glueSr.drawMode = SpriteDrawMode.Simple;
        }

        // 5) 定位到底边中心 + 偏移
        glueObj.transform.position = new Vector3(centerX, rowBottomY + yOffsetLocal, target.position.z);
    }
}