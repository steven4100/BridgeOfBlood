using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Everything needed to spawn one attack entity. Produced by AttackEntityBuilder from authoring data.
/// Serializable for baked copies on item combat-reaction entries.
/// </summary>
[Serializable]
public struct AttackEntitySpawnPayload
{
    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public float critChance;
    public float critDamageMultiplier;
    public float knockbackStrength;
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
    public BleedApplierRuntime bleedApplier;
    public EntityVisual visual;
    public AudioUnitRuntime onDamageSound;
    public EffectSpriteConfigRuntime onHitEffect;
    public EffectSpriteConfigRuntime onKillEffect;
    public int spellId;
    public int spellInvocationId;

    /// <summary>
    /// Copy for spawning a new entity from a baked template (e.g. item combat reaction): stamps spell ids from the
    /// triggering event and clears chain/rehit transient state while keeping baked config.
    /// </summary>
    public AttackEntitySpawnPayload WithSpellProvenanceForNewEntity(int spellId, int spellInvocationId)
    {
        var p = this;
        p.spellId = spellId;
        p.spellInvocationId = spellInvocationId;

        ChainPolicyRuntime tc = p.chain;
        p.chain = new ChainPolicyRuntime
        {
            isActive = tc.isActive,
            chainCount = tc.chainCount,
            chainRange = tc.chainRange,
            targetSelect = tc.targetSelect,
            excludePreviouslyHit = tc.excludePreviouslyHit,
            enabled = tc.enabled,
            chainHitsSoFar = 0,
            hitEnemyIds = default
        };

        float cd = p.rehit.rehitCooldownSeconds;
        p.rehit = RehitPolicyRuntime.Default();
        p.rehit.rehitCooldownSeconds = cd;

        return p;
    }
}

/// <summary>
/// Per-keyframe inputs for rolling <see cref="AttackEntityData"/> ranges deterministically once per build.
/// </summary>
public readonly struct AttackEntityBuildContext
{
    public readonly int spellId;
    public readonly int spellInvocationId;
    public readonly int keyframeIndex;
    public readonly int attackDataInstanceId;

    public AttackEntityBuildContext(int spellId, int spellInvocationId, int keyframeIndex, int attackDataInstanceId)
    {
        this.spellId = spellId;
        this.spellInvocationId = spellInvocationId;
        this.keyframeIndex = keyframeIndex;
        this.attackDataInstanceId = attackDataInstanceId;
    }
}

/// <summary>
/// Builds AttackEntitySpawnPayload from AttackEntityData (class with optional behaviors list).
/// Missing behaviors get default runtime policies (unlimited pierce, no expiration, chain disabled).
/// </summary>
public static class AttackEntityBuilder
{
    /// <summary>
    /// Builds a spawn payload from authoring data. Iterates behaviors and applies first of each type.
    /// Damage and crit stats are rolled once from ranges using <paramref name="context"/>.
    /// </summary>
    public static AttackEntitySpawnPayload Build(AttackEntityData data, in AttackEntityBuildContext context, uint visualSeed = 0)
    {
        uint seed = AttackEntityBuildRngSeed.Mix(context.spellId, context.spellInvocationId, context.keyframeIndex, context.attackDataInstanceId);
        var rng = Unity.Mathematics.Random.CreateFromIndex(seed);

        float physicalDamage = data.physicalDamageRange.ResolveUniform(ref rng);
        float coldDamage = data.coldDamageRange.ResolveUniform(ref rng);
        float fireDamage = data.fireDamageRange.ResolveUniform(ref rng);
        float lightningDamage = data.lightningDamageRange.ResolveUniform(ref rng);
        float critChance = Mathf.Clamp01(data.critChanceRange.ResolveUniform(ref rng));
        float critDamageMultiplier = Mathf.Max(1f, data.critDamageMultiplierRange.ResolveUniform(ref rng));

        if (visualSeed == 0u)
            visualSeed = seed ^ 0x9E3779B9u;
        uint audioSeed = seed ^ 0x7F4A7C15u;

        var payload = new AttackEntitySpawnPayload
        {
            physicalDamage = physicalDamage,
            coldDamage = coldDamage,
            fireDamage = fireDamage,
            lightningDamage = lightningDamage,
            critChance = critChance,
            critDamageMultiplier = critDamageMultiplier,
            knockbackStrength = Mathf.Max(0f, data.knockbackStrength),
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
            bleedApplier = BleedApplierRuntime.Default(),
            visual = data.visual != null
                ? data.visual.Resolve(visualSeed)
                : EntityVisual.None,
            onDamageSound = data.onDamageSound != null
                ? data.onDamageSound.ToRuntime(audioSeed)
                : AudioUnitRuntime.None,
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
