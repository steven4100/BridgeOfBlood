using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class OnHitEffectBehavior : AttackEntityBehavior
{
    [Tooltip("Shared effect sprite config for on-hit visuals.")]
    public EffectSpriteConfig config;

    public override AttackEntityBehavior Clone() => new OnHitEffectBehavior { config = config };

    public override void ApplyTo(AttackEntityManager manager, int index, SpellModifications mods, SpellAttributeMask mask)
    {
        if (config == null) return;
        var entities = manager.GetEntities();
        var e = entities[index];
        e.onHitEffect = config.ToRuntime();
        entities[index] = e;
    }
}
