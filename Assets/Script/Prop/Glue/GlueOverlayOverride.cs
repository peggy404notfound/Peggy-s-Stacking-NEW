using UnityEngine;

public class GlueOverlayOverride : MonoBehaviour
{
    [Header("�Ƿ����ø�д������ѡ=�� GluePlacer ȫ�����ã�")]
    public bool enableOverride = true;

    [Header("X ���򳤶ȣ��صױߣ�")]
    [Tooltip("����ʹ�á������������� bottomRowCells = 0����ʹ�� xScale �˵ױ����Ͽ�ȡ�")]
    public int bottomRowCells = 0;        // �ױ��м���2��1 ���� 2��L/T �����������������
    [Range(0.2f, 2.5f)]
    public float xScale = 1.0f;           // ��Եױ����Ͽ�ȵı�����bottomRowCells=0 ʱ��Ч��

    [Header("Y �����ȣ���ֱ����")]
    [Range(0.3f, 2f)]
    public float yScale = 1.0f;           // ��ԡ�����߶�10%���ı���

    [Header("΢��")]
    public float yOffset = 0f;            // ճ�ĸ���/���ɣ����絥λ��
    public bool renderOnTop = true;
    public int sortingDelta = 1;
}