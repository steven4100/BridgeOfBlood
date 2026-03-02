using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;

/// <summary>
/// Consumes HitEvents (from HitResolver + ChainSystem), applies damage to enemies, increments enemiesHit,
/// and emits EnemyHitEvent / EnemyKilledEvent. No collision or chain logic—pure application of hit results.
/// Assumes hit indices are valid; caller (e.g. AttackEntityManager.ValidateHitEvents) must validate upstream.
/// </summary>
public class DamageSystem
{
    public const float WeaknessMultiplier = 1.5f;

    /// <summary>
    /// For each HitEvent, applies the attack entity's damage to the enemy, updates health and enemiesHit, emits telemetry.
    /// </summary>
    public void ProcessHits(
        NativeList<HitEvent> hitEvents,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies,
        NativeList<EnemyHitEvent> outHitEvents,
        NativeList<EnemyKilledEvent> outKillEvents)
    {
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
