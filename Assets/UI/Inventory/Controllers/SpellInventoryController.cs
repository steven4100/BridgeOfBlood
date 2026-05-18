using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Renders the player's spell loop as a horizontal strip and commits drag-reorder to the
/// owning <see cref="ISpellInventoryService"/> on release. Binds from <see cref="ServiceLocator.Current"/>
/// (<see cref="Initialize()"/> / <c>Start</c>) after installers or session code register
/// <see cref="ISpellInventoryService"/>; explicit <see cref="Initialize(ISpellInventoryService)"/> remains for tests.
/// </summary>
[DefaultExecutionOrder(50)]
public class SpellInventoryController : MonoBehaviour
{
    [SerializeField] private Transform LayoutGroupRoot;
    [SerializeField] private RuntimeSpellPresenter RuntimeSpellPresenterPrefab;

    private ISpellInventoryService _service;
    private HorizontalLayoutReorderGroup _reorderGroup;

    private readonly List<RuntimeSpellPresenter> spellUiInstances = new List<RuntimeSpellPresenter>();
    private readonly List<int> _orderScratch = new List<int>();
    private bool _poolSeeded;

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
        {
            _service.SpellsUpdated -= OnSpellsUpdated;
            _reorderGroup.ReorderEndDrag -= CommitReorderedSpellOrder;
        }

        _service = service;

        if (!_poolSeeded)
        {
            _reorderGroup = LayoutGroupRoot.GetComponent<HorizontalLayoutReorderGroup>();
            SeedPoolFromExistingChildren();
            _poolSeeded = true;
        }

        _service.SpellsUpdated += OnSpellsUpdated;
        _reorderGroup.ReorderEndDrag += CommitReorderedSpellOrder;

        OnSpellsUpdated();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (_service != null)
            _service.SpellsUpdated -= OnSpellsUpdated;
        if (_reorderGroup != null)
            _reorderGroup.ReorderEndDrag -= CommitReorderedSpellOrder;
    }

    private void OnSpellsUpdated()
    {
        RenderSpells(_service.GetSpells());
    }

    private void RenderSpells(IReadOnlyList<RuntimeSpell> runtimeSpellUiDTOs)
    {
        EnsureEnoughRuntimeSpellInstances(runtimeSpellUiDTOs.Count);

        for (int i = 0; i < runtimeSpellUiDTOs.Count; i++)
        {
            RuntimeSpellPresenter inst = spellUiInstances[i];
            inst.SetVisible(true);
            inst.Bind(runtimeSpellUiDTOs[i]);
            inst.transform.SetSiblingIndex(i);
        }

        for (int i = runtimeSpellUiDTOs.Count; i < spellUiInstances.Count; i++)
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

    private void CommitReorderedSpellOrder()
    {
        _orderScratch.Clear();
        int childCount = LayoutGroupRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = LayoutGroupRoot.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;
            var presenter = child.GetComponent<RuntimeSpellPresenter>();
            if (presenter == null)
                continue;
            _orderScratch.Add(presenter.SpellId);
        }

        _service.TrySetSpellOrder(_orderScratch);
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
