using UnityEngine;

public class BlockMark : MonoBehaviour
{
    // ������ң�1 = P1, 2 = P2, 0 = δ����
    [Range(0, 2)]
    public int ownerPlayerId = 0;

    // �غ���״̬����ѡ��
    [HideInInspector] public bool isCurrentTurn = false;
    [HideInInspector] public bool touchedStack = false;
    [HideInInspector] public bool hasDropped = false;

    [Header("What layers count as 'stack/base'?")]
    public LayerMask stackLayers; // �� Inspector ��ѡ��Base��Stack �Ȳ�

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // ֻҪײ�� stackLayers �������㣬����Ϊ���Ӵ�������/���桱
        if (((1 << collision.collider.gameObject.layer) & stackLayers) != 0)
        {
            touchedStack = true;
        }
    }
}