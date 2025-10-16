using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventorySlotUI : MonoBehaviour
{
    [Header("�󶨣�����ҵĵ��۱���")]
    public PlayerInventoryOneSlot inventory;

    [Header("�󶨣����߲۵� Image����Canvas�£�")]
    public Image slotImage;

    [Header("�󶨣�ȫ��ͼ���")]
    public PropIconLibrary iconLibrary;

    [Header("�޵���ʱ����ͼ��")]
    public bool hideWhenEmpty = true;

    void OnEnable()
    {
        if (inventory != null)
            inventory.OnChanged += RefreshNow;
        RefreshNow(); // ��ʼ��
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnChanged -= RefreshNow;
    }

    public void RefreshNow()
    {
        if (slotImage == null || inventory == null) return;

        // ��ֻ�����ԣ�������˽���ֶ�
        var id = inventory.CurrentPropId;

        if (string.IsNullOrEmpty(id))
        {
            slotImage.sprite = null;
            slotImage.enabled = !hideWhenEmpty;
        }
        else
        {
            var sprite = iconLibrary ? iconLibrary.GetIcon(id) : null;
            slotImage.sprite = sprite;
            slotImage.enabled = true;
        }
    }
}