using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shop;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapts ShopRepository to IShopService, splitting all purchasables into items and spell gems.
/// </summary>
public class RepositoryShopService : IShopService
{
    private readonly PlayerInventory _inventory;
    private readonly List<IPurchasable> _items = new List<IPurchasable>();
    private readonly List<ISpellTargetPurchasable> _spellGems = new List<ISpellTargetPurchasable>();

    public event Action ShopRefreshed;

    public RepositoryShopService(ShopRepository repository, PlayerInventory inventory)
    {
        _inventory = inventory;

        foreach (IPurchasable p in repository.GetAll())
        {
            if (p is ISpellTargetPurchasable spellGem)
                _spellGems.Add(spellGem);
            else if (!OwnsInventoryPayload(p))
                _items.Add(p);
        }
    }

    public bool TryPurchase(IPurchasable purchasable, PurchaseContext context)
    {
        if (!(purchasable is ISpellTargetPurchasable) && OwnsInventoryPayload(purchasable))
            return false;

        if (!ShopPurchase.TryPurchase(purchasable, context))
            return false;
        RemoveFromShelf(purchasable);
        ShopRefreshed?.Invoke();
        return true;
    }

    private bool OwnsInventoryPayload(IPurchasable purchasable)
    {
        return purchasable is ScriptableObject asset && _inventory.OwnsPayload(asset);
    }

    private void RemoveFromShelf(IPurchasable purchasable)
    {
        if (purchasable is ISpellTargetPurchasable)
        {
            for (int i = _spellGems.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_spellGems[i], purchasable))
                {
                    _spellGems.RemoveAt(i);
                    return;
                }
            }
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_items[i], purchasable))
            {
                _items.RemoveAt(i);
                return;
            }
        }
    }

    public List<IPurchasable> GetItemPurchasables() => _items;
    public List<ISpellTargetPurchasable> GetSpellGemPurchasables() => _spellGems;
}
