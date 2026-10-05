using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using System;
using System.Collections.Generic;
using UnityEngine;
using BridgeOfBlood.Effects;

[DefaultExecutionOrder(60)]
public class ShopController : MonoBehaviour
{
    public Transform SpellTargetPurchasableRoot;
    public Transform ItemsPurchasableRoot;

    public PurchasableItemSpellUI SpellUI;
    public PurchasableItemJokerUI JokerUI;
    public PurchasableItemGemUI GemUI;
    public PurchasableItemMapModTicketUI MapModTicketUI;

    private readonly List<PurchasableItemUI> _listingInstances = new List<PurchasableItemUI>();

    private IShopService shopService;
    private PurchaseContext purchaseContext;

    private void OnEnable()
    {
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

    private void OnShopRefreshed()
    {
        ClearItems();
        SpawnItems();
    }

    public void FocusRegularRow(PurchasableItemUI row)
    {
        for (int i = 0; i < _listingInstances.Count; i++)
            _listingInstances[i].HideConfirmPurchaseButton();

        row.ShowConfirmPurchaseButton();
    }

    public bool TryPurchase(IPurchasable purchasable)
    {
        return shopService.TryPurchase(purchasable, purchaseContext);
    }

    private void SpawnItems()
    {
        foreach (IPurchasable purchasable in shopService.GetItemPurchasables())
        {
            PurchasableItemUI item = null;
            switch (purchasable)
            {
                case SpellAuthoringData:
                    PurchasableItemSpellUI spellUI = Instantiate(SpellUI, ItemsPurchasableRoot);
                    spellUI.InitializeShopItem(purchasable, purchaseContext, this);
                    spellUI.SetSpell(purchasable as SpellAuthoringData);
                    item = spellUI;
                    break;
                case Gem:
                    PurchasableItemGemUI gemUI = Instantiate(GemUI, ItemsPurchasableRoot);
                    gemUI.InitializeShopItem(purchasable, purchaseContext, this);
                    gemUI.SetGem(purchasable as Gem);
                    item = gemUI;
                    break;
                case MapModifierPlacementTicket:
                    PurchasableItemMapModTicketUI ticketUI = Instantiate(MapModTicketUI, ItemsPurchasableRoot);
                    ticketUI.InitializeShopItem(purchasable, purchaseContext, this);
                    item = ticketUI;
                    break;
                case Item:
                    PurchasableItemJokerUI itemUI = Instantiate(JokerUI, ItemsPurchasableRoot);
                    itemUI.InitializeShopItem(purchasable, purchaseContext, this);
                    itemUI.SetJoker(purchasable as Item);
                    item = itemUI;
                    break;
            }
            if(item == null) 
                Debug.LogError($"Failed to spawn item for {purchasable}");
            _listingInstances.Add(item);
            item.InitializeShopItem(purchasable, purchaseContext, this);
        }
    }

    private void ClearItems()
    {
        for (int i = 0; i < _listingInstances.Count; i++)
            Destroy(_listingInstances[i].gameObject);
        _listingInstances.Clear();
    }
}


public interface IShopService
{
    event Action ShopRefreshed;

    bool TryPurchase(IPurchasable purchasable, PurchaseContext context);

    List<IPurchasable> GetItemPurchasables();
    List<ISpellTargetPurchasable> GetSpellGemPurchasables();
}
