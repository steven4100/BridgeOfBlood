using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct StunnedDecideJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<HitEvent> HitEvents;
    [ReadOnly] public NativeArray<AttackEntity> AttackEntities;
    [ReadOnly] public NativeArray<StunnedApplierRuntime> Appliers;
    [WriteOnly] public NativeArray<StatusAilmentFlag> Decisions;
    public uint Seed;

    public void Execute(int i)
    {
        HitEvent hit = HitEvents[i];
        StunnedApplierRuntime applier = Appliers[hit.attackEntityIndex];
        if (!applier.isActive)
        {
            Decisions[i] = StatusAilmentFlag.None;
            return;
        }

        if (applier.applyChance >= 1f)
        {
            Decisions[i] = StatusAilmentFlag.Stunned;
            return;
        }

        var rng = Unity.Mathematics.Random.CreateFromIndex(Seed + (uint)i);
        Decisions[i] = rng.NextFloat() < applier.applyChance ? StatusAilmentFlag.Stunned : StatusAilmentFlag.None;
    }
}

public class StunnedApplicationSystem
{
    public JobHandle ScheduleDecide(
        NativeArray<HitEvent> hitEvents,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<StunnedApplierRuntime> appliers,
        NativeArray<StatusAilmentFlag> decisions,
        uint seed,
        int batchSize)
    {
        return new StunnedDecideJob
        {
            HitEvents = hitEvents,
            AttackEntities = attackEntities,
            Appliers = appliers,
            Decisions = decisions,
            Seed = seed
        }.Schedule(hitEvents.Length, batchSize);
    }

    public void ApplyDecisions(
        NativeArray<HitEvent> hitEvents,
        NativeArray<StatusAilmentFlag> decisions,
        NativeArray<AttackEntity> attackEntities,
        NativeArray<Enemy> enemies,
        NativeList<StatusAilmentAppliedEvent> ailmentEvents)
    {
        for (int i = 0; i < hitEvents.Length; i++)
        {
            if (decisions[i] == StatusAilmentFlag.None) continue;

            HitEvent hit = hitEvents[i];
            Enemy enemy = enemies[hit.enemyIndex];
            enemy.statusAilmentFlag |= StatusAilmentFlag.Stunned;
            enemies[hit.enemyIndex] = enemy;

            AttackEntity atk = attackEntities[hit.attackEntityIndex];
            ailmentEvents.Add(new StatusAilmentAppliedEvent
            {
                spellId = atk.spellId,
                spellInvocationId = atk.spellInvocationId,
                enemyIndex = hit.enemyIndex,
                ailmentFlag = StatusAilmentFlag.Stunned
            });
        }
    }
}
