using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Effects;
using System;
using System.Collections.Generic;

/// <summary>
/// In-memory IInventoryService for test scenes. Accepts all mutations without persistence.
/// </summary>
public class MockInventoryService : IInventoryService
{
    readonly ItemCollection _items = new ItemCollection();
    readonly Stash _stash = new Stash(8, 4);

    public event Action ItemsUpdated;

    public ItemCollection Items => _items;
    public Stash Stash => _stash;

    public MockInventoryService()
    {
        _items.ItemsUpdated += () => ItemsUpdated?.Invoke();
    }

    public IReadOnlyList<RuntimeItem> GetItems() => _items.Items;

    public bool TrySetItemOrder(IReadOnlyList<RuntimeItem> reordered)
    {
        return _items.TrySetOrder(reordered);
    }

    public void AddItem(Item item)
    {
        if (item == null || item is SpellItem)
            return;
        _items.TryInsert(new RuntimeItem(item), _items.Count);
    }
}
