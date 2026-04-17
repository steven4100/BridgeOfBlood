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
    public NativeList<EnemyIgniteStatus> List;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyIgniteStatus row = List[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        List[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyFrozenLifetimeJob : IJobParallelFor
{
    public NativeList<EnemyFrozenStatus> List;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyFrozenStatus row = List[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        List[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyStunnedLifetimeJob : IJobParallelFor
{
    public NativeList<EnemyStunnedStatus> List;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyStunnedStatus row = List[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        List[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyPoisonLifetimeJob : IJobParallelFor
{
    public NativeList<EnemyPoisonStatus> List;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyPoisonStatus row = List[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        List[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyShockedLifetimeJob : IJobParallelFor
{
    public NativeList<EnemyShockedStatus> List;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyShockedStatus row = List[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        List[index] = row;
    }
}

[BurstCompile]
public struct StepEnemyBleedLifetimeJob : IJobParallelFor
{
    public NativeList<EnemyBleedStatus> List;
    public float DeltaTime;

    public void Execute(int index)
    {
        EnemyBleedStatus row = List[index];
        row.lifetime -= DeltaTime;
        if (row.lifetime < 0f)
            row.lifetime = 0f;
        List[index] = row;
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
    public NativeArray<Enemy> Enemies;
    [ReadOnly] public NativeHashMap<int, StatusAilmentFlag> EntityFlags;
    public StatusAilmentFlag TrackedAilmentMask;

    public void Execute(int index)
    {
        Enemy e = Enemies[index];
        StatusAilmentFlag fromTrackers = StatusAilmentFlag.None;
        if (EntityFlags.TryGetValue(e.entityId, out StatusAilmentFlag f))
            fromTrackers = f;
        e.statusAilmentFlag = (e.statusAilmentFlag & ~TrackedAilmentMask) | (fromTrackers & TrackedAilmentMask);
        Enemies[index] = e;
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
        | StatusAilmentFlag.Shocked;

    public static void Tick(
        NativeArray<Enemy> enemies,
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
            JobHandle hBleed = new StepEnemyBleedLifetimeJob { List = bleed, DeltaTime = deltaTime }.Schedule(bleed.Length, math.max(1, bleed.Length / 32), default);
            JobHandle hFrozen = new StepEnemyFrozenLifetimeJob { List = frozen, DeltaTime = deltaTime }.Schedule(frozen.Length, math.max(1, frozen.Length / 32), default);
            JobHandle hIgnite = new StepEnemyIgniteLifetimeJob { List = ignite, DeltaTime = deltaTime }.Schedule(ignite.Length, math.max(1, ignite.Length / 32), default);
            JobHandle hStunned = new StepEnemyStunnedLifetimeJob { List = stunned, DeltaTime = deltaTime }.Schedule(stunned.Length, math.max(1, stunned.Length / 32), default);
            JobHandle hPoison = new StepEnemyPoisonLifetimeJob { List = poison, DeltaTime = deltaTime }.Schedule(poison.Length, math.max(1, poison.Length / 32), default);
            JobHandle hShocked = new StepEnemyShockedLifetimeJob { List = shocked, DeltaTime = deltaTime }.Schedule(shocked.Length, math.max(1, shocked.Length / 32), default);

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

        if (enemies.IsCreated && enemies.Length > 0)
        {
            h = new BuildEntityStatusAilmentFlagsMapJob
            {
                Frozen = frozen,
                Ignite = ignite,
                Stunned = stunned,
                Poison = poison,
                Shocked = shocked,
                OutMap = entityFlagsScratch
            }.Schedule(h);

            int batch = math.max(1, enemies.Length / 32);
            h = new ApplyEntityStatusAilmentFlagsJob
            {
                Enemies = enemies,
                EntityFlags = entityFlagsScratch,
                TrackedAilmentMask = TrackedStatusAilmentMask
            }.Schedule(enemies.Length, batch, h);
        }

        h.Complete();
    }
}
