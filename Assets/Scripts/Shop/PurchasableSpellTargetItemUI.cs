using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

public class PurchasableSpellTargetItemUI : PurchasableItemUI
{
    public ISpellTargetPurchasable SpellTargetPurchasable => spellTargetPurchasable;

    private ISpellTargetPurchasable spellTargetPurchasable;

    public void SetSpellPurchasable(ISpellTargetPurchasable spellPurchasable,
        PurchaseContext purchaseContext,
        ShopController shopController)
    {
        spellTargetPurchasable = spellPurchasable;
        base.SetPurchasable(spellPurchasable, purchaseContext, shopController);
    }

    protected override void Present()
    {
        base.Present();
        ConfirmPurchaseButton.gameObject.SetActive(false);
    }

    protected override void OnItemClicked()
    {
        shopController.FocusSpellTargetRow(this);
    }
}
