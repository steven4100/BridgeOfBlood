using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Renders passive <see cref="Item"/> rows as a horizontal strip and commits drag-reorder to the
/// owning <see cref="IInventoryService"/> on release. Binds from <see cref="ServiceLocator.Current"/>
/// (<see cref="Initialize()"/> / <c>Start</c>) after installers or session code register
/// <see cref="IInventoryService"/>; explicit <see cref="Initialize(IInventoryService)"/> remains for tests.
/// </summary>
[DefaultExecutionOrder(50)]
public class ItemInventoryController : MonoBehaviour
{
    [SerializeField] private Transform LayoutGroupRoot;
    [SerializeField] private RuntimeItemPresenter RuntimeItemPresenterPrefab;

    private IInventoryService _service;
    private HorizontalLayoutReorderGroup _reorderGroup;

    private readonly List<RuntimeItemPresenter> _itemUiInstances = new List<RuntimeItemPresenter>();
    private readonly List<InventoryItem> _orderScratch = new List<InventoryItem>();
    private bool _poolSeeded;

    /// <summary>Resolves <see cref="IInventoryService"/> from <see cref="ServiceLocator.Current"/>.</summary>
    public void Initialize()
    {
        Initialize(ServiceLocator.Current.GetService<IInventoryService>());
    }

    public void Initialize(IInventoryService service)
    {
        if (ReferenceEquals(_service, service))
            return;

        if (_service != null)
        {
            _service.ItemsUpdated -= OnItemsUpdated;
            _reorderGroup.ReorderEndDrag -= CommitReorderedItemOrder;
        }

        _service = service;

        if (!_poolSeeded)
        {
            _reorderGroup = LayoutGroupRoot.GetComponent<HorizontalLayoutReorderGroup>();
            SeedPoolFromExistingChildren();
            _poolSeeded = true;
        }

        _service.ItemsUpdated += OnItemsUpdated;
        _reorderGroup.ReorderEndDrag += CommitReorderedItemOrder;

        OnItemsUpdated();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (_service != null)
            _service.ItemsUpdated -= OnItemsUpdated;
        if (_reorderGroup != null)
            _reorderGroup.ReorderEndDrag -= CommitReorderedItemOrder;
    }

    private void OnItemsUpdated()
    {
        IReadOnlyList<InventoryItem> rows = _service.GetPassiveItemRows();
        RenderItems(rows);
    }

    private void RenderItems(IReadOnlyList<InventoryItem> rows)
    {
        EnsureEnoughRuntimeItemInstances(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            RuntimeItemPresenter inst = _itemUiInstances[i];
            inst.SetVisible(true);
            inst.Bind(rows[i]);
            inst.transform.SetSiblingIndex(i);
        }

        for (int i = rows.Count; i < _itemUiInstances.Count; i++)
            _itemUiInstances[i].SetVisible(false);
    }

    private void EnsureEnoughRuntimeItemInstances(int needed)
    {
        while (_itemUiInstances.Count < needed)
        {
            RuntimeItemPresenter inst = Instantiate(RuntimeItemPresenterPrefab, LayoutGroupRoot);
            _itemUiInstances.Add(inst);
        }
    }

    private void CommitReorderedItemOrder()
    {
        _orderScratch.Clear();
        int childCount = LayoutGroupRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = LayoutGroupRoot.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;
            var presenter = child.GetComponent<RuntimeItemPresenter>();
            if (presenter == null)
                continue;
            _orderScratch.Add(presenter.Row);
        }

        _service.TrySetPassiveItemOrder(_orderScratch);
    }

    private void SeedPoolFromExistingChildren()
    {
        int childCount = LayoutGroupRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = LayoutGroupRoot.GetChild(i);
            var presenter = child.GetComponent<RuntimeItemPresenter>();
            if (presenter != null)
                _itemUiInstances.Add(presenter);
        }
    }
}

public interface IInventoryService
{
    public void AddInventoryItem(InventoryItem item);
    IReadOnlyList<InventoryItem> GetPassiveItemRows();
    bool TrySetPassiveItemOrder(IReadOnlyList<InventoryItem> reorderedItemRows);
    event Action ItemsUpdated;
}

public interface IWalletService
{
    public int Gold { get; }

    public bool TrySpend(int amount);
}
