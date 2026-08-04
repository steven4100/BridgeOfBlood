using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(60)]
public class ShopController : MonoBehaviour
{
    public Transform SpellTargetPurchasableRoot;
    public Transform ItemsPurchasableRoot;

    public PurchasableItemUI PurchasableItemUIPF;
    public PurchasableSpellTargetItemUI PurchasableSpellTargetItemUIPF;

    [SerializeField]
    [Tooltip("Required: spell strip for gem targeting highlights and spell-tile clicks.")]
    SpellInventoryController spellInventoryController;

    private List<PurchasableItemUI> ItemPurchasableInstances = new List<PurchasableItemUI>();
    private List<PurchasableSpellTargetItemUI> SpellTargetPurchasableInstances = new List<PurchasableSpellTargetItemUI>();

    private IShopService shopService;
    private PurchaseContext purchaseContext;

    private PurchasableSpellTargetItemUI focusedSpellTargetRow;

    private void OnEnable()
    {
        spellInventoryController.SpellTileClicked += OnSpellTileClicked;
        spellInventoryController.SpellStripRendered += OnSpellStripRendered;
        if (shopService != null)
            shopService.ShopRefreshed += OnShopRefreshed;

        // Last: a replayed ServicesRegisteredEvent rebinds shopService, so the wiring above must already be in place.
        ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
    }

    private void OnDisable()
    {
        ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
        if (shopService != null)
            shopService.ShopRefreshed -= OnShopRefreshed;
        spellInventoryController.SpellTileClicked -= OnSpellTileClicked;
        spellInventoryController.SpellStripRendered -= OnSpellStripRendered;
        ClearSpellTargetingUi();
    }

    void OnServicesRegistered(ref ServicesRegisteredEvent _)
    {
        if (shopService != null)
            shopService.ShopRefreshed -= OnShopRefreshed;

        shopService = ServiceLocator.Current.GetService<IShopService>();
        IInventoryService inventoryService = ServiceLocator.Current.GetService<IInventoryService>();
        IWalletService walletService = ServiceLocator.Current.GetService<IWalletService>();
        ISpellInventoryService spellInventorySvc = ServiceLocator.Current.GetService<ISpellInventoryService>();
        purchaseContext = new PurchaseContext(inventoryService, walletService, spellInventorySvc);

        shopService.ShopRefreshed += OnShopRefreshed;
        ClearItems();
        SpawnItems();
    }

    private void OnSpellStripRendered()
    {
        if (focusedSpellTargetRow != null)
            RefreshSpellHighlightsFor(focusedSpellTargetRow.SpellTargetPurchasable);
    }

    private void OnShopRefreshed()
    {
        ClearItems();
        SpawnItems();
    }

    public void FocusRegularRow(PurchasableItemUI row)
    {
        foreach (PurchasableItemUI item in ItemPurchasableInstances)
            item.HideConfirmPurchaseButton();

        focusedSpellTargetRow = null;
        ClearSpellTargetingUi();

        row.ShowConfirmPurchaseButton();
    }

    public void FocusSpellTargetRow(PurchasableSpellTargetItemUI row)
    {
        foreach (PurchasableItemUI item in ItemPurchasableInstances)
            item.HideConfirmPurchaseButton();

        focusedSpellTargetRow = row;

        RefreshSpellHighlightsFor(row.SpellTargetPurchasable);
    }

    private void OnSpellTileClicked(int spellId)
    {
        if (focusedSpellTargetRow == null)
            return;

        foreach (RuntimeSpell spell in purchaseContext.SpellInventory.GetSpells())
        {
            if (spell.spellId != spellId)
                continue;

            ISpellTargetPurchasable gem = focusedSpellTargetRow.SpellTargetPurchasable;
            if (!gem.CanBeApplied(spell))
                return;

            purchaseContext.SpellGemTarget = spell;
            bool ok = shopService.TryPurchase((IPurchasable)gem, purchaseContext);

            if (ok)
                ClearSpellTargetingUi();
            return;
        }
    }

    private void RefreshSpellHighlightsFor(ISpellTargetPurchasable gem)
    {
        spellInventoryController.ApplyShopHighlights(s =>
            gem.CanBeApplied(s) ? ShopSpellHighlight.Applicable : ShopSpellHighlight.NotApplicable);
    }

    private void ClearSpellTargetingUi()
    {
        spellInventoryController.ClearShopHighlights();
    }

    public bool TryPurchase(IPurchasable purchasable)
    {
        return shopService.TryPurchase(purchasable, purchaseContext);
    }

    private void SpawnItems()
    {
        foreach (IPurchasable purchasable in shopService.GetItemPurchasables())
        {
            PurchasableItemUI item = Instantiate(PurchasableItemUIPF, ItemsPurchasableRoot);
            ItemPurchasableInstances.Add(item);
            item.SetPurchasable(purchasable, purchaseContext, this);
        }
        foreach (ISpellTargetPurchasable spellPurchasable in shopService.GetSpellGemPurchasables())
        {
            PurchasableSpellTargetItemUI item = Instantiate(PurchasableSpellTargetItemUIPF, SpellTargetPurchasableRoot);
            SpellTargetPurchasableInstances.Add(item);
            item.SetSpellPurchasable(spellPurchasable, purchaseContext, this);
        }
    }

    private void ClearItems()
    {
        focusedSpellTargetRow = null;
        ClearSpellTargetingUi();

        foreach (PurchasableItemUI item in ItemPurchasableInstances)
            Destroy(item.gameObject);
        foreach (PurchasableSpellTargetItemUI item in SpellTargetPurchasableInstances)
            Destroy(item.gameObject);
        ItemPurchasableInstances.Clear();
        SpellTargetPurchasableInstances.Clear();
    }
}


public interface IShopService
{
    event Action ShopRefreshed;

    bool TryPurchase(IPurchasable purchasable, PurchaseContext context);

    List<IPurchasable> GetItemPurchasables();
    List<ISpellTargetPurchasable> GetSpellGemPurchasables();
}
