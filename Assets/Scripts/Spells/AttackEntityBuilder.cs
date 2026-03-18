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
    public int spellId;
    public int spellInvocationId;
}

/// <summary>
/// Builds AttackEntitySpawnPayload from AttackEntityData (class with optional behaviors list).
/// Missing behaviors get default runtime policies (unlimited pierce, no expiration, chain disabled).
/// </summary>
public static class AttackEntityBuilder
{
    static PiercePolicyRuntime DefaultPierce() => new PiercePolicyRuntime { isActive = false, maxEnemiesHit = 0 };
    static ExpirationPolicyRuntime DefaultExpiration() => new ExpirationPolicyRuntime { isActive = false, maxTimeAlive = 0f, maxDistanceTravelled = 0f };
    static ChainPolicyRuntime DefaultChain() => new ChainPolicyRuntime { isActive = false, enabled = false, chainCount = 0, chainRange = 0f };
    static RehitPolicyRuntime DefaultRehit() => new RehitPolicyRuntime { rehitCooldownSeconds = 0f };
    static FrozenApplierRuntime DefaultFrozenApplier() => new FrozenApplierRuntime { isActive = false, applyChance = 0f };
    static IgnitedApplierRuntime DefaultIgnitedApplier() => new IgnitedApplierRuntime { isActive = false, applyChance = 0f };
    static ShockedApplierRuntime DefaultShockedApplier() => new ShockedApplierRuntime { isActive = false, applyChance = 0f };
    static PoisonedApplierRuntime DefaultPoisonedApplier() => new PoisonedApplierRuntime { isActive = false, applyChance = 0f };
    static StunnedApplierRuntime DefaultStunnedApplier() => new StunnedApplierRuntime { isActive = false, applyChance = 0f };

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
            pierce = DefaultPierce(),
            expiration = DefaultExpiration(),
            chain = DefaultChain(),
            rehit = DefaultRehit(),
            frozenApplier = DefaultFrozenApplier(),
            ignitedApplier = DefaultIgnitedApplier(),
            shockedApplier = DefaultShockedApplier(),
            poisonedApplier = DefaultPoisonedApplier(),
            stunnedApplier = DefaultStunnedApplier(),
            visual = data.visual != null
                ? data.visual.Resolve(visualSeed)
                : new EntityVisual { frameIndex = -1, scale = 1f }
        };

        if (data.behaviors == null) return payload;

        for (int i = 0; i < data.behaviors.Count; i++)
        {
            var b = data.behaviors[i];
            if (b == null) continue;

            if (b is PierceBehavior pb)
                payload.pierce = pb.ToRuntime();
            else if (b is ExpirationBehavior eb)
                payload.expiration = eb.ToRuntime();
            else if (b is ChainBehavior cb)
                payload.chain = cb.ToRuntime();
            else if (b is ApplyFrozenBehavior fb)
                payload.frozenApplier = fb.ToRuntime();
            else if (b is ApplyIgnitedBehavior ib)
                payload.ignitedApplier = ib.ToRuntime();
            else if (b is ApplyShockedBehavior sb)
                payload.shockedApplier = sb.ToRuntime();
            else if (b is ApplyPoisonedBehavior pob)
                payload.poisonedApplier = pob.ToRuntime();
            else if (b is ApplyStunnedBehavior stb)
                payload.stunnedApplier = stb.ToRuntime();
        }

        payload.rehit.rehitCooldownSeconds = data.rehitCooldownSeconds;

        return payload;
    }
}
