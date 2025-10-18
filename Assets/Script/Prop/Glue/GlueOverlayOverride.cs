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
    [Range(0.3f, 100f)]
    public float yScale = 1.0f;           // ��ԡ�����߶�10%���ı���
                                          // ���У�public float yScale = 1.0f;��������

    [Header("��ȸ�д����ѡ��")]
    [Tooltip("��ѡ��ʹ�ù̶����絥λ��ȣ����ܵ���߶�Ӱ��")]
    public bool useFixedThickness = false;

    [Tooltip("����ѡ���濪��ʱ��Ч������ 0.06~0.10")]
    [Range(0.005f, 0.30f)]
    public float fixedThicknessWorld = 0.08f;

    [Tooltip("����ѡ�̶����ʱ���ã�0=����д����ȫ�֡�>0 ��ʾ������߶ȵ�������������� 0.30=30%")]
    [Range(0f, 0.8f)]
    public float thicknessOfCell = 0f;  // 0 = ����дȫ��


    [Header("΢��")]
    public float yOffset = 0f;            // ճ�ĸ���/���ɣ����絥λ��
    public bool renderOnTop = true;
    public int sortingDelta = 1;
}