using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;

/// <summary>
/// Base type for optional attack-entity behaviors. Used only as [SerializeReference] target.
/// </summary>
[Serializable]
public abstract class AttackEntityBehavior
{
    public abstract AttackEntityBehavior Clone();
    public abstract void ApplyTo(ref AttackEntitySpawnPayload payload);
    public virtual void ApplyModifications(SpellModifications mods, SpellAttributeMask spellMask) { }
}
