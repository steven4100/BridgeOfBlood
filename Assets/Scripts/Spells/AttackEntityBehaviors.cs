using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;

/// <summary>
/// Base type for optional attack-entity behaviors. Used only as [SerializeReference] target.
/// <para>
/// At spawn time, <see cref="ApplyTo"/> writes this behavior's contribution (a policy runtime or an
/// entity scalar) directly into the manager's parallel lists by index. Spell modifications for this
/// behavior are resolved inline from <paramref name="mods"/> / <paramref name="mask"/>; there is no
/// separate ApplyModifications pass and no intermediate payload struct.
/// </para>
/// </summary>
[Serializable]
public abstract class AttackEntityBehavior
{
    public abstract AttackEntityBehavior Clone();

    /// <summary>
    /// Writes this behavior's runtime contribution into the just-spawned entity at <paramref name="index"/>.
    /// </summary>
    public abstract void ApplyTo(AttackEntityManager manager, int index, SpellModifications mods, SpellAttributeMask mask);
}
