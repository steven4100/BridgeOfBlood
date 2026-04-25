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
    public NativeArray<int> EntityIds;
    public NativeArray<StatusAilmentFlag> Status;
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

            int entityId = EntityIds[hit.enemyIndex];
            StatusAilmentFlag flags = Status[hit.enemyIndex];
            bool alreadyHad = (flags & StatusAilmentFlag.Stunned) != 0;

            Tracker.Add(new EnemyStunnedStatus
            {
                entityID = entityId,
                spellId = hit.spellId,
                spellInvocationId = hit.spellInvocationId,
                timeApplied = TimeApplied,
                lifetime = TrackedLifetime
            });

            flags |= StatusAilmentFlag.Stunned;
            Status[hit.enemyIndex] = flags;

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
        NativeArray<int> entityIds,
        NativeArray<StatusAilmentFlag> status,
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
