using UnityEngine;

public class BlockMark : MonoBehaviour
{
    [HideInInspector] public bool isCurrentTurn = false;
    [HideInInspector] public bool touchedStack = false;
    [HideInInspector] public bool hasDropped = false;   // 玩家是否已按键放下

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Stack"))
            touchedStack = true; // 包括 Base 与已落方块
    }
}