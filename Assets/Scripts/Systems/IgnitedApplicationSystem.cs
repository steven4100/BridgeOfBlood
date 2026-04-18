using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct IgnitedTrackAndApplyJob : IJob
{
    [ReadOnly] public NativeArray<DamageEvent> HitEvents;
    [ReadOnly] public NativeArray<IgnitedApplierRuntime> Appliers;
    public NativeArray<Enemy> Enemies;
    public NativeList<EnemyIgniteStatus> Tracker;
    public NativeList<StatusAilmentAppliedEvent> AilmentEvents;
    public float TimeApplied;
    public float TrackedLifetime;
    public uint Seed;

    public void Execute()
    {
        for (int i = 0; i < HitEvents.Length; i++)
        {
            DamageEvent hit = HitEvents[i];
            IgnitedApplierRuntime applier = Appliers[hit.attackEntityIndex];
            if (!applier.isActive)
                continue;

            bool proc = applier.applyChance >= 1f;
            if (!proc)
            {
                var rng = Unity.Mathematics.Random.CreateFromIndex(Seed + (uint)i);
                proc = rng.NextFloat() < applier.applyChance;
            }

            if (!proc)
                continue;

            Enemy enemy = Enemies[hit.enemyIndex];
            bool alreadyHad = (enemy.statusAilmentFlag & StatusAilmentFlag.Ignited) != 0;

            const float dotFrac = 0.2f;
            const float neverTicked = -100000f;
            float damagePerTick = hit.damageDealt * dotFrac;
            Tracker.Add(new EnemyIgniteStatus
            {
                entityID = enemy.entityId,
                spellId = hit.spellId,
                spellInvocationId = hit.spellInvocationId,
                timeApplied = TimeApplied,
                lifetime = TrackedLifetime,
                damagerPerTick = damagePerTick,
                lastTimeTicked = neverTicked
            });

            enemy.statusAilmentFlag |= StatusAilmentFlag.Ignited;
            Enemies[hit.enemyIndex] = enemy;

            if (!alreadyHad)
            {
                AilmentEvents.Add(new StatusAilmentAppliedEvent
                {
                    spellId = hit.spellId,
                    spellInvocationId = hit.spellInvocationId,
                    enemyIndex = hit.enemyIndex,
                    ailmentFlag = StatusAilmentFlag.Ignited
                });
            }
        }
    }
}

public class IgnitedApplicationSystem
{
    private const float DefaultTrackedLifetime = 4f;

    public JobHandle ScheduleTrack(
        NativeArray<DamageEvent> damageEvents,
        NativeArray<IgnitedApplierRuntime> appliers,
        NativeArray<Enemy> enemies,
        NativeList<EnemyIgniteStatus> tracker,
        NativeList<StatusAilmentAppliedEvent> ailmentEvents,
        float timeApplied,
        uint seed,
        JobHandle dependsOn = default)
    {
        return new IgnitedTrackAndApplyJob
        {
            HitEvents = damageEvents,
            Appliers = appliers,
            Enemies = enemies,
            Tracker = tracker,
            AilmentEvents = ailmentEvents,
            TimeApplied = timeApplied,
            TrackedLifetime = DefaultTrackedLifetime,
            Seed = seed
        }.Schedule(dependsOn);
    }
}
