using System;
using UnityEngine;

[Serializable]
public class OnHitEffectBehavior : AttackEntityBehavior
{
    [Tooltip("Shared effect sprite config for on-hit visuals.")]
    public EffectSpriteConfig config;

    public override AttackEntityBehavior Clone() => new OnHitEffectBehavior { config = config };

    public override void ApplyTo(ref AttackEntitySpawnPayload payload)
    {
        if (config != null)
            payload.onHitEffect = config.ToRuntime();
    }
}
