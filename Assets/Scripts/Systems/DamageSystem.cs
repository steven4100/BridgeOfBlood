using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

/// <summary>
/// Commits resolved HitEvents to enemies: weakness, crit roll, shock taken, health, enemiesHit,
/// and DamageEvent / EnemyHitEvent / EnemyKilledEvent. Hit modifiers and knockback run in other systems.
/// Hits where the target already has no HP remaining are ignored (no damage, events, or enemiesHit).
/// Map-object hits increment enemiesHit and skip health, damage events, kills, and ailments.
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

            int ei = hit.enemyEntityId.Index;
            int ai = hit.attackEntityId.Index;
            if (!enemies.IsValid(hit.enemyEntityId))
                continue;

            if (enemies.Ids[ei].IsMapObject != 0)
            {
                AttackEntity mapHitAttack = attackEntities[ai];
                mapHitAttack.enemiesHit++;
                attackEntities[ai] = mapHitAttack;
                continue;
            }

            EnemyVitality vit = enemies.Vitality[ei];
            if (vit.health <= 0f)
                continue;

            AttackEntity atk = attackEntities[ai];
            EntityId entityId = hit.enemyEntityId;
            EnemyCombatTraits traits = enemies.CombatTraits[ei];
            StatusAilmentFlag status = enemies.Status[ei];
            EnemyMotion motion = enemies.Motion[ei];
            EnemyPresentation presentation = enemies.Presentation[ei];

            float physical = ApplyDamageType(hit.physicalDamage, DamageType.Physical, entityId, traits.elementalWeakness, outHitEvents);
            float cold = ApplyDamageType(hit.coldDamage, DamageType.Cold, entityId, traits.elementalWeakness, outHitEvents);
            float fire = ApplyDamageType(hit.fireDamage, DamageType.Fire, entityId, traits.elementalWeakness, outHitEvents);
            float lightning = ApplyDamageType(hit.lightningDamage, DamageType.Lightning, entityId, traits.elementalWeakness, outHitEvents);

            bool isCrit = hit.critIsActive && hit.critChance > 0f && hit.critMultiplier >= 1f && UnityEngine.Random.value < hit.critChance;
            if (isCrit)
            {
                float m = hit.critMultiplier;
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

            if (emitDamageEvents && totalDamage > 0f)
            {
                outDamageEvents.Add(new DamageEvent
                {
                    position = hit.hitPosition,
                    damageDealt = totalDamage,
                    enemyEntityId = entityId,
                    attackEntityId = hit.attackEntityId,
                    isCrit = isCrit,
                    physicalDamage = physical,
                    fireDamage = fire,
                    coldDamage = cold,
                    lightningDamage = lightning,
                    spellId = hit.spellId,
                    spellInvocationId = hit.spellInvocationId,
                    wasKill = killed,
                    overkillDamage = overkill,
                    bloodExtracted = totalDamage + overkill,
                    onDamageSound = hit.onDamageSound,
                    onHitEffectForVfx = hit.onHitEffect,
                    onKillEffectForVfx = hit.onKillEffect,
                    enemyStatusAilments = status
                });
            }

            if (killed)
            {
                outKillEvents.Add(new EnemyKilledEvent
                {
                    enemyEntityId = entityId,
                    spellId = hit.spellId,
                    spellInvocationId = hit.spellInvocationId,
                    position = motion.position,
                    killingBlowDamage = totalDamage,
                    overkillDamage = overkill,
                    onDeathSound = presentation.onDeathSound,
                    finalStatusAilments = status,
                });
            }

            enemies.Vitality[ei] = vit;

            atk.enemiesHit++;
            attackEntities[ai] = atk;
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
