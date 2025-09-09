using UnityEngine;

public class BlockMark : MonoBehaviour
{
    [HideInInspector] public bool isCurrentTurn = false;
    [HideInInspector] public bool touchedStack = false;
    [HideInInspector] public bool hasDropped = false;   // ����Ƿ��Ѱ�������

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.gameObject.layer == LayerMask.NameToLayer("Stack"))
            touchedStack = true; // ���� Base �����䷽��
    }
}