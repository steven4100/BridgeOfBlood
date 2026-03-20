using System;
using UnityEngine;

[Serializable]
public class ApplyStunnedBehavior : AttackEntityBehavior
{
    [Tooltip("When false, stunned application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Stunned per hit. 1 = always.")]
    public float applyChance = 1f;

    public StunnedApplierRuntime ToRuntime() => new StunnedApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyStunnedBehavior { isActive = isActive, applyChance = applyChance };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.stunnedApplier = ToRuntime();
}

public struct StunnedApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static StunnedApplierRuntime Default() => new StunnedApplierRuntime { isActive = false, applyChance = 0f };
}
