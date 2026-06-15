using System;
using System.Collections.Generic;
using System.Linq;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;

/// <summary>
/// Holds one ordered list of <see cref="RuntimeSpell"/> rows; each row has a unique <see cref="RuntimeSpell.spellId"/>.
/// Owned by <see cref="BridgeOfBlood.Data.Inventory.PlayerInventory"/>; list reference is stable for <see cref="LoopedSpellCaster"/>.
/// </summary>
public class SpellCollection : ISpellInventoryService
{
    private readonly List<RuntimeSpell> _runtimeSpells;
    private readonly List<RuntimeSpell> _orderScratch = new List<RuntimeSpell>();
    public IReadOnlyList<RuntimeSpell> RuntimeSpells => _runtimeSpells;
    public int Count => _runtimeSpells.Count;
    Action SpellsUpdated;

    event Action ISpellInventoryService.SpellsUpdated
    {
        add
        {
            SpellsUpdated += value;
        }

        remove
        {
            SpellsUpdated -= value;
        }   
    }

    public SpellCollection(IReadOnlyList<SpellAuthoringData> authoringList)
    {
        _runtimeSpells = new List<RuntimeSpell>();
        if (authoringList != null)
        {
            foreach (var spell in authoringList)
            {
                if (spell == null) continue;
                AddSpell(spell);
            }
        }
    }

    public void AddSpell(SpellAuthoringData spell){
        _runtimeSpells.Add(new RuntimeSpell(spell));
        SpellsUpdated?.Invoke();
    }

    public bool RemoveSpell(SpellAuthoringData spell)
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
        {
            if (ReferenceEquals(_runtimeSpells[i].Definition, spell))
            {
                _runtimeSpells.RemoveAt(i);
                SpellsUpdated?.Invoke();
                return true;
            }
        }
        return false;
    }

    /// <summary>Clears all spells (e.g. before <see cref="BridgeOfBlood.Data.Inventory.PlayerInventory.RebuildFromStartingDefinition"/>).</summary>
    public void ClearSpells()
    {
        _runtimeSpells.Clear();
        SpellsUpdated?.Invoke();
    }

    public void ClearRuntimeSpellTracking()
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
        {
            RuntimeSpell s = _runtimeSpells[i];
            s.invocationCount = 0;
            s.roundTimeInvokedAt = 0;
        }
    }

    IReadOnlyList<RuntimeSpell> ISpellInventoryService.GetSpells()
    {
        return _runtimeSpells;
        //return _runtimeSpells.Select(s => new RuntimeSpellUiDTO(s.Definition.name, s.spellId, s.Definition.icon)).ToList();
    }

    bool ISpellInventoryService.TrySetSpellOrder(IReadOnlyList<int> spellIdOrder)
    {
        if (spellIdOrder.Count != _runtimeSpells.Count)
            return false;

        _orderScratch.Clear();
        for (int i = 0; i < spellIdOrder.Count; i++)
        {
            int targetId = spellIdOrder[i];
            RuntimeSpell match = null;
            for (int j = 0; j < _runtimeSpells.Count; j++)
            {
                RuntimeSpell candidate = _runtimeSpells[j];
                if (candidate.spellId != targetId)
                    continue;
                if (_orderScratch.Contains(candidate))
                {
                    _orderScratch.Clear();
                    return false;
                }
                match = candidate;
                break;
            }
            if (match == null)
            {
                _orderScratch.Clear();
                return false;
            }
            _orderScratch.Add(match);
        }

        _runtimeSpells.Clear();
        _runtimeSpells.AddRange(_orderScratch);
        _orderScratch.Clear();

        SpellsUpdated?.Invoke();
        return true;
    }

    public void NotifySpellsChanged()
    {
        SpellsUpdated?.Invoke();
    }
}
