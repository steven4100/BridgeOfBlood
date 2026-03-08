using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

/// <summary>
/// Builds and holds the runtime spell list from authoring data. Assigns a static spellId per asset
/// (GetInstanceID) so reordering the list does not change a spell's identity.
/// </summary>
public class SpellCollection
{
    private readonly List<Spell> _runtimeSpells;
    private readonly List<SpellAuthoringData> _authoringData;

    public IReadOnlyList<Spell> RuntimeSpells => _runtimeSpells;
    public IReadOnlyList<SpellAuthoringData> AuthoringData => _authoringData;
    public int Count => _runtimeSpells.Count;

    public SpellCollection(IReadOnlyList<SpellAuthoringData> authoringList)
    {
        _runtimeSpells = new List<Spell>();
        _authoringData = new List<SpellAuthoringData>();

        if (authoringList == null) return;

        for (int i = 0; i < authoringList.Count; i++)
        {
            SpellAuthoringData a = authoringList[i];
            if (a == null) continue;

            _authoringData.Add(a);
            _runtimeSpells.Add(new Spell
            {
                spellId = a.GetInstanceID(),
                baseMultiplier = a.baseMultiplier,
                castCompletionDuration = a.castCompletionDuration,
                castTime = a.castTime,
                attributeMask = a.attributeMask,
                invocationCount = 0,
                roundTimeInvokedAt = 0
            });
        }
    }
}
