using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventorySlotUI : MonoBehaviour
{
    [Header("绑定：该玩家的单槽背包")]
    public PlayerInventoryOneSlot inventory;

    [Header("绑定：道具槽的 Image（在Canvas下）")]
    public Image slotImage;

    [Header("绑定：全局图标库")]
    public PropIconLibrary iconLibrary;

    [Header("无道具时隐藏图标")]
    public bool hideWhenEmpty = true;

    void OnEnable()
    {
        if (inventory != null)
            inventory.OnChanged += RefreshNow;
        RefreshNow(); // 初始化
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnChanged -= RefreshNow;
    }

    public void RefreshNow()
    {
        if (slotImage == null || inventory == null) return;

        // 用只读属性，而不是私有字段
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