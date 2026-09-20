using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Random = Unity.Mathematics.Random;

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
    /// <paramref name="rng"/> is the spawn-time roll stream (shared across behaviors on one entity).
    /// </summary>
    public abstract void ApplyTo(
        AttackEntityManager manager,
        int index,
        ModificationSet mods,
        SpellAttributeMask mask,
        ref Random rng);
}

/// <summary>
/// Behavior that may apply <see cref="ModificationSet"/> data in <see cref="AttackEntityBehavior.ApplyTo"/>.
/// Player-facing stats are described via <see cref="IPresentableSpellAttribute"/>.
/// </summary>
[Serializable]
public abstract class ModifiableAttackEntityBehavior : AttackEntityBehavior, IPresentableSpellAttribute
{
    public abstract string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask);
}

/// <summary>
/// Behavior whose authored values are not scaled by spell modifications (e.g. fixed VFX or ailment application).
/// </summary>
[Serializable]
public abstract class FixedAttackEntityBehavior : AttackEntityBehavior
{
}

public interface IPresentableSpellAttribute
{
    string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask);
}

public static class SpellAttributeDisplay
{
    public static string ChanceToApply(bool isActive, float applyChance, string ailment)
    {
        if (!isActive || applyChance <= 0f)
            return string.Empty;
        if (applyChance >= 1f)
            return $"Applies {ailment}";
        return $"{applyChance * 100f:0.##}% chance to apply {ailment}";
    }
}
