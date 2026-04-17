using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct ShockedTrackAndApplyJob : IJob
{
    [ReadOnly] public NativeArray<DamageEvent> HitEvents;
    [ReadOnly] public NativeArray<ShockedApplierRuntime> Appliers;
    public NativeArray<Enemy> Enemies;
    public NativeList<EnemyShockedStatus> Tracker;
    public NativeList<StatusAilmentAppliedEvent> AilmentEvents;
    public float TimeApplied;
    public float TrackedLifetime;
    public float DamageMultiplier;
    public uint Seed;

    public void Execute()
    {
        for (int i = 0; i < HitEvents.Length; i++)
        {
            DamageEvent hit = HitEvents[i];
            ShockedApplierRuntime applier = Appliers[hit.attackEntityIndex];
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
            bool alreadyHad = (enemy.statusAilmentFlag & StatusAilmentFlag.Shocked) != 0;

            Tracker.Add(new EnemyShockedStatus
            {
                entityID = enemy.entityId,
                spellId = hit.spellId,
                spellInvocationId = hit.spellInvocationId,
                timeApplied = TimeApplied,
                lifetime = TrackedLifetime,
                damagerMultiplier = DamageMultiplier
            });

            enemy.statusAilmentFlag |= StatusAilmentFlag.Shocked;
            Enemies[hit.enemyIndex] = enemy;

            if (!alreadyHad)
            {
                AilmentEvents.Add(new StatusAilmentAppliedEvent
                {
                    spellId = hit.spellId,
                    spellInvocationId = hit.spellInvocationId,
                    enemyIndex = hit.enemyIndex,
                    ailmentFlag = StatusAilmentFlag.Shocked
                });
            }
        }
    }
}

public class ShockedApplicationSystem
{
    private const float DefaultTrackedLifetime = 4f;

    public JobHandle ScheduleTrack(
        NativeArray<DamageEvent> damageEvents,
        NativeArray<ShockedApplierRuntime> appliers,
        NativeArray<Enemy> enemies,
        NativeList<EnemyShockedStatus> tracker,
        NativeList<StatusAilmentAppliedEvent> ailmentEvents,
        float timeApplied,
        uint seed,
        JobHandle dependsOn = default)
    {
        return new ShockedTrackAndApplyJob
        {
            HitEvents = damageEvents,
            Appliers = appliers,
            Enemies = enemies,
            Tracker = tracker,
            AilmentEvents = ailmentEvents,
            TimeApplied = timeApplied,
            TrackedLifetime = DefaultTrackedLifetime,
            DamageMultiplier = 1f,
            Seed = seed
        }.Schedule(dependsOn);
    }
}
