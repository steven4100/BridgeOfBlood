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
    public float2 velocity;
    public HitBoxData hitBoxData;
    public PiercePolicyRuntime pierce;
    public ExpirationPolicyRuntime expiration;
    public ChainPolicyRuntime chain;
    public RehitPolicyRuntime rehit;
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
    static RehitPolicyRuntime DefaultRehit() => new RehitPolicyRuntime { rehitCooldownSeconds = 100000000 };

    /// <summary>
    /// Builds a spawn payload from authoring data. Iterates behaviors and applies first of each type.
    /// </summary>
    public static AttackEntitySpawnPayload Build(AttackEntityData data)
    {
        var payload = new AttackEntitySpawnPayload
        {
            physicalDamage = data.physicalDamage,
            coldDamage = data.coldDamage,
            fireDamage = data.fireDamage,
            lightningDamage = data.lightningDamage,
            velocity = new float2(data.entityVelocity.x, data.entityVelocity.y),
            hitBoxData = data.hitBoxData,
            pierce = DefaultPierce(),
            expiration = DefaultExpiration(),
            chain = DefaultChain(),
            rehit = DefaultRehit()
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
        }

        payload.rehit.rehitCooldownSeconds = data.rehitCooldownSeconds;

        return payload;
    }
}
