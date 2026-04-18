using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Discrete DoT ticks at per-ailment intervals; tracker <c>damagerPerTick</c> is damage per tick event.
/// Emits thin tick signals, resolves into <see cref="TickDamageEvent"/>, applies health in resolution order (ignite → poison → bleed signals).
/// </summary>
public static class TickDamagePipeline
{
    public const float DotDamageFractionOfHit = 0.2f;

    /// <summary>Fresh ailment rows use this so the first simulation frame always satisfies the tick interval check.</summary>
    public const float NeverTickedSentinel = -100000f;

    public const float IgniteTickIntervalSeconds = 0.6f;
    public const float PoisonTickIntervalSeconds = 0.4f;
    public const float BleedTickIntervalSeconds = 0.2f;

    public static void EmitTimeBasedIgniteSignals(
        NativeList<EnemyIgniteStatus> ignite,
        NativeList<IgniteTickSignal> outSignals,
        float simulationTime)
    {
        for (int i = 0; i < ignite.Length; i++)
        {
            EnemyIgniteStatus row = ignite[i];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;
            if (simulationTime - row.lastTimeTicked < IgniteTickIntervalSeconds)
                continue;
            outSignals.Add(new IgniteTickSignal { igniteListIndex = i });
        }
    }

    public static void EmitTimeBasedPoisonSignals(
        NativeList<EnemyPoisonStatus> poison,
        NativeList<PoisonTickSignal> outSignals,
        float simulationTime)
    {
        for (int i = 0; i < poison.Length; i++)
        {
            EnemyPoisonStatus row = poison[i];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;
            if (simulationTime - row.lastTimeTicked < PoisonTickIntervalSeconds)
                continue;
            outSignals.Add(new PoisonTickSignal { poisonListIndex = i });
        }
    }

    public static void EmitTimeBasedBleedSignals(
        NativeList<EnemyBleedStatus> bleed,
        NativeList<BleedTickSignal> outSignals,
        float simulationTime)
    {
        for (int i = 0; i < bleed.Length; i++)
        {
            EnemyBleedStatus row = bleed[i];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;
            if (simulationTime - row.lastTimeTicked < BleedTickIntervalSeconds)
                continue;
            outSignals.Add(new BleedTickSignal { bleedListIndex = i });
        }
    }

    struct PendingTick
    {
        public int enemyIndex;
        public int igniteListIndex;
        public int poisonListIndex;
        public int bleedListIndex;
        public float damage;
        public TickDamageSource source;
        public int spellId;
        public int spellInvocationId;
        public float2 position;
    }

