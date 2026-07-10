using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class ApplyIgnitedBehavior : AttackEntityBehavior
{
    [Tooltip("When false, ignited application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Ignited per hit. 1 = always.")]
    public float applyChance = 1f;

    public IgnitedApplierRuntime ToRuntime() => new IgnitedApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyIgnitedBehavior { isActive = isActive, applyChance = applyChance };

    public override void ApplyTo(AttackEntityManager manager, int index, SpellModifications mods, SpellAttributeMask mask)
    {
        var arr = manager.GetIgnitedAppliers();
        arr[index] = ToRuntime();
    }
}

[Serializable]
public struct IgnitedApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static IgnitedApplierRuntime Default() => new IgnitedApplierRuntime { isActive = false, applyChance = 0f };
}
