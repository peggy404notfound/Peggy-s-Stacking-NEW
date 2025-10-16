using UnityEngine;

public class GluePlacer : MonoBehaviour
{
    [Header("ֻ����ճ����Щ�㣨����/������ľ��")]
    public LayerMask stickableLayers = ~0;

    [Header("�ײ��Ӵ��ж����� Vector2.up �ĵ����ֵ")]
    [Range(0f, 1f)] public float bottomDotThreshold = 0.5f;

    [Header("��ط���")]
    public AudioClip glueStickSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    public GameObject glueSplashPrefab;

    [Header("���ӻ���ͨ�ý�ˮ�ױ���ͼ��͸��PNG��")]
    public Sprite glueOverlaySprite;
    [Tooltip("��ˮ��Է����ȵı�����1=�뷽��ȿ�")]
    public float overlayWidthScale = 1.0f;
    [Tooltip("��ˮ�������ԭʼ�߶ȵı������񱡣�")]
    public float overlayHeightScale = 1.0f;
    [Tooltip("����/��΢��������=����Ųһ�㣨��λ�����絥λ��")]
    public float overlayYOffset = 0.0f;
    [Tooltip("�ѽ�ˮ���ڷ������滹��������Ⱦ")]
    public bool overlayRenderOnTop = true;
    [Tooltip("��Ⱦ˳����Է����ƫ��")]
    public int overlaySortingDelta = 1;

    /// <summary>�Ը���ҡ���ǰ�����¡��ķ������ý�ˮ����� BottomGlueEffect + ���ӵײ���ˮ��ͼ</summary>
    public void Begin(int playerId)
    {
        var target = FindCurrentFallingPiece(playerId);
        if (!target) return;

        var eff = target.GetComponent<BottomGlueEffect>() ?? target.gameObject.AddComponent<BottomGlueEffect>();
        eff.Init(stickableLayers, bottomDotThreshold, glueStickSfx, sfxVolume, glueSplashPrefab);

        // ��ȡ��д�������Ϊ�գ�
        var ov = target.GetComponent<GlueOverlayOverride>();
        TryCreateOrUpdateOverlay(target, ov);
        Debug.Log($"[GluePlacer] ��Ϊ {target.name} ���ý�ˮ�������ж� + �Ӿ����ǣ���");
    }


    // ========== Ѱ�ҵ�ǰ�������¡����ǿ� ==========
    Transform FindCurrentFallingPiece(int playerId)
    {
        // ����������Ŀ��ı������������ֺ��㹤��һ��
        var marks = FindObjectsOfType<BlockMark>();
        Transform best = null;
        float bestY = float.NegativeInfinity;

        foreach (var mk in marks)
        {
            if (mk.ownerPlayerId != playerId) continue;

            var rb = mk.GetComponent<Rigidbody2D>();
            if (!rb) continue;

            // �����С��Ѳ������塱�ı�־�����ų�
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

    // ========== ����/���� ��ˮ���Ӳ㣨֧�ֿ�ѡ��д�� ==========
    void TryCreateOrUpdateOverlay(Transform target, GlueOverlayOverride ov)
    {
        if (!glueOverlaySprite)
        {
            Debug.LogWarning("[GluePlacer] glueOverlaySprite δ���ã�ֻ����ճ���߼����޿��ӻ���");
            return;
        }

        // 1) �ռ����� SpriteRenderer������֧��ƴ�飨L/T/�����Σ�
        var renderers = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("[GluePlacer] Ŀ�귽��㼶��û�� SpriteRenderer���޷����ý�ˮ��ͼ��");
            return;
        }

        // �ҡ�����С����ӿ飨����΢С�ݲ
        float minY = float.PositiveInfinity;
        foreach (var r in renderers) minY = Mathf.Min(minY, r.bounds.min.y);
        const float tol = 0.02f;
        var bottomRow = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var r in renderers)
            if (Mathf.Abs(r.bounds.min.y - minY) <= tol) bottomRow.Add(r);
        if (bottomRow.Count == 0) return;

        // �������Ͽ�� & ����
        float left = float.PositiveInfinity, right = float.NegativeInfinity, rowBottomY = minY;
        foreach (var r in bottomRow)
        {
            left = Mathf.Min(left, r.bounds.min.x);
            right = Mathf.Max(right, r.bounds.max.x);
            rowBottomY = Mathf.Min(rowBottomY, r.bounds.min.y);
        }
        float unionWidth = Mathf.Max(0.0001f, right - left);
        float centerX = 0.5f * (left + right);

        // �ο�����ߴ磨ȡ���е�һ�飩
        var refR = bottomRow[0];
        float oneCellW = refR.bounds.size.x;
        float oneCellH = refR.bounds.size.y;

        // 2) ��ȡ��д����ȫ��Ĭ��
        bool useOv = ov != null && ov.enableOverride;
        bool onTop = useOv ? ov.renderOnTop : overlayRenderOnTop;
        int orderDelta = useOv ? ov.sortingDelta : overlaySortingDelta;
        float xScaleLocal = useOv ? ov.xScale : overlayWidthScale;
        float yScaleLocal = useOv ? ov.yScale : overlayHeightScale;
        float yOffsetLocal = useOv ? ov.yOffset : overlayYOffset;
        int cellsLocal = useOv ? ov.bottomRowCells : 0;

        // Ŀ���ȣ����Ȱ��������������򰴱�����
        float targetWidthWorld = (cellsLocal > 0)
            ? oneCellW * cellsLocal
            : unionWidth * Mathf.Clamp(xScaleLocal, 0.2f, 2.5f);

        // Ŀ���ȣ��ԡ�һ��߶ȵ�10%��Ϊ��׼��
        float desiredThicknessWorld = oneCellH * 0.10f * Mathf.Clamp(yScaleLocal, 0.3f, 2f);

        // 3) ����/���� Overlay
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

        // ��������õ���
        glueSr.sprite = glueOverlaySprite;
        glueSr.sortingLayerID = refR.sortingLayerID;
        glueSr.sortingOrder = refR.sortingOrder + (onTop ? Mathf.Abs(orderDelta) : -Mathf.Abs(orderDelta));

        // 4) ������ Sliced + size����ͷ�����Σ�Sprite ������ Border��
        glueSr.drawMode = SpriteDrawMode.Sliced;
        glueSr.size = new Vector2(targetWidthWorld, desiredThicknessWorld);
        glueObj.transform.localScale = Vector3.one;

        // ���ף���û�� Border ���� Sliced ��Ч�������Ϊ������
        if (glueSr.drawMode != SpriteDrawMode.Sliced && glueSr.drawMode != SpriteDrawMode.Tiled)
        {
            Vector2 native = glueOverlaySprite.bounds.size;
            float scaleX = targetWidthWorld / Mathf.Max(native.x, 1e-5f);
            float scaleY = desiredThicknessWorld / Mathf.Max(native.y, 1e-5f);
            glueObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            glueSr.drawMode = SpriteDrawMode.Simple;
        }

        // 5) ��λ���ױ����� + ƫ��
        glueObj.transform.position = new Vector3(centerX, rowBottomY + yOffsetLocal, target.position.z);
    }
}