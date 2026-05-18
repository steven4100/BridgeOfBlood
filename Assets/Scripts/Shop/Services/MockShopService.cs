using BridgeOfBlood.Data.Shop;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IShopService for test scenes. Assign items in the Inspector; lists are built on first request.
/// Attach as a MonoBehaviour on the installer GameObject and wire into ShopTestServiceInstaller.
/// </summary>
public class MockShopService : MonoBehaviour, IShopService
{
    [Header("Regular Items")]
    public List<Item> items = new List<Item>();

    [Header("Spell Gem Items (ISpellTargetPurchasable)")]
    public List<SpellItem> spellGems = new List<SpellItem>();

    [Header("Spell Purchasables")]
    public List<SpellAuthoringData> spellPurchasables = new List<SpellAuthoringData>();

    public event Action ShopRefreshed;

    public bool TryPurchase(IPurchasable purchasable, PurchaseContext context)
    {
        if (!ShopPurchase.TryPurchase(purchasable, context))
            return false;
        RemoveFromShelf(purchasable);
        ShopRefreshed?.Invoke();
        return true;
    }

    private void RemoveFromShelf(IPurchasable purchasable)
    {
        if (TryRemove(spellGems, purchasable))
            return;
        if (TryRemove(items, purchasable))
            return;
        TryRemove(spellPurchasables, purchasable);
    }

    private static bool TryRemove<T>(List<T> list, IPurchasable target)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i], target))
            {
                list.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public List<IPurchasable> GetItemPurchasables()
    {
        var result = new List<IPurchasable>(items.Count + spellPurchasables.Count);
        result.AddRange(items);
        result.AddRange(spellPurchasables);
        return result;
    }

    public List<ISpellTargetPurchasable> GetSpellGemPurchasables()
    {
        var result = new List<ISpellTargetPurchasable>(spellGems.Count);
        result.AddRange(spellGems);
        return result;
    }
}
