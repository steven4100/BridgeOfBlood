using System;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using UnityEngine;

[Serializable]
public class OnKillEffectBehavior : FixedAttackEntityBehavior
{
    [Tooltip("Shared effect sprite config for on-kill visuals.")]
    public EffectSpriteConfig config;

    public override AttackEntityBehavior Clone() => new OnKillEffectBehavior { config = config };

    public override void ApplyTo(AttackEntityManager manager, int index, SpellModifications mods, SpellAttributeMask mask, ref Unity.Mathematics.Random rng)
    {
        if (config == null) return;
        var entities = manager.GetEntities();
        var e = entities[index];
        e.onKillEffect = config.ToRuntime();
        entities[index] = e;
    }
}
