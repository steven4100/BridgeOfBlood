using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Effects;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single tile in the item inventory strip. Carries the <see cref="InventoryItem"/> row so
/// <see cref="ItemInventoryController"/> can read sibling order and commit a reorder.
/// </summary>
public class RuntimeItemPresenter : MonoBehaviour
{
    [SerializeField] Image iconImage;

    public InventoryItem Row { get; private set; }

    public void Bind(InventoryItem row)
    {
        Row = row;
        var item = (Item)row.Payload;
        string name = item.ShopItemDefinition != null ? item.ShopItemDefinition.DisplayName : item.name;
        gameObject.name = $"Item_{name}";

        if (iconImage != null)
            iconImage.sprite = null;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
}
