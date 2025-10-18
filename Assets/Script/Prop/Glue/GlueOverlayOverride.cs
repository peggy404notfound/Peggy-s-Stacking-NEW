using UnityEngine;

public class GlueOverlayOverride : MonoBehaviour
{
    [Header("是否启用覆写（不勾选=走 GluePlacer 全局设置）")]
    public bool enableOverride = true;

    [Header("X 方向长度（沿底边）")]
    [Tooltip("优先使用“按格数”。若 bottomRowCells = 0，则使用 xScale 乘底边联合宽度。")]
    public int bottomRowCells = 0;        // 底边有几格（2×1 就填 2；L/T 看最底行连续格数）
    [Range(0.2f, 2.5f)]
    public float xScale = 1.0f;           // 相对底边联合宽度的比例（bottomRowCells=0 时生效）

    [Header("Y 方向厚度（垂直方向）")]
    [Range(0.3f, 100f)]
    public float yScale = 1.0f;           // 相对“单格高度10%”的比例
                                          // 现有：public float yScale = 1.0f;（保留）

    [Header("厚度覆写（可选）")]
    [Tooltip("勾选后使用固定世界单位厚度，不受单格高度影响")]
    public bool useFixedThickness = false;

    [Tooltip("当勾选上面开关时生效；建议 0.06~0.10")]
    [Range(0.005f, 0.30f)]
    public float fixedThicknessWorld = 0.08f;

    [Tooltip("不勾选固定厚度时可用；0=不覆写，走全局。>0 表示按单格高度的这个比例，例如 0.30=30%")]
    [Range(0f, 0.8f)]
    public float thicknessOfCell = 0f;  // 0 = 不覆写全局


    [Header("微调")]
    public float yOffset = 0f;            // 粘的更紧/更松（世界单位）
    public bool renderOnTop = true;
    public int sortingDelta = 1;
}