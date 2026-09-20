using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class PierceBehavior : ModifiableAttackEntityBehavior
{
    [Tooltip("When false, pierce logic is skipped (unlimited hits, no pierce removal).")]
    public bool isActive = true;

    [Tooltip("Max enemies this attack can hit. 0 = unlimited (pierce forever).")]
    public int maxEnemiesHit;

    public PiercePolicyRuntime ToRuntime()
    {
        return new PiercePolicyRuntime { isActive = isActive, maxEnemiesHit = maxEnemiesHit };
    }

    public override AttackEntityBehavior Clone() => new PierceBehavior { isActive = isActive, maxEnemiesHit = maxEnemiesHit };

    public override void ApplyTo(AttackEntityManager manager, int index, ModificationSet mods, SpellAttributeMask mask, ref Unity.Mathematics.Random rng)
    {
        var arr = manager.GetPiercePolicies();
        arr[index] = new PiercePolicyRuntime { isActive = isActive, maxEnemiesHit = ResolveMaxEnemiesHit(mods, mask) };
    }

    public override string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask)
    {
        if (!isActive)
            return string.Empty;
        int maxHit = ResolveMaxEnemiesHit(mods, mask);
        return maxHit == 0 ? "Pierce : Unlimited" : $"Pierce : {maxHit}";
    }

    int ResolveMaxEnemiesHit(ModificationSet mods, SpellAttributeMask mask)
    {
        if (mods == null)
            return maxEnemiesHit;
        var resolved = ParameterModificationMerger.Merge(
            mods.Get<PierceModificationData>(),
            modification => modification.parameters,
            modification => modification.filter,
            mask);
        return Mathf.Max(0, (int)(maxEnemiesHit * resolved.Multiplier) + (int)resolved.flat);
    }
}

/// <summary>
/// Runtime pierce policy. State (hits so far) lives on AttackEntity.enemiesHit.
/// </summary>
[Serializable]
public struct PiercePolicyRuntime
{
    /// <summary>When false, pierce filtering and removal are skipped for this entity.</summary>
    public bool isActive;
    /// <summary>Max enemies this attack can hit. 0 = unlimited.</summary>
    public int maxEnemiesHit;

    public static PiercePolicyRuntime Default() => new PiercePolicyRuntime { isActive = false, maxEnemiesHit = 0 };
}
