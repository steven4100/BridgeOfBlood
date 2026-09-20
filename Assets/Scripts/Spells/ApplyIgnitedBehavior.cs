using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class ApplyIgnitedBehavior : FixedAttackEntityBehavior, IPresentableSpellAttribute
{
    [Tooltip("When false, ignited application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Ignited per hit. 1 = always.")]
    public float applyChance = 1f;

    public IgnitedApplierRuntime ToRuntime() => new IgnitedApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyIgnitedBehavior { isActive = isActive, applyChance = applyChance };

    public override void ApplyTo(AttackEntityManager manager, int index, ModificationSet mods, SpellAttributeMask mask, ref Unity.Mathematics.Random rng)
    {
        var arr = manager.GetIgnitedAppliers();
        arr[index] = ToRuntime();
    }

    public string GetDisplayDescription(ModificationSet mods, SpellAttributeMask mask) =>
        SpellAttributeDisplay.ChanceToApply(isActive, applyChance, "Ignited");
}

[Serializable]
public struct IgnitedApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static IgnitedApplierRuntime Default() => new IgnitedApplierRuntime { isActive = false, applyChance = 0f };
}
