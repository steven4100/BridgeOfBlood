using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

/// <summary>
/// Consumes HitEvents (from HitResolver + ChainSystem), applies damage to enemies, increments enemiesHit,
/// and emits EnemyHitEvent / EnemyKilledEvent. Crit is rolled per hit: if roll < critChance, damage is multiplied by critDamageMultiplier.
/// Hits where the target already has no HP remaining are ignored (no damage, events, or enemiesHit).
/// Assumes hit indices are valid; caller (e.g. AttackEntityManager.ValidateHitEvents) must validate upstream.
/// </summary>
public class DamageSystem
{
    public const float WeaknessMultiplier = 1.5f;

    static readonly float KnockbackDirectionEpsilonSq = 1e-12f;

    public void ProcessHits(
        NativeArray<HitEvent>.ReadOnly hitEvents,
        NativeArray<AttackEntity> attackEntities,
        EnemyBuffers enemies,
        NativeList<EnemyHitEvent> outHitEvents,
        NativeList<EnemyKilledEvent> outKillEvents,
        NativeList<DamageEvent> outDamageEvents = default,
        NativeHashMap<int, float> shockDamageTakenMultiplierByEntityId = default,
        IReadOnlyDictionary<int, List<AttackEntityModifier>> hitModifierSets = null)
    {
        bool emitDamageEvents = outDamageEvents.IsCreated;
        bool useShock = shockDamageTakenMultiplierByEntityId.IsCreated;
        bool useHitModifiers = hitModifierSets != null && hitModifierSets.Count > 0;

        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];

            int ei = hit.enemyIndex;
            if (!enemies.IsValid(hit.enemyEntityId))
                continue;

            EnemyVitality vit = enemies.Vitality[ei];
            if (vit.health <= 0f)
                continue;

            AttackEntity atk = attackEntities[hit.attackEntityIndex];
            EntityId entityId = hit.enemyEntityId;
            EnemyCombatTraits traits = enemies.CombatTraits[ei];
            StatusAilmentFlag status = enemies.Status[ei];
            EnemyMotion motion = enemies.Motion[ei];
            EnemyPresentation presentation = enemies.Presentation[ei];

            // Hit-conditional modifiers operate on scratch values only; atk is written back below (enemiesHit++)
            // and must keep its rolled damage so re-hits and other targets are unaffected.
            float physBase = atk.physicalDamage;
            float coldBase = atk.coldDamage;
            float fireBase = atk.fireDamage;
            float lightningBase = atk.lightningDamage;
            float critChance = atk.critChance;
            float critMult = atk.critDamageMultiplier;

            if (useHitModifiers && hitModifierSets.TryGetValue(atk.entityId, out List<AttackEntityModifier> hitMods))
            {
                HitConditionalEvaluationSystem.ApplyMatching(
                    ei, enemies, hitMods,
                    ref physBase, ref coldBase, ref fireBase, ref lightningBase, ref critChance, ref critMult);
            }

            float physical = ApplyDamageType(physBase, DamageType.Physical, entityId, traits.elementalWeakness, outHitEvents);
            float cold = ApplyDamageType(coldBase, DamageType.Cold, entityId, traits.elementalWeakness, outHitEvents);
            float fire = ApplyDamageType(fireBase, DamageType.Fire, entityId, traits.elementalWeakness, outHitEvents);
            float lightning = ApplyDamageType(lightningBase, DamageType.Lightning, entityId, traits.elementalWeakness, outHitEvents);

            bool isCrit = critChance > 0f && critMult >= 1f && UnityEngine.Random.value < critChance;
            if (isCrit)
            {
                float m = critMult;
                physical *= m;
                cold *= m;
                fire *= m;
                lightning *= m;
            }

            if (useShock && shockDamageTakenMultiplierByEntityId.TryGetValue(entityId.Index, out float shockMult))
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

            if (totalDamage > 0f && atk.knockbackStrength > 0f)
            {
                float2 delta = motion.position - atk.position;
                float2 dir;
                if (math.lengthsq(delta) > KnockbackDirectionEpsilonSq)
                    dir = math.normalize(delta);
                else if (math.lengthsq(atk.velocity) > KnockbackDirectionEpsilonSq)
                    dir = math.normalize(-atk.velocity);
                else
                    dir = new float2(-1f, 0f);

                motion.knockbackVelocity += dir * atk.knockbackStrength;
                enemies.Motion[ei] = motion;
            }

            if (emitDamageEvents && totalDamage > 0f)
            {
                outDamageEvents.Add(new DamageEvent
                {
                    position = hit.hitPosition,
                    damageDealt = totalDamage,
                    enemyIndex = hit.enemyIndex,
                    enemyEntityId = entityId,
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
                    onDamageSound = atk.onDamageSound,
                    onHitEffectForVfx = atk.onHitEffect,
                    onKillEffectForVfx = atk.onKillEffect
                });
            }

            if (killed)
            {
                outKillEvents.Add(new EnemyKilledEvent
                {
                    enemyEntityId = entityId,
                    spellId = atk.spellId,
                    spellInvocationId = atk.spellInvocationId,
                    position = motion.position,
                    killingBlowDamage = totalDamage,
                    overkillDamage = overkill,
                    onDeathSound = presentation.onDeathSound,
                    finalStatusAilments = status,
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
        EntityId enemyEntityId,
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
