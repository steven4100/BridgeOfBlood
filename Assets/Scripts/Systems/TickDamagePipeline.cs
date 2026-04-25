using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Discrete DoT ticks at per-ailment intervals; tracker <c>damagerPerTick</c> is damage per tick event.
/// Applies health in resolution order (ignite rows → poison → bleed) and appends <see cref="TickDamageEvent"/>.
/// </summary>
public static class TickDamagePipeline
{
    public const float DotDamageFractionOfHit = 0.2f;
    public const float NeverTickedSentinel = -100000f;
    public const float IgniteTickIntervalSeconds = 0.6f;
    public const float PoisonTickIntervalSeconds = 0.4f;
    public const float BleedTickIntervalSeconds = 0.2f;
    public const float DotFlashDurationSeconds = 0.16f;

    /// <param name="entityIdToEnemyIndex">Entity id → index into <paramref name="enemies"/>; caller builds once per frame.</param>
    public static void ProcessTimeBasedDotTicks(
        NativeList<EnemyIgniteStatus> ignite,
        NativeList<EnemyPoisonStatus> poison,
        NativeList<EnemyBleedStatus> bleed,
        EnemyBuffers enemies,
        NativeHashMap<int, int> entityIdToEnemyIndex,
        float deltaTime,
        float simulationTime,
        NativeList<TickDamageEvent> outEvents)
    {
        if (deltaTime <= 0f)
            return;

        for (int i = 0; i < ignite.Length; i++)
        {
            EnemyIgniteStatus row = ignite[i];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;
            if (simulationTime - row.lastTimeTicked < IgniteTickIntervalSeconds)
                continue;
            if (!entityIdToEnemyIndex.TryGetValue(row.entityID, out int enemyIndex))
                continue;

            float fire = row.damagerPerTick;
            if (enemyIndex < enemies.Length && enemies.CombatTraits[enemyIndex].elementalWeakness == DamageType.Fire)
                fire *= DamageSystem.WeaknessMultiplier;

            if (fire <= 0f)
                continue;

            float2 position = enemies.Motion[enemyIndex].position;
            ApplyDotDamage(
                enemies,
                enemyIndex,
                fire,
                TickDamageSource.Fire,
                row.spellId,
                row.spellInvocationId,
                position,
                outEvents);

            row.lastTimeTicked = simulationTime;
            ignite[i] = row;
        }

        for (int i = 0; i < poison.Length; i++)
        {
            EnemyPoisonStatus row = poison[i];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;
            if (simulationTime - row.lastTimeTicked < PoisonTickIntervalSeconds)
                continue;
            if (!entityIdToEnemyIndex.TryGetValue(row.entityID, out int enemyIndex))
                continue;

            float phys = row.damagerPerTick;
            if (enemyIndex < enemies.Length && enemies.CombatTraits[enemyIndex].elementalWeakness == DamageType.Physical)
                phys *= DamageSystem.WeaknessMultiplier;

            if (phys <= 0f)
                continue;

            float2 position = enemies.Motion[enemyIndex].position;
            ApplyDotDamage(
                enemies,
                enemyIndex,
                phys,
                TickDamageSource.Poison,
                row.spellId,
                row.spellInvocationId,
                position,
                outEvents);

            row.lastTimeTicked = simulationTime;
            poison[i] = row;
        }

        for (int i = 0; i < bleed.Length; i++)
        {
            EnemyBleedStatus row = bleed[i];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;
            if (simulationTime - row.lastTimeTicked < BleedTickIntervalSeconds)
                continue;
            if (!entityIdToEnemyIndex.TryGetValue(row.entityID, out int enemyIndex))
                continue;

            float dmg = row.damagerPerTick;
            if (enemyIndex < enemies.Length && enemies.CombatTraits[enemyIndex].elementalWeakness == DamageType.Physical)
                dmg *= DamageSystem.WeaknessMultiplier;

            if (dmg <= 0f)
                continue;

            float2 position = enemies.Motion[enemyIndex].position;
            ApplyDotDamage(
                enemies,
                enemyIndex,
                dmg,
                TickDamageSource.Bleed,
                row.spellId,
                row.spellInvocationId,
                position,
                outEvents);

            row.lastTimeTicked = simulationTime;
            bleed[i] = row;
        }
    }

    private static void ApplyDotDamage(
        EnemyBuffers enemies,
        int enemyIndex,
        float d,
        TickDamageSource source,
        int spellId,
        int spellInvocationId,
        float2 position,
        NativeList<TickDamageEvent> outEvents)
    {
        if (enemyIndex < 0 || enemyIndex >= enemies.Length)
            return;

        EnemyVitality vit = enemies.Vitality[enemyIndex];
        float healthBefore = vit.health;
        vit.health -= d;
        bool killed = healthBefore > 0f && vit.health <= 0f;
        float overkill = killed ? -vit.health : 0f;

        float phys = 0f, fireD = 0f, cold = 0f, light = 0f;
        if (source == TickDamageSource.Fire)
            fireD = d;
        else
            phys = d;

        outEvents.Add(new TickDamageEvent
        {
            position = position,
            damageDealt = d,
            enemyIndex = enemyIndex,
            spellId = spellId,
            spellInvocationId = spellInvocationId,
            wasKill = killed,
            overkillDamage = overkill,
            bloodExtracted = d + overkill,
            physicalDamage = phys,
            fireDamage = fireD,
            coldDamage = cold,
            lightningDamage = light,
            source = source
        });

        EnemyPresentation p = enemies.Presentation[enemyIndex];
        p.ailmentFlashTimer = DotFlashDurationSeconds;
        p.ailmentFlashSource = source;
        enemies.Presentation[enemyIndex] = p;

        enemies.Vitality[enemyIndex] = vit;
    }
}
