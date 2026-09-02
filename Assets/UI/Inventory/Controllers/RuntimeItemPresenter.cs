using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Effects;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single tile in the item inventory strip.
/// </summary>
public class RuntimeItemPresenter : MonoBehaviour
{
    [SerializeField] Image iconImage;

    public RuntimeItem Item { get; private set; }

    public void Bind(RuntimeItem item, IItemReceptacle source, int index)
    {
        Item = item;
        string name = item.Definition.ShopItemDefinition != null
            ? item.Definition.ShopItemDefinition.DisplayName
            : item.Definition.name;
        gameObject.name = $"Item_{name}";

        if (iconImage != null)
            iconImage.sprite = item.GhostSprite;

        InventoryDragHandle.Bind(gameObject, item, source, index, default);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }
}
