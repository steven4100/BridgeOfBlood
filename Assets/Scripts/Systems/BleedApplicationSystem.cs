using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct BleedTrackAndApplyJob : IJob
{
    [ReadOnly] public NativeArray<DamageEvent> HitEvents;
    [ReadOnly] public NativeArray<BleedApplierRuntime> Appliers;
    public NativeArray<int> EntityIds;
    public NativeArray<StatusAilmentFlag> Status;
    public NativeList<EnemyBleedStatus> Tracker;
    public NativeList<StatusAilmentAppliedEvent> AilmentEvents;
    public float TimeApplied;
    public float TrackedLifetime;
    public uint Seed;

    public void Execute()
    {
        for (int i = 0; i < HitEvents.Length; i++)
        {
            DamageEvent hit = HitEvents[i];
            BleedApplierRuntime applier = Appliers[hit.attackEntityIndex];
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
            bool alreadyHad = (flags & StatusAilmentFlag.Bleeding) != 0;

            const float frac = 0.2f;
            float damagePerTick = hit.damageDealt * frac;

            Tracker.Add(new EnemyBleedStatus
            {
                entityID = entityId,
                spellId = hit.spellId,
                spellInvocationId = hit.spellInvocationId,
                timeApplied = TimeApplied,
                lifetime = TrackedLifetime,
                damagerPerTick = damagePerTick,
                lastTimeTicked = TickDamagePipeline.NeverTickedSentinel
            });

            flags |= StatusAilmentFlag.Bleeding;
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
                    ailmentFlag = StatusAilmentFlag.Bleeding
                });
            }
        }
    }
}

public class BleedApplicationSystem
{
    private const float DefaultTrackedLifetime = 4f;

    public JobHandle ScheduleTrack(
        NativeArray<DamageEvent> damageEvents,
        NativeArray<BleedApplierRuntime> appliers,
        NativeArray<int> entityIds,
        NativeArray<StatusAilmentFlag> status,
        NativeList<EnemyBleedStatus> tracker,
        NativeList<StatusAilmentAppliedEvent> ailmentEvents,
        float timeApplied,
        uint seed,
        JobHandle dependsOn = default)
    {
        return new BleedTrackAndApplyJob
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
