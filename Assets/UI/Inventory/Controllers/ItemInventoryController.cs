using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using EZServiceLocation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders passive jokers as a horizontal strip. Binds from <see cref="ServiceLocator.Current"/>
/// on <see cref="ServicesRegisteredEvent"/>; explicit <see cref="Initialize(IInventoryService)"/> remains for tests.
/// </summary>
[DefaultExecutionOrder(50)]
public class ItemInventoryController : MonoBehaviour, IItemReceptacle
{
    [SerializeField] private Transform LayoutGroupRoot;
    [SerializeField] private RuntimeItemPresenter RuntimeItemPresenterPrefab;

    private IInventoryService _service;
    private readonly List<RuntimeItemPresenter> _itemUiInstances = new List<RuntimeItemPresenter>();
    private bool _poolSeeded;
    Image _dropPreviewImage;
    Color _dropPreviewBase = Color.white;

    public void SetDropPreview(bool? valid)
    {
        InventoryDropPreview.Apply(_dropPreviewImage, ref _dropPreviewBase, valid);
    }

    public bool VisitSpell(RuntimeSpell spell, ref ReceptacleDropContext ctx) => false;

    public bool VisitGem(RuntimeGem gem, ref ReceptacleDropContext ctx) => false;

    public bool VisitItem(RuntimeItem item, ref ReceptacleDropContext ctx)
    {
        ctx.InsertIndex = InventoryDropSite.StripInsertIndex((RectTransform)LayoutGroupRoot, ctx);
        if (!ctx.Commit)
            return true;
        return _service.Items.TryInsert(item, ctx.InsertIndex);
    }

    public bool TryRemove(IInventoryOccupant occupant)
    {
        return occupant is RuntimeItem item && _service.Items.TryRemove(item);
    }

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
            _service.ItemsUpdated -= OnItemsUpdated;

        _service = service;

        if (!_poolSeeded)
        {
            SeedPoolFromExistingChildren();
            _poolSeeded = true;
            var reorder = LayoutGroupRoot.GetComponent<HorizontalLayoutReorderGroup>();
            if (reorder != null)
                reorder.enabled = false;
            InventoryDropPreview.EnsureRaycastGraphic(LayoutGroupRoot.gameObject);
            _dropPreviewImage = LayoutGroupRoot.GetComponent<Image>();
            if (_dropPreviewImage != null)
                _dropPreviewBase = _dropPreviewImage.color;
        }

        _service.ItemsUpdated += OnItemsUpdated;

        OnItemsUpdated();
    }

    void OnEnable()
    {
        ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
    }

    void OnDisable()
    {
        ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
    }

    void OnServicesRegistered(ref ServicesRegisteredEvent _)
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (_service != null)
            _service.ItemsUpdated -= OnItemsUpdated;
    }

    private void OnItemsUpdated()
    {
        IReadOnlyList<RuntimeItem> rows = _service.GetItems();
        RenderItems(rows);
    }

    private void RenderItems(IReadOnlyList<RuntimeItem> rows)
    {
        EnsureEnoughRuntimeItemInstances(rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            RuntimeItemPresenter inst = _itemUiInstances[i];
            inst.SetVisible(true);
            inst.Bind(rows[i], this, i);
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
    void AddItem(Item item);
    IReadOnlyList<RuntimeItem> GetItems();
    bool TrySetItemOrder(IReadOnlyList<RuntimeItem> reordered);
    ItemCollection Items { get; }
    Stash Stash { get; }
    event Action ItemsUpdated;
}

public interface IWalletService
{
    public int Gold { get; }

    public bool TrySpend(int amount);
}
