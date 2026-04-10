using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Everything needed to spawn one attack entity. Produced by AttackEntityBuilder from authoring data.
/// </summary>
public struct AttackEntitySpawnPayload
{
    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public float critChance;
    public float critDamageMultiplier;
    public float2 velocity;
    public HitBoxData hitBoxData;
    public PiercePolicyRuntime pierce;
    public ExpirationPolicyRuntime expiration;
    public ChainPolicyRuntime chain;
    public RehitPolicyRuntime rehit;
    public FrozenApplierRuntime frozenApplier;
    public IgnitedApplierRuntime ignitedApplier;
    public ShockedApplierRuntime shockedApplier;
    public PoisonedApplierRuntime poisonedApplier;
    public StunnedApplierRuntime stunnedApplier;
    public EntityVisual visual;
    public EffectSpriteConfigRuntime onHitEffect;
    public EffectSpriteConfigRuntime onKillEffect;
    public int spellId;
    public int spellInvocationId;
}

/// <summary>
/// Builds AttackEntitySpawnPayload from AttackEntityData (class with optional behaviors list).
/// Missing behaviors get default runtime policies (unlimited pierce, no expiration, chain disabled).
/// </summary>
public static class AttackEntityBuilder
{
    /// <summary>
    /// Builds a spawn payload from authoring data. Iterates behaviors and applies first of each type.
    /// </summary>
    public static AttackEntitySpawnPayload Build(AttackEntityData data, uint visualSeed = 0)
    {
        var payload = new AttackEntitySpawnPayload
        {
            physicalDamage = data.physicalDamage,
            coldDamage = data.coldDamage,
            fireDamage = data.fireDamage,
            lightningDamage = data.lightningDamage,
            critChance = data.critChance,
            critDamageMultiplier = data.critDamageMultiplier > 0f ? data.critDamageMultiplier : 1f,
            velocity = new float2(data.entityVelocity.x, data.entityVelocity.y),
            hitBoxData = data.hitBoxData,
            pierce = PiercePolicyRuntime.Default(),
            expiration = ExpirationPolicyRuntime.Default(),
            chain = ChainPolicyRuntime.Default(),
            rehit = RehitPolicyRuntime.Default(),
            frozenApplier = FrozenApplierRuntime.Default(),
            ignitedApplier = IgnitedApplierRuntime.Default(),
            shockedApplier = ShockedApplierRuntime.Default(),
            poisonedApplier = PoisonedApplierRuntime.Default(),
            stunnedApplier = StunnedApplierRuntime.Default(),
            visual = data.visual != null
                ? data.visual.Resolve(visualSeed)
                : EntityVisual.None,
            onHitEffect = EffectSpriteConfigRuntime.Default(),
            onKillEffect = EffectSpriteConfigRuntime.Default()
        };

        if (data.behaviors == null) return payload;

        for (int i = 0; i < data.behaviors.Count; i++)
        {
            var b = data.behaviors[i];
            if (b == null) continue;
            b.ApplyTo(ref payload);
        }

        payload.rehit.rehitCooldownSeconds = data.rehitCooldownSeconds;

        return payload;
    }
}
