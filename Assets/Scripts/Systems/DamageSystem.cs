using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;

/// <summary>
/// Consumes CollisionEvents and applies damage to enemies.
/// Looks up the attack entity's damage list, checks elemental weakness,
/// reduces enemy health, and emits EnemyHitEvent / EnemyKilledEvent telemetry.
/// </summary>
public class DamageSystem
{
    public const float WeaknessMultiplier = 1.5f;

    /// <summary>
    /// Processes collision events, applies damage to enemies, and emits telemetry.
    /// Call after CollisionSystem.Detect and before EnemyManager cleanup.
    /// </summary>
    public void ProcessCollisions(
        NativeList<CollisionEvent> collisions,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies,
        NativeList<EnemyHitEvent> hitEvents,
        NativeList<EnemyKilledEvent> killEvents)
    {
        for (int c = 0; c < collisions.Length; c++)
        {
            CollisionEvent col = collisions[c];

            if (col.attackEntityIndex < 0 || col.attackEntityIndex >= attackEntities.Length) continue;
            if (col.enemyIndex < 0 || col.enemyIndex >= enemies.Length) continue;

            AttackEntity atk = attackEntities[col.attackEntityIndex];
            Enemy enemy = enemies[col.enemyIndex];

            float totalDamage = 0f;

            totalDamage += ApplyDamageType(atk.physicalDamage, DamageType.Physical, enemy, hitEvents);
            totalDamage += ApplyDamageType(atk.coldDamage, DamageType.Cold, enemy, hitEvents);
            totalDamage += ApplyDamageType(atk.fireDamage, DamageType.Fire, enemy, hitEvents);
            totalDamage += ApplyDamageType(atk.lightningDamage, DamageType.Lightning, enemy, hitEvents);

            enemy.health -= totalDamage;

            if (enemy.health <= 0f)
            {
                killEvents.Add(new EnemyKilledEvent
                {
                    enemyEntityId = enemy.entityId,
                    overkillDamage = -enemy.health,
                    corruptionFlag = enemy.corruptionFlag,
                    finalStatusAilments = enemy.statusAilmentFlag
                });
            }

            enemies[col.enemyIndex] = enemy;
        }
    }

    static float ApplyDamageType(
        float baseDamage, DamageType type, Enemy enemy,
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
