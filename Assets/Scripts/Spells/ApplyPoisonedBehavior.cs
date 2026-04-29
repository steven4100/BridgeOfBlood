using System;
using UnityEngine;

[Serializable]
public class ApplyPoisonedBehavior : AttackEntityBehavior
{
    [Tooltip("When false, poisoned application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Poisoned per hit. 1 = always.")]
    public float applyChance = 1f;

    public PoisonedApplierRuntime ToRuntime() => new PoisonedApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyPoisonedBehavior { isActive = isActive, applyChance = applyChance };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.poisonedApplier = ToRuntime();
}

[Serializable]
public struct PoisonedApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static PoisonedApplierRuntime Default() => new PoisonedApplierRuntime { isActive = false, applyChance = 0f };
}
