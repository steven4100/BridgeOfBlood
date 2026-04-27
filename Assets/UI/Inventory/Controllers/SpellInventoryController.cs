using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the player's spell loop as a horizontal strip and commits drag-reorder to the
/// owning <see cref="ISpellInventoryService"/> on release. Lifecycle is driven by an explicit
/// <see cref="Initialize"/> call from the owning scene/session controller — the service is not
/// available at <c>OnEnable</c>, so subscriptions happen there instead.
/// </summary>
public class SpellInventoryController : MonoBehaviour
{
    [SerializeField] private Transform LayoutGroupRoot;
    [SerializeField] private RuntimeSpellPresenter RuntimeSpellPresenterPrefab;

    private ISpellInventoryService _service;
    private HorizontalLayoutReorderGroup _reorderGroup;

    private readonly List<RuntimeSpellPresenter> spellUiInstances = new List<RuntimeSpellPresenter>();
    private readonly List<int> _orderScratch = new List<int>();
    private bool _poolSeeded;

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

    private void OnDestroy()
    {
        if (_service != null)
            _service.SpellsUpdated -= OnSpellsUpdated;
        if (_reorderGroup != null)
            _reorderGroup.ReorderEndDrag -= CommitReorderedSpellOrder;
    }

    private void OnSpellsUpdated()
    {
        RenderSpells(_service.GetSpellUi());
    }

    private void RenderSpells(List<RuntimeSpellUiDTO> runtimeSpellUiDTOs)
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
    public List<RuntimeSpellUiDTO> GetSpellUi();

    public bool TrySetSpellOrder(IReadOnlyList<int> spellIdOrder);

    public event Action SpellsUpdated;

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
