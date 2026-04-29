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
    public NativeArray<int> EntityIds;
    public NativeArray<StatusAilmentFlag> Status;
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

            int ei = hit.enemyIndex;
            int entityId = EntityIds[ei];
            StatusAilmentFlag flags = Status[ei];
            bool alreadyHad = (flags & StatusAilmentFlag.Ignited) != 0;

            const float dotFrac = 0.2f;
            const float neverTicked = -100000f;
            float damagePerTick = hit.damageDealt * dotFrac;
            Tracker.Add(new EnemyIgniteStatus
            {
                entityID = entityId,
                spellId = hit.spellId,
                spellInvocationId = hit.spellInvocationId,
                timeApplied = TimeApplied,
                lifetime = TrackedLifetime,
                damagerPerTick = damagePerTick,
                lastTimeTicked = neverTicked
            });

            flags |= StatusAilmentFlag.Ignited;
            Status[ei] = flags;

            if (!alreadyHad)
            {
                AilmentEvents.Add(new StatusAilmentAppliedEvent
                {
                    spellId = hit.spellId,
                    spellInvocationId = hit.spellInvocationId,
                    enemyIndex = ei,
                    enemyEntityId = entityId,
                    position = hit.position,
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
        NativeArray<int> entityIds,
        NativeArray<StatusAilmentFlag> status,
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
            EntityIds = entityIds,
            Status = status,
            Tracker = tracker,
            AilmentEvents = ailmentEvents,
            TimeApplied = timeApplied,
            TrackedLifetime = DefaultTrackedLifetime,
            Seed = seed
        }.Schedule(dependsOn);
    }
}
