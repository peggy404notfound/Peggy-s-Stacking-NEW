using UnityEngine;

public class BlockMark : MonoBehaviour
{
    // 归属玩家：1 = P1, 2 = P2, 0 = 未设置
    [Range(0, 2)]
    public int ownerPlayerId = 0;

    // 回合内状态（可选）
    [HideInInspector] public bool isCurrentTurn = false;
    [HideInInspector] public bool touchedStack = false;
    [HideInInspector] public bool hasDropped = false;

    [Header("What layers count as 'stack/base'?")]
    public LayerMask stackLayers; // 在 Inspector 勾选：Base、Stack 等层

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 只要撞到 stackLayers 里的任意层，就认为“接触到塔体/地面”
        if (((1 << collision.collider.gameObject.layer) & stackLayers) != 0)
        {
            touchedStack = true;
        }
    }
}