    /// <summary>
    /// Resolves signals into pending damage and applies sequentially to HP in list order (ignite rows, then poison, then bleed).
    /// Updates <c>lastTimeTicked</c> on tracker rows to <paramref name="simulationTime"/> after each applied tick.
    /// </summary>
    /// <param name="entityIdToEnemyIndex"><see cref="Enemy.entityId"/> → index into <paramref name="enemies"/>; caller builds once per frame.</param>
    public static void ResolveApplyAndAppend(
        NativeList<IgniteTickSignal> igniteSignals,
        NativeList<PoisonTickSignal> poisonSignals,
        NativeList<BleedTickSignal> bleedSignals,
        NativeList<EnemyIgniteStatus> ignite,
        NativeList<EnemyPoisonStatus> poison,
        NativeList<EnemyBleedStatus> bleed,
        NativeArray<Enemy> enemies,
        NativeHashMap<int, int> entityIdToEnemyIndex,
        float deltaTime,
        float simulationTime,
        NativeList<TickDamageEvent> outEvents)
    {
        if (deltaTime <= 0f)
        {
            igniteSignals.Clear();
            poisonSignals.Clear();
            bleedSignals.Clear();
            return;
        }

        var pending = new NativeList<PendingTick>(igniteSignals.Length + poisonSignals.Length + bleedSignals.Length, Allocator.Temp);

        for (int i = 0; i < igniteSignals.Length; i++)
        {
            int idx = igniteSignals[i].igniteListIndex;
            if (idx < 0 || idx >= ignite.Length)
                continue;
            EnemyIgniteStatus row = ignite[idx];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;

            if (!entityIdToEnemyIndex.TryGetValue(row.entityID, out int enemyIndex))
                continue;

            float fire = row.damagerPerTick;
            if (enemyIndex < enemies.Length && enemies[enemyIndex].elementalWeakness == DamageType.Fire)
                fire *= DamageSystem.WeaknessMultiplier;

            if (fire <= 0f)
                continue;

            pending.Add(new PendingTick
            {
                enemyIndex = enemyIndex,
                igniteListIndex = idx,
                poisonListIndex = -1,
                bleedListIndex = -1,
                damage = fire,
                source = TickDamageSource.Fire,
                spellId = row.spellId,
                spellInvocationId = row.spellInvocationId,
                position = enemies[enemyIndex].position
            });
        }

        for (int i = 0; i < poisonSignals.Length; i++)
        {
            int idx = poisonSignals[i].poisonListIndex;
            if (idx < 0 || idx >= poison.Length)
                continue;
            EnemyPoisonStatus row = poison[idx];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;

            if (!entityIdToEnemyIndex.TryGetValue(row.entityID, out int enemyIndex))
                continue;

            float phys = row.damagerPerTick;
            if (enemyIndex < enemies.Length && enemies[enemyIndex].elementalWeakness == DamageType.Physical)
                phys *= DamageSystem.WeaknessMultiplier;

            if (phys <= 0f)
                continue;

            pending.Add(new PendingTick
            {
                enemyIndex = enemyIndex,
                igniteListIndex = -1,
                poisonListIndex = idx,
                bleedListIndex = -1,
                damage = phys,
                source = TickDamageSource.Poison,
                spellId = row.spellId,
                spellInvocationId = row.spellInvocationId,
                position = enemies[enemyIndex].position
            });
        }

        for (int i = 0; i < bleedSignals.Length; i++)
        {
            int idx = bleedSignals[i].bleedListIndex;
            if (idx < 0 || idx >= bleed.Length)
                continue;
            EnemyBleedStatus row = bleed[idx];
            if (row.lifetime <= 0f || row.damagerPerTick <= 0f)
                continue;

            if (!entityIdToEnemyIndex.TryGetValue(row.entityID, out int enemyIndex))
                continue;

            float dmg = row.damagerPerTick;
            if (enemyIndex < enemies.Length && enemies[enemyIndex].elementalWeakness == DamageType.Physical)
                dmg *= DamageSystem.WeaknessMultiplier;

            if (dmg <= 0f)
                continue;

            pending.Add(new PendingTick
            {
                enemyIndex = enemyIndex,
                igniteListIndex = -1,
                poisonListIndex = -1,
                bleedListIndex = idx,
                damage = dmg,
                source = TickDamageSource.Bleed,
                spellId = row.spellId,
                spellInvocationId = row.spellInvocationId,
                position = enemies[enemyIndex].position
            });
        }

        igniteSignals.Clear();
        poisonSignals.Clear();
        bleedSignals.Clear();

        if (pending.Length == 0)
        {
            pending.Dispose();
            return;
        }

        for (int p = 0; p < pending.Length; p++)
        {
            PendingTick t = pending[p];
            if (t.enemyIndex < 0 || t.enemyIndex >= enemies.Length)
                continue;

            Enemy e = enemies[t.enemyIndex];
            float healthBefore = e.health;
            float d = t.damage;
            e.health -= d;
            bool killed = healthBefore > 0f && e.health <= 0f;
            float overkill = killed ? -e.health : 0f;

            float phys = 0f, fireD = 0f, cold = 0f, light = 0f;
            if (t.source == TickDamageSource.Fire)
                fireD = d;
            else
                phys = d;

            outEvents.Add(new TickDamageEvent
            {
                position = t.position,
                damageDealt = d,
                enemyIndex = t.enemyIndex,
                spellId = t.spellId,
                spellInvocationId = t.spellInvocationId,
                wasKill = killed,
                overkillDamage = overkill,
                bloodExtracted = d + overkill,
                physicalDamage = phys,
                fireDamage = fireD,
                coldDamage = cold,
                lightningDamage = light,
                source = t.source
            });

            enemies[t.enemyIndex] = e;

            if (t.igniteListIndex >= 0)
            {
                EnemyIgniteStatus row = ignite[t.igniteListIndex];
                row.lastTimeTicked = simulationTime;
                ignite[t.igniteListIndex] = row;
            }
            else if (t.poisonListIndex >= 0)
            {
                EnemyPoisonStatus row = poison[t.poisonListIndex];
                row.lastTimeTicked = simulationTime;
                poison[t.poisonListIndex] = row;
            }
            else if (t.bleedListIndex >= 0)
            {
                EnemyBleedStatus row = bleed[t.bleedListIndex];
                row.lastTimeTicked = simulationTime;
                bleed[t.bleedListIndex] = row;
            }
        }

        pending.Dispose();
    }
}
