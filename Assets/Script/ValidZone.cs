using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ValidZone : MonoBehaviour
{
    // ScoreManager ɨ���Ψһ�ź�
    public bool isTowerMember = false;

    // ����Ĳ������� Base/Stack������ Inspector ����
    [Header("Layer names")]
    public string baseLayerName = "Base";
    public string blockLayerName = "Stack";

    // ����
    private Collider2D selfCol;
    private LayerMask baseMask;
    private ContactFilter2D filterBlocks;
    private readonly Collider2D[] contacts = new Collider2D[12]; // С�������㹻

    void Awake()
    {
        selfCol = GetComponent<Collider2D>();

        // ͨ������ȡ LayerMask�������ֶ���ק
        baseMask = LayerMask.GetMask(baseLayerName);

        filterBlocks = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask(blockLayerName),
            useTriggers = false
        };
    }

    void FixedUpdate()
    {
        // 1) �Ƿ�Ӵ� Base �㣨��죩
        bool onBase = selfCol.IsTouchingLayers(baseMask);

        // 2) û�� Base �ٿ��Ƿ�����Ա֧�ţ����߽Ӵ�Ҳ�㣩
        bool onMember = false;
        if (!onBase)
        {
            int n = selfCol.GetContacts(filterBlocks, contacts); // �Ӵ��������ص�
            for (int i = 0; i < n; i++)
            {
                var otherCol = contacts[i];
                if (!otherCol) continue;

                var otherVZ = otherCol.GetComponentInParent<ValidZone>();
                if (otherVZ && otherVZ != this && otherVZ.isTowerMember)
                {
                    onMember = true;
                    break; // ���ˣ���ǰ����
                }
            }
        }

        bool newVal = onBase || onMember;
        if (newVal != isTowerMember)
            isTowerMember = newVal;
    }
}