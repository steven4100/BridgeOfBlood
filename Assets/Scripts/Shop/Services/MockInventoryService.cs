using BridgeOfBlood.Data.Inventory;
using System;
using System.Collections.Generic;

/// <summary>
/// In-memory IInventoryService for test scenes. Accepts all mutations without persistence.
/// </summary>
public class MockInventoryService : IInventoryService
{
    private readonly List<InventoryItem> _rows = new List<InventoryItem>();
    private readonly List<InventoryItem> _passiveScratch = new List<InventoryItem>();

    public event Action ItemsUpdated;

    public IReadOnlyList<InventoryItem> GetPassiveItemRows()
    {
        _passiveScratch.Clear();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Payload != null)
                _passiveScratch.Add(_rows[i]);
        }
        return _passiveScratch;
    }

    public bool TrySetPassiveItemOrder(IReadOnlyList<InventoryItem> reorderedItemRows)
    {
        return true;
    }

    public void AddInventoryItem(InventoryItem row)
    {
        if (row == null) return;
        _rows.Add(row);
        ItemsUpdated?.Invoke();
    }
}
