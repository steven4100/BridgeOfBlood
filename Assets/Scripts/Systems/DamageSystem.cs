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

    /// <summary>
    /// For each HitEvent, applies the attack entity's damage to the enemy (with weakness and crit), updates health and enemiesHit, emits telemetry.
    /// When outDamageEvents is created, appends one DamageEvent per hit for the text/damage-number system.
    /// </summary>
    public void ProcessHits(
        NativeList<HitEvent> hitEvents,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies,
        NativeList<EnemyHitEvent> outHitEvents,
        NativeList<EnemyKilledEvent> outKillEvents,
        NativeList<DamageEvent> outDamageEvents = default)
    {
        bool emitDamageEvents = outDamageEvents.IsCreated;

        for (int i = 0; i < hitEvents.Length; i++)
        {
            HitEvent hit = hitEvents[i];

            AttackEntity atk = attackEntities[hit.attackEntityIndex];
            Enemy enemy = enemies[hit.enemyIndex];

            float totalDamage = 0f;
            totalDamage += ApplyDamageType(atk.physicalDamage, DamageType.Physical, enemy, outHitEvents);
            totalDamage += ApplyDamageType(atk.coldDamage, DamageType.Cold, enemy, outHitEvents);
            totalDamage += ApplyDamageType(atk.fireDamage, DamageType.Fire, enemy, outHitEvents);
            totalDamage += ApplyDamageType(atk.lightningDamage, DamageType.Lightning, enemy, outHitEvents);

            bool isCrit = atk.critChance > 0f && atk.critDamageMultiplier >= 1f && Random.value < atk.critChance;
            if (isCrit)
                totalDamage *= atk.critDamageMultiplier;

            if (emitDamageEvents && totalDamage > 0f)
            {
                outDamageEvents.Add(new DamageEvent
                {
                    position = hit.hitPosition,
                    damageDealt = totalDamage,
                    enemyIndex = hit.enemyIndex,
                    isCrit = isCrit
                });
            }

            enemy.health -= totalDamage;

            if (enemy.health <= 0f)
            {
                outKillEvents.Add(new EnemyKilledEvent
                {
                    enemyEntityId = enemy.entityId,
                    overkillDamage = -enemy.health,
                    corruptionFlag = enemy.corruptionFlag,
                    finalStatusAilments = enemy.statusAilmentFlag
                });
            }

            enemies[hit.enemyIndex] = enemy;

            atk.enemiesHit++;
            attackEntities[hit.attackEntityIndex] = atk;
        }
    }

    static float ApplyDamageType(
        float baseDamage,
        DamageType type,
        Enemy enemy,
        NativeList<EnemyHitEvent> hitEvents)
    {
        if (baseDamage <= 0f) return 0f;

        float amount = baseDamage;
        if (type == enemy.elementalWeakness)
            amount *= WeaknessMultiplier;

        hitEvents.Add(new EnemyHitEvent
        {
            enemyEntityId = enemy.entityId,
            damageDealt = amount,
            damageType = type
        });

        return amount;
    }
}
