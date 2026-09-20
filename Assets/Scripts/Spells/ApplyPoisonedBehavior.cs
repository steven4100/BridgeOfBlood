using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class ApplyPoisonedBehavior : FixedAttackEntityBehavior, IPresentableSpellAttribute
{
    [Tooltip("When false, poisoned application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Poisoned per hit. 1 = always.")]
    public float applyChance = 1f;

    public PoisonedApplierRuntime ToRuntime() => new PoisonedApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyPoisonedBehavior { isActive = isActive, applyChance = applyChance };

    public override void ApplyTo(AttackEntityManager manager, int index, ModificationSet mods, SpellAttributeMask mask, ref Unity.Mathematics.Random rng)
    {
        var arr = manager.GetPoisonedAppliers();
        arr[index] = ToRuntime();
    }

    public string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask) =>
        SpellAttributeDisplay.ChanceToApply(isActive, applyChance, "Poisoned");
}

[Serializable]
public struct PoisonedApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static PoisonedApplierRuntime Default() => new PoisonedApplierRuntime { isActive = false, applyChance = 0f };
}
