using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;

/// <summary>
/// Holds one list of <see cref="RuntimeSpell"/> rows keyed by static <see cref="SpellAuthoringData"/> definitions.
/// List reference is stable for <see cref="LoopedSpellCaster"/>.
/// </summary>
public class SpellCollection
{
    private readonly List<RuntimeSpell> _runtimeSpells;

    public IReadOnlyList<RuntimeSpell> RuntimeSpells => _runtimeSpells;
    public int Count => _runtimeSpells.Count;

    public SpellCollection(IReadOnlyList<SpellAuthoringData> authoringList)
    {
        _runtimeSpells = new List<RuntimeSpell>();
        RebuildFrom(authoringList);
    }

    /// <summary>
    /// Clears and repopulates from <paramref name="sources"/>. Retains the same <c>List&lt;RuntimeSpell&gt;</c> instance.
    /// </summary>
    /// <remarks>
    /// Not used every frame. Each rebuild creates new <see cref="RuntimeSpell"/> instances (invocation counters reset).
    /// When the definition sequence is unchanged, prefer <see cref="SyncSpellLoopFromInventory"/>.
    /// </remarks>
    public void RebuildFrom(IReadOnlyList<SpellAuthoringData> sources)
    {
        _runtimeSpells.Clear();

        if (sources == null) return;

        for (int i = 0; i < sources.Count; i++)
        {
            SpellAuthoringData a = sources[i];
            if (a == null) continue;

            _runtimeSpells.Add(new RuntimeSpell(a));
        }
    }

    /// <summary>
    /// If the definition sequence matches, only clears invocation tracking; otherwise <see cref="RebuildFrom"/>.
    /// </summary>
    public void SyncSpellLoopFromInventory(IReadOnlyList<SpellAuthoringData> sources)
    {
        if (DefinitionSequenceEquals(sources))
            ClearRuntimeSpellTracking();
        else
            RebuildFrom(sources);
    }

    bool DefinitionSequenceEquals(IReadOnlyList<SpellAuthoringData> sources)
    {
        if (sources == null)
            return _runtimeSpells.Count == 0;

        int n = sources.Count;
        if (_runtimeSpells.Count != n)
            return false;

        for (int i = 0; i < n; i++)
        {
            if (!ReferenceEquals(_runtimeSpells[i].Definition, sources[i]))
                return false;
        }

        return true;
    }

    static void ClearRuntimeSpellTracking(List<RuntimeSpell> spells)
    {
        for (int i = 0; i < spells.Count; i++)
        {
            RuntimeSpell s = spells[i];
            s.invocationCount = 0;
            s.roundTimeInvokedAt = 0;
        }
    }

    void ClearRuntimeSpellTracking()
    {
        ClearRuntimeSpellTracking(_runtimeSpells);
    }
}
