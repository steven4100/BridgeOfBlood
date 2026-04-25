using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Decrements <see cref="EnemyIgniteStatus.lifetime"/> (remaining duration) per row; clamps at zero.
/// </summary>
[BurstCompile]
public struct StepEnemyIgniteLifetimeJob : IJobParallelFor
{
    public NativeArray<EnemyIgniteStatus> Items;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyIgniteStatus row = Items[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        Items[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyFrozenLifetimeJob : IJobParallelFor
{
    public NativeArray<EnemyFrozenStatus> Items;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyFrozenStatus row = Items[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        Items[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyStunnedLifetimeJob : IJobParallelFor
{
    public NativeArray<EnemyStunnedStatus> Items;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyStunnedStatus row = Items[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        Items[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyPoisonLifetimeJob : IJobParallelFor
{
    public NativeArray<EnemyPoisonStatus> Items;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyPoisonStatus row = Items[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        Items[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyShockedLifetimeJob : IJobParallelFor
{
    public NativeArray<EnemyShockedStatus> Items;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyShockedStatus row = Items[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        Items[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyBleedLifetimeJob : IJobParallelFor
{
    public NativeArray<EnemyBleedStatus> Items;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyBleedStatus row = Items[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        Items[index] = row;
    }
}

/// <summary>
/// Removes tracker rows whose <c>lifetime &lt;= 0</c> (swap-back compact, order not preserved).
/// </summary>
[BurstCompile]
public struct CompactExpiredAilmentRowsJob : IJob
{
    public NativeList<EnemyBleedStatus> Bleed;
    public NativeList<EnemyFrozenStatus> Frozen;
    public NativeList<EnemyIgniteStatus> Ignite;
    public NativeList<EnemyStunnedStatus> Stunned;
    public NativeList<EnemyPoisonStatus> Poison;
    public NativeList<EnemyShockedStatus> Shocked;

    public void Execute()
    {
        Compact(Bleed);
        Compact(Frozen);
        Compact(Ignite);
        Compact(Stunned);
        Compact(Poison);
        Compact(Shocked);
    }

    private static void Compact(NativeList<EnemyBleedStatus> list)
    {
        int w = 0;
        for (int r = 0; r < list.Length; r++)
        {
            EnemyBleedStatus row = list[r];
            if (row.lifetime > 0f)
            {
                if (w != r)
                    list[w] = row;
                w++;
            }
        }
        list.Resize(w, NativeArrayOptions.UninitializedMemory);
    }

    private static void Compact(NativeList<EnemyFrozenStatus> list)
    {
        int w = 0;
        for (int r = 0; r < list.Length; r++)
        {
            EnemyFrozenStatus row = list[r];
            if (row.lifetime > 0f)
            {
                if (w != r)
                    list[w] = row;
                w++;
            }
        }
        list.Resize(w, NativeArrayOptions.UninitializedMemory);
    }

    private static void Compact(NativeList<EnemyIgniteStatus> list)
    {
        int w = 0;
        for (int r = 0; r < list.Length; r++)
        {
            EnemyIgniteStatus row = list[r];
            if (row.lifetime > 0f)
            {
                if (w != r)
                    list[w] = row;
                w++;
            }
        }
        list.Resize(w, NativeArrayOptions.UninitializedMemory);
    }

    private static void Compact(NativeList<EnemyStunnedStatus> list)
    {
        int w = 0;
        for (int r = 0; r < list.Length; r++)
        {
            EnemyStunnedStatus row = list[r];
            if (row.lifetime > 0f)
            {
                if (w != r)
                    list[w] = row;
                w++;
            }
        }
        list.Resize(w, NativeArrayOptions.UninitializedMemory);
    }

    private static void Compact(NativeList<EnemyPoisonStatus> list)
    {
        int w = 0;
        for (int r = 0; r < list.Length; r++)
        {
            EnemyPoisonStatus row = list[r];
            if (row.lifetime > 0f)
            {
                if (w != r)
                    list[w] = row;
                w++;
            }
        }
        list.Resize(w, NativeArrayOptions.UninitializedMemory);
    }

    private static void Compact(NativeList<EnemyShockedStatus> list)
    {
        int w = 0;
        for (int r = 0; r < list.Length; r++)
        {
            EnemyShockedStatus row = list[r];
            if (row.lifetime > 0f)
            {
                if (w != r)
                    list[w] = row;
                w++;
            }
        }
        list.Resize(w, NativeArrayOptions.UninitializedMemory);
    }
}

/// <summary>
/// Aggregates active status ailments per entity id from all tracker lists (after compact).
/// </summary>
[BurstCompile]
public struct BuildEntityStatusAilmentFlagsMapJob : IJob
{
    [ReadOnly] public NativeList<EnemyFrozenStatus> Frozen;
    [ReadOnly] public NativeList<EnemyIgniteStatus> Ignite;
    [ReadOnly] public NativeList<EnemyStunnedStatus> Stunned;
    [ReadOnly] public NativeList<EnemyPoisonStatus> Poison;
    [ReadOnly] public NativeList<EnemyShockedStatus> Shocked;
    [ReadOnly] public NativeList<EnemyBleedStatus> Bleed;

    public NativeHashMap<int, StatusAilmentFlag> OutMap;

    public void Execute()
    {
        OutMap.Clear();

        for (int i = 0; i < Frozen.Length; i++)
        {
            EnemyFrozenStatus row = Frozen[i];
            if (row.lifetime > 0f)
                OrFlag(row.entityID, StatusAilmentFlag.Frozen);
        }

        for (int i = 0; i < Stunned.Length; i++)
        {
            EnemyStunnedStatus row = Stunned[i];
            if (row.lifetime > 0f)
                OrFlag(row.entityID, StatusAilmentFlag.Stunned);
        }

        for (int i = 0; i < Poison.Length; i++)
        {
            EnemyPoisonStatus row = Poison[i];
            if (row.lifetime > 0f)
                OrFlag(row.entityID, StatusAilmentFlag.Poisoned);
        }

        for (int i = 0; i < Ignite.Length; i++)
        {
            EnemyIgniteStatus row = Ignite[i];
            if (row.lifetime > 0f)
                OrFlag(row.entityID, StatusAilmentFlag.Ignited);
        }

        for (int i = 0; i < Shocked.Length; i++)
        {
            EnemyShockedStatus row = Shocked[i];
            if (row.lifetime > 0f)
                OrFlag(row.entityID, StatusAilmentFlag.Shocked);
        }

        for (int i = 0; i < Bleed.Length; i++)
        {
            EnemyBleedStatus row = Bleed[i];
            if (row.lifetime > 0f)
                OrFlag(row.entityID, StatusAilmentFlag.Bleeding);
        }
    }

    private void OrFlag(int entityId, StatusAilmentFlag bit)
    {
        StatusAilmentFlag acc = StatusAilmentFlag.None;
        OutMap.TryGetValue(entityId, out acc);
        acc |= bit;
        OutMap[entityId] = acc;
    }
}

/// <summary>
/// Writes status ailment bits on enemies from <see cref="BuildEntityStatusAilmentFlagsMapJob"/> output;
/// clears tracked bits when no active tracker row remains.
/// </summary>
[BurstCompile]
public struct ApplyEntityStatusAilmentFlagsJob : IJobParallelFor
{
    public NativeArray<int> EntityIds;
    public NativeArray<StatusAilmentFlag> Status;
    [ReadOnly] public NativeHashMap<int, StatusAilmentFlag> EntityFlags;
    public StatusAilmentFlag TrackedAilmentMask;

    public void Execute(int index)
    {
        int id = EntityIds[index];
        StatusAilmentFlag s = Status[index];
        StatusAilmentFlag fromTrackers = StatusAilmentFlag.None;
        if (EntityFlags.TryGetValue(id, out StatusAilmentFlag f))
            fromTrackers = f;
        s = (s & ~TrackedAilmentMask) | (fromTrackers & TrackedAilmentMask);
        Status[index] = s;
    }
}

/// <summary>
/// Schedules ailment duration step, expiry removal, and enemy flag sync.
/// </summary>
public static class AilmentTimeScheduler
{
    public static readonly StatusAilmentFlag TrackedStatusAilmentMask =
        StatusAilmentFlag.Frozen
        | StatusAilmentFlag.Stunned
        | StatusAilmentFlag.Poisoned
        | StatusAilmentFlag.Ignited
        | StatusAilmentFlag.Shocked
        | StatusAilmentFlag.Bleeding;

    public static void Tick(
        NativeArray<int> entityIds,
        NativeArray<StatusAilmentFlag> status,
        float deltaTime,
        NativeList<EnemyBleedStatus> bleed,
        NativeList<EnemyFrozenStatus> frozen,
        NativeList<EnemyIgniteStatus> ignite,
        NativeList<EnemyStunnedStatus> stunned,
        NativeList<EnemyPoisonStatus> poison,
        NativeList<EnemyShockedStatus> shocked,
        NativeHashMap<int, StatusAilmentFlag> entityFlagsScratch)
    {
        if (deltaTime <= 0f)
            return;

        bool anyTrackers =
            bleed.Length > 0
            || frozen.Length > 0
            || ignite.Length > 0
            || stunned.Length > 0
            || poison.Length > 0
            || shocked.Length > 0;

        JobHandle h = default;

        if (anyTrackers)
        {
            JobHandle hBleed = new StepEnemyBleedLifetimeJob { Items = bleed.AsArray(), DeltaTime = deltaTime }.Schedule(bleed.Length, math.max(1, bleed.Length / 32), default);
            JobHandle hFrozen = new StepEnemyFrozenLifetimeJob { Items = frozen.AsArray(), DeltaTime = deltaTime }.Schedule(frozen.Length, math.max(1, frozen.Length / 32), default);
            JobHandle hIgnite = new StepEnemyIgniteLifetimeJob { Items = ignite.AsArray(), DeltaTime = deltaTime }.Schedule(ignite.Length, math.max(1, ignite.Length / 32), default);
            JobHandle hStunned = new StepEnemyStunnedLifetimeJob { Items = stunned.AsArray(), DeltaTime = deltaTime }.Schedule(stunned.Length, math.max(1, stunned.Length / 32), default);
            JobHandle hPoison = new StepEnemyPoisonLifetimeJob { Items = poison.AsArray(), DeltaTime = deltaTime }.Schedule(poison.Length, math.max(1, poison.Length / 32), default);
            JobHandle hShocked = new StepEnemyShockedLifetimeJob { Items = shocked.AsArray(), DeltaTime = deltaTime }.Schedule(shocked.Length, math.max(1, shocked.Length / 32), default);

            var deps = new NativeArray<JobHandle>(6, Allocator.TempJob);
            deps[0] = hBleed;
            deps[1] = hFrozen;
            deps[2] = hIgnite;
            deps[3] = hStunned;
            deps[4] = hPoison;
            deps[5] = hShocked;
            h = JobHandle.CombineDependencies(deps);
            deps.Dispose();

            h = new CompactExpiredAilmentRowsJob
            {
                Bleed = bleed,
                Frozen = frozen,
                Ignite = ignite,
                Stunned = stunned,
                Poison = poison,
                Shocked = shocked
            }.Schedule(h);
        }

        if (entityIds.IsCreated && entityIds.Length > 0)
        {
            h = new BuildEntityStatusAilmentFlagsMapJob
            {
                Frozen = frozen,
                Ignite = ignite,
                Stunned = stunned,
                Poison = poison,
                Shocked = shocked,
                Bleed = bleed,
                OutMap = entityFlagsScratch
            }.Schedule(h);

            int batch = math.max(1, entityIds.Length / 32);
            h = new ApplyEntityStatusAilmentFlagsJob
            {
                EntityIds = entityIds,
                Status = status,
                EntityFlags = entityFlagsScratch,
                TrackedAilmentMask = TrackedStatusAilmentMask
            }.Schedule(entityIds.Length, batch, h);
        }

        h.Complete();
    }
}
