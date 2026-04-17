using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct StunnedTrackAndApplyJob : IJob
{
    [ReadOnly] public NativeArray<DamageEvent> HitEvents;
    [ReadOnly] public NativeArray<StunnedApplierRuntime> Appliers;
    public NativeArray<Enemy> Enemies;
    public NativeList<EnemyStunnedStatus> Tracker;
    public NativeList<StatusAilmentAppliedEvent> AilmentEvents;
    public float TimeApplied;
    public float TrackedLifetime;
    public uint Seed;

    public void Execute()
    {
        for (int i = 0; i < HitEvents.Length; i++)
        {
            DamageEvent hit = HitEvents[i];
            StunnedApplierRuntime applier = Appliers[hit.attackEntityIndex];
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
            bool alreadyHad = (enemy.statusAilmentFlag & StatusAilmentFlag.Stunned) != 0;

            Tracker.Add(new EnemyStunnedStatus
            {
                entityID = enemy.entityId,
                spellId = hit.spellId,
                spellInvocationId = hit.spellInvocationId,
                timeApplied = TimeApplied,
                lifetime = TrackedLifetime
            });

            enemy.statusAilmentFlag |= StatusAilmentFlag.Stunned;
            Enemies[hit.enemyIndex] = enemy;

            if (!alreadyHad)
            {
                AilmentEvents.Add(new StatusAilmentAppliedEvent
                {
                    spellId = hit.spellId,
                    spellInvocationId = hit.spellInvocationId,
                    enemyIndex = hit.enemyIndex,
                    ailmentFlag = StatusAilmentFlag.Stunned
                });
            }
        }
    }
}

public class StunnedApplicationSystem
{
    private const float DefaultTrackedLifetime = 4f;

    public JobHandle ScheduleTrack(
        NativeArray<DamageEvent> damageEvents,
        NativeArray<StunnedApplierRuntime> appliers,
        NativeArray<Enemy> enemies,
        NativeList<EnemyStunnedStatus> tracker,
        NativeList<StatusAilmentAppliedEvent> ailmentEvents,
        float timeApplied,
        uint seed,
        JobHandle dependsOn = default)
    {
        return new StunnedTrackAndApplyJob
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
