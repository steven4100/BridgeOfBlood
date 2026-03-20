using System;
using UnityEngine;

[Serializable]
public class OnKillEffectBehavior : AttackEntityBehavior
{
    [Tooltip("Shared effect sprite config for on-kill visuals.")]
    public EffectSpriteConfig config;

    public override AttackEntityBehavior Clone() => new OnKillEffectBehavior { config = config };

    public override void ApplyTo(ref AttackEntitySpawnPayload payload)
    {
        if (config != null)
            payload.onKillEffect = config.ToRuntime();
    }
}
