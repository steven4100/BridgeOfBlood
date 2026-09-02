using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the player's spell loop as a horizontal strip. Binds from <see cref="ServiceLocator.Current"/>
/// on <see cref="ServicesRegisteredEvent"/>; explicit <see cref="Initialize(ISpellInventoryService)"/> remains for tests.
/// </summary>
[DefaultExecutionOrder(50)]
public class SpellInventoryController : MonoBehaviour, IItemReceptacle
{
    [SerializeField] private Transform LayoutGroupRoot;
    [SerializeField] private RuntimeSpellPresenter RuntimeSpellPresenterPrefab;

    private ISpellInventoryService _service;
    private readonly List<RuntimeSpellPresenter> spellUiInstances = new List<RuntimeSpellPresenter>();
    private bool _poolSeeded;
    Image _dropPreviewImage;
    Color _dropPreviewBase = Color.white;

    public void SetDropPreview(bool? valid)
    {
        InventoryDropPreview.Apply(_dropPreviewImage, ref _dropPreviewBase, valid);
    }

    public bool VisitSpell(RuntimeSpell spell, ref ReceptacleDropContext ctx)
    {
        ctx.InsertIndex = InventoryDropSite.StripInsertIndex((RectTransform)LayoutGroupRoot, ctx);
        if (!ctx.Commit)
            return true;
        return _service.TryInsert(spell, ctx.InsertIndex);
    }

    public bool VisitGem(RuntimeGem gem, ref ReceptacleDropContext ctx) => false;

    public bool VisitItem(RuntimeItem item, ref ReceptacleDropContext ctx) => false;

    public bool TryRemove(IInventoryOccupant occupant)
    {
        return occupant is RuntimeSpell spell && _service.TryRemove(spell);
    }

    /// <summary>Fired when a spell tile’s click button is used (<see cref="RuntimeSpellPresenter"/>).</summary>
    public event Action<int> SpellTileClicked;

    /// <summary>Fired after the spell strip finishes binding tiles (same frame as <c>RenderSpells</c>).</summary>
    public event Action SpellStripRendered;

    /// <summary>Resolves <see cref="ISpellInventoryService"/> from <see cref="ServiceLocator.Current"/>.</summary>
    public void Initialize()
    {
        Initialize(ServiceLocator.Current.GetService<ISpellInventoryService>());
    }

    public void Initialize(ISpellInventoryService service)
    {
        if (ReferenceEquals(_service, service))
            return;

        if (_service != null)
            _service.SpellsUpdated -= OnSpellsUpdated;

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

        _service.SpellsUpdated += OnSpellsUpdated;

        OnSpellsUpdated();
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
            _service.SpellsUpdated -= OnSpellsUpdated;
    }

    private void OnSpellsUpdated()
    {
        RenderSpells(_service.GetSpells());
    }

    private void RenderSpells(IReadOnlyList<RuntimeSpell> runtimeSpells)
    {
        EnsureEnoughRuntimeSpellInstances(runtimeSpells.Count);

        IItemReceptacle source = this;
        for (int i = 0; i < runtimeSpells.Count; i++)
        {
            RuntimeSpellPresenter inst = spellUiInstances[i];
            inst.SetVisible(true);
            inst.Bind(runtimeSpells[i], source, i);
            inst.transform.SetSiblingIndex(i);
        }

        for (int i = runtimeSpells.Count; i < spellUiInstances.Count; i++)
            spellUiInstances[i].SetVisible(false);

        RefreshSpellClickHandlers();
        SpellStripRendered?.Invoke();
    }

    public void ApplyShopHighlights(Func<RuntimeSpell, ShopSpellHighlight> selector)
    {
        if (_service == null)
            return;

        IReadOnlyList<RuntimeSpell> spells = _service.GetSpells();
        for (int i = 0; i < spellUiInstances.Count; i++)
        {
            if (i < spells.Count && spellUiInstances[i].gameObject.activeSelf)
                spellUiInstances[i].SetShopHighlight(selector(spells[i]));
            else
                spellUiInstances[i].SetShopHighlight(ShopSpellHighlight.None);
        }
    }

    public void ClearShopHighlights()
    {
        for (int i = 0; i < spellUiInstances.Count; i++)
            spellUiInstances[i].SetShopHighlight(ShopSpellHighlight.None);
    }

    void RefreshSpellClickHandlers()
    {
        for (int i = 0; i < spellUiInstances.Count; i++)
            spellUiInstances[i].SetSpellTileClickHandler(OnPresenterSpellTileClicked);
    }

    void OnPresenterSpellTileClicked(int spellId)
    {
        SpellTileClicked?.Invoke(spellId);
    }

    private void EnsureEnoughRuntimeSpellInstances(int needed)
    {
        while (spellUiInstances.Count < needed)
        {
            RuntimeSpellPresenter inst = Instantiate(RuntimeSpellPresenterPrefab, LayoutGroupRoot);
            spellUiInstances.Add(inst);
        }
    }

    private void SeedPoolFromExistingChildren()
    {
        int childCount = LayoutGroupRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = LayoutGroupRoot.GetChild(i);
            var presenter = child.GetComponent<RuntimeSpellPresenter>();
            if (presenter != null)
                spellUiInstances.Add(presenter);
        }
    }
}

public interface ISpellInventoryService
{
    void AddSpell(SpellAuthoringData spell);

    bool TryInsert(RuntimeSpell spell, int index);

    bool TryRemove(RuntimeSpell spell);

    IReadOnlyList<RuntimeSpell> GetSpells();

    bool TrySetSpellOrder(IReadOnlyList<int> spellIdOrder);

    /// <summary>Notify listeners after in-place mutation of runtime spells (e.g. gem attached).</summary>
    void NotifySpellsChanged();

    event Action SpellsUpdated;
}

public struct RuntimeSpellUiDTO
{
    public string name;
    public int id;
    public Sprite icon;

    public RuntimeSpellUiDTO(string name, int id, Sprite icon)
    {
        this.name = name;
        this.id = id;
        this.icon = icon;
    }
}
