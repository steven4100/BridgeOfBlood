using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

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
        add => SpellsUpdated += value;
        remove => SpellsUpdated -= value;
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

    public void AddSpell(SpellAuthoringData spell)
    {
        TryInsert(new RuntimeSpell(spell), _runtimeSpells.Count);
    }

    public int IndexOf(RuntimeSpell spell)
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
        {
            if (ReferenceEquals(_runtimeSpells[i], spell) || _runtimeSpells[i].spellId == spell.spellId)
                return i;
        }
        return -1;
    }

    public bool TryInsert(RuntimeSpell spell, int index)
    {
        if (spell == null)
            return false;
        if (IndexOf(spell) >= 0)
            return false;

        if (index < 0 || index > _runtimeSpells.Count)
            index = _runtimeSpells.Count;
        _runtimeSpells.Insert(index, spell);
        spell.GemsChanged += NotifySpellsChanged;
        SpellsUpdated?.Invoke();
        return true;
    }

    public bool RemoveSpell(SpellAuthoringData spell)
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
        {
            if (ReferenceEquals(_runtimeSpells[i].Definition, spell))
                return TryRemove(_runtimeSpells[i]);
        }
        return false;
    }

    /// <summary>Clears all spells (e.g. before <see cref="BridgeOfBlood.Data.Inventory.PlayerInventory.RebuildFromStartingDefinition"/>).</summary>
    public void ClearSpells()
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
            _runtimeSpells[i].GemsChanged -= NotifySpellsChanged;
        _runtimeSpells.Clear();
        SpellsUpdated?.Invoke();
    }

    public void ClearRuntimeSpellTracking()
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
            _runtimeSpells[i].ResetTracking();
    }

    IReadOnlyList<RuntimeSpell> ISpellInventoryService.GetSpells()
    {
        return _runtimeSpells;
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

    public bool OwnsPayload(ScriptableObject asset)
    {
        for (int i = 0; i < _runtimeSpells.Count; i++)
        {
            RuntimeSpell spell = _runtimeSpells[i];
            if (ReferenceEquals(spell.Definition, asset))
                return true;
            if (spell.Gems.OwnsPayload(asset))
                return true;
        }
        return false;
    }

    public bool TryRemove(RuntimeSpell spell)
    {
        int index = IndexOf(spell);
        if (index < 0)
            return false;
        _runtimeSpells[index].GemsChanged -= NotifySpellsChanged;
        _runtimeSpells.RemoveAt(index);
        SpellsUpdated?.Invoke();
        return true;
    }
}
