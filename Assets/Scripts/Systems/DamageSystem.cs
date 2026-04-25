using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Consumes HitEvents (from HitResolver + ChainSystem), applies damage to enemies, increments enemiesHit,
/// and emits EnemyHitEvent / EnemyKilledEvent. Crit is rolled per hit: if roll < critChance, damage is multiplied by critDamageMultiplier.
/// Assumes hit indices are valid; caller (e.g. AttackEntityManager.ValidateHitEvents) must validate upstream.
/// </summary>
public class DamageSystem
{
    public const float WeaknessMultiplier = 1.5f;

    public void ProcessHits(
        NativeArray<HitEvent>.ReadOnly hitEvents,
        NativeArray<AttackEntity> attackEntities,
        EnemyBuffers enemies,
        NativeList<EnemyHitEvent> outHitEvents,
        NativeList<EnemyKilledEvent> outKillEvents,
        NativeList<DamageEvent> outDamageEvents = default,
        NativeHashMap<int, float> shockDamageTakenMultiplierByEntityId = default)
    {
        bool emitDamageEvents = outDamageEvents.IsCreated;
        bool useShock = shockDamageTakenMultiplierByEntityId.IsCreated;

        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];

            AttackEntity atk = attackEntities[hit.attackEntityIndex];
            int ei = hit.enemyIndex;
            int entityId = enemies.EntityIds[ei];
            EnemyCombatTraits traits = enemies.CombatTraits[ei];
            EnemyVitality vit = enemies.Vitality[ei];
            StatusAilmentFlag status = enemies.Status[ei];

            float physical = ApplyDamageType(atk.physicalDamage, DamageType.Physical, entityId, traits.elementalWeakness, outHitEvents);
            float cold = ApplyDamageType(atk.coldDamage, DamageType.Cold, entityId, traits.elementalWeakness, outHitEvents);
            float fire = ApplyDamageType(atk.fireDamage, DamageType.Fire, entityId, traits.elementalWeakness, outHitEvents);
            float lightning = ApplyDamageType(atk.lightningDamage, DamageType.Lightning, entityId, traits.elementalWeakness, outHitEvents);

            bool isCrit = atk.critChance > 0f && atk.critDamageMultiplier >= 1f && Random.value < atk.critChance;
            if (isCrit)
            {
                float m = atk.critDamageMultiplier;
                physical *= m;
                cold *= m;
                fire *= m;
                lightning *= m;
            }

            if (useShock && shockDamageTakenMultiplierByEntityId.TryGetValue(entityId, out float shockMult))
            {
                physical *= shockMult;
                cold *= shockMult;
                fire *= shockMult;
                lightning *= shockMult;
            }

            float totalDamage = physical + cold + fire + lightning;
            float healthBefore = vit.health;
            vit.health -= totalDamage;
            bool killed = healthBefore > 0f && vit.health <= 0f;
            float overkill = killed ? -vit.health : 0f;

            if (emitDamageEvents && totalDamage > 0f)
            {
                outDamageEvents.Add(new DamageEvent
                {
                    position = hit.hitPosition,
                    damageDealt = totalDamage,
                    enemyIndex = hit.enemyIndex,
                    attackEntityIndex = hit.attackEntityIndex,
                    isCrit = isCrit,
                    physicalDamage = physical,
                    fireDamage = fire,
                    coldDamage = cold,
                    lightningDamage = lightning,
                    spellId = atk.spellId,
                    spellInvocationId = atk.spellInvocationId,
                    wasKill = killed,
                    overkillDamage = overkill,
                    bloodExtracted = totalDamage + overkill,
                    onHitEffectForVfx = atk.onHitEffect,
                    onKillEffectForVfx = atk.onKillEffect
                });
            }

            if (killed)
            {
                outKillEvents.Add(new EnemyKilledEvent
                {
                    enemyEntityId = entityId,
                    overkillDamage = overkill,
                    corruptionFlag = traits.corruptionFlag,
                    finalStatusAilments = status
                });
            }

            enemies.Vitality[ei] = vit;

            atk.enemiesHit++;
            attackEntities[hit.attackEntityIndex] = atk;
        }
    }

    static float ApplyDamageType(
        float baseDamage,
        DamageType type,
        int enemyEntityId,
        DamageType elementalWeakness,
        NativeList<EnemyHitEvent> hitEvents)
    {
        if (baseDamage <= 0f) return 0f;

        float amount = baseDamage;
        if (type == elementalWeakness)
            amount *= WeaknessMultiplier;

        hitEvents.Add(new EnemyHitEvent
        {
            enemyEntityId = enemyEntityId,
            damageDealt = amount,
            damageType = type
        });

        return amount;
    }
}
