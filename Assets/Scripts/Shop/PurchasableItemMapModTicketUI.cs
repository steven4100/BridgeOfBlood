using TMPro;
using UnityEngine;

/// <summary>Shop tile for <see cref="MapModifierPlacementTicket"/>: price plus display name.</summary>
public sealed class PurchasableItemMapModTicketUI : PurchasableItemUI
{
    [SerializeField] TMP_Text nameText;

    protected override void Present()
    {
        base.Present();
        nameText.text = purchasable.ShopItemDefinition.DisplayName;
    }
}
