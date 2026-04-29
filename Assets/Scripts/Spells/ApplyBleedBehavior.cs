using System;
using UnityEngine;

[Serializable]
public class ApplyBleedBehavior : AttackEntityBehavior
{
    [Tooltip("When false, bleed application is skipped for this entity.")]
    public bool isActive = true;

    [Range(0f, 1f)]
    [Tooltip("Probability of applying Bleed per hit. 1 = always.")]
    public float applyChance = 1f;

    public BleedApplierRuntime ToRuntime() => new BleedApplierRuntime { isActive = isActive, applyChance = applyChance };

    public override AttackEntityBehavior Clone() => new ApplyBleedBehavior { isActive = isActive, applyChance = applyChance };
    public override void ApplyTo(ref AttackEntitySpawnPayload payload) => payload.bleedApplier = ToRuntime();
}

[Serializable]
public struct BleedApplierRuntime
{
    public bool isActive;
    public float applyChance;

    public static BleedApplierRuntime Default() => new BleedApplierRuntime { isActive = false, applyChance = 0f };
}
