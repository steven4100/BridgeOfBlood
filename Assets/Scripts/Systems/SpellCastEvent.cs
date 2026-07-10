using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

/// <summary>
/// Raised when the looped spell caster starts a spell cast.
/// Subscribers should consume the spell list during the callback only.
/// </summary>
public struct SpellCastEvent : IEvent
{
    public SpellCastResult castResult;
    public IReadOnlyList<RuntimeSpell> spells;
    public float2 origin;
}
