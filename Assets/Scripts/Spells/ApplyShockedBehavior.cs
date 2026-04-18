using System;
using UnityEngine;

[Serializable]
public class ApplyShockedBehavior : AttackEntityBehavior
{
    [Tooltip("When false, shocked application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Shocked per hit. 1 = always.")]
    public float applyChance = 1f;

    [Tooltip("Multiplier on damage taken while shocked.")]
    public float incomingDamageTakenMultiplier = 1.25f;

    public ShockedApplierRuntime ToRuntime() => new ShockedApplierRuntime
    {
        isActive = isActive,
        applyChance = applyChance,
        incomingDamageTakenMultiplier = incomingDamageTakenMultiplier
    };

    public override AttackEntityBehavior Clone() => new ApplyShockedBehavior { isActive = isActive, applyChance = applyChance, incomingDamageTakenMultiplier = incomingDamageTakenMultiplier };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.shockedApplier = ToRuntime();
}

public struct ShockedApplierRuntime
{
    public bool isActive;
    public float applyChance;
    public float incomingDamageTakenMultiplier;

    public static ShockedApplierRuntime Default() => new ShockedApplierRuntime { isActive = false, applyChance = 0f, incomingDamageTakenMultiplier = 1f };
}
