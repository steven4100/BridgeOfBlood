using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Resolves collisions into hit information. Pierce policy is applied here so the pipeline is
/// Detect → Resolve → Chain → Damage with no separate pierce step.
/// No side effects—no damage, no telemetry, no enemiesHit update.
/// Outputs HitEvents (ids, contact, and attack base stats) for later pipeline steps.
/// Assumes collision entity ids are valid; caller (e.g. AttackEntityManager / pipeline owner) must validate upstream.
/// </summary>
public class HitResolver
{
    /// <summary>
    /// Converts each collision into one HitEvent when allowed by pierce and rehit cooldown. Clears and fills the hitEvents list.
    /// Only collisions that pass the per-entity pierce limit and are not in rehit cooldown produce hits.
    /// If piercePolicies is empty, pierce is skipped. If rehitCooldownSeconds &lt;= 0, rehit is skipped.
    /// Does not modify enemies or attack entities; may prune rehitPolicies.
    /// </summary>
    public void Resolve(
        NativeArray<CollisionEvent>.ReadOnly collisions,
        NativeArray<AttackEntity>.ReadOnly attackEntities,
        NativeArray<PhysicalDamageRuntime>.ReadOnly physicalDamages,
        NativeArray<ColdDamageRuntime>.ReadOnly coldDamages,
        NativeArray<FireDamageRuntime>.ReadOnly fireDamages,
        NativeArray<LightningDamageRuntime>.ReadOnly lightningDamages,
        NativeArray<CritRuntime>.ReadOnly crits,
        NativeArray<PiercePolicyRuntime>.ReadOnly piercePolicies,
        NativeArray<RehitPolicyRuntime> rehitPolicies,
        NativeList<HitEvent> hitEvents)
    {
        hitEvents.Clear();
        if (collisions.Length == 0) return;

        bool applyPierce = piercePolicies.Length > 0 && attackEntities.Length > 0;
        bool applyRehit = rehitPolicies.Length == attackEntities.Length;
        NativeArray<int> countPerEntity = default;
        if (applyPierce)
        {
            countPerEntity = new NativeArray<int>(attackEntities.Length, Allocator.Temp);
        }

        try
        {
            for (int c = 0; c < collisions.Length; c++)
            {
                CollisionEvent col = collisions[c];
                int ai = col.attackEntityId.Index;
                AttackEntity atk = attackEntities[ai];

                if (applyPierce && countPerEntity.IsCreated)
                {
                    PiercePolicyRuntime pierce = piercePolicies[ai];
                    int hitsRemaining = int.MaxValue;
                    if (pierce.isActive && pierce.maxEnemiesHit > 0)
                        hitsRemaining = pierce.maxEnemiesHit - atk.enemiesHit;
                    if (hitsRemaining <= 0) continue;
                    if (countPerEntity[ai] >= hitsRemaining) continue;
                    countPerEntity[ai]++;
                }

                if (applyRehit)
                {
                    RehitPolicyRuntime rehit = rehitPolicies[ai];
                    if (rehit.rehitCooldownSeconds > 0f)
                    {
                        PruneExpiredRehitEntries(ref rehit, atk.timeAlive);
                        rehitPolicies[ai] = rehit;
                        if (IsEnemyInRehitCooldown(rehit, col.enemyEntityId, atk.timeAlive))
                            continue;
                    }
                }

                CritRuntime crit = crits[ai];
                hitEvents.Add(new HitEvent
                {
                    attackEntityId = col.attackEntityId,
                    enemyEntityId = col.enemyEntityId,
                    hitPosition = col.enemyPosition,
                    physicalDamage = physicalDamages[ai].amount,
                    coldDamage = coldDamages[ai].amount,
                    fireDamage = fireDamages[ai].amount,
                    lightningDamage = lightningDamages[ai].amount,
                    critIsActive = crit.isActive,
                    critChance = crit.chance,
                    critMultiplier = crit.multiplier,
                    spellId = atk.spellId,
                    spellInvocationId = atk.spellInvocationId,
                    onDamageSound = atk.onDamageSound,
                    onHitEffect = atk.onHitEffect,
                    onKillEffect = atk.onKillEffect
                });
            }
        }
        finally
        {
            if (countPerEntity.IsCreated)
                countPerEntity.Dispose();
        }
    }

    static void PruneExpiredRehitEntries(ref RehitPolicyRuntime rehit, float timeAlive)
    {
        float cooldown = rehit.rehitCooldownSeconds;
        for (int i = rehit.recentHits.Length - 1; i >= 0; i--)
        {
            if (timeAlive - rehit.recentHits[i].hitTimeAlive >= cooldown)
                rehit.recentHits.RemoveAt(i);
        }
    }

    static bool IsEnemyInRehitCooldown(RehitPolicyRuntime rehit, EntityId enemyEntityId, float timeAlive)
    {
        float cooldown = rehit.rehitCooldownSeconds;
        for (int i = 0; i < rehit.recentHits.Length; i++)
        {
            if (rehit.recentHits[i].enemyId == enemyEntityId)
                return (timeAlive - rehit.recentHits[i].hitTimeAlive) < cooldown;
        }
        return false;
    }
}
