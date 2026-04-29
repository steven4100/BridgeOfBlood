using System;
using UnityEngine;

[Serializable]
public class ApplyFrozenBehavior : AttackEntityBehavior
{
    [Tooltip("When false, frozen application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Frozen per hit. 1 = always.")]
    public float applyChance = 1f;

    public FrozenApplierRuntime ToRuntime() => new FrozenApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyFrozenBehavior { isActive = isActive, applyChance = applyChance };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.frozenApplier = ToRuntime();
}

[Serializable]
public struct FrozenApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static FrozenApplierRuntime Default() => new FrozenApplierRuntime { isActive = false, applyChance = 0f };
}